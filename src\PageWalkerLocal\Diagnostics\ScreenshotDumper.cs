using System.Drawing.Imaging;
using PageWalkerLocal.Core;
using PageWalkerLocal.Perception;

namespace PageWalkerLocal.Diagnostics;

public sealed class ScreenshotDumper
{
    private readonly RuntimePaths _paths;
    private readonly AppConfig _config;
    private readonly AppLogger _logger;

    public ScreenshotDumper(RuntimePaths paths, AppConfig config, AppLogger logger)
    {
        _paths = paths;
        _config = config;
        _logger = logger;
    }

    public string? Save(CaptureFrame frame, int step)
    {
        if (!_config.Logging.SaveScreenshots)
        {
            return null;
        }

        var file = Path.Combine(_paths.DebugDirectory, $"screenshot-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}-step-{step:D4}.png");
        frame.Bitmap.Save(file, ImageFormat.Png);
        _logger.Debug($"Saved screenshot: {file}");
        return file;
    }
}
