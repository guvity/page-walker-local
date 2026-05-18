using PageWalkerLocal.Perception;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Brain;

public sealed class ActionPlan
{
    public WalkerAction Action { get; init; } = WalkerAction.NoOp;
    public string? TargetId { get; init; }
    public CandidateElement? Target { get; init; }
    public string Reason { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public ConsoleKey? Key { get; init; }
    public string? TextInput { get; init; }
    public ScreenPoint? Point => Target?.Center;

    public static ActionPlan Stop(string reason) => new()
    {
        Action = WalkerAction.Stop,
        Reason = reason,
        Confidence = 1.0
    };
}

public enum WalkerAction
{
    NoOp,
    Scroll,
    HumanRead,
    ClickSafeLink,
    ClickSafeButton,
    ClosePopup,
    AnswerSimpleGate,
    FillAllowedFormField,
    PressKey,
    CloseOwnTab,
    Stop
}
