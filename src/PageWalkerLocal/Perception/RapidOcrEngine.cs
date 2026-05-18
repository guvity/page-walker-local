using System.Drawing;
using System.Drawing.Imaging;
using PageWalkerLocal.Core;
using PageWalkerLocal.Diagnostics;
using PageWalkerLocal.Windowing;
using RapidOcrNet;
using SkiaSharp;

namespace PageWalkerLocal.Perception;

public sealed class RapidOcrEngine : IOcrEngine, IDisposable
{
    private readonly AppLogger _logger;
    private readonly OcrModelSet? _modelSet;
    private readonly string _requestedModelsPath;
    private readonly bool _configEnabled;
    private readonly object _gate = new();
    private RapidOcr? _engine;
    private bool _failed;
    private bool _statusLogged;

    public RapidOcrEngine(OcrModelSet? modelSet, string requestedModelsPath, bool configEnabled, AppLogger logger)
    {
        _modelSet = modelSet;
        _requestedModelsPath = requestedModelsPath;
        _configEnabled = configEnabled;
        _logger = logger;
    }

    public string Name => "RapidOcrNet";
    public bool IsAvailable => !_failed;
    public RapidOcrFailureKind LastFailureKind { get; private set; } = RapidOcrFailureKind.None;
    public Exception? LastException { get; private set; }

    public bool TryInitialize() => EnsureEngine() is not null;

    public Task<OcrResult> ReadAsync(Bitmap bitmap, ScreenBounds screenBounds, CancellationToken cancellationToken)
    {
        if (_failed)
        {
            return Task.FromResult(OcrResult.Empty);
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
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
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _failed = true;
                LastFailureKind = RapidOcrFailureKind.DetectFailed;
                LastException = ex;
                _logger.Error("RapidOCR Detect failed. OCR will run in limited mode for this process.", ex);
                LogOcrStatus(null, "NullOcrEngine fallback");
                return OcrResult.Empty;
            }
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
                _logger.Info($"RapidOCR requested modelsPath: '{_requestedModelsPath}'.");
                if (_modelSet is not null)
                {
                    LogSelectedModelSet(_modelSet);
                    engine.InitModels(_modelSet.DetPath, _modelSet.ClsPath, _modelSet.RecPath, _modelSet.KeysPath);
                    _logger.Info($"RapidOCR initialized with custom models from '{_modelSet.DirectoryPath}'.");
                }
                else
                {
                    _logger.Warning("RapidOCR has no selected custom model set. Trying bundled/default RapidOcrNet models.");
                    engine.InitModels();
                    _logger.Info("RapidOCR initialized with bundled PP-OCRv5 latin models.");
                }

                _engine = engine;
                LastFailureKind = RapidOcrFailureKind.None;
                LogOcrStatus(null, "RapidOCR");
                return _engine;
            }
            catch (Exception ex)
            {
                _failed = true;
                LastFailureKind = IsOnnxRuntimeNativeFailure(ex)
                    ? RapidOcrFailureKind.OnnxRuntimeNativeInitializationFailed
                    : RapidOcrFailureKind.InitModelsFailed;
                LastException = ex;
                _logger.Error("RapidOCR initialization failed. OCR will run in limited mode for this process.", ex);
                NativeDependencyReport? report = null;
                if (LastFailureKind == RapidOcrFailureKind.OnnxRuntimeNativeInitializationFailed)
                {
                    _logger.Error("ONNX Runtime native initialization failed. This usually means onnxruntime.dll or one of its native dependencies could not load. Check Visual C++ Redistributable x64, bundled native DLLs, file permissions, and CPU/OS compatibility.");
                    report = NativeDependencyDiagnostics.CheckOnnxRuntime(_logger);
                }

                LogOcrStatus(report, "NullOcrEngine fallback");
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

    private static ScreenBounds ToScreenBounds(IReadOnlyList<SKPointI> points, ScreenBounds captureBounds)
    {
        if (points.Count == 0)
        {
            return captureBounds;
        }

        var minX = Convert.ToDouble(points.Min(point => point.X));
        var minY = Convert.ToDouble(points.Min(point => point.Y));
        var maxX = Convert.ToDouble(points.Max(point => point.X));
        var maxY = Convert.ToDouble(points.Max(point => point.Y));
        return new ScreenBounds(
            captureBounds.X + (int)Math.Floor(minX),
            captureBounds.Y + (int)Math.Floor(minY),
            Math.Max(1, (int)Math.Ceiling(maxX - minX)),
            Math.Max(1, (int)Math.Ceiling(maxY - minY)));
    }

    private void LogSelectedModelSet(OcrModelSet modelSet)
    {
        _logger.Info("RapidOCR model set selected:");
        _logger.Info($"Det: {modelSet.DetPath}");
        _logger.Info($"Cls: {modelSet.ClsPath}");
        _logger.Info($"Rec: {modelSet.RecPath}");
        _logger.Info($"Dict: {modelSet.KeysPath}");
        foreach (var file in modelSet.Files)
        {
            _logger.Info($"RapidOCR model file readable: {file}: {FileSystemAccess.YesNo(FileSystemAccess.CanReadFile(file))}");
        }

        var writable = Directory.Exists(modelSet.DirectoryPath)
            && FileSystemAccess.CanWriteDirectoryWithoutCreating(modelSet.DirectoryPath);
        _logger.Info($"RapidOCR selected model directory writable: {FileSystemAccess.YesNo(writable)}.");
        if (!writable)
        {
            _logger.Info("RapidOCR model directory is read-only for current user. This is supported.");
        }
    }

    private void LogOcrStatus(NativeDependencyReport? report, string finalMode)
    {
        if (_statusLogged && finalMode != "NullOcrEngine fallback")
        {
            return;
        }

        _statusLogged = true;
        var selected = _modelSet is null ? "bundled/default" : _modelSet.SourceDescription;
        var readable = _modelSet is null
            ? "n/a"
            : FileSystemAccess.YesNo(_modelSet.Files.All(FileSystemAccess.CanReadFile));
        var directoryWritable = _modelSet is null
            ? "n/a"
            : FileSystemAccess.YesNo(Directory.Exists(_modelSet.DirectoryPath)
                && FileSystemAccess.CanWriteDirectoryWithoutCreating(_modelSet.DirectoryPath));
        var dlls = report?.OnnxRuntimeDllsUnderBaseDirectory.Count.ToString() ?? "not checked";
        var nativeResult = report is null
            ? "not checked"
            : report.SessionOptionsSucceeded ? "ok" : $"failed: {report.ExceptionType}: {report.ExceptionMessage}";

        Action<string> write = finalMode == "RapidOCR" ? _logger.Info : _logger.Warning;
        write("OCR status:");
        write($"- config enabled: {_configEnabled}");
        write($"- requested modelsPath: {_requestedModelsPath}");
        write($"- selected model set: {selected}");
        write($"- model files readable: {readable}");
        write($"- model directory writable: {directoryWritable}");
        write("- read-only model directory supported: yes");
        write($"- onnxruntime dlls found: {dlls}");
        write($"- native diagnostics result: {nativeResult}");
        write($"- final OCR engine mode: {finalMode}");
    }

    private static bool IsOnnxRuntimeNativeFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var typeName = current.GetType().FullName ?? string.Empty;
            if (typeName.Contains("OnnxRuntime", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("onnxruntime", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("NativeMethods", StringComparison.OrdinalIgnoreCase)
                || current is DllNotFoundException
                || current is TypeInitializationException)
            {
                return true;
            }
        }

        return false;
    }
}

public enum RapidOcrFailureKind
{
    None,
    OnnxRuntimeNativeInitializationFailed,
    InitModelsFailed,
    DetectFailed
}
