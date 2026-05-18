using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Perception;

public sealed record CandidateElement(
    string Id,
    CandidateKind Kind,
    string Text,
    ScreenBounds Bounds,
    double Confidence,
    string Source)
{
    public ScreenPoint Center => Bounds.Center;
}

public enum CandidateKind
{
    Unknown,
    Button,
    Link,
    Input,
    Text,
    AddressBar,
    TabItem,
    PopupClose,
    AgeGate,
    FormField,
    BrowserClose,
    TechnicalAction
}
