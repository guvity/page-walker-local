using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Perception;

public sealed class PerceptionState
{
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.Now;
    public ScreenBounds Bounds { get; init; }
    public string WindowTitle { get; init; } = string.Empty;
    public string VisibleText { get; init; } = string.Empty;
    public string ScreenshotHash { get; init; } = string.Empty;
    public string TextHash { get; init; } = string.Empty;
    public List<CandidateElement> Candidates { get; init; } = [];
    public bool IsTechnicalPage { get; set; }
    public bool HasCaptchaLikeText { get; set; }
    public List<string> ClassifierSignals { get; init; } = [];

    public IEnumerable<CandidateElement> OfKind(CandidateKind kind) =>
        Candidates.Where(candidate => candidate.Kind == kind);
}
