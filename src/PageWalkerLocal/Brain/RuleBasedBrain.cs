using PageWalkerLocal.Core;
using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Brain;

public sealed class RuleBasedBrain : IBrain
{
    private readonly AppConfig _config;
    private readonly AllowedActionGenerator _allowedActions;

    public RuleBasedBrain(AppConfig config)
    {
        _config = config;
        _allowedActions = new AllowedActionGenerator(config);
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

        var allowed = _allowedActions.Build(context, state);
        var priority = PickFirst(allowed,
            WalkerAction.ClosePopup,
            WalkerAction.AnswerSimpleGate,
            WalkerAction.FillAllowedFormField);
        if (priority is not null)
        {
            return Task.FromResult(priority);
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

        priority = PickFirst(allowed, WalkerAction.ClickSafeLink, WalkerAction.ClickSafeButton);
        if (priority is not null)
        {
            return Task.FromResult(priority);
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

    private static ActionPlan? PickFirst(IReadOnlyList<ActionPlan> plans, params WalkerAction[] actions)
    {
        foreach (var action in actions)
        {
            var plan = plans.FirstOrDefault(candidate => candidate.Action == action);
            if (plan is not null)
            {
                return plan;
            }
        }

        return null;
    }
}
