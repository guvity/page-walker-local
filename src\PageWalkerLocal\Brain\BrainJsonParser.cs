using System.Text.Json;

namespace PageWalkerLocal.Brain;

public sealed class BrainJsonParser
{
    public ActionPlan? TryParse(string json, IReadOnlyList<ActionPlan> allowedActions)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var actionName = root.GetProperty("action").GetString();
            var confidence = root.TryGetProperty("confidence", out var confElement)
                ? confElement.GetDouble()
                : 0.0;
            var reason = root.TryGetProperty("reason", out var reasonElement)
                ? reasonElement.GetString() ?? string.Empty
                : string.Empty;
            var targetId = root.TryGetProperty("targetId", out var targetElement) && targetElement.ValueKind != JsonValueKind.Null
                ? targetElement.GetString()
                : null;

            if (!Enum.TryParse<WalkerAction>(actionName, ignoreCase: true, out var action))
            {
                return null;
            }

            var allowed = allowedActions.FirstOrDefault(plan => plan.Action == action
                && (targetId is null || string.Equals(plan.TargetId, targetId, StringComparison.OrdinalIgnoreCase)));
            if (allowed is null)
            {
                return null;
            }

            return new ActionPlan
            {
                Action = allowed.Action,
                TargetId = allowed.TargetId,
                Target = allowed.Target,
                Key = allowed.Key,
                TextInput = allowed.TextInput,
                Reason = reason,
                Confidence = Math.Min(confidence, allowed.Confidence)
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
