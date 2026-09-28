namespace AntonsBackupManager.Infrastructure.Scanning;

internal static class FileAccessFailure
{
    // Only sharing and byte-range locks are retried as individual files.
    // Access denial, missing drives and other I/O failures still stop the run.
    public static bool IsLocked(IOException error) => OperatingSystem.IsWindows() &&
        (error.HResult & 0xffff) is 32 or 33;
}
