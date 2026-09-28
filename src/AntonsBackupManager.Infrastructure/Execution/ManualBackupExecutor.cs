using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Infrastructure.Scanning;

namespace AntonsBackupManager.Infrastructure.Execution;

public static class ManualBackupExecutor
{
    public static ManualBackupReport ExecuteNewFiles(string sourceRoot, string destinationRoot, BackupPlan plan, string versionsDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (FileSystemPathSafety.IsSameOrNestedPath(sourceRoot, destinationRoot) ||
            FileSystemPathSafety.IsSameOrNestedPath(destinationRoot, sourceRoot))
            throw new ArgumentException("Quelle und Ziel dürfen nicht überlappen.");
        foreach (var operation in plan.Operations.Where(o => o.Kind == PlannedOperationKind.CopyNewFile))
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerifyUnchangedSincePreview(operation, ResolveUnderRoot(sourceRoot, operation.RelativePath), ResolveUnderRoot(destinationRoot, operation.RelativePath));
        }
        var copiedRelativePaths = new List<string>();
        try
        {
        foreach (var operation in plan.Operations.Where(operation => operation.Kind == PlannedOperationKind.CopyNewFile))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = ResolveUnderRoot(sourceRoot, operation.RelativePath);
            var destinationPath = ResolveUnderRoot(destinationRoot, operation.RelativePath);
            VerifyUnchangedSincePreview(operation, sourcePath, destinationPath);
            SafeFileCopier.CopyNewFile(sourcePath, destinationPath, operation.ExpectedSource);
            copiedRelativePaths.Add(operation.RelativePath);
        }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException or ArgumentException)
        {
            throw new BackupExecutionException(new ManualBackupReport(copiedRelativePaths.ToArray(), plan.Conflicts), exception);
        }

        return new ManualBackupReport(copiedRelativePaths, plan.Conflicts);
    }

    private static void VerifyUnchangedSincePreview(PlannedOperation operation, string sourcePath, string destinationPath)
    {
        if (operation.ExpectedSource is null)
        {
            throw new InvalidOperationException("Der Plan enthält keinen erwarteten Zustand der Quelldatei.");
        }

        var currentSource = ReadSnapshot(sourcePath, operation.RelativePath);
        if (currentSource is null || !operation.ExpectedSource.HasSameContentMarkerAs(currentSource))
        {
            throw new IOException("Die Quelldatei wurde seit der Vorschau geändert oder entfernt.");
        }

        var currentDestination = ReadSnapshot(destinationPath, operation.RelativePath);
        if (operation.ExpectedDestination is null && currentDestination is not null)
        {
            throw new IOException("Die Zieldatei wurde nach der Vorschau erstellt und wird nicht überschrieben.");
        }

        if (operation.ExpectedDestination is not null &&
            (currentDestination is null || !operation.ExpectedDestination.HasSameContentMarkerAs(currentDestination)))
        {
            throw new IOException("Die Zieldatei wurde seit der Vorschau geändert oder entfernt.");
        }
    }

    private static FileSnapshot? ReadSnapshot(string path, string relativePath)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return FileSnapshotReader.Read(relativePath, path);
    }

    private static string ResolveUnderRoot(string root, string relativePath)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var resolvedPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
        if (!FileSystemPathSafety.IsSameOrNestedPath(normalizedRoot, resolvedPath))
        {
            throw new ArgumentException("Der geplante Dateipfad liegt außerhalb des zugelassenen Ordners.");
        }

        FileSystemPathSafety.EnsureNoReparsePointsBetween(normalizedRoot, resolvedPath);

        return resolvedPath;
    }
}
