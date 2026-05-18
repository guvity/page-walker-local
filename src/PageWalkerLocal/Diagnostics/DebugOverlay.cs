using PageWalkerLocal.Core;

namespace PageWalkerLocal.Diagnostics;

public sealed class DebugOverlay
{
    private readonly AppLogger _logger;

    public DebugOverlay(AppLogger logger)
    {
        _logger = logger;
    }

    public void ShowStatus(string message)
    {
        // Phase 3 placeholder. The MVP intentionally avoids drawing over the desktop.
        _logger.Debug($"DebugOverlay: {message}");
    }
}
