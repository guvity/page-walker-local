namespace PageWalkerLocal.Core;

public sealed class RuntimePaths
{
    public required string RootDirectory { get; init; }
    public required string LogsDirectory { get; init; }
    public required string DebugDirectory { get; init; }

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
            DebugDirectory = Path.Combine(root, "debug")
        };
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(DebugDirectory);
    }
}
