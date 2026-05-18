using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PageWalkerLocal.Core;

namespace PageWalkerLocal.Windowing;

public sealed class TargetWindowFinder
{
    private readonly AppConfig _config;
    private readonly AppLogger _logger;

    public TargetWindowFinder(AppConfig config, AppLogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public TargetWindow? Find()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            _logger.Warning("No active foreground window was found.");
            return null;
        }

        var window = ReadWindow(hwnd);
        if (window is null)
        {
            return null;
        }

        if (!IsAllowedTarget(window))
        {
            _logger.Warning($"Active window process/title is not allowed: Process='{window.ProcessName}', Title='{window.Title}'.");
            return null;
        }

        if (string.Equals(_config.TargetMode, "Rectangle", StringComparison.OrdinalIgnoreCase))
        {
            window = window with { AllowedBounds = ScreenBounds.FromConfig(_config.Rectangle) };
        }

        return window;
    }

    public TargetWindow? ReadWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd))
        {
            return null;
        }

        GetWindowThreadProcessId(hwnd, out var processId);
        string processName;
        try
        {
            processName = Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            processName = string.Empty;
        }

        var title = GetWindowTitle(hwnd);
        if (!GetWindowRect(hwnd, out var rect))
        {
            return null;
        }

        var bounds = new ScreenBounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return new TargetWindow(hwnd, processName, title, processId, bounds, bounds);
    }

    public bool IsAllowedTarget(TargetWindow window)
    {
        if (_config.TargetProcessNames.Count == 0)
        {
            return true;
        }

        return _config.TargetProcessNames.Any(allowed =>
        {
            var normalized = allowed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? allowed[..^4]
                : allowed;
            return string.Equals(window.ProcessName, normalized, StringComparison.OrdinalIgnoreCase)
                || window.Title.Contains(allowed, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        var builder = new StringBuilder(Math.Max(length + 1, 256));
        _ = GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);
}

public sealed record TargetWindow(
    IntPtr Handle,
    string ProcessName,
    string Title,
    uint ProcessId,
    ScreenBounds WindowBounds,
    ScreenBounds AllowedBounds);

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}
