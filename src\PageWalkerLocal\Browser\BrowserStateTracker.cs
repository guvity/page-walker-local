using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Browser;

public sealed class BrowserStateTracker
{
    public string BuildPageKey(PerceptionState state)
    {
        if (!string.IsNullOrWhiteSpace(state.TextHash))
        {
            return $"{state.WindowTitle}|{state.TextHash}";
        }

        return $"{state.WindowTitle}|{state.ScreenshotHash}";
    }
}
