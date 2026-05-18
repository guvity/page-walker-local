using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using PageWalkerLocal.Core;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Perception;

public sealed class UiaReader
{
    private const int MaxElements = 1200;
    private readonly AppLogger _logger;
    private bool _warned;

    public UiaReader(AppLogger logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<CandidateElement>> ReadCandidatesAsync(TargetWindow window, CancellationToken cancellationToken)
    {
        try
        {
            using var automation = new UIA3Automation();
            var root = automation.FromHandle(window.Handle);
            var candidates = new List<CandidateElement>();
            AddElement(candidates, root, window, "uia-root", 0);

            var descendants = root.FindAllDescendants();
            var index = 1;
            foreach (var element in descendants.Take(MaxElements))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddElement(candidates, element, window, $"uia-{index}", index);
                index++;
            }

            return Task.FromResult<IReadOnlyList<CandidateElement>>(candidates
                .GroupBy(candidate => $"{candidate.Kind}:{candidate.Text}:{candidate.Bounds}")
                .Select(group => group.OrderByDescending(candidate => candidate.Confidence).First())
                .ToArray());
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException or UnauthorizedAccessException)
        {
            WarnOnce($"FlaUI UIA tree read failed: {ex.Message}");
            return Task.FromResult<IReadOnlyList<CandidateElement>>(Fallback(window));
        }
    }

    private static void AddElement(List<CandidateElement> candidates, AutomationElement element, TargetWindow window, string id, int index)
    {
        if (!TryReadBounds(element, window.AllowedBounds, out var bounds))
        {
            return;
        }

        var name = Safe(() => element.Name) ?? string.Empty;
        var automationId = Safe(() => element.AutomationId) ?? string.Empty;
        var className = Safe(() => element.ClassName) ?? string.Empty;
        var value = TryReadValue(element);
        var text = FirstNonEmpty(value, name, automationId, className);
        var controlType = Safe(() => element.ControlType);
        var kind = MapKind(controlType, name, automationId, className, value);

        if (kind == CandidateKind.Unknown && string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var label = BuildLabel(text, name, automationId, className);
        var confidence = kind switch
        {
            CandidateKind.AddressBar => 0.92,
            CandidateKind.Button or CandidateKind.Link or CandidateKind.Input => 0.82,
            CandidateKind.TabItem => 0.80,
            CandidateKind.Text => 0.68,
            _ => 0.58
        };

        candidates.Add(new CandidateElement(id, kind, label, bounds, confidence, "fla-ui"));
    }

    private static IReadOnlyList<CandidateElement> Fallback(TargetWindow window)
    {
        if (string.IsNullOrWhiteSpace(window.Title))
        {
            return Array.Empty<CandidateElement>();
        }

        return
        [
            new CandidateElement("window-title", CandidateKind.Text, window.Title, window.AllowedBounds, 0.55, "window-title")
        ];
    }

    private static CandidateKind MapKind(ControlType controlType, string name, string automationId, string className, string? value)
    {
        var merged = $"{name} {automationId} {className} {value}".Trim();
        if (controlType == ControlType.Edit && LooksLikeAddressBar(merged, value))
        {
            return CandidateKind.AddressBar;
        }

        if (controlType == ControlType.Button || controlType == ControlType.MenuItem || controlType == ControlType.SplitButton)
        {
            return CandidateKind.Button;
        }

        if (controlType == ControlType.Hyperlink)
        {
            return CandidateKind.Link;
        }

        if (controlType == ControlType.Edit || controlType == ControlType.ComboBox)
        {
            return CandidateKind.Input;
        }

        if (controlType == ControlType.Text || controlType == ControlType.Document)
        {
            return CandidateKind.Text;
        }

        if (controlType == ControlType.TabItem)
        {
            return CandidateKind.TabItem;
        }

        return CandidateKind.Unknown;
    }

    private static bool LooksLikeAddressBar(string merged, string? value)
    {
        return merged.Contains("address", StringComparison.OrdinalIgnoreCase)
            || merged.Contains("omnibox", StringComparison.OrdinalIgnoreCase)
            || Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool TryReadBounds(AutomationElement element, ScreenBounds allowed, out ScreenBounds bounds)
    {
        bounds = default;
        try
        {
            var rect = element.BoundingRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return false;
            }

            bounds = new ScreenBounds(
                (int)Math.Round(Convert.ToDouble(rect.Left)),
                (int)Math.Round(Convert.ToDouble(rect.Top)),
                Math.Max(1, (int)Math.Round(Convert.ToDouble(rect.Width))),
                Math.Max(1, (int)Math.Round(Convert.ToDouble(rect.Height))));
            return allowed.Contains(bounds.Center) || allowed.Contains(bounds);
        }
        catch
        {
            return false;
        }
    }

    private static string? TryReadValue(AutomationElement element)
    {
        try
        {
            var valuePattern = element.Patterns.Value.PatternOrDefault;
            return valuePattern?.Value.ValueOrDefault;
        }
        catch
        {
            return null;
        }
    }

    private static T? Safe<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return default;
        }
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string BuildLabel(string text, string name, string automationId, string className)
    {
        var parts = new[] { text, name, automationId, className }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3);
        return string.Join(" | ", parts);
    }

    private void WarnOnce(string message)
    {
        if (_warned)
        {
            return;
        }

        _logger.Warning(message);
        _warned = true;
    }
}
