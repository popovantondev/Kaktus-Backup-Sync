using System.Runtime.InteropServices;
using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.App;

internal static class RemovableVolumeIdentity
{
    public static IReadOnlyList<AvailableVolume> AvailableVolumes() => DriveInfo.GetDrives()
        .Select(drive => (drive.RootDirectory.FullName, Serial: ForPath(drive.RootDirectory.FullName)))
        .Where(drive => drive.Serial is not null).Select(drive => new AvailableVolume(drive.FullName, drive.Serial!)).ToArray();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumeInformation(
        string rootPathName, System.Text.StringBuilder? volumeNameBuffer, int volumeNameSize,
        out uint volumeSerialNumber, out uint maximumComponentLength, out uint fileSystemFlags,
        System.Text.StringBuilder? fileSystemNameBuffer, int fileSystemNameSize);

    public static string? ForPath(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (root is null || new DriveInfo(root) is not { DriveType: DriveType.Removable, IsReady: true }) return null;
            return GetVolumeInformation(root, null, 0, out var serial, out _, out _, null, 0)
                ? serial.ToString("X8")
                : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (ArgumentException) { return null; }
    }

    public static string? ForTaskPaths(string source, string destination) =>
        ForPath(source) ?? ForPath(destination);

    public static bool IsExpectedVolumeAvailable(string path, string? expectedSerial) =>
        expectedSerial is not null && string.Equals(ForPath(path), expectedSerial, StringComparison.OrdinalIgnoreCase);
}
