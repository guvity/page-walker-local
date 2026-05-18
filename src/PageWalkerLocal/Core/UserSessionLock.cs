using System.Security.Principal;

namespace PageWalkerLocal.Core;

public sealed class UserSessionLock : IDisposable
{
    private readonly Mutex _mutex;

    private UserSessionLock(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static UserSessionLock? TryAcquire(AppLogger logger)
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var session = Environment.ProcessId > 0
            ? System.Diagnostics.Process.GetCurrentProcess().SessionId
            : 0;
        var safeSid = new string(sid.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        var mutexName = $@"Local\PageWalkerLocal_{safeSid}_{session}";
        var mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        logger.Debug($"Acquired per-user/session mutex '{mutexName}'.");
        return new UserSessionLock(mutex);
    }

    public void Dispose()
    {
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // The mutex may already be released during process shutdown.
        }

        _mutex.Dispose();
    }
}
