using System.Text.Json;
using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Brain;

public sealed class BrainPromptBuilder
{
    public string Build(PlannerContext context, PerceptionState state, IReadOnlyList<ActionPlan> allowedActions)
    {
        return Build(context, state, allowedActions, maxVisibleTextChars: 6000, maxCandidates: 120, maxTextChars: null);
    }

    public string BuildCompact(PlannerContext context, PerceptionState state, IReadOnlyList<ActionPlan> allowedActions, int maxTextChars)
    {
        return Build(context, state, allowedActions, maxVisibleTextChars: Math.Min(3000, maxTextChars / 2), maxCandidates: 48, maxTextChars);
    }

    private string Build(
        PlannerContext context,
        PerceptionState state,
        IReadOnlyList<ActionPlan> allowedActions,
        int maxVisibleTextChars,
        int maxCandidates,
        int? maxTextChars)
    {
        var allowedTargetIds = allowedActions
            .Select(action => action.TargetId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = state.Candidates
            .Where(candidate => allowedTargetIds.Contains(candidate.Id) || candidate.Confidence >= 0.70)
            .OrderByDescending(candidate => allowedTargetIds.Contains(candidate.Id))
            .ThenByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .Take(maxCandidates)
            .Select(candidate => new
            {
                candidate.Id,
                kind = candidate.Kind.ToString(),
                Text = Trim(candidate.Text, 160),
                candidate.Confidence
            });

        var prompt = new
        {
            instruction = "Choose exactly one allowed action. Return strict JSON only. Use action and targetId exactly from allowedActionDetails. If selected action has targetId null, return targetId null. Do not control input directly.",
            context = new
            {
                context.Step,
                context.Depth,
                context.ScrollsOnCurrentPage,
                elapsedSeconds = context.Elapsed.TotalSeconds
            },
            page = new
            {
                VisibleText = Trim(state.VisibleText, maxVisibleTextChars),
                state.IsTechnicalPage,
                state.HasCaptchaLikeText,
                candidates
            },
            allowedActions = allowedActions.Select(action => action.Action.ToString())
                .ToArray(),
            allowedActionDetails = allowedActions.Select(action => new
            {
                action = action.Action.ToString(),
                action.TargetId,
                targetText = Trim(action.Target?.Text ?? string.Empty, 160),
                action.Reason,
                action.Confidence
            })
        };

        var json = JsonSerializer.Serialize(prompt);
        if (!maxTextChars.HasValue || json.Length <= maxTextChars.Value)
        {
            return json;
        }

        return Build(context, state, allowedActions, maxVisibleTextChars: 1000, maxCandidates: 20, maxTextChars: null);
    }

    private static string Trim(string text, int maxChars)
    {
        text = text.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return text.Length <= maxChars ? text : text[..maxChars] + "...";
    }
}
