namespace PageWalkerLocal.Core;

public sealed class AppLogger : IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private readonly LogSeverity _minimum;

    private AppLogger(StreamWriter writer, LogSeverity minimum)
    {
        _writer = writer;
        _minimum = minimum;
    }

    public static AppLogger Create(RuntimePaths paths, string level)
    {
        paths.EnsureCreated();
        var logFile = Path.Combine(paths.LogsDirectory, $"pagewalker-{DateTimeOffset.Now:yyyyMMdd}.log");
        var writer = new StreamWriter(new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
        return new AppLogger(writer, Parse(level));
    }

    public void Debug(string message) => Write(LogSeverity.Debug, message);
    public void Info(string message) => Write(LogSeverity.Information, message);
    public void Warning(string message) => Write(LogSeverity.Warning, message);

    public void Error(string message, Exception? ex = null)
    {
        Write(LogSeverity.Error, ex is null ? message : $"{message} {ex}");
    }

    public void Dispose() => _writer.Dispose();

    private void Write(LogSeverity severity, string message)
    {
        if (severity < _minimum)
        {
            return;
        }

        var line = $"{DateTimeOffset.Now:O} [{severity}] {message}";
        lock (_gate)
        {
            Console.WriteLine(line);
            _writer.WriteLine(line);
        }
    }

    private static LogSeverity Parse(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "trace" or "debug" => LogSeverity.Debug,
            "warning" or "warn" => LogSeverity.Warning,
            "error" => LogSeverity.Error,
            _ => LogSeverity.Information
        };
    }
}

public enum LogSeverity
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3
}
