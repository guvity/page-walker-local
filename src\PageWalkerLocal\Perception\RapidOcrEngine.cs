using System.Drawing;
using PageWalkerLocal.Core;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Perception;

public sealed class RapidOcrEngine : IOcrEngine
{
    private readonly NullOcrEngine _fallback;
    private readonly AppLogger _logger;
    private readonly string _modelsPath;
    private bool _warned;

    public RapidOcrEngine(string modelsPath, AppLogger logger)
    {
        _modelsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, modelsPath));
        _logger = logger;
        _fallback = new NullOcrEngine(logger);
    }

    public string Name => "RapidOCR-placeholder";
    public bool IsAvailable => Directory.Exists(_modelsPath);

    public Task<OcrResult> ReadAsync(Bitmap bitmap, ScreenBounds screenBounds, CancellationToken cancellationToken)
    {
        if (!_warned)
        {
            if (IsAvailable)
            {
                _logger.Warning("RapidOCR models directory exists, but Phase 1 uses the IOcrEngine placeholder. Add RapidOCR binding in Phase 2.");
            }
            else
            {
                _logger.Warning($"RapidOCR models not found at '{_modelsPath}'. Falling back to NullOCR.");
            }

            _warned = true;
        }

        return _fallback.ReadAsync(bitmap, screenBounds, cancellationToken);
    }
}
