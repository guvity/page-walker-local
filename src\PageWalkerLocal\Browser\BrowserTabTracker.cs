using PageWalkerLocal.Core;

namespace PageWalkerLocal.Browser;

public sealed class BrowserTabTracker
{
    private readonly AppLogger _logger;
    private int _ownOpenedTabs;

    public BrowserTabTracker(AppLogger logger)
    {
        _logger = logger;
    }

    public int OwnOpenedTabs => _ownOpenedTabs;

    public void MarkOwnTabOpened(string reason)
    {
        _ownOpenedTabs++;
        _logger.Info($"Marked own opened tab. Count={_ownOpenedTabs}. Reason={reason}");
    }

    public void MarkOwnTabClosed()
    {
        if (_ownOpenedTabs > 0)
        {
            _ownOpenedTabs--;
        }
    }

    public int TabsToCloseOnFinish(bool enabled)
    {
        if (!enabled)
        {
            return 0;
        }

        return Math.Max(0, _ownOpenedTabs);
    }
}
