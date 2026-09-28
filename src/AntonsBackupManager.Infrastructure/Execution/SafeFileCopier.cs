using AntonsBackupManager.Infrastructure.Scanning;
using AntonsBackupManager.Core.Planning;

namespace AntonsBackupManager.Infrastructure.Execution;

public sealed record CopyExecutionResult(string DestinationPath, string? PreviousVersionPath);

public static class SafeFileCopier
{
    public static CopyExecutionResult CopyNewFile(string sourcePath, string destinationPath, FileSnapshot? expectedSource = null,
        CancellationToken cancellationToken = default, IFileTransfer? transfer = null)
    {
        destinationPath = Path.GetFullPath(destinationPath);
        using var staged = Stage(sourcePath, destinationPath, expectedSource, cancellationToken, transfer);
        cancellationToken.ThrowIfCancellationRequested();
        File.Move(staged.Path, destinationPath, overwrite: false);
        return new CopyExecutionResult(destinationPath, PreviousVersionPath: null);
    }

    internal static StagedFile Stage(string sourcePath, string destinationPath, FileSnapshot? expectedSource,
        CancellationToken cancellationToken, IFileTransfer? transfer, bool replacing = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        sourcePath = Path.GetFullPath(sourcePath);
        destinationPath = Path.GetFullPath(destinationPath);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Die Quelldatei wurde nicht gefunden.", sourcePath);
        }

        FileSystemPathSafety.EnsureSafeExistingDirectory(Path.GetDirectoryName(Path.GetFullPath(sourcePath))!, "Source");
        FileSystemPathSafety.ThrowIfReparsePoint(new FileInfo(sourcePath), "Source");
        var existingParent = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
        while (!existingParent.Exists) existingParent = existingParent.Parent ?? throw new DirectoryNotFoundException();
        FileSystemPathSafety.EnsureNoReparsePointsBetween(existingParent.FullName, destinationPath);

        if (!replacing && File.Exists(destinationPath))
        {
            throw new IOException("Die Zieldatei existiert bereits und wird nicht überschrieben.");
        }

        var sourceSnapshot = FileSnapshotReader.Read(Path.GetFileName(sourcePath), sourcePath, cancellationToken);
        if (expectedSource is not null && !expectedSource.HasSameContentMarkerAs(sourceSnapshot))
        {
            throw new IOException("Die Quelldatei wurde seit der Vorschau geändert und wird nicht kopiert.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var staged = new StagedFile(destinationPath);
        try
        {
            (transfer ?? new LocalFileTransfer()).Copy(sourcePath, staged.Path, cancellationToken);
            VerifySameContent(sourceSnapshot, staged.Path, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return staged;
        }
        catch
        {
            staged.Dispose();
            throw;
        }
    }


    private static void VerifySameContent(FileSnapshot expectedSnapshot, string actualPath, CancellationToken cancellationToken = default)
    {
        var actualSnapshot = FileSnapshotReader.Read(expectedSnapshot.RelativePath, actualPath, cancellationToken);
        if (!expectedSnapshot.HasSameContentMarkerAs(actualSnapshot))
        {
            throw new IOException("Der Inhalt konnte nach dem Kopieren nicht bestätigt werden.");
        }
    }
}
