using PageWalkerLocal.Core;
using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Brain;

public sealed class LocalLlmBrain : IBrain
{
    private readonly AppConfig _config;
    private readonly IBrain _fallback;
    private readonly AppLogger _logger;
    private bool _warned;

    public LocalLlmBrain(AppConfig config, IBrain fallback, AppLogger logger)
    {
        _config = config;
        _fallback = fallback;
        _logger = logger;
    }

    public Task<ActionPlan> DecideAsync(PlannerContext context, PerceptionState state, CancellationToken cancellationToken)
    {
        if (!_config.LocalBrain.Enabled)
        {
            return _fallback.DecideAsync(context, state, cancellationToken);
        }

        var modelPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _config.LocalBrain.ModelPath));
        if (!File.Exists(modelPath))
        {
            if (!_warned)
            {
                _logger.Warning($"Local LLM is enabled, but model was not found at '{modelPath}'. Falling back to RuleBasedBrain.");
                _warned = true;
            }

            return _fallback.DecideAsync(context, state, cancellationToken);
        }

        if (!_warned)
        {
            _logger.Warning("Local LLM Phase 2 adapter is not active in this MVP. RuleBasedBrain remains authoritative.");
            _warned = true;
        }

        return _fallback.DecideAsync(context, state, cancellationToken);
    }
}
