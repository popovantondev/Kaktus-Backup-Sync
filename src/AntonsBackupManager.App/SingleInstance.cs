using System.Security.Principal;

namespace AntonsBackupManager.App;

internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activation;
    private RegisteredWaitHandle? listener;
    public bool IsPrimary { get; }

    public SingleInstance(string? identity = null)
    {
        identity ??= "AntonsBackupManager-" + WindowsIdentity.GetCurrent().User!.Value;
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + identity + "-activate");
        mutex = new Mutex(false, @"Local\" + identity);
        try { IsPrimary = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
        if (!IsPrimary) activation.Set();
    }

    public void Listen(Action activate) => listener = ThreadPool.RegisterWaitForSingleObject(activation,
        (_, timedOut) => { if (!timedOut) activate(); }, null, Timeout.Infinite, false);

    public void Dispose()
    {
        listener?.Unregister(null);
        if (IsPrimary) mutex.ReleaseMutex();
        mutex.Dispose();
        activation.Dispose();
    }
}
