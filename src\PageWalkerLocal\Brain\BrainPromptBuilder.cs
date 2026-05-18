using System.Text.Json;
using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Brain;

public sealed class BrainPromptBuilder
{
    public string Build(PlannerContext context, PerceptionState state, IReadOnlyList<ActionPlan> allowedActions)
    {
        var prompt = new
        {
            instruction = "Choose exactly one allowed action. Return strict JSON only. Do not control input directly.",
            context = new
            {
                context.Step,
                context.Depth,
                context.ScrollsOnCurrentPage,
                elapsedSeconds = context.Elapsed.TotalSeconds
            },
            page = new
            {
                state.VisibleText,
                state.IsTechnicalPage,
                state.HasCaptchaLikeText,
                candidates = state.Candidates.Select(candidate => new
                {
                    candidate.Id,
                    kind = candidate.Kind.ToString(),
                    candidate.Text,
                    candidate.Confidence
                })
            },
            allowedActions = allowedActions.Select(action => action.Action.ToString())
        };

        return JsonSerializer.Serialize(prompt);
    }
}
