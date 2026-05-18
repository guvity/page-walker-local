using PageWalkerLocal.Brain;
using PageWalkerLocal.Core;
using PageWalkerLocal.Perception;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.HumanInput;

public sealed class HumanInteractionEngine
{
    private readonly InteractionProfile _profile;
    private readonly Random _random;
    private readonly HumanMousePathGenerator _mousePathGenerator;
    private readonly HumanScrollGenerator _scrollGenerator;
    private readonly HumanReadingDelayEstimator _readingDelayEstimator;
    private readonly SafeInputController _input;
    private readonly AppConfig _config;
    private readonly AppLogger _logger;

    public HumanInteractionEngine(AppConfig config, AppLogger logger)
    {
        _config = config;
        _logger = logger;
        _profile = InteractionProfile.FromName(config.BehaviorProfile);
        _random = config.RandomSeed.HasValue ? new Random(config.RandomSeed.Value) : new Random();
        _mousePathGenerator = new HumanMousePathGenerator(_random);
        _scrollGenerator = new HumanScrollGenerator(_random);
        _readingDelayEstimator = new HumanReadingDelayEstimator(_random);
        _input = new SafeInputController(config.DryRun, logger);
    }

    public async Task<string> ExecuteAsync(ActionPlan plan, PerceptionState state, WindowGuard guard, CancellationToken cancellationToken)
    {
        switch (plan.Action)
        {
            case WalkerAction.HumanRead:
                await HumanReadAsync(state, guard, cancellationToken).ConfigureAwait(false);
                return "read";
            case WalkerAction.Scroll:
                await ScrollAsync(state, guard, cancellationToken).ConfigureAwait(false);
                return "scrolled";
            case WalkerAction.ClosePopup:
            case WalkerAction.AnswerSimpleGate:
            case WalkerAction.ClickSafeButton:
            case WalkerAction.ClickSafeLink:
                await ClickTargetAsync(plan, guard, cancellationToken).ConfigureAwait(false);
                return "clicked";
            case WalkerAction.PressKey:
                await _input.KeyPressAsync(plan.Key ?? ConsoleKey.F5, guard, cancellationToken).ConfigureAwait(false);
                return "key-pressed";
            case WalkerAction.CloseOwnTab:
                await _input.CtrlKeyPressAsync(ConsoleKey.W, guard, cancellationToken).ConfigureAwait(false);
                return "close-own-tab-requested";
            case WalkerAction.FillAllowedFormField:
                await ClickTargetAsync(plan, guard, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(plan.TextInput))
                {
                    await _input.TypeTextAsync(plan.TextInput, guard, cancellationToken).ConfigureAwait(false);
                }
                return "form-filled";
            case WalkerAction.NoOp:
                await Task.Delay(_config.DryRun ? 20 : 150, cancellationToken).ConfigureAwait(false);
                return "no-op";
            case WalkerAction.Stop:
                return "stop";
            default:
                return "ignored";
        }
    }

    private async Task ClickTargetAsync(ActionPlan plan, WindowGuard guard, CancellationToken cancellationToken)
    {
        if (plan.Target is null)
        {
            throw new InvalidOperationException($"Action {plan.Action} requires a target.");
        }

        var target = plan.Target.Center;
        var current = Win32Input.GetCursorPosition();
        var path = BuildSafePath(current, target, guard.AllowedBounds);
        await _input.MoveAlongPathAsync(path, guard, plan.Confidence, cancellationToken).ConfigureAwait(false);
        var preClickDelay = _random.Next(_profile.MinPreClickDelayMs, _profile.MaxPreClickDelayMs + 1);
        await _input.ClickAsync(target, guard, plan.Confidence, preClickDelay, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScrollAsync(PerceptionState state, WindowGuard guard, CancellationToken cancellationToken)
    {
        await EnsureCursorInsideAsync(guard, cancellationToken).ConfigureAwait(false);
        var steps = _scrollGenerator.Generate(_profile, state.VisibleText.Length);
        await _input.ScrollAsync(steps, guard, cancellationToken).ConfigureAwait(false);
    }

    private async Task HumanReadAsync(PerceptionState state, WindowGuard guard, CancellationToken cancellationToken)
    {
        var delay = _readingDelayEstimator.EstimateDelayMs(state.VisibleText, _profile);
        if (_config.DryRun)
        {
            delay = Math.Min(delay, _profile.DryRunDelayCapMs);
        }

        _logger.Info($"Planned reading dwell time: {delay} ms for visibleTextLength={state.VisibleText.Length}.");
        await MaybeMicroMoveAsync(guard, cancellationToken).ConfigureAwait(false);
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureCursorInsideAsync(WindowGuard guard, CancellationToken cancellationToken)
    {
        var current = Win32Input.GetCursorPosition();
        if (guard.AllowedBounds.Contains(current))
        {
            return;
        }

        var target = guard.AllowedBounds.Center;
        var path = BuildSafePath(current, target, guard.AllowedBounds);
        await _input.MoveAlongPathAsync(path, guard, 0.8, cancellationToken).ConfigureAwait(false);
    }

    private async Task MaybeMicroMoveAsync(WindowGuard guard, CancellationToken cancellationToken)
    {
        if (_random.NextDouble() > 0.35)
        {
            return;
        }

        var center = guard.AllowedBounds.Center;
        var target = new ScreenPoint(
            center.X + _random.Next(-Math.Max(8, guard.AllowedBounds.Width / 12), Math.Max(9, guard.AllowedBounds.Width / 12)),
            center.Y + _random.Next(-Math.Max(8, guard.AllowedBounds.Height / 12), Math.Max(9, guard.AllowedBounds.Height / 12)));
        target = guard.AllowedBounds.Clamp(target);
        var current = Win32Input.GetCursorPosition();
        var path = BuildSafePath(current, target, guard.AllowedBounds);
        await _input.MoveAlongPathAsync(path, guard, 0.8, cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyList<TimedMousePoint> BuildSafePath(ScreenPoint current, ScreenPoint target, ScreenBounds allowedBounds)
    {
        target = allowedBounds.Clamp(target);
        var raw = _mousePathGenerator.Generate(current, target, _profile);
        var safe = new List<TimedMousePoint>(raw.Count);
        var enteredBounds = false;
        for (var i = 0; i < raw.Count; i++)
        {
            var point = raw[i].Point;
            enteredBounds |= allowedBounds.Contains(point);
            if (enteredBounds || i == raw.Count - 1)
            {
                point = allowedBounds.Clamp(point);
            }

            safe.Add(raw[i] with { Point = i == raw.Count - 1 ? target : point });
        }

        return safe;
    }
}
