using PageWalkerLocal.Core;
using PageWalkerLocal.Perception;

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

    public static int CountVisibleTabs(PerceptionState state) =>
        state.OfKind(CandidateKind.TabItem)
            .Select(candidate => candidate.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

    public void MarkOwnTabOpenedIfCountIncreased(int before, int after, string reason)
    {
        if (after > before)
        {
            MarkOwnTabOpened($"{reason}; tabs before={before}, after={after}");
        }
        else
        {
            _logger.Debug($"No new tab detected. Tabs before={before}, after={after}. Reason={reason}");
        }
    }
}
