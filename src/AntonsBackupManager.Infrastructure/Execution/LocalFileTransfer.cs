using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AntonsBackupManager.Infrastructure.Execution;

public sealed class LocalFileTransfer(IProgress<long>? progress = null) : IFileTransfer
{
    private const uint FailIfExists = 1;
    private const uint Continue = 0;
    private const uint Cancel = 1;

    public void Copy(string source, string destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            File.Copy(source, destination, false);
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }
        Exception? callbackError = null;
        CopyProgress callback = (_, transferred, _, _, _, _, _, _, _) =>
        {
            try { progress?.Report(transferred); }
            catch (Exception error) { callbackError = error; return Cancel; }
            return cancellationToken.IsCancellationRequested ? Cancel : Continue;
        };
        // CopyFileEx retains Windows file attributes and alternate streams, unlike
        // copying only the default stream. Cancellation is checked within a file.
        var copied = CopyFileEx(ExtendedPath(source), ExtendedPath(destination), callback, IntPtr.Zero, IntPtr.Zero, FailIfExists);
        var code = Marshal.GetLastWin32Error();
        if (callbackError is not null) throw new IOException("Transfer progress failed.", callbackError);
        cancellationToken.ThrowIfCancellationRequested();
        if (!copied) throw new IOException("File transfer failed: " + new Win32Exception(code).Message, unchecked((int)(0x80070000u | (uint)code)));
    }

    private static string ExtendedPath(string path)
    {
        path = Path.GetFullPath(path);
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) return path;
        return path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..] : @"\\?\" + path;
    }

    private delegate uint CopyProgress(long size, long transferred, long streamSize, long streamTransferred,
        uint streamNumber, uint reason, IntPtr source, IntPtr destination, IntPtr data);

    [DllImport("kernel32.dll", EntryPoint = "CopyFileExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CopyFileEx(string source, string destination, CopyProgress progress,
        IntPtr data, IntPtr cancel, uint flags);
}
