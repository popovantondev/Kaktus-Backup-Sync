using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AntonsBackupManager.Infrastructure.Scanning;

public static class WindowsReparsePoint
{
    // Only Cloud Files tags are allowed. Junctions, symlinks and unknown tags fail closed.
    // https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-point-tags
    public static bool IsCloudTag(uint tag) => (tag & 0xffff0fffU) == 0x9000001aU;

    public static uint ReadTag(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var handle = CreateFileW(path, 0, 7, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid || !GetFileInformationByHandleEx(handle, 9, out var info, 8))
            throw new IOException("Cannot inspect the filesystem entry.", new Win32Exception(Marshal.GetLastWin32Error()));
        return info.ReparseTag;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AttributeTagInfo { public uint Attributes; public uint ReparseTag; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass,
        out AttributeTagInfo info, uint size);
}

public sealed class UnsupportedLinkException(string path) : IOException("Unsupported link: " + path)
{
    public string EntryPath { get; } = path;
}
