using PageWalkerLocal.Core;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.HumanInput;

public sealed class SafeInputController
{
    private readonly bool _dryRun;
    private readonly AppLogger _logger;

    public SafeInputController(bool dryRun, AppLogger logger)
    {
        _dryRun = dryRun;
        _logger = logger;
    }

    public async Task MoveAlongPathAsync(IReadOnlyList<TimedMousePoint> points, WindowGuard guard, double confidence, CancellationToken cancellationToken)
    {
        if (points.Count == 0)
        {
            return;
        }

        _logger.Info($"Planned human-like mouse path: points={points.Count}, from={points[0].Point}, to={points[^1].Point}, durationMs={points[^1].At.TotalMilliseconds:0}.");
        if (_dryRun)
        {
            return;
        }

        var previous = TimeSpan.Zero;
        var enteredAllowedBounds = false;
        for (var i = 0; i < points.Count; i++)
        {
            var timed = points[i];
            cancellationToken.ThrowIfCancellationRequested();

            if (!guard.CheckWindowStillActive())
            {
                throw new InvalidOperationException("Target window lost focus during mouse movement.");
            }

            enteredAllowedBounds |= guard.AllowedBounds.Contains(timed.Point);
            var mustValidatePoint = enteredAllowedBounds || i == points.Count - 1;
            if (mustValidatePoint)
            {
                var guardResult = guard.CheckAction(timed.Point, confidence);
                if (!guardResult.Allowed)
                {
                    throw new InvalidOperationException(guardResult.Reason);
                }
            }

            var delay = timed.At - previous;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            Win32Input.MoveMouseAbsolute(timed.Point);
            previous = timed.At;
        }
    }

    public async Task ClickAsync(ScreenPoint point, WindowGuard guard, double confidence, int preClickDelayMs, CancellationToken cancellationToken)
    {
        var guardResult = guard.CheckAction(point, confidence);
        if (!guardResult.Allowed)
        {
            throw new InvalidOperationException(guardResult.Reason);
        }

        _logger.Info($"Planned click at {point}, preClickDelayMs={preClickDelayMs}, dryRun={_dryRun}.");
        if (_dryRun)
        {
            return;
        }

        await Task.Delay(preClickDelayMs, cancellationToken).ConfigureAwait(false);
        Win32Input.LeftClick();
    }

    public async Task ScrollAsync(IReadOnlyList<ScrollStep> steps, WindowGuard guard, CancellationToken cancellationToken)
    {
        _logger.Info($"Planned human-like scroll sequence: steps={steps.Count}, dryRun={_dryRun}.");
        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!guard.CheckWindowStillActive())
            {
                throw new InvalidOperationException("Target window lost focus before scroll.");
            }

            _logger.Debug($"Scroll delta={step.WheelDelta}, pauseMs={step.PauseMs}.");
            if (!_dryRun)
            {
                Win32Input.ScrollWheel(step.WheelDelta);
            }

            await Task.Delay(_dryRun ? Math.Min(step.PauseMs, 40) : step.PauseMs, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task KeyPressAsync(ConsoleKey key, WindowGuard guard, CancellationToken cancellationToken)
    {
        if (!guard.CheckWindowStillActive())
        {
            throw new InvalidOperationException("Target window lost focus before key press.");
        }

        _logger.Info($"Planned key press {key}, dryRun={_dryRun}.");
        if (!_dryRun)
        {
            Win32Input.KeyPress(key);
        }

        return Task.CompletedTask;
    }

    public Task CtrlKeyPressAsync(ConsoleKey key, WindowGuard guard, CancellationToken cancellationToken)
    {
        if (!guard.CheckWindowStillActive())
        {
            throw new InvalidOperationException("Target window lost focus before key chord.");
        }

        _logger.Info($"Planned Ctrl+{key}, dryRun={_dryRun}.");
        if (!_dryRun)
        {
            Win32Input.CtrlKeyPress(key);
        }

        return Task.CompletedTask;
    }

    public Task TypeTextAsync(string text, WindowGuard guard, CancellationToken cancellationToken)
    {
        if (!guard.CheckWindowStillActive())
        {
            throw new InvalidOperationException("Target window lost focus before typing.");
        }

        _logger.Info($"Planned text typing length={text.Length}, dryRun={_dryRun}.");
        if (!_dryRun)
        {
            Win32Input.TypeText(text);
        }

        return Task.CompletedTask;
    }
}
