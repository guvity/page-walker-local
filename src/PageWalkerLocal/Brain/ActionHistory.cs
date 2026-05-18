namespace PageWalkerLocal.Brain;

public sealed class ActionHistory
{
    private readonly List<ActionHistoryEntry> _entries = [];

    public IReadOnlyList<ActionHistoryEntry> Entries => _entries;
    public int Count => _entries.Count;

    public void Add(ActionPlan plan, string outcome)
    {
        _entries.Add(new ActionHistoryEntry(DateTimeOffset.Now, plan.Action, plan.TargetId, plan.Reason, plan.Confidence, outcome));
    }
}

public sealed record ActionHistoryEntry(
    DateTimeOffset Timestamp,
    WalkerAction Action,
    string? TargetId,
    string Reason,
    double Confidence,
    string Outcome);
