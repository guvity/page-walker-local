using System.Runtime.InteropServices;
using PageWalkerLocal.Core;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Browser;

public sealed class BrowserWindowTracker
{
    private const uint WmClose = 0x0010;
    private readonly TargetWindowFinder _windowFinder;
    private readonly AppLogger _logger;
    private readonly HashSet<IntPtr> _baselineHandles = [];
    private readonly Dictionary<IntPtr, TargetWindow> _openedWindows = [];
    private IntPtr _targetHandle;

    public BrowserWindowTracker(TargetWindowFinder windowFinder, AppLogger logger)
    {
        _windowFinder = windowFinder;
        _logger = logger;
    }

    public int OpenedWindowCount => _openedWindows.Count;

    public void CaptureBaseline(TargetWindow target)
    {
        _targetHandle = target.Handle;
        _baselineHandles.Clear();
        _openedWindows.Clear();

        foreach (var window in EnumerateAllowedTopLevelWindows())
        {
            _baselineHandles.Add(window.Handle);
        }

        _baselineHandles.Add(target.Handle);
        _logger.Info($"Captured browser window baseline. Existing allowed top-level windows={_baselineHandles.Count}.");
    }

    public void ObserveNewWindows(string reason)
    {
        if (_targetHandle == IntPtr.Zero)
        {
            return;
        }

        foreach (var window in EnumerateAllowedTopLevelWindows())
        {
            if (window.Handle == _targetHandle
                || _baselineHandles.Contains(window.Handle)
                || _openedWindows.ContainsKey(window.Handle))
            {
                continue;
            }

            _openedWindows[window.Handle] = window;
            _logger.Info($"Tracked new browser window opened during run. Handle=0x{window.Handle.ToInt64():X}, Process='{window.ProcessName}', Title='{window.Title}', Reason={reason}.");
        }
    }

    public void CloseTrackedWindows(bool dryRun)
    {
        if (_openedWindows.Count == 0)
        {
            _logger.Info("No new external browser windows to close.");
            return;
        }

        foreach (var item in _openedWindows.ToArray())
        {
            var handle = item.Key;
            var window = item.Value;
            if (handle == _targetHandle || !IsWindow(handle))
            {
                _openedWindows.Remove(handle);
                continue;
            }

            if (dryRun)
            {
                _logger.Info($"DryRun: would close new external browser window. Handle=0x{handle.ToInt64():X}, Process='{window.ProcessName}', Title='{window.Title}'.");
                continue;
            }

            if (PostMessage(handle, WmClose, IntPtr.Zero, IntPtr.Zero))
            {
                _logger.Info($"Requested close for new external browser window. Handle=0x{handle.ToInt64():X}, Process='{window.ProcessName}', Title='{window.Title}'.");
            }
            else
            {
                _logger.Warning($"Failed to post close message to new external browser window. Handle=0x{handle.ToInt64():X}, Process='{window.ProcessName}', Title='{window.Title}'.");
            }
        }
    }

    private IReadOnlyList<TargetWindow> EnumerateAllowedTopLevelWindows()
    {
        var windows = new List<TargetWindow>();
        EnumWindows((hwnd, _) =>
        {
            var window = _windowFinder.ReadWindow(hwnd);
            if (window is not null
                && !window.WindowBounds.IsEmpty
                && _windowFinder.IsAllowedTarget(window)
                && window.WindowBounds.Width > 120
                && window.WindowBounds.Height > 80)
            {
                windows.Add(window);
            }

            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
