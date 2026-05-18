using System.Reflection;
using PageWalkerLocal.Core;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Perception;

public sealed class UiaReader
{
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
            var fromFlaUi = TryReadWithFlaUi(window);
            if (fromFlaUi.Count > 0)
            {
                return Task.FromResult<IReadOnlyList<CandidateElement>>(fromFlaUi);
            }
        }
        catch (Exception ex) when (ex is TargetInvocationException or InvalidOperationException or TypeLoadException)
        {
            WarnOnce($"FlaUI UIA read failed: {ex.Message}");
        }

        var fallback = new List<CandidateElement>();
        if (!string.IsNullOrWhiteSpace(window.Title))
        {
            fallback.Add(new CandidateElement("window-title", CandidateKind.Unknown, window.Title, window.AllowedBounds, 0.55, "window-title"));
        }

        return Task.FromResult<IReadOnlyList<CandidateElement>>(fallback);
    }

    private List<CandidateElement> TryReadWithFlaUi(TargetWindow window)
    {
        // Kept reflective so the MVP can still start if FlaUI cannot initialize in a locked-down session.
        var uia3Type = Type.GetType("FlaUI.UIA3.UIA3Automation, FlaUI.UIA3");
        var elementType = Type.GetType("FlaUI.Core.AutomationElements.AutomationElement, FlaUI.Core");
        if (uia3Type is null || elementType is null)
        {
            WarnOnce("FlaUI assemblies are not available at runtime; UIA is limited.");
            return [];
        }

        using var automation = Activator.CreateInstance(uia3Type) as IDisposable;
        if (automation is null)
        {
            return [];
        }

        var fromHandle = elementType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method => method.Name == "FromHandle"
                && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(IntPtr)));

        if (fromHandle is null)
        {
            WarnOnce("FlaUI AutomationElement.FromHandle was not found; UIA is limited.");
            return [];
        }

        var parameters = fromHandle.GetParameters();
        var args = parameters.Length switch
        {
            1 => new object?[] { window.Handle },
            2 => parameters[0].ParameterType.IsInstanceOfType(automation)
                ? new object?[] { automation, window.Handle }
                : new object?[] { window.Handle, automation },
            _ => null
        };

        if (args is null)
        {
            return [];
        }

        var root = fromHandle.Invoke(null, args);
        if (root is null)
        {
            return [];
        }

        var name = root.GetType().GetProperty("Name")?.GetValue(root)?.ToString();
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        return
        [
            new CandidateElement("uia-root", CandidateKind.Unknown, name, window.AllowedBounds, 0.6, "fla-ui-root")
        ];
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
