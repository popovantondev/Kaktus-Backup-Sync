using AntonsBackupManager.Core.Planning;

namespace AntonsBackupManager.Infrastructure.Scanning;

public static class FileTreeScanner
{
    public static IReadOnlyList<FileSnapshot> Scan(string rootDirectory)
        => Scan(rootDirectory, CancellationToken.None);

    public static IReadOnlyList<FileSnapshot> Scan(string rootDirectory, CancellationToken cancellationToken, IProgress<string>? progress = null, bool excludeVersions = false, FileScanCache? cache = null, ICollection<string>? lockedFiles = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        var root = new DirectoryInfo(Path.GetFullPath(rootDirectory));
        FileSystemPathSafety.EnsureSafeExistingDirectory(root.FullName, "Ordner");
        var files = new List<FileSnapshot>();
        ScanDirectory(root, root, files, cancellationToken, progress, excludeVersions, cache, lockedFiles);

        return files
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ScanDirectory(DirectoryInfo root, DirectoryInfo current, ICollection<FileSnapshot> files, CancellationToken cancellationToken, IProgress<string>? progress, bool excludeVersions, FileScanCache? cache, ICollection<string>? lockedFiles)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileSystemPathSafety.ThrowIfReparsePoint(current, "Ordner");
        foreach (var file in current.EnumerateFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileSystemPathSafety.ThrowIfReparsePoint(file, "Datei");
            progress?.Report(file.FullName);
            var relative = Path.GetRelativePath(root.FullName, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
            try
            {
                files.Add(cache is null ? FileSnapshotReader.Read(relative, file.FullName, cancellationToken) : cache.Read(relative, file.FullName, cancellationToken));
            }
            catch (IOException error) when (lockedFiles is not null && FileAccessFailure.IsLocked(error))
            {
                lockedFiles.Add(relative);
            }
        }

        foreach (var directory in current.EnumerateDirectories())
        {
            if (directory.Name.Equals(Execution.StagedFile.DirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                if (!Execution.StagedFile.IsOwned(directory.FullName)) throw new IOException("Unrecognized temporary-work folder.");
                continue;
            }
            if (current.FullName == root.FullName && directory.Name.Equals(".SyncVersions", StringComparison.OrdinalIgnoreCase))
            {
                if (excludeVersions) continue;
                throw new ArgumentException(".SyncVersions is reserved for destination recovery files.");
            }
            ScanDirectory(root, directory, files, cancellationToken, progress, excludeVersions, cache, lockedFiles);
        }
    }
}
