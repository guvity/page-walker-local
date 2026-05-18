using PageWalkerLocal.Core;
using PageWalkerLocal.HumanInput;

var paths = RuntimePaths.CreateDefault();
paths.EnsureCreated();

var bootstrapLogger = AppLogger.Create(paths, "Information");
var configPath = CliArgs.GetValue(args, "--config") ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var config = AppConfig.Load(configPath, bootstrapLogger);
using var logger = AppLogger.Create(paths, config.Logging.Level);

logger.Info($"PageWalkerLocal starting. Config='{configPath}', DryRun={config.DryRun}.");

using var sessionLock = UserSessionLock.TryAcquire(logger);
if (sessionLock is null)
{
    logger.Warning("Another PageWalkerLocal instance is already running for this user/session.");
    return 2;
}

using var cts = new CancellationTokenSource();
using var hotkey = Win32Input.StartEmergencyHotkeyListener(
    () =>
    {
        logger.Warning("Emergency stop hotkey Ctrl+Alt+F12 received.");
        cts.Cancel();
    },
    logger);

try
{
    var runner = Runner.Create(config, paths, logger);
    return await runner.RunAsync(cts.Token).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
    logger.Warning("Run cancelled.");
    return 130;
}
catch (Exception ex)
{
    logger.Error("Fatal error.", ex);
    return 1;
}

internal static class CliArgs
{
    public static string? GetValue(string[] args, string key)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return i + 1 < args.Length ? args[i + 1] : null;
        }

        return null;
    }
}
