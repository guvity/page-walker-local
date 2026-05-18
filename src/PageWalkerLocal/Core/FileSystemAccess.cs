using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PageWalkerLocal.Core;

public static class FileSystemAccess
{
    private const uint FileListDirectory = 0x0001;
    private const uint FileAddFile = 0x0002;
    private const uint FileAddSubdirectory = 0x0004;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;

    public static bool CanReadFile(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool CanReadDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                _ = Directory.EnumerateFileSystemEntries(path).Take(1).ToArray();
                return true;
            }
            catch
            {
                return false;
            }
        }

        using var handle = CreateDirectoryHandle(path, FileListDirectory);
        return !handle.IsInvalid;
    }

    public static bool CanWriteDirectoryWithoutCreating(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var handle = CreateDirectoryHandle(path, FileAddFile | FileAddSubdirectory);
        return !handle.IsInvalid;
    }

    public static bool CanWriteTestFile(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, $".pagewalker-write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(file, "ok");
            File.Delete(file);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string YesNo(bool value) => value ? "yes" : "no";

    private static SafeFileHandle CreateDirectoryHandle(string path, uint desiredAccess) =>
        CreateFileW(
            path,
            desiredAccess,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);
}
