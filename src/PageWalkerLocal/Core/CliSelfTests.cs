using System.Drawing;
using PageWalkerLocal.Diagnostics;
using PageWalkerLocal.Perception;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.Core;

public static class CliSelfTests
{
    public static int RunModelDiscoveryTest(AppConfig config, RuntimePaths paths, AppLogger logger)
    {
        try
        {
            var permissions = RuntimePermissionDiagnostics.Check(config, paths, logger);
            if (!permissions.UserRuntimeWritable)
            {
                return 1;
            }

            var discovery = new ModelDiscovery();
            var roots = discovery.GetModelRoots(config, paths);
            logger.Info("Model discovery roots:");
            foreach (var root in roots)
            {
                logger.Info($"- path='{root.RootPath}', source='{root.Source}', exists={YesNo(root.Exists)}, readable={YesNo(root.IsReadable)}, writable={YesNo(root.IsWritable)}, read-only ok=yes, model files found={root.ModelFileCount}");
            }

            var ocrSets = discovery.FindOcrModelSets(config, paths, logger);
            logger.Info($"RapidOCR model sets found: {ocrSets.Count}");
            foreach (var set in ocrSets)
            {
                logger.Info($"- OCR set: {set.SourceDescription}");
                logger.Info($"  det={set.DetPath}");
                logger.Info($"  cls={set.ClsPath}");
                logger.Info($"  rec={set.RecPath}");
                logger.Info($"  dict={set.KeysPath}");
            }

            var llmModels = discovery.FindLlmModels(config, paths, logger);
            logger.Info($"GGUF models found: {llmModels.Count}");
            foreach (var model in llmModels)
            {
                logger.Info($"- GGUF: {model.ModelPath}, bytes={model.Length}, readable={YesNo(model.IsReadable)}, source={model.SourceDescription}");
            }

            var selectedOcr = discovery.SelectOcrModelSet(config, paths, logger);
            logger.Info($"Selected OCR model set: {(selectedOcr is null ? "none" : selectedOcr.SourceDescription)}");
            var selectedLlm = discovery.SelectLlmModelPath(config, paths, logger);
            logger.Info($"Selected LLM GGUF model: {selectedLlm ?? "none"}");
            return 0;
        }
        catch (Exception ex)
        {
            logger.Error("Model discovery self-test crashed.", ex);
            return 1;
        }
    }

    public static async Task<int> RunOcrSelfTestAsync(AppConfig config, RuntimePaths paths, AppLogger logger, CancellationToken cancellationToken)
    {
        RapidOcrEngine? engine = null;
        try
        {
            var permissions = RuntimePermissionDiagnostics.Check(config, paths, logger);
            if (!permissions.UserRuntimeWritable)
            {
                return 1;
            }

            var discovery = new ModelDiscovery();
            var selected = discovery.SelectOcrModelSet(config, paths, logger);
            if (selected is null)
            {
                logger.Warning("OCR self-test did not find a complete custom OCR model set. Bundled/default RapidOcrNet initialization will be tried.");
            }
            else
            {
                foreach (var file in selected.Files)
                {
                    logger.Info($"OCR self-test file readable: {file}: {YesNo(FileSystemAccess.CanReadFile(file))}");
                }

                var writable = Directory.Exists(selected.DirectoryPath)
                    && FileSystemAccess.CanWriteDirectoryWithoutCreating(selected.DirectoryPath);
                logger.Info($"OCR self-test selected model directory writable: {YesNo(writable)}.");
                logger.Info("Read-only OCR model directory supported: yes.");
            }

            var nativeReport = NativeDependencyDiagnostics.CheckOnnxRuntime(logger);
            engine = new RapidOcrEngine(selected, config.Ocr.ModelsPath, config.Ocr.Enabled, logger);
            var initialized = engine.TryInitialize();
            if (!initialized)
            {
                if (engine.LastFailureKind == RapidOcrFailureKind.OnnxRuntimeNativeInitializationFailed
                    || nativeReport.NativeInitializationFailed)
                {
                    return 10;
                }

                return selected is null ? 2 : 11;
            }

            if (nativeReport.NativeInitializationFailed)
            {
                return 10;
            }

            using var bitmap = CreateTinyTestBitmap();
            var result = await engine.ReadAsync(bitmap, new ScreenBounds(0, 0, bitmap.Width, bitmap.Height), cancellationToken).ConfigureAwait(false);
            if (engine.LastFailureKind == RapidOcrFailureKind.DetectFailed)
            {
                return 12;
            }

            logger.Info($"OCR self-test completed. Text length={result.Text.Length}, line count={result.Lines.Count}.");
            return selected is null ? 2 : 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.Error("OCR self-test crashed during Detect.", ex);
            return engine?.LastFailureKind switch
            {
                RapidOcrFailureKind.OnnxRuntimeNativeInitializationFailed => 10,
                RapidOcrFailureKind.InitModelsFailed => 11,
                _ => 12
            };
        }
        finally
        {
            engine?.Dispose();
        }
    }

    private static Bitmap CreateTinyTestBitmap()
    {
        var bitmap = new Bitmap(260, 96);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        using var brush = new SolidBrush(Color.Black);
        graphics.DrawString("PageWalker test", SystemFonts.DefaultFont, brush, new PointF(12, 30));
        return bitmap;
    }

    private static string YesNo(bool value) => FileSystemAccess.YesNo(value);
}
