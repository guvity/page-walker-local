using System.Text.Json;

namespace PageWalkerLocal.Core;

public sealed record ModelRootStatus(
    string RootPath,
    string Source,
    bool Exists,
    bool IsReadable,
    bool IsWritable,
    int ModelFileCount,
    string? Error);

public sealed record OcrModelSet(
    string DetPath,
    string ClsPath,
    string RecPath,
    string KeysPath,
    string SourceDescription)
{
    public string DirectoryPath => Path.GetDirectoryName(DetPath) ?? string.Empty;

    public IReadOnlyList<string> Files => [DetPath, ClsPath, RecPath, KeysPath];
}

public sealed record LlmModelCandidate(
    string ModelPath,
    long Length,
    bool IsReadable,
    string SourceDescription);

public sealed class ModelDiscovery
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    public IReadOnlyList<ModelRootStatus> GetModelRoots(AppConfig config, RuntimePaths paths)
    {
        var roots = new List<(string Path, string Source)>();

        AddConfiguredModelRoot(roots, config.ModelsRoot);
        AddRoot(roots, Path.Combine(AppContext.BaseDirectory, "models"), "AppContext.BaseDirectory/models");
        AddRoot(roots, Path.Combine(Directory.GetCurrentDirectory(), "models"), "CurrentDirectory/models");
        AddRoot(roots, paths.UserModelsDirectory, "%LOCALAPPDATA%/PageWalkerLocal/models");

        return roots
            .GroupBy(root => Normalize(root.Path), PathComparer)
            .Select(group => group.First())
            .Select(root => BuildRootStatus(root.Path, root.Source))
            .OrderBy(status => status.RootPath, PathComparer)
            .ToArray();
    }

    public IReadOnlyList<OcrModelSet> FindOcrModelSets(AppConfig config, RuntimePaths paths, AppLogger logger)
    {
        var requestedSets = FindRequestedOcrModelSets(config, paths, logger);
        if (requestedSets.Count > 0 && !ShouldAutoDiscover(config.Ocr.ModelsPath))
        {
            PersistDiscoveryCache(paths, requestedSets, Array.Empty<LlmModelCandidate>(), logger);
            return requestedSets;
        }

        var discovered = GetModelRoots(config, paths)
            .Where(root => root.Exists && root.IsReadable)
            .SelectMany(root => FindOcrModelSetsInRoot(root.RootPath, root.Source, logger))
            .Concat(requestedSets)
            .DistinctBy(set => $"{Normalize(set.DetPath)}|{Normalize(set.ClsPath)}|{Normalize(set.RecPath)}|{Normalize(set.KeysPath)}", PathComparer)
            .OrderByDescending(ScoreOcrModelSet)
            .ThenBy(set => set.DirectoryPath, PathComparer)
            .ThenBy(set => set.DetPath, PathComparer)
            .ToArray();

        PersistDiscoveryCache(paths, discovered, Array.Empty<LlmModelCandidate>(), logger);
        return discovered;
    }

    public OcrModelSet? SelectOcrModelSet(AppConfig config, RuntimePaths paths, AppLogger logger)
    {
        logger.Info($"RapidOCR requested modelsPath: '{config.Ocr.ModelsPath}'.");
        var sets = FindOcrModelSets(config, paths, logger);
        var selected = sets.FirstOrDefault();
        if (selected is null)
        {
            logger.Warning("No complete RapidOCR model set was found through configured paths or automatic model roots.");
            return null;
        }

        logger.Info("RapidOCR model set selected:");
        logger.Info($"Det: {selected.DetPath}");
        logger.Info($"Cls: {selected.ClsPath}");
        logger.Info($"Rec: {selected.RecPath}");
        logger.Info($"Dict: {selected.KeysPath}");
        return selected;
    }

    public IReadOnlyList<LlmModelCandidate> FindLlmModels(AppConfig config, RuntimePaths paths, AppLogger logger)
    {
        var roots = BuildLlmSearchRoots(config, paths)
            .GroupBy(root => Normalize(root.Path), PathComparer)
            .Select(group => group.First())
            .OrderBy(root => root.Path, PathComparer)
            .ToArray();

        var candidates = roots
            .Where(root => Directory.Exists(root.Path) && FileSystemAccess.CanReadDirectory(root.Path))
            .SelectMany(root => EnumerateFilesSafe(root.Path, "*.gguf", logger)
                .Select(file => new LlmModelCandidate(file, SafeLength(file), FileSystemAccess.CanReadFile(file), root.Source)))
            .DistinctBy(candidate => Normalize(candidate.ModelPath), PathComparer)
            .OrderBy(candidate => candidate.ModelPath, PathComparer)
            .ToArray();

        PersistDiscoveryCache(paths, Array.Empty<OcrModelSet>(), candidates, logger);
        return candidates;
    }

    public string? SelectLlmModelPath(AppConfig config, RuntimePaths paths, AppLogger logger)
    {
        logger.Info($"Local LLM requested modelPath: '{config.LocalBrain.ModelPath}'.");
        var requested = ResolveRequestedFile(config.LocalBrain.ModelPath, paths);
        foreach (var file in requested)
        {
            if (!File.Exists(file))
            {
                continue;
            }

            if (!FileSystemAccess.CanReadFile(file))
            {
                logger.Warning("LLM model found but is not readable by current user.");
                logger.Warning($"Unreadable LLM model: {file}");
                continue;
            }

            logger.Info($"Selected local LLM model: {file}");
            return file;
        }

        var candidates = FindLlmModels(config, paths, logger);
        if (candidates.Count == 0)
        {
            logger.Warning("Local LLM is enabled, but no GGUF model was found. Falling back to RuleBasedBrain.");
            return null;
        }

        foreach (var unreadable in candidates.Where(candidate => !candidate.IsReadable))
        {
            logger.Warning("LLM model found but is not readable by current user.");
            logger.Warning($"Unreadable LLM model: {unreadable.ModelPath}");
        }

        var readable = candidates.Where(candidate => candidate.IsReadable).ToArray();
        if (readable.Length == 0)
        {
            logger.Warning("No readable GGUF model was found. Falling back to RuleBasedBrain.");
            return null;
        }

        var pool = readable.Any(candidate => !IsFp16(candidate.ModelPath))
            ? readable.Where(candidate => !IsFp16(candidate.ModelPath))
            : readable;
        var selected = pool
            .OrderByDescending(ScoreLlmModel)
            .ThenBy(candidate => Path.GetFileName(candidate.ModelPath), StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.ModelPath, PathComparer)
            .First();

        logger.Info($"Selected local LLM model: {selected.ModelPath}");
        return selected.ModelPath;
    }

    public IReadOnlyList<string> ResolveOcrRequestedDirectories(AppConfig config, RuntimePaths paths) =>
        ResolveRequestedDirectory(config.Ocr.ModelsPath, paths);

    private static void AddConfiguredModelRoot(List<(string Path, string Source)> roots, string? modelsRoot)
    {
        if (ShouldAutoDiscover(modelsRoot))
        {
            return;
        }

        foreach (var path in ResolveAgainstBaseAndCurrent(modelsRoot!))
        {
            AddRoot(roots, path, $"config modelsRoot '{modelsRoot}'");
        }
    }

    private static void AddRoot(List<(string Path, string Source)> roots, string path, string source)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            roots.Add((Normalize(path), source));
        }
    }

    private static ModelRootStatus BuildRootStatus(string rootPath, string source)
    {
        try
        {
            var exists = Directory.Exists(rootPath);
            var readable = exists && FileSystemAccess.CanReadDirectory(rootPath);
            var writable = exists && FileSystemAccess.CanWriteDirectoryWithoutCreating(rootPath);
            var count = readable
                ? CountModelFilesBestEffort(rootPath, 5000)
                : 0;
            return new ModelRootStatus(rootPath, source, exists, readable, writable, count, null);
        }
        catch (Exception ex)
        {
            return new ModelRootStatus(rootPath, source, false, false, false, 0, ex.Message);
        }
    }

    private IReadOnlyList<OcrModelSet> FindRequestedOcrModelSets(AppConfig config, RuntimePaths paths, AppLogger logger)
    {
        if (ShouldAutoDiscover(config.Ocr.ModelsPath))
        {
            return Array.Empty<OcrModelSet>();
        }

        var requestedDirectories = ResolveRequestedDirectory(config.Ocr.ModelsPath, paths);
        var sets = requestedDirectories
            .Where(Directory.Exists)
            .Where(FileSystemAccess.CanReadDirectory)
            .SelectMany(directory => FindOcrModelSetsInRoot(directory, $"ocr.modelsPath '{config.Ocr.ModelsPath}'", logger))
            .OrderByDescending(ScoreOcrModelSet)
            .ThenBy(set => set.DirectoryPath, PathComparer)
            .ToArray();

        if (sets.Length == 0)
        {
            logger.Warning($"Configured OCR modelsPath '{config.Ocr.ModelsPath}' did not contain a complete RapidOCR model set. Automatic discovery will be used.");
        }

        return sets;
    }

    private static IReadOnlyList<OcrModelSet> FindOcrModelSetsInRoot(string root, string source, AppLogger logger)
    {
        var grouped = EnumerateFilesSafe(root, "*", logger)
            .Where(file => file.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .GroupBy(file => Path.GetDirectoryName(file) ?? root, PathComparer);
        var sets = new List<OcrModelSet>();

        foreach (var group in grouped)
        {
            var files = group.OrderBy(file => file, PathComparer).ToArray();
            var onnx = files.Where(file => file.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase)).ToArray();
            var txt = files.Where(file => file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).ToArray();
            var det = onnx.FirstOrDefault(file => FileNameContains(file, "det"));
            var cls = onnx.FirstOrDefault(file => FileNameContains(file, "cls"));
            var rec = onnx.FirstOrDefault(file => FileNameContains(file, "rec"));
            var keys = txt.FirstOrDefault(file => FileNameContains(file, "dict") || FileNameContains(file, "keys"));

            if (det is not null && cls is not null && rec is not null && keys is not null)
            {
                sets.Add(new OcrModelSet(det, cls, rec, keys, $"{source}: {group.Key}"));
            }
        }

        return sets;
    }

    private static IEnumerable<(string Path, string Source)> BuildLlmSearchRoots(AppConfig config, RuntimePaths paths)
    {
        if (!ShouldAutoDiscover(config.ModelsRoot))
        {
            foreach (var root in ResolveAgainstBaseAndCurrent(config.ModelsRoot))
            {
                yield return (Path.Combine(root, "llm"), $"config modelsRoot '{config.ModelsRoot}'/llm");
                yield return (root, $"config modelsRoot '{config.ModelsRoot}'");
            }
        }

        yield return (Path.Combine(AppContext.BaseDirectory, "models", "llm"), "AppContext.BaseDirectory/models/llm");
        yield return (Path.Combine(AppContext.BaseDirectory, "models"), "AppContext.BaseDirectory/models");
        yield return (Path.Combine(Directory.GetCurrentDirectory(), "models", "llm"), "CurrentDirectory/models/llm");
        yield return (paths.UserLlmModelsDirectory, "%LOCALAPPDATA%/PageWalkerLocal/models/llm");
    }

    private static IReadOnlyList<string> ResolveRequestedDirectory(string? value, RuntimePaths paths)
    {
        if (ShouldAutoDiscover(value))
        {
            return Array.Empty<string>();
        }

        if (Path.IsPathRooted(value!))
        {
            return [Normalize(value!)];
        }

        return ResolveAgainstBaseAndCurrent(value!)
            .Append(Path.Combine(paths.RootDirectory, value!))
            .Select(Normalize)
            .Distinct(PathComparer)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveRequestedFile(string? value, RuntimePaths paths)
    {
        if (ShouldAutoDiscover(value))
        {
            return Array.Empty<string>();
        }

        if (Path.IsPathRooted(value!))
        {
            return [Normalize(value!)];
        }

        return ResolveAgainstBaseAndCurrent(value!)
            .Append(Path.Combine(paths.RootDirectory, value!))
            .Select(Normalize)
            .Distinct(PathComparer)
            .ToArray();
    }

    private static IEnumerable<string> ResolveAgainstBaseAndCurrent(string relativeOrAbsolute)
    {
        if (Path.IsPathRooted(relativeOrAbsolute))
        {
            yield return Normalize(relativeOrAbsolute);
            yield break;
        }

        yield return Path.Combine(AppContext.BaseDirectory, relativeOrAbsolute);
        yield return Path.Combine(Directory.GetCurrentDirectory(), relativeOrAbsolute);
    }

    private static int CountModelFilesBestEffort(string root, int limit)
    {
        var count = 0;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0 && count < limit)
        {
            var current = pending.Pop();
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            count += files.Count(file => file.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase));

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories)
            {
                pending.Push(directory);
            }
        }

        return Math.Min(count, limit);
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root, string searchPattern, AppLogger logger)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current, searchPattern);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                logger.Warning($"Model discovery could not read files in '{current}': {ex.Message}");
                continue;
            }

            foreach (var file in files)
            {
                yield return Normalize(file);
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                logger.Warning($"Model discovery could not read directories in '{current}': {ex.Message}");
                continue;
            }

            foreach (var directory in directories.OrderByDescending(directory => directory, PathComparer))
            {
                pending.Push(directory);
            }
        }
    }

    private static int ScoreOcrModelSet(OcrModelSet set)
    {
        var text = $"{set.DirectoryPath} {string.Join(' ', set.Files.Select(Path.GetFileName))}";
        var score = 0;
        if (text.Contains("v5", StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
        }

        if (text.Contains("PP-OCRv5", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ppocrv5", StringComparison.OrdinalIgnoreCase))
        {
            score += 500;
        }

        if (text.Contains("latin", StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        return score;
    }

    private static int ScoreLlmModel(LlmModelCandidate candidate)
    {
        var name = Path.GetFileName(candidate.ModelPath).ToLowerInvariant();
        var score = 0;
        if (name.Contains("qwen2.5-0.5b", StringComparison.Ordinal))
        {
            score += 600;
        }
        else if (name.Contains("qwen2.5", StringComparison.Ordinal))
        {
            score += 500;
        }
        else if (name.Contains("smollm", StringComparison.Ordinal))
        {
            score += 400;
        }
        else if (name.Contains("tinyllama", StringComparison.Ordinal))
        {
            score += 300;
        }

        score += name switch
        {
            var value when value.Contains("q4_k_m", StringComparison.Ordinal) => 80,
            var value when value.Contains("q4_0", StringComparison.Ordinal) => 70,
            var value when value.Contains("q3_k_m", StringComparison.Ordinal) => 60,
            var value when value.Contains("q5_k_m", StringComparison.Ordinal) => 50,
            var value when value.Contains("q5_0", StringComparison.Ordinal) => 40,
            var value when value.Contains("q8_0", StringComparison.Ordinal) => 30,
            var value when value.Contains("fp16", StringComparison.Ordinal) => 0,
            _ => 10
        };
        return score;
    }

    private static bool ShouldAutoDiscover(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || string.Equals(value.Trim(), "auto", StringComparison.OrdinalIgnoreCase);

    private static bool FileNameContains(string path, string value) =>
        Path.GetFileName(path).Contains(value, StringComparison.OrdinalIgnoreCase);

    private static bool IsFp16(string path) =>
        Path.GetFileName(path).Contains("fp16", StringComparison.OrdinalIgnoreCase);

    private static long SafeLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static string Normalize(string path) => Path.GetFullPath(path);

    private static void PersistDiscoveryCache(
        RuntimePaths paths,
        IReadOnlyList<OcrModelSet> ocrSets,
        IReadOnlyList<LlmModelCandidate> llmCandidates,
        AppLogger logger)
    {
        try
        {
            var payload = new
            {
                timestamp = DateTimeOffset.Now,
                ocrSets,
                llmCandidates
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(paths.CacheDirectory);
            File.WriteAllText(paths.ModelDiscoveryCacheFile, json);
        }
        catch (Exception ex)
        {
            logger.Debug($"Could not write model discovery cache under user cache directory: {ex.Message}");
        }
    }
}
