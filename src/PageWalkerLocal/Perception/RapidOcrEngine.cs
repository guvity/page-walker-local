using System.Drawing;
using System.Drawing.Imaging;
using PageWalkerLocal.Core;
using PageWalkerLocal.Windowing;
using RapidOcrNet;
using SkiaSharp;

namespace PageWalkerLocal.Perception;

public sealed class RapidOcrEngine : IOcrEngine, IDisposable
{
    private readonly AppLogger _logger;
    private readonly string _modelsPath;
    private readonly object _gate = new();
    private RapidOcr? _engine;
    private bool _failed;

    public RapidOcrEngine(string modelsPath, AppLogger logger)
    {
        _modelsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, modelsPath));
        _logger = logger;
    }

    public string Name => "RapidOcrNet";
    public bool IsAvailable => !_failed;

    public Task<OcrResult> ReadAsync(Bitmap bitmap, ScreenBounds screenBounds, CancellationToken cancellationToken)
    {
        if (_failed)
        {
            return Task.FromResult(OcrResult.Empty);
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var engine = EnsureEngine();
            if (engine is null)
            {
                return OcrResult.Empty;
            }

            using var skBitmap = ToSkBitmap(bitmap);
            var rapidResult = engine.Detect(skBitmap, RapidOcrOptions.Default);
            var lines = new List<OcrTextLine>();

            foreach (var block in rapidResult.TextBlocks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = block.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var bounds = ToScreenBounds(block.BoxPoints, screenBounds);
                var confidence = block.CharScores is not null && block.CharScores.Any()
                    ? Math.Clamp(block.CharScores.Average(score => (double)score), 0.0, 1.0)
                    : 0.80;
                lines.Add(new OcrTextLine(text, bounds, confidence));
            }

            var textResult = string.IsNullOrWhiteSpace(rapidResult.StrRes)
                ? string.Join(Environment.NewLine, lines.Select(line => line.Text))
                : rapidResult.StrRes.Trim();
            return new OcrResult(textResult, lines);
        }, cancellationToken);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _engine?.Dispose();
            _engine = null;
        }
    }

    private RapidOcr? EnsureEngine()
    {
        lock (_gate)
        {
            if (_engine is not null)
            {
                return _engine;
            }

            try
            {
                var engine = new RapidOcr();
                var custom = FindCustomModelSet(_modelsPath);
                if (custom is not null)
                {
                    engine.InitModels(custom.DetPath, custom.ClsPath, custom.RecPath, custom.KeysPath);
                    _logger.Info($"RapidOCR initialized with custom models from '{_modelsPath}'.");
                }
                else
                {
                    engine.InitModels();
                    _logger.Info("RapidOCR initialized with bundled PP-OCRv5 latin models.");
                }

                _engine = engine;
                return _engine;
            }
            catch (Exception ex)
            {
                _failed = true;
                _logger.Error("RapidOCR initialization failed. OCR will run in limited mode for this process.", ex);
                return null;
            }
        }
    }

    private static SKBitmap ToSkBitmap(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        var decoded = SKBitmap.Decode(stream);
        if (decoded is null)
        {
            throw new InvalidOperationException("RapidOCR could not decode screenshot bitmap.");
        }

        return decoded;
    }

    private static ScreenBounds ToScreenBounds(IReadOnlyList<SKPoint> points, ScreenBounds captureBounds)
    {
        if (points.Count == 0)
        {
            return captureBounds;
        }

        var minX = points.Min(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxX = points.Max(point => point.X);
        var maxY = points.Max(point => point.Y);
        return new ScreenBounds(
            captureBounds.X + (int)Math.Floor(minX),
            captureBounds.Y + (int)Math.Floor(minY),
            Math.Max(1, (int)Math.Ceiling(maxX - minX)),
            Math.Max(1, (int)Math.Ceiling(maxY - minY)));
    }

    private static OcrModelSet? FindCustomModelSet(string modelsPath)
    {
        if (!Directory.Exists(modelsPath))
        {
            return null;
        }

        var onnx = Directory.GetFiles(modelsPath, "*.onnx", SearchOption.AllDirectories);
        var det = onnx.FirstOrDefault(path => Path.GetFileName(path).Contains("det", StringComparison.OrdinalIgnoreCase));
        var cls = onnx.FirstOrDefault(path => Path.GetFileName(path).Contains("cls", StringComparison.OrdinalIgnoreCase));
        var rec = onnx.FirstOrDefault(path => Path.GetFileName(path).Contains("rec", StringComparison.OrdinalIgnoreCase));
        var keys = Directory.GetFiles(modelsPath, "*.txt", SearchOption.AllDirectories)
            .FirstOrDefault(path => Path.GetFileName(path).Contains("dict", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Contains("keys", StringComparison.OrdinalIgnoreCase));

        return det is not null && cls is not null && rec is not null && keys is not null
            ? new OcrModelSet(det, cls, rec, keys)
            : null;
    }

    private sealed record OcrModelSet(string DetPath, string ClsPath, string RecPath, string KeysPath);
}
