using System.Text.Json;

namespace PageWalkerLocal.Brain;

public sealed class BrainJsonParser
{
    public ActionPlan? TryParse(string json, IReadOnlyList<ActionPlan> allowedActions)
    {
        try
        {
            json = ExtractJsonObject(json);
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

            if (!TryParseAction(actionName, out var action))
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
                MemoryKey = allowed.MemoryKey,
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

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private static bool TryParseAction(string? value, out WalkerAction action)
    {
        action = WalkerAction.Stop;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        foreach (var candidate in Enum.GetValues<WalkerAction>())
        {
            var candidateName = candidate.ToString().Replace("_", string.Empty, StringComparison.Ordinal);
            if (string.Equals(candidateName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                action = candidate;
                return true;
            }
        }

        return false;
    }
}
