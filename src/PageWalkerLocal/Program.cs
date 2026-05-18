using PageWalkerLocal.Core;
using PageWalkerLocal.Diagnostics;
using PageWalkerLocal.HumanInput;

var paths = RuntimePaths.CreateDefault();
try
{
    paths.EnsureCreated();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"User runtime directory is not writable. PageWalkerLocal cannot run safely. {ex.Message}");
    return 1;
}

var bootstrapLogger = AppLogger.Create(paths, "Information");
var configPath = CliArgs.GetValue(args, "--config") ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var config = AppConfig.Load(configPath, bootstrapLogger);
using var logger = AppLogger.Create(paths, config.Logging.Level);

logger.Info($"PageWalkerLocal starting. Config='{configPath}', DryRun={config.DryRun}.");
var permissionReport = RuntimePermissionDiagnostics.Check(config, paths, logger);
if (!permissionReport.UserRuntimeWritable)
{
    return 1;
}

if (CliArgs.HasFlag(args, "--model-discovery-test"))
{
    return CliSelfTests.RunModelDiscoveryTest(config, paths, logger);
}

if (CliArgs.HasFlag(args, "--ocr-self-test"))
{
    return await CliSelfTests.RunOcrSelfTestAsync(config, paths, logger, CancellationToken.None).ConfigureAwait(false);
}

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
    public static bool HasFlag(string[] args, string key) =>
        args.Any(arg => string.Equals(arg, key, StringComparison.OrdinalIgnoreCase));

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
