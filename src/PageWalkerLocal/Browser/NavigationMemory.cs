using PageWalkerLocal.Brain;

namespace PageWalkerLocal.Browser;

public sealed class NavigationMemory
{
    private readonly HashSet<string> _visited = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> Visited => _visited;

    public bool MarkVisited(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return _visited.Add(key);
    }

    public bool WasVisited(string key) => _visited.Contains(key);

    public void MarkPlan(ActionPlan plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.MemoryKey))
        {
            MarkVisited(plan.MemoryKey);
        }
    }
}
