using System.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;
using PageWalkerLocal.Core;
using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Brain;

public sealed class LocalLlmBrain : IBrain, IDisposable
{
    private readonly AppConfig _config;
    private readonly IBrain _fallback;
    private readonly AppLogger _logger;
    private readonly AllowedActionGenerator _allowedActions;
    private readonly BrainPromptBuilder _promptBuilder = new();
    private readonly BrainJsonParser _parser = new();
    private readonly SemaphoreSlim _modelLock = new(1, 1);
    private LLamaWeights? _model;
    private ModelParams? _modelParams;
    private bool _loadFailed;
    private bool _warnedMissing;

    public LocalLlmBrain(AppConfig config, IBrain fallback, AppLogger logger)
    {
        _config = config;
        _fallback = fallback;
        _logger = logger;
        _allowedActions = new AllowedActionGenerator(config);
    }

    public async Task<ActionPlan> DecideAsync(PlannerContext context, PerceptionState state, CancellationToken cancellationToken)
    {
        if (!_config.LocalBrain.Enabled)
        {
            return await _fallback.DecideAsync(context, state, cancellationToken).ConfigureAwait(false);
        }

        var allowed = _allowedActions.Build(context, state);
        if (allowed.Count == 0)
        {
            return ActionPlan.Stop("No allowed actions were generated.");
        }

        var modelPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _config.LocalBrain.ModelPath));
        if (!File.Exists(modelPath))
        {
            WarnMissing(modelPath);
            return await _fallback.DecideAsync(context, state, cancellationToken).ConfigureAwait(false);
        }

        if (!await EnsureLoadedAsync(modelPath, cancellationToken).ConfigureAwait(false))
        {
            return await _fallback.DecideAsync(context, state, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var prompt = BuildStrictPrompt(context, state, allowed);
            var output = await InferAsync(prompt, cancellationToken).ConfigureAwait(false);
            var parsed = _parser.TryParse(output, allowed);
            if (parsed is not null && parsed.Confidence >= _config.LocalBrain.MinConfidence)
            {
                return parsed;
            }

            _logger.Warning($"Local LLM returned invalid or low-confidence JSON. Raw='{TrimForLog(output)}'. Falling back to RuleBasedBrain.");
        }
        catch (Exception ex)
        {
            _logger.Error("Local LLM inference failed. Falling back to RuleBasedBrain.", ex);
        }

        return await _fallback.DecideAsync(context, state, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _model?.Dispose();
        _modelLock.Dispose();
    }

    private async Task<bool> EnsureLoadedAsync(string modelPath, CancellationToken cancellationToken)
    {
        if (_model is not null)
        {
            return true;
        }

        if (_loadFailed)
        {
            return false;
        }

        await _modelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_model is not null)
            {
                return true;
            }

            _modelParams = new ModelParams(modelPath)
            {
                ContextSize = 2048,
                GpuLayerCount = 0,
                Seed = _config.RandomSeed ?? 1337
            };
            _model = LLamaWeights.LoadFromFile(_modelParams);
            _logger.Info($"Loaded local GGUF model through LLamaSharp: '{modelPath}'.");
            return true;
        }
        catch (Exception ex)
        {
            _loadFailed = true;
            _logger.Error($"Failed to load local GGUF model at '{modelPath}'.", ex);
            return false;
        }
        finally
        {
            _modelLock.Release();
        }
    }

    private async Task<string> InferAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_model is null || _modelParams is null)
        {
            throw new InvalidOperationException("Local LLM model is not loaded.");
        }

        using var context = _model.CreateContext(_modelParams);
        var executor = new InteractiveExecutor(context);
        var inferenceParams = new InferenceParams
        {
            MaxTokens = Math.Clamp(_config.LocalBrain.MaxTokens, 16, 512),
            AntiPrompts = ["\n\n", "</json>", "User:"],
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = (float)Math.Clamp(_config.LocalBrain.Temperature, 0.0, 1.2)
            }
        };

        var builder = new StringBuilder();
        await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken).ConfigureAwait(false))
        {
            builder.Append(token);
            if (builder.ToString().Contains('}'))
            {
                break;
            }
        }

        return builder.ToString();
    }

    private string BuildStrictPrompt(PlannerContext context, PerceptionState state, IReadOnlyList<ActionPlan> allowed)
    {
        return """
You are PageWalkerLocal local decision brain.
Return strict JSON only. No markdown. No extra text.
Choose exactly one action from allowedActionDetails.
Never invent targetId or coordinates.
Never choose payment, deposit, CAPTCHA, account creation, or forbidden actions.
Schema:
{"action":"SCROLL","targetId":null,"reason":"short reason","confidence":0.75}

State:
""" + _promptBuilder.Build(context, state, allowed) + "\nJSON:";
    }

    private void WarnMissing(string modelPath)
    {
        if (_warnedMissing)
        {
            return;
        }

        _logger.Warning($"Local LLM is enabled, but model was not found at '{modelPath}'. Falling back to RuleBasedBrain.");
        _warnedMissing = true;
    }

    private static string TrimForLog(string text) =>
        text.Length <= 240 ? text : text[..240] + "...";
}
