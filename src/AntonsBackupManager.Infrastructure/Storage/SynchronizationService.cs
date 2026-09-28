using AntonsBackupManager.Infrastructure.Planning;
using AntonsBackupManager.Infrastructure.Execution;
using System.Text.Json;
using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Infrastructure.Scanning;
using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.Infrastructure.Storage;

public sealed record PreviousVersion(string RelativePath, string BlobName, string Sha256, DateTimeOffset SavedAtUtc)
{
    public int SchemaVersion { get; init; } = 1;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsLegacy { get; init; }
}
public sealed record SyncBaseline(string Source, string Destination, IReadOnlyList<FileSnapshot> Files)
{
    public int SchemaVersion { get; init; } = 1;
}

public sealed class SynchronizationService(string stateDirectory, IFileTransfer? transfer = null, Action<BackupTaskDefinition>? validateLocation = null)
{
    public void RebindTaskLocation(BackupTaskDefinition previous, BackupTaskDefinition current)
    {
        if (previous.Id != current.Id) throw new ArgumentException("Task identity changed.");
        ValidateState(current);
        var path = BaselinePath(previous);
        if (File.Exists(path))
        {
            var baseline = JsonSerializer.Deserialize<SyncBaseline>(File.ReadAllText(path)) ?? throw new IOException("Invalid baseline.");
            if (baseline.SchemaVersion != 1) throw new IOException("Unsupported baseline version.");
            if (SamePath(baseline.Source, previous.SourceDirectory) && SamePath(baseline.Destination, previous.DestinationDirectory))
                SaveJson(path, baseline with { Source = current.SourceDirectory, Destination = current.DestinationDirectory });
        }
        Decisions(previous).Rebind(previous, current);
    }
    public int CachedFilesInLastPreview { get; private set; }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private string TaskFolder(BackupTaskDefinition task) => Path.Combine(stateDirectory, task.Id.ToString("N"));
    private string BaselinePath(BackupTaskDefinition task) => Path.Combine(TaskFolder(task), "baseline.json");
    private static string VersionFolder(BackupTaskDefinition task) => Path.Combine(task.DestinationDirectory, ".SyncVersions", task.Id.ToString("N"));
    private ConflictDecisionStore Decisions(BackupTaskDefinition task) => new(Path.Combine(TaskFolder(task), "conflicts.json"));

    private static bool SamePath(string a, string b) => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);

    private void ValidateState(BackupTaskDefinition task)
    {
        foreach (var (root, isSource) in new[] { (task.SourceDirectory, true), (task.DestinationDirectory, false) })
            if (FileSystemPathSafety.IsSameOrNestedPath(root, stateDirectory) || FileSystemPathSafety.IsSameOrNestedPath(stateDirectory, root))
                throw new StateDirectoryOverlapException(root, Path.GetFullPath(stateDirectory), isSource);
    }

    private IReadOnlyList<FileSnapshot> ReadBaseline(BackupTaskDefinition task)
    {
        var path = BaselinePath(task);
        if (!File.Exists(path)) return [];
        var baseline = JsonSerializer.Deserialize<SyncBaseline>(File.ReadAllText(path)) ?? throw new IOException("Ungültige Vergleichsbasis.");
        if (baseline.SchemaVersion != 1) throw new IOException("Unsupported baseline version.");
        if (!SamePath(baseline.Source, task.SourceDirectory) || !SamePath(baseline.Destination, task.DestinationDirectory)) return [];
        return baseline.Files ?? throw new IOException("Ungültige Vergleichsbasis.");
    }

    public BackupPlan Preview(BackupTaskDefinition task, CancellationToken token = default, IProgress<string>? progress = null, bool allowCache = false)
    {
        BackupTaskValidator.Validate(task);
        ValidateState(task);
        validateLocation?.Invoke(task);
        var cache = new FileScanCache(Path.Combine(TaskFolder(task), "scan-cache.json"), allowCache, task.FullCheckIntervalMinutes);
        var plan = new DryRunService().CreatePlan(task.SourceDirectory, task.DestinationDirectory, ReadBaseline(task), token, progress, cache);
        CachedFilesInLastPreview = cache.CachedFiles;
        cache.Save();
        return Decisions(task).Apply(task, plan);
    }

    public ManualBackupReport Synchronize(BackupTaskDefinition task, BackupPlan plan, CancellationToken token = default)
    {
        BackupTaskValidator.Validate(task);
        token.ThrowIfCancellationRequested();
        ValidateState(task);
        validateLocation?.Invoke(task);
        FileSystemPathSafety.EnsureSafeExistingDirectory(task.SourceDirectory, "Source");
        FileSystemPathSafety.EnsureSafeExistingDirectory(task.DestinationDirectory, "Destination");
        if (FileSystemPathSafety.IsSameOrNestedPath(task.SourceDirectory, task.DestinationDirectory) ||
            FileSystemPathSafety.IsSameOrNestedPath(task.DestinationDirectory, task.SourceDirectory))
            throw new ArgumentException("Quelle und Ziel überlappen.");
        var operations = plan.Operations.Where(o => o.Kind is PlannedOperationKind.CopyNewFile or PlannedOperationKind.UpdateDestinationFile or PlannedOperationKind.RenameDestinationFile).ToArray();
        var deferred = plan.Operations.Where(op => op.Kind == PlannedOperationKind.RetryLockedFile)
            .Select(op => op.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var kept in plan.Operations.Where(op => op.Kind == PlannedOperationKind.KeepDestination))
        {
            token.ThrowIfCancellationRequested();
            try { ValidateKeptFile(task, kept, token); }
            catch (IOException error) when (FileAccessFailure.IsLocked(error)) { deferred.Add(kept.RelativePath); }
        }
        foreach (var operation in operations)
        {
            token.ThrowIfCancellationRequested();
            try { ValidateOperation(task, operation, token); }
            catch (IOException error) when (FileAccessFailure.IsLocked(error)) { deferred.Add(operation.RelativePath); }
        }
        var completed = new List<string>();
        var renamed = new List<string>();
        ManualBackupReport Report() => new(completed.ToArray(), plan.Conflicts)
        {
            RenamedRelativePaths = renamed.ToArray(), DeferredRelativePaths = deferred.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
        };
        try
        {
            foreach (var operation in operations.Where(op => !deferred.Contains(op.RelativePath)))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    ValidateOperation(task, operation, token);
                    validateLocation?.Invoke(task);
                    ExecuteOperation(task, operation, token);
                    if (operation.Kind == PlannedOperationKind.RenameDestinationFile) renamed.Add(operation.RelativePath);
                    else completed.Add(operation.RelativePath);
                }
                catch (IOException error) when (FileAccessFailure.IsLocked(error)) { deferred.Add(operation.RelativePath); }
            }
            token.ThrowIfCancellationRequested();
            var baseline = ReadBaseline(task).ToDictionary(f => f.RelativePath, StringComparer.OrdinalIgnoreCase);
            var successful = completed.Concat(renamed).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var op in plan.Operations.Where(op => successful.Contains(op.RelativePath) || op.Kind == PlannedOperationKind.Unchanged))
            {
                if (op.ExpectedSource is null) continue;
                if (op.Kind == PlannedOperationKind.RenameDestinationFile) baseline.Remove(op.PreviousRelativePath!);
                baseline[op.RelativePath] = op.ExpectedSource;
            }
            SaveJson(BaselinePath(task), new SyncBaseline(task.SourceDirectory, task.DestinationDirectory, baseline.Values.ToArray()));
            Decisions(task).Save(task, new BackupPlan(plan.Operations.Where(op => !deferred.Contains(op.RelativePath)).ToArray()));
            TrimVersions(task);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException or ArgumentException)
        {
            throw new BackupExecutionException(Report(), error);
        }
        return Report();
    }

    private static void ValidateKeptFile(BackupTaskDefinition task, PlannedOperation kept, CancellationToken token)
    {
        var target = Resolve(task.DestinationDirectory, kept.RelativePath);
        if (kept.ExpectedDestination?.Sha256 is null || !kept.ExpectedDestination.HasSameContentMarkerAs(FileSnapshotReader.Read(kept.RelativePath, target, token)))
            throw new IOException("Destination changed after the conflict decision.");
        var source = Resolve(task.SourceDirectory, kept.RelativePath);
        if (kept.ExpectedSource is null ? File.Exists(source) || Directory.Exists(source) :
            !kept.ExpectedSource.HasSameContentMarkerAs(FileSnapshotReader.Read(kept.RelativePath, source, token)))
            throw new IOException("Source changed after the conflict decision.");
    }

    private void ExecuteOperation(BackupTaskDefinition task, PlannedOperation operation, CancellationToken token)
    {
        var source = Resolve(task.SourceDirectory, operation.RelativePath);
        var destination = Resolve(task.DestinationDirectory, operation.RelativePath);
        if (operation.Kind == PlannedOperationKind.CopyNewFile)
            SafeFileCopier.CopyNewFile(source, destination, operation.ExpectedSource, token, transfer);
        else if (operation.Kind == PlannedOperationKind.RenameDestinationFile)
            DestinationFileRenamer.Rename(Resolve(task.DestinationDirectory, operation.PreviousRelativePath!), destination, operation.ExpectedDestination!);
        else
        {
            var versionFolder = VersionFolder(task);
            FileSystemPathSafety.EnsureNoReparsePointsBetween(task.DestinationDirectory, versionFolder);
            Directory.CreateDirectory(versionFolder);
            if (OperatingSystem.IsWindows()) File.SetAttributes(Path.GetDirectoryName(versionFolder)!, File.GetAttributes(Path.GetDirectoryName(versionFolder)!) | FileAttributes.Hidden);
            var name = Path.GetFileName(operation.RelativePath);
            if (name.Length > 80) name = name[..80];
            var blob = name + "." + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "." + Guid.NewGuid().ToString("N") + ".previous";
            // Metadata is durable before replacement. A missing blob is ignored by ListVersions.
            var version = new PreviousVersion(operation.RelativePath, blob, operation.ExpectedDestination!.Sha256!, DateTimeOffset.UtcNow);
            SaveJson(Path.Combine(versionFolder, blob + ".json"), version);
            VersionedFileUpdater.Update(source, destination, operation.ExpectedSource!, operation.ExpectedDestination!, Path.Combine(versionFolder, blob), token, transfer);
        }
    }

    private static void ValidateOperation(BackupTaskDefinition task, PlannedOperation operation, CancellationToken token)
    {
        var source = Resolve(task.SourceDirectory, operation.RelativePath);
        var destination = Resolve(task.DestinationDirectory, operation.RelativePath);
        if (operation.ExpectedSource?.Sha256 is null ||
            !operation.ExpectedSource.HasSameContentMarkerAs(FileSnapshotReader.Read(operation.RelativePath, source, token)))
            throw new IOException("Quelle wurde seit der Vorschau geändert.");
        if (operation.Kind is PlannedOperationKind.CopyNewFile or PlannedOperationKind.RenameDestinationFile)
        {
            var caseOnly = operation.Kind == PlannedOperationKind.RenameDestinationFile &&
                string.Equals(operation.RelativePath, operation.PreviousRelativePath, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(operation.RelativePath, operation.PreviousRelativePath, StringComparison.Ordinal);
            if (caseOnly)
            {
                RequireExactFileName(source);
                RequireExactFileName(Resolve(task.DestinationDirectory, operation.PreviousRelativePath!));
            }
            if (!caseOnly && (File.Exists(destination) || Directory.Exists(destination))) throw new IOException("Ziel existiert bereits.");
            if (operation.Kind == PlannedOperationKind.RenameDestinationFile)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(operation.PreviousRelativePath);
                var oldSource = Resolve(task.SourceDirectory, operation.PreviousRelativePath);
                if (!caseOnly && (File.Exists(oldSource) || Directory.Exists(oldSource))) throw new IOException("Original source path reappeared after preview.");
                var previous = Resolve(task.DestinationDirectory, operation.PreviousRelativePath);
                if (operation.ExpectedDestination?.Sha256 is null ||
                    !operation.ExpectedSource.HasSameContentMarkerAs(operation.ExpectedDestination) ||
                    !operation.ExpectedDestination.HasSameContentMarkerAs(FileSnapshotReader.Read(operation.PreviousRelativePath, previous, token)))
                    throw new IOException("Rename target changed after preview.");
            }
        }
        else if (operation.ExpectedDestination?.Sha256 is null ||
            !operation.ExpectedDestination.HasSameContentMarkerAs(FileSnapshotReader.Read(operation.RelativePath, destination, token)))
            throw new IOException("Ziel wurde seit der Vorschau geändert.");
    }

    private static void RequireExactFileName(string path)
    {
        var expected = Path.GetFileName(path);
        if (!Directory.EnumerateFiles(Path.GetDirectoryName(path)!).Any(file =>
            Path.GetFileName(file).Equals(expected, StringComparison.Ordinal)))
            throw new IOException("File name changed after preview.");
    }

    private static string Resolve(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new ArgumentException("Relativer Pfad erforderlich.");
        var resolved = Path.GetFullPath(Path.Combine(root, relative));
        if (!FileSystemPathSafety.IsSameOrNestedPath(root, resolved) || SamePath(root, resolved))
            throw new ArgumentException("Pfad außerhalb des Ordners.");
        FileSystemPathSafety.EnsureNoReparsePointsBetween(root, resolved);
        return resolved;
    }

    public IReadOnlyList<PreviousVersion> ListVersions(BackupTaskDefinition task)
    {
        return ReadVersions(VersionFolder(task), false)
            .Concat(ReadVersions(Path.Combine(TaskFolder(task), "versions"), true))
            .OrderByDescending(v => v.SavedAtUtc).ToArray();
    }

    private static IReadOnlyList<PreviousVersion> ReadVersions(string folder, bool legacy)
    {
        if (!Directory.Exists(folder)) return [];
        FileSystemPathSafety.EnsureSafeExistingDirectory(folder, "Versions");
        return Directory.EnumerateFiles(folder, "*.json")
            .Select(path => JsonSerializer.Deserialize<PreviousVersion>(File.ReadAllText(path)) ?? throw new IOException("Ungültige Version."))
            .Select(v => v.SchemaVersion == 1 ? v : throw new IOException("Unsupported recovery version."))
            .Where(v => Path.GetFileName(v.BlobName) == v.BlobName && File.Exists(Path.Combine(folder, v.BlobName)))
            .Select(v => v with { IsLegacy = legacy })
            .OrderByDescending(v => v.SavedAtUtc).ToArray();
    }

    private static void TrimVersions(BackupTaskDefinition task)
    {
        var folder = VersionFolder(task);
        foreach (var group in ReadVersions(folder, false).GroupBy(v => v.RelativePath, StringComparer.OrdinalIgnoreCase))
            foreach (var old in group.OrderByDescending(v => v.SavedAtUtc).Skip(task.VersionLimit))
            {
                var blob = Resolve(folder, old.BlobName);
                var metadata = Resolve(folder, old.BlobName + ".json");
                File.Delete(blob);
                File.Delete(metadata);
            }
    }

    public void ExportVersion(BackupTaskDefinition task, PreviousVersion version, string destination)
    {
        var folder = version.IsLegacy ? Path.Combine(TaskFolder(task), "versions") : VersionFolder(task);
        var source = Resolve(folder, version.BlobName);
        var snapshot = FileSnapshotReader.Read(version.RelativePath, source);
        if (!string.Equals(snapshot.Sha256, version.Sha256, StringComparison.Ordinal))
            throw new IOException("Die gespeicherte Version ist beschädigt.");
        SafeFileCopier.CopyNewFile(source, destination, snapshot);
    }

    private static void SaveJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(value, JsonOptions)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
