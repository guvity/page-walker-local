using PageWalkerLocal.Core;
using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Brain;

public sealed class AllowedActionGenerator
{
    private readonly AppConfig _config;

    public AllowedActionGenerator(AppConfig config)
    {
        _config = config;
    }

    public IReadOnlyList<ActionPlan> Build(PlannerContext context, PerceptionState state)
    {
        var plans = new List<ActionPlan>();

        foreach (var popup in Ranked(state.OfKind(CandidateKind.PopupClose)).Where(candidate => candidate.Confidence >= 0.65))
        {
            plans.Add(Targeted(WalkerAction.ClosePopup, popup, "Allowed popup/modal close candidate."));
        }

        if (_config.AllowAgeGate || _config.AllowSimpleConfirmations)
        {
            foreach (var gate in Ranked(state.OfKind(CandidateKind.AgeGate)).Where(candidate => candidate.Confidence >= 0.65))
            {
                plans.Add(Targeted(WalkerAction.AnswerSimpleGate, gate, "Allowed simple age/confirmation gate candidate."));
            }
        }

        if (_config.AllowForms)
        {
            foreach (var formField in Ranked(state.OfKind(CandidateKind.FormField)))
            {
                var field = _config.AllowedFormFields.FirstOrDefault(name => formField.Text.Contains(name, StringComparison.OrdinalIgnoreCase));
                if (field is null || !_config.TestFormData.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                plans.Add(new ActionPlan
                {
                    Action = WalkerAction.FillAllowedFormField,
                    TargetId = formField.Id,
                    Target = formField,
                    Reason = $"Allowed test form field '{field}'.",
                    Confidence = formField.Confidence,
                    MemoryKey = CandidateMemoryKey(formField),
                    TextInput = value
                });
            }
        }

        if (context.Depth < _config.MaxDepth)
        {
            foreach (var link in Ranked(state.OfKind(CandidateKind.Link)).Where(candidate => IsCandidateNavigationAllowed(candidate, context)))
            {
                plans.Add(Targeted(WalkerAction.ClickSafeLink, link, "Safe link candidate within depth and domain limits."));
            }
        }

        foreach (var button in Ranked(state.OfKind(CandidateKind.Button)).Where(candidate => !LooksDangerous(candidate.Text)))
        {
            plans.Add(Targeted(WalkerAction.ClickSafeButton, button, "Safe non-payment button candidate."));
        }

        if (context.ScrollsOnCurrentPage < _config.MaxScrollsPerPage)
        {
            plans.Add(new ActionPlan
            {
                Action = WalkerAction.HumanRead,
                Reason = "Visible content can be read safely.",
                Confidence = 0.84
            });
            plans.Add(new ActionPlan
            {
                Action = WalkerAction.Scroll,
                Reason = "Visible content can be scanned further.",
                Confidence = 0.82
            });
        }

        plans.Add(ActionPlan.Stop("STOP is always allowed as the conservative fallback."));
        return plans;
    }

    public static string CandidateMemoryKey(CandidateElement candidate) =>
        $"candidate:{candidate.Kind}:{candidate.Text}:{candidate.Bounds.X},{candidate.Bounds.Y},{candidate.Bounds.Width},{candidate.Bounds.Height}";

    private ActionPlan Targeted(WalkerAction action, CandidateElement candidate, string reason)
    {
        return new ActionPlan
        {
            Action = action,
            TargetId = candidate.Id,
            Target = candidate,
            Reason = reason,
            Confidence = candidate.Confidence,
            MemoryKey = CandidateMemoryKey(candidate)
        };
    }

    private bool IsCandidateNavigationAllowed(CandidateElement candidate, PlannerContext context)
    {
        if (LooksDangerous(candidate.Text))
        {
            return false;
        }

        var memoryKey = CandidateMemoryKey(candidate);
        if (context.VisitedKeys.Contains(memoryKey))
        {
            return false;
        }

        if (_config.AllowExternalNavigation)
        {
            return true;
        }

        var uri = ExtractUri(candidate.Text);
        if (uri is not null && context.VisitedKeys.Contains("url:" + uri.ToString()))
        {
            return false;
        }

        if (uri is null)
        {
            return true;
        }

        var host = NormalizeHost(uri.Host);
        if (_config.AllowedDomains.Count > 0)
        {
            return _config.AllowedDomains.Any(domain => DomainMatches(host, domain));
        }

        return context.CurrentDomain is not null && DomainMatches(host, context.CurrentDomain);
    }

    private static IEnumerable<CandidateElement> Ranked(IEnumerable<CandidateElement> candidates) =>
        candidates.OrderByDescending(candidate => candidate.Confidence);

    private static Uri? ExtractUri(string text)
    {
        var token = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || part.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || part.StartsWith("www.", StringComparison.OrdinalIgnoreCase));
        if (token is null)
        {
            return null;
        }

        token = token.TrimEnd('.', ',', ';', ')', ']', '"', '\'');
        if (token.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            token = "https://" + token;
        }

        return Uri.TryCreate(token, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static bool DomainMatches(string host, string configured)
    {
        configured = NormalizeHost(configured);
        return string.Equals(host, configured, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + configured, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHost(string host)
    {
        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(host, UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : host;
        }

        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    private static bool LooksDangerous(string text)
    {
        var blocked = new[]
        {
            "buy", "pay", "subscribe", "deposit", "confirm payment", "purchase", "order now",
            "checkout", "billing", "card", "crypto", "wallet"
        };
        return blocked.Any(item => text.Contains(item, StringComparison.OrdinalIgnoreCase));
    }
}
