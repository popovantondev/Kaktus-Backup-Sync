using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Infrastructure.Scanning;

namespace AntonsBackupManager.Infrastructure.Execution;

internal static class DestinationFileRenamer
{
    public static void Rename(string oldPath, string newPath, FileSnapshot expected)
    {
        FileSystemPathSafety.EnsureSafeExistingDirectory(Path.GetDirectoryName(oldPath)!, "Destination");
        FileSystemPathSafety.ThrowIfReparsePoint(new FileInfo(oldPath), "Destination");
        // Deny writes through the move. External directory renames are not controlled here.
        using var writeGuard = new FileStream(oldPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (!expected.HasSameContentMarkerAs(FileSnapshotReader.Read(expected.RelativePath, oldPath)))
            throw new IOException("Rename target changed after preview.");
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        FileSystemPathSafety.EnsureSafeExistingDirectory(Path.GetDirectoryName(newPath)!, "Destination");
        File.Move(oldPath, newPath, overwrite: false);
    }
}
