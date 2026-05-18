using System.Drawing;
using PageWalkerLocal.Core;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Perception;

public sealed class NullOcrEngine : IOcrEngine
{
    private readonly AppLogger _logger;
    private bool _warned;

    public NullOcrEngine(AppLogger logger)
    {
        _logger = logger;
    }

    public string Name => "NullOCR";
    public bool IsAvailable => false;

    public Task<OcrResult> ReadAsync(Bitmap bitmap, ScreenBounds screenBounds, CancellationToken cancellationToken)
    {
        if (!_warned)
        {
            _logger.Warning("OCR engine is unavailable. Running in limited mode with UI Automation/window-text signals only.");
            _warned = true;
        }

        return Task.FromResult(OcrResult.Empty);
    }
}
