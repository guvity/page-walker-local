using PageWalkerLocal.Core;

namespace PageWalkerLocal.Diagnostics;

public sealed record PathPermissionStatus(
    string Path,
    string Label,
    bool Exists,
    bool IsReadable,
    bool IsWritable,
    string? Error);

public sealed record RuntimePermissionReport(
    bool UserRuntimeWritable,
    IReadOnlyList<PathPermissionStatus> ProgramDirectories,
    IReadOnlyList<PathPermissionStatus> ModelDirectories,
    IReadOnlyList<PathPermissionStatus> NativeFiles);

public static class RuntimePermissionDiagnostics
{
    public static RuntimePermissionReport Check(AppConfig config, RuntimePaths paths, AppLogger logger)
    {
        var programDirectory = BuildDirectoryStatus(AppContext.BaseDirectory, "program directory");
        var currentDirectory = BuildDirectoryStatus(Directory.GetCurrentDirectory(), "current directory");
        var programDirectories = new[] { programDirectory, currentDirectory }
            .DistinctBy(status => status.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var status in programDirectories)
        {
            logger.Info($"Runtime permission: {status.Label}: path='{status.Path}', exists={YesNo(status.Exists)}, readable={YesNo(status.IsReadable)}, writable={YesNo(status.IsWritable)}.");
            if (status.Label == "program directory" && !status.IsWritable)
            {
                logger.Info("Program directory is read-only for current user. This is supported. Runtime data will be written to %LOCALAPPDATA%\\PageWalkerLocal.");
            }
        }

        var userRuntimeWritable = CheckUserRuntime(paths, logger);

        var discovery = new ModelDiscovery();
        var modelDirectories = discovery.GetModelRoots(config, paths)
            .Select(root => new PathPermissionStatus(root.RootPath, $"model root ({root.Source})", root.Exists, root.IsReadable, root.IsWritable, root.Error))
            .ToArray();
        foreach (var status in modelDirectories)
        {
            logger.Info($"Model root permission: path='{status.Path}', exists={YesNo(status.Exists)}, readable={YesNo(status.IsReadable)}, writable={YesNo(status.IsWritable)}, read-only supported=yes.");
            if (status.Exists && !status.IsReadable)
            {
                logger.Error($"Model directory exists but is not readable by current user: {status.Path}");
            }
        }

        var nativeFiles = FindNativeFiles()
            .Select(file => new PathPermissionStatus(file, "native dependency", File.Exists(file), FileSystemAccess.CanReadFile(file), false, null))
            .ToArray();
        foreach (var file in nativeFiles)
        {
            logger.Info($"Native file permission: path='{file.Path}', readable={YesNo(file.IsReadable)}.");
            if (!file.IsReadable)
            {
                logger.Error($"Native dependency file is not readable by current user: {file.Path}");
            }
        }

        return new RuntimePermissionReport(userRuntimeWritable, programDirectories, modelDirectories, nativeFiles);
    }

    private static bool CheckUserRuntime(RuntimePaths paths, AppLogger logger)
    {
        try
        {
            paths.EnsureCreated();
            var writable = FileSystemAccess.CanWriteTestFile(paths.TempDirectory);
            logger.Info($"User runtime directory: path='{paths.RootDirectory}', writable={YesNo(writable)}.");
            if (!writable)
            {
                logger.Error("User runtime directory is not writable. PageWalkerLocal cannot run safely.");
            }

            return writable;
        }
        catch (Exception ex)
        {
            logger.Error("User runtime directory is not writable. PageWalkerLocal cannot run safely.", ex);
            return false;
        }
    }

    private static PathPermissionStatus BuildDirectoryStatus(string path, string label)
    {
        try
        {
            var normalized = Path.GetFullPath(path);
            var exists = Directory.Exists(normalized);
            return new PathPermissionStatus(
                normalized,
                label,
                exists,
                exists && FileSystemAccess.CanReadDirectory(normalized),
                exists && FileSystemAccess.CanWriteDirectoryWithoutCreating(normalized),
                null);
        }
        catch (Exception ex)
        {
            return new PathPermissionStatus(path, label, false, false, false, ex.Message);
        }
    }

    private static IReadOnlyList<string> FindNativeFiles()
    {
        var roots = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .ToArray();
        var files = new List<string>();
        foreach (var root in roots)
        {
            files.AddRange(EnumerateFiles(root, "onnxruntime*.dll"));
            files.AddRange(EnumerateFiles(root, "SkiaSharp*.dll"));
            var nativeRoot = Path.Combine(root, "runtimes", "win-x64", "native");
            if (Directory.Exists(nativeRoot))
            {
                files.AddRange(EnumerateFiles(nativeRoot, "*.dll"));
            }
        }

        return files
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateFiles(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string YesNo(bool value) => FileSystemAccess.YesNo(value);
}
