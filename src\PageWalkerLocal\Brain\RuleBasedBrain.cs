using PageWalkerLocal.Core;
using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Brain;

public sealed class RuleBasedBrain : IBrain
{
    private readonly AppConfig _config;

    public RuleBasedBrain(AppConfig config)
    {
        _config = config;
    }

    public Task<ActionPlan> DecideAsync(PlannerContext context, PerceptionState state, CancellationToken cancellationToken)
    {
        if (state.HasCaptchaLikeText)
        {
            return Task.FromResult(ActionPlan.Stop("CAPTCHA-like text detected. PageWalkerLocal does not solve or bypass CAPTCHA."));
        }

        if (state.IsTechnicalPage)
        {
            return Task.FromResult(TechnicalPagePlan());
        }

        var blocked = _config.BlockedTexts.FirstOrDefault(text => state.VisibleText.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(blocked))
        {
            return Task.FromResult(ActionPlan.Stop($"Blocked text '{blocked}' detected."));
        }

        var popup = Best(state.OfKind(CandidateKind.PopupClose));
        if (popup is not null && popup.Confidence >= 0.65)
        {
            return Task.FromResult(new ActionPlan
            {
                Action = WalkerAction.ClosePopup,
                TargetId = popup.Id,
                Target = popup,
                Reason = "Popup/modal close candidate has priority before page walking.",
                Confidence = popup.Confidence
            });
        }

        var gate = Best(state.OfKind(CandidateKind.AgeGate));
        if (gate is not null && gate.Confidence >= 0.65)
        {
            return Task.FromResult(new ActionPlan
            {
                Action = WalkerAction.AnswerSimpleGate,
                TargetId = gate.Id,
                Target = gate,
                Reason = "Allowed simple age/confirmation gate candidate.",
                Confidence = gate.Confidence
            });
        }

        if (context.ScrollsOnCurrentPage < _config.MaxScrollsPerPage)
        {
            var readFirst = context.Step == 0 || context.Step % 3 == 0;
            return Task.FromResult(new ActionPlan
            {
                Action = readFirst ? WalkerAction.HumanRead : WalkerAction.Scroll,
                Reason = readFirst ? "Visible content should be read before the next interaction." : "Continue scanning visible content.",
                Confidence = 0.82
            });
        }

        var link = BestSafeLink(state);
        if (link is not null && context.Depth < _config.MaxDepth)
        {
            return Task.FromResult(new ActionPlan
            {
                Action = WalkerAction.ClickSafeLink,
                TargetId = link.Id,
                Target = link,
                Reason = "Safe link candidate selected within configured depth.",
                Confidence = link.Confidence
            });
        }

        return Task.FromResult(ActionPlan.Stop("No safe action remains within configured limits."));
    }

    private ActionPlan TechnicalPagePlan()
    {
        var action = _config.TechnicalPageAction.Trim().ToLowerInvariant();
        return action switch
        {
            "retry" => new ActionPlan { Action = WalkerAction.PressKey, Key = ConsoleKey.F5, Reason = "Technical page detected; configured action is retry.", Confidence = 0.9 },
            "back" => new ActionPlan { Action = WalkerAction.PressKey, Key = ConsoleKey.BrowserBack, Reason = "Technical page detected; configured action is back.", Confidence = 0.9 },
            "close_tab" => new ActionPlan { Action = WalkerAction.CloseOwnTab, Reason = "Technical page detected; configured action is close_tab.", Confidence = 0.9 },
            _ => ActionPlan.Stop("Technical page detected; configured action is stop.")
        };
    }

    private CandidateElement? BestSafeLink(PerceptionState state)
    {
        return state.OfKind(CandidateKind.Link)
            .Where(candidate => !LooksDangerous(candidate.Text))
            .OrderByDescending(candidate => candidate.Confidence)
            .FirstOrDefault();
    }

    private static CandidateElement? Best(IEnumerable<CandidateElement> candidates) =>
        candidates.OrderByDescending(candidate => candidate.Confidence).FirstOrDefault();

    private static bool LooksDangerous(string text)
    {
        var blocked = new[]
        {
            "buy", "pay", "subscribe", "deposit", "confirm payment", "purchase", "order now",
            "оплат", "купить", "подпис", "депозит", "заказать"
        };
        return blocked.Any(item => text.Contains(item, StringComparison.OrdinalIgnoreCase));
    }
}
