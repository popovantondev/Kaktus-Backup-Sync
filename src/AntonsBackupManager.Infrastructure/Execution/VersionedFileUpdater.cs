using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Infrastructure.Scanning;

namespace AntonsBackupManager.Infrastructure.Execution;

public static class VersionedFileUpdater
{
    // Existing target is write-locked while its verified previous content is saved.
    // Directory renames by hostile external processes are outside this guarantee.
    public static void Update(string source, string destination, FileSnapshot expectedSource,
        FileSnapshot expectedDestination, string previousVersion, CancellationToken cancellationToken = default, IFileTransfer? transfer = null)
    {
        FileSystemPathSafety.EnsureSafeExistingDirectory(Path.GetDirectoryName(destination)!, "Ziel");
        FileSystemPathSafety.ThrowIfReparsePoint(new FileInfo(destination), "Ziel");
        using var targetLock = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (!expectedDestination.HasSameContentMarkerAs(FileSnapshotReader.Read(expectedDestination.RelativePath, destination)))
            throw new IOException("Ziel wurde seit der Vorschau geändert.");
        SafeFileCopier.CopyNewFile(destination, previousVersion, expectedDestination, cancellationToken, transfer);
        using var staged = SafeFileCopier.Stage(source, destination, expectedSource, cancellationToken, transfer, replacing: true);
        if (!expectedDestination.HasSameContentMarkerAs(FileSnapshotReader.Read(expectedDestination.RelativePath, destination)))
            throw new IOException("Ziel wurde seit der Vorschau geändert.");
        cancellationToken.ThrowIfCancellationRequested();
        File.Replace(staged.Path, destination, null);
    }
}
