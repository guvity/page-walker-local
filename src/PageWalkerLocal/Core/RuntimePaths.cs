namespace PageWalkerLocal.Core;

public sealed class RuntimePaths
{
    public required string RootDirectory { get; init; }
    public required string LogsDirectory { get; init; }
    public required string DebugDirectory { get; init; }
    public required string CacheDirectory { get; init; }
    public required string TempDirectory { get; init; }
    public required string ReportsDirectory { get; init; }
    public required string ModelCacheDirectory { get; init; }
    public required string DecisionLogsDirectory { get; init; }

    public static RuntimePaths CreateDefault()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.Combine(Path.GetTempPath(), "PageWalkerLocal");
        }

        var root = Path.Combine(localAppData, "PageWalkerLocal");
        return new RuntimePaths
        {
            RootDirectory = root,
            LogsDirectory = Path.Combine(root, "logs"),
            DebugDirectory = Path.Combine(root, "debug"),
            CacheDirectory = Path.Combine(root, "cache"),
            TempDirectory = Path.Combine(root, "temp"),
            ReportsDirectory = Path.Combine(root, "reports"),
            ModelCacheDirectory = Path.Combine(root, "model-cache"),
            DecisionLogsDirectory = Path.Combine(root, "decision-logs")
        };
    }

    public void EnsureCreated()
    {
        foreach (var directory in AllDirectories())
        {
            Directory.CreateDirectory(directory);
        }

        if (!FileSystemAccess.CanWriteTestFile(TempDirectory))
        {
            throw new InvalidOperationException(
                $"User runtime directory is not writable. PageWalkerLocal cannot run safely. Directory: {RootDirectory}");
        }
    }

    public IReadOnlyList<string> AllDirectories() =>
    [
        RootDirectory,
        LogsDirectory,
        DebugDirectory,
        CacheDirectory,
        TempDirectory,
        ReportsDirectory,
        ModelCacheDirectory,
        DecisionLogsDirectory
    ];

    public string ModelDiscoveryCacheFile => Path.Combine(CacheDirectory, "model-discovery.json");
    public string UserModelsDirectory => Path.Combine(RootDirectory, "models");
    public string UserLlmModelsDirectory => Path.Combine(UserModelsDirectory, "llm");
}
