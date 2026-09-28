using AntonsBackupManager.Infrastructure.Scanning;

using AntonsBackupManager.Core.Planning;

namespace AntonsBackupManager.Infrastructure.Planning;

public sealed class DryRunService : IDryRunService
{
    public BackupPlan CreatePlan(string sourceDirectory, string destinationDirectory)
        => CreatePlan(sourceDirectory, destinationDirectory, CancellationToken.None);

    public BackupPlan CreatePlan(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
        => CreatePlan(sourceDirectory, destinationDirectory, [], cancellationToken);

    public BackupPlan CreatePlan(string sourceDirectory, string destinationDirectory, IEnumerable<FileSnapshot> baseline, CancellationToken cancellationToken, IProgress<string>? progress = null, FileScanCache? cache = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = NormalizeExistingDirectory(sourceDirectory, "Quelle");
        var destination = NormalizeExistingDirectory(destinationDirectory, "Ziel");

        if (PathsOverlap(source, destination))
        {
            throw new ArgumentException("Quelle und Ziel dürfen nicht identisch oder ineinander verschachtelt sein.");
        }

        var locked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceFiles = FileTreeScanner.Scan(source, cancellationToken, progress, cache: cache, lockedFiles: locked);
        var destinationFiles = FileTreeScanner.Scan(destination, cancellationToken, progress, excludeVersions: true, cache: cache, lockedFiles: locked);
        var plan = BackupPlanBuilder.PlanAll(sourceFiles.Where(file => !locked.Contains(file.RelativePath)),
            destinationFiles.Where(file => !locked.Contains(file.RelativePath)), baseline,
            excludedPaths: locked);
        // A locked file is unknown, never an absent file or a rename candidate.
        return new BackupPlan(plan.Operations.Concat(locked.Select(path =>
            new PlannedOperation(path, PlannedOperationKind.RetryLockedFile, "RetryLockedFile")))
            .OrderBy(op => op.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string NormalizeExistingDirectory(string path, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        if (!Directory.Exists(normalized))
        {
            throw new DirectoryNotFoundException($"{label}-Ordner wurde nicht gefunden.");
        }

        FileSystemPathSafety.EnsureSafeExistingDirectory(normalized, label);

        return normalized;
    }

    private static bool PathsOverlap(string first, string second)
    {
        return FileSystemPathSafety.IsSameOrNestedPath(first, second)
            || FileSystemPathSafety.IsSameOrNestedPath(second, first);
    }
}
