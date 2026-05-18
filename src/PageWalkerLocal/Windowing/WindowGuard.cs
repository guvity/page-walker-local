using PageWalkerLocal.Core;

namespace PageWalkerLocal.Windowing;

public sealed class WindowGuard
{
    private readonly TargetWindowFinder _finder;
    private readonly AppConfig _config;
    private readonly AppLogger _logger;
    private readonly IntPtr _targetHandle;
    private readonly uint _targetProcessId;
    private readonly ScreenBounds _allowedBounds;

    public WindowGuard(TargetWindowFinder finder, AppConfig config, AppLogger logger, TargetWindow target)
    {
        _finder = finder;
        _config = config;
        _logger = logger;
        _targetHandle = target.Handle;
        _targetProcessId = target.ProcessId;
        _allowedBounds = target.AllowedBounds;
    }

    public ScreenBounds AllowedBounds => _allowedBounds;

    public GuardResult CheckAction(ScreenPoint point, double confidence)
    {
        if (confidence < _config.LocalBrain.MinConfidence)
        {
            return GuardResult.Deny($"Confidence {confidence:0.00} is below minimum {_config.LocalBrain.MinConfidence:0.00}.");
        }

        if (!_allowedBounds.Contains(point))
        {
            return GuardResult.Deny($"Point {point} is outside allowed bounds {_allowedBounds}.");
        }

        var active = _finder.Find();
        if (active is null)
        {
            return GuardResult.Deny("No active target window is available.");
        }

        if (active.Handle != _targetHandle || active.ProcessId != _targetProcessId)
        {
            return GuardResult.Deny("Active window/process changed; pausing for safety.");
        }

        if (!_finder.IsAllowedTarget(active))
        {
            return GuardResult.Deny("Active process is no longer allowed by config.");
        }

        return GuardResult.Allow();
    }

    public bool CheckWindowStillActive()
    {
        var active = _finder.Find();
        var ok = active is not null && active.Handle == _targetHandle && active.ProcessId == _targetProcessId;
        if (!ok)
        {
            _logger.Warning("Target window lost focus or changed process.");
        }

        return ok;
    }
}

public readonly record struct GuardResult(bool Allowed, string Reason)
{
    public static GuardResult Allow() => new(true, "allowed");
    public static GuardResult Deny(string reason) => new(false, reason);
}
