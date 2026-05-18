using System.Text.Json;
using PageWalkerLocal.Brain;
using PageWalkerLocal.Core;
using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Diagnostics;

public sealed class DecisionLogWriter
{
    private readonly string _file;
    private readonly AppConfig _config;
    private readonly object _gate = new();

    public DecisionLogWriter(RuntimePaths paths, AppConfig config)
    {
        _config = config;
        _file = Path.Combine(paths.DebugDirectory, $"decisions-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.jsonl");
    }

    public void Write(int step, PlannerContext context, PerceptionState state, ActionPlan plan, string? screenshotPath)
    {
        if (!_config.Logging.SaveDecisionJson)
        {
            return;
        }

        var row = new
        {
            timestamp = DateTimeOffset.Now,
            step,
            context = new
            {
                context.Depth,
                context.ScrollsOnCurrentPage,
                elapsedSeconds = context.Elapsed.TotalSeconds
            },
            page = new
            {
                state.WindowTitle,
                state.TextHash,
                state.ScreenshotHash,
                state.IsTechnicalPage,
                state.HasCaptchaLikeText,
                signals = state.ClassifierSignals,
                candidateCount = state.Candidates.Count
            },
            plan = new
            {
                action = plan.Action.ToString(),
                plan.TargetId,
                plan.Reason,
                plan.Confidence
            },
            screenshotPath
        };

        var json = JsonSerializer.Serialize(row);
        lock (_gate)
        {
            File.AppendAllText(_file, json + Environment.NewLine);
        }
    }
}
