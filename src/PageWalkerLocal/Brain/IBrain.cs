using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Brain;

public interface IBrain
{
    Task<ActionPlan> DecideAsync(PlannerContext context, PerceptionState state, CancellationToken cancellationToken);
}

public sealed class PlannerContext
{
    public int Step { get; init; }
    public int Depth { get; init; }
    public int ScrollsOnCurrentPage { get; init; }
    public TimeSpan Elapsed { get; init; }
    public IReadOnlySet<string> VisitedKeys { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public string? CurrentUrl { get; init; }
    public string? CurrentDomain { get; init; }
    public string? InitialDomain { get; init; }
}
