using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using PageWalkerLocal.Core;

namespace PageWalkerLocal.Diagnostics;

public sealed record NativeDependencyFileStatus(
    string Path,
    long Length,
    bool IsReadable);

public sealed record NativeDependencyReport(
    Architecture ProcessArchitecture,
    string OsVersion,
    string BaseDirectory,
    string CurrentDirectory,
    IReadOnlyList<string> RelevantPathEntries,
    bool OnnxRuntimeDllExistsInBaseDirectory,
    IReadOnlyList<NativeDependencyFileStatus> OnnxRuntimeDllsUnderBaseDirectory,
    IReadOnlyList<NativeDependencyFileStatus> OnnxRuntimeDllsUnderCurrentDirectory,
    bool OnnxRuntimeAssemblyLoaded,
    bool SessionOptionsSucceeded,
    string? ExceptionType,
    string? ExceptionMessage,
    int? HResult,
    string? StackTrace)
{
    public bool NativeInitializationFailed => !SessionOptionsSucceeded;
}

public static class NativeDependencyDiagnostics
{
    public static NativeDependencyReport CheckOnnxRuntime() => CheckOnnxRuntime(null);

    public static NativeDependencyReport CheckOnnxRuntime(AppLogger? logger)
    {
        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var currentDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());
        var baseDlls = FindDlls(baseDirectory);
        var currentDlls = string.Equals(baseDirectory, currentDirectory, StringComparison.OrdinalIgnoreCase)
            ? Array.Empty<NativeDependencyFileStatus>()
            : FindDlls(currentDirectory);

        var assemblyLoaded = AppDomain.CurrentDomain.GetAssemblies()
            .Any(assembly => string.Equals(assembly.GetName().Name, "Microsoft.ML.OnnxRuntime", StringComparison.OrdinalIgnoreCase));
        _ = typeof(SessionOptions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        assemblyLoaded = assemblyLoaded || AppDomain.CurrentDomain.GetAssemblies()
            .Any(assembly => string.Equals(assembly.GetName().Name, "Microsoft.ML.OnnxRuntime", StringComparison.OrdinalIgnoreCase));

        var sessionOptionsSucceeded = false;
        string? exceptionType = null;
        string? exceptionMessage = null;
        int? hresult = null;
        string? stackTrace = null;
        try
        {
            using var _ = new SessionOptions();
            sessionOptionsSucceeded = true;
        }
        catch (Exception ex)
        {
            var root = Unwrap(ex);
            exceptionType = root.GetType().FullName;
            exceptionMessage = root.Message;
            hresult = root.HResult;
            stackTrace = ex.ToString();
        }

        var report = new NativeDependencyReport(
            RuntimeInformation.ProcessArchitecture,
            RuntimeInformation.OSDescription,
            baseDirectory,
            currentDirectory,
            RelevantPathEntries(),
            File.Exists(Path.Combine(baseDirectory, "onnxruntime.dll")),
            baseDlls,
            currentDlls,
            assemblyLoaded,
            sessionOptionsSucceeded,
            exceptionType,
            exceptionMessage,
            hresult,
            stackTrace);

        if (logger is not null)
        {
            LogReport(report, logger);
        }

        return report;
    }

    private static void LogReport(NativeDependencyReport report, AppLogger logger)
    {
        logger.Info("ONNX Runtime diagnostics:");
        logger.Info($"Process architecture: {report.ProcessArchitecture}");
        logger.Info($"OS version: {report.OsVersion}");
        logger.Info($"AppContext.BaseDirectory: {report.BaseDirectory}");
        logger.Info($"Current directory: {report.CurrentDirectory}");
        logger.Info($"Microsoft.ML.OnnxRuntime assembly loaded: {YesNo(report.OnnxRuntimeAssemblyLoaded)}");
        logger.Info($"onnxruntime.dll exists in output root: {YesNo(report.OnnxRuntimeDllExistsInBaseDirectory)}");
        logger.Info($"SessionOptions initialization succeeded: {YesNo(report.SessionOptionsSucceeded)}");

        foreach (var entry in report.RelevantPathEntries)
        {
            logger.Info($"PATH relevant entry: {entry}");
        }

        foreach (var dll in report.OnnxRuntimeDllsUnderBaseDirectory)
        {
            logger.Info($"ONNX Runtime DLL under output: {dll.Path} ({dll.Length} bytes), readable={YesNo(dll.IsReadable)}");
        }

        foreach (var dll in report.OnnxRuntimeDllsUnderCurrentDirectory)
        {
            logger.Info($"ONNX Runtime DLL under current directory: {dll.Path} ({dll.Length} bytes), readable={YesNo(dll.IsReadable)}");
        }

        if (!report.SessionOptionsSucceeded)
        {
            logger.Error($"ONNX Runtime SessionOptions failed. Type={report.ExceptionType}, HResult=0x{report.HResult.GetValueOrDefault():X8}, Message={report.ExceptionMessage}");
            if (!string.IsNullOrWhiteSpace(report.StackTrace))
            {
                logger.Debug(report.StackTrace);
            }
        }
    }

    private static NativeDependencyFileStatus[] FindDlls(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(root, "onnxruntime*.dll", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .Select(info => new NativeDependencyFileStatus(info.FullName, info.Length, FileSystemAccess.CanReadFile(info.FullName)))
                .OrderBy(status => status.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<string> RelevantPathEntries()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => entry.Contains("PageWalkerLocal", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("onnx", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("runtimes", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("native", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Exception Unwrap(Exception ex)
    {
        while (ex is TypeInitializationException or TargetInvocationException)
        {
            if (ex.InnerException is null)
            {
                return ex;
            }

            ex = ex.InnerException;
        }

        return ex;
    }

    private static string YesNo(bool value) => FileSystemAccess.YesNo(value);
}
