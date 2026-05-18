using System.Diagnostics;
using System.Runtime.InteropServices;
using PageWalkerLocal.Brain;
using PageWalkerLocal.Browser;
using PageWalkerLocal.Diagnostics;
using PageWalkerLocal.HumanInput;
using PageWalkerLocal.Perception;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Core;

public sealed class Runner
{
    private readonly AppConfig _config;
    private readonly RuntimePaths _paths;
    private readonly AppLogger _logger;
    private readonly TargetWindowFinder _windowFinder;
    private readonly IScreenCapture _screenCapture;
    private readonly IOcrEngine _ocrEngine;
    private readonly UiaReader _uiaReader;
    private readonly PageClassifier _classifier;
    private readonly Planner _planner;
    private readonly HumanInteractionEngine _humanInteraction;
    private readonly ScreenshotDumper _screenshotDumper;
    private readonly DecisionLogWriter _decisionLogWriter;
    private readonly HtmlReportWriter _reportWriter;
    private readonly NavigationMemory _navigationMemory = new();
    private readonly BrowserStateTracker _browserStateTracker = new();
    private readonly BrowserTabTracker _tabTracker;
    private readonly ActionHistory _history = new();

    private Runner(
        AppConfig config,
        RuntimePaths paths,
        AppLogger logger,
        TargetWindowFinder windowFinder,
        IScreenCapture screenCapture,
        IOcrEngine ocrEngine,
        UiaReader uiaReader,
        PageClassifier classifier,
        Planner planner,
        HumanInteractionEngine humanInteraction,
        ScreenshotDumper screenshotDumper,
        DecisionLogWriter decisionLogWriter,
        HtmlReportWriter reportWriter,
        BrowserTabTracker tabTracker)
    {
        _config = config;
        _paths = paths;
        _logger = logger;
        _windowFinder = windowFinder;
        _screenCapture = screenCapture;
        _ocrEngine = ocrEngine;
        _uiaReader = uiaReader;
        _classifier = classifier;
        _planner = planner;
        _humanInteraction = humanInteraction;
        _screenshotDumper = screenshotDumper;
        _decisionLogWriter = decisionLogWriter;
        _reportWriter = reportWriter;
        _tabTracker = tabTracker;
    }

    public static Runner Create(AppConfig config, RuntimePaths paths, AppLogger logger)
    {
        var windowFinder = new TargetWindowFinder(config, logger);
        var ruleBrain = new RuleBasedBrain(config);
        var brain = new LocalLlmBrain(config, ruleBrain, logger);
        var planner = new Planner(brain, new PlannerRules(config), logger);
        var ocr = config.Ocr.Enabled
            ? new RapidOcrEngine(config.Ocr.ModelsPath, logger)
            : new NullOcrEngine(logger);

        return new Runner(
            config,
            paths,
            logger,
            windowFinder,
            new ScreenCaptureGdi(),
            ocr,
            new UiaReader(logger),
            new PageClassifier(config),
            planner,
            new HumanInteractionEngine(config, logger),
            new ScreenshotDumper(paths, config, logger),
            new DecisionLogWriter(paths, config),
            new HtmlReportWriter(paths),
            new BrowserTabTracker(logger));
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        _logger.Info($"Runtime root: {_paths.RootDirectory}");
        var target = _windowFinder.Find();
        if (target is null)
        {
            _logger.Error("No allowed active Chromium window was found. Focus a browser window or use Rectangle mode.");
            return 3;
        }

        _logger.Info($"Target window: Process='{target.ProcessName}', Title='{target.Title}', Bounds={target.AllowedBounds}.");
        var guard = new WindowGuard(_windowFinder, _config, _logger, target);
        var stopwatch = Stopwatch.StartNew();
        var step = 0;
        var depth = 0;
        var scrollsOnPage = 0;
        var technicalRetries = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (step >= _config.MaxSteps)
            {
                _logger.Info($"Stopping at maxSteps={_config.MaxSteps}.");
                break;
            }

            if (stopwatch.Elapsed.TotalSeconds >= _config.MaxRuntimeSeconds)
            {
                _logger.Info($"Stopping at maxRuntimeSeconds={_config.MaxRuntimeSeconds}.");
                break;
            }

            if (!guard.CheckWindowStillActive())
            {
                _logger.Warning("Pausing/stopping because focus left the target window.");
                return 4;
            }

            using var frame = _screenCapture.Capture(guard.AllowedBounds);
            var screenshotPath = _screenshotDumper.Save(frame, step);
            var ocr = await _ocrEngine.ReadAsync(frame.Bitmap, frame.Bounds, cancellationToken).ConfigureAwait(false);
            var uia = await _uiaReader.ReadCandidatesAsync(target, cancellationToken).ConfigureAwait(false);
            var state = _classifier.Classify(frame, target, ocr, uia);
            var key = _browserStateTracker.BuildPageKey(state);
            _navigationMemory.MarkVisited(key);

            var context = new PlannerContext
            {
                Step = step,
                Depth = depth,
                ScrollsOnCurrentPage = scrollsOnPage,
                Elapsed = stopwatch.Elapsed,
                VisitedKeys = _navigationMemory.Visited
            };

            var plan = await _planner.PlanAsync(context, state, cancellationToken).ConfigureAwait(false);
            _decisionLogWriter.Write(step, context, state, plan, screenshotPath);
            _logger.Info($"Decision step={step}: action={plan.Action}, confidence={plan.Confidence:0.00}, reason={plan.Reason}");

            if (plan.Action == WalkerAction.Stop)
            {
                _history.Add(plan, "stop");
                break;
            }

            if (state.IsTechnicalPage && plan.Action == WalkerAction.PressKey)
            {
                if (technicalRetries >= _config.RetryCount)
                {
                    _logger.Warning("Technical page retry limit reached.");
                    break;
                }

                technicalRetries++;
            }
            else if (!state.IsTechnicalPage)
            {
                technicalRetries = 0;
            }

            string outcome;
            try
            {
                outcome = await _humanInteraction.ExecuteAsync(plan, state, guard, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ExternalException)
            {
                _logger.Error("Safety guard stopped action execution.", ex);
                _history.Add(plan, $"guard-stopped: {ex.Message}");
                return 5;
            }

            _history.Add(plan, outcome);

            if (plan.Action == WalkerAction.Scroll)
            {
                scrollsOnPage++;
            }
            else if (plan.Action == WalkerAction.ClickSafeLink)
            {
                depth++;
                scrollsOnPage = 0;
            }
            else if (plan.Action == WalkerAction.CloseOwnTab)
            {
                _tabTracker.MarkOwnTabClosed();
            }

            if (state.IsTechnicalPage && _config.RetryDelayMs > 0)
            {
                await Task.Delay(_config.DryRun ? Math.Min(_config.RetryDelayMs, 100) : _config.RetryDelayMs, cancellationToken).ConfigureAwait(false);
            }

            step++;
        }

        await CleanupOwnTabsAsync(guard, cancellationToken).ConfigureAwait(false);
        var report = _reportWriter.Write(_history);
        _logger.Info($"Run finished. Report: {report}");
        return 0;
    }

    private async Task CleanupOwnTabsAsync(WindowGuard guard, CancellationToken cancellationToken)
    {
        var tabsToClose = _tabTracker.TabsToCloseOnFinish(_config.CloseOwnTabsOnFinish);
        if (tabsToClose <= 0)
        {
            _logger.Info("No owned tabs to close.");
            return;
        }

        _logger.Info($"Closing {tabsToClose} owned tab(s).");
        for (var i = 0; i < tabsToClose; i++)
        {
            var plan = new ActionPlan
            {
                Action = WalkerAction.CloseOwnTab,
                Reason = "Cleanup of tab opened by PageWalkerLocal.",
                Confidence = 0.95
            };
            var outcome = await _humanInteraction.ExecuteAsync(plan, new PerceptionState(), guard, cancellationToken).ConfigureAwait(false);
            _history.Add(plan, outcome);
            _tabTracker.MarkOwnTabClosed();
        }
    }
}
