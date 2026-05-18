using PageWalkerLocal.Core;
using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Brain;

public sealed class PlannerRules
{
    private readonly AppConfig _config;

    public PlannerRules(AppConfig config)
    {
        _config = config;
    }

    public GuardedPlan Validate(ActionPlan plan, PerceptionState state)
    {
        if (plan.Confidence < _config.LocalBrain.MinConfidence && plan.Action is not WalkerAction.NoOp and not WalkerAction.HumanRead and not WalkerAction.Scroll)
        {
            return GuardedPlan.Deny($"Plan confidence {plan.Confidence:0.00} is below configured threshold.");
        }

        if (state.HasCaptchaLikeText)
        {
            return GuardedPlan.Deny("CAPTCHA-like text is present. Stopping.");
        }

        if (plan.Target is not null && LooksForbidden(plan.Target.Text))
        {
            return GuardedPlan.Deny($"Target text is forbidden: '{plan.Target.Text}'.");
        }

        if (plan.Action == WalkerAction.FillAllowedFormField && !_config.AllowForms)
        {
            return GuardedPlan.Deny("Form filling is disabled.");
        }

        if (plan.Action == WalkerAction.AnswerSimpleGate && !_config.AllowAgeGate && !_config.AllowSimpleConfirmations)
        {
            return GuardedPlan.Deny("Gate/confirmation actions are disabled.");
        }

        return GuardedPlan.Allow(plan);
    }

    private bool LooksForbidden(string text)
    {
        var builtIns = new[]
        {
            "buy", "pay", "subscribe", "deposit", "confirm payment", "purchase", "order now",
            "checkout", "billing", "card", "crypto", "wallet"
        };

        return _config.BlockedTexts.Concat(builtIns)
            .Any(blocked => text.Contains(blocked, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record GuardedPlan(bool Allowed, ActionPlan Plan, string Reason)
{
    public static GuardedPlan Allow(ActionPlan plan) => new(true, plan, "allowed");
    public static GuardedPlan Deny(string reason) => new(false, ActionPlan.Stop(reason), reason);
}
