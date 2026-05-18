using PageWalkerLocal.Core;
using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Brain;

public sealed class Planner
{
    private readonly IBrain _brain;
    private readonly PlannerRules _rules;
    private readonly AppLogger _logger;

    public Planner(IBrain brain, PlannerRules rules, AppLogger logger)
    {
        _brain = brain;
        _rules = rules;
        _logger = logger;
    }

    public async Task<ActionPlan> PlanAsync(PlannerContext context, PerceptionState state, CancellationToken cancellationToken)
    {
        var raw = await _brain.DecideAsync(context, state, cancellationToken).ConfigureAwait(false);
        var guarded = _rules.Validate(raw, state);
        if (!guarded.Allowed)
        {
            _logger.Warning($"Planner rejected action {raw.Action}: {guarded.Reason}");
        }

        return guarded.Plan;
    }
}
