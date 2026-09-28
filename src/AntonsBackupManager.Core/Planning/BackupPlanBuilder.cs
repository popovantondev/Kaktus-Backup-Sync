namespace AntonsBackupManager.Core.Planning;

public static class BackupPlanBuilder
{
    public static BackupPlan PlanAll(
        IEnumerable<FileSnapshot> sourceFiles,
        IEnumerable<FileSnapshot> destinationFiles,
        IEnumerable<FileSnapshot> lastSuccessfulDestinationFiles,
        IReadOnlySet<string>? excludedPaths = null)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentNullException.ThrowIfNull(destinationFiles);
        ArgumentNullException.ThrowIfNull(lastSuccessfulDestinationFiles);

        var destinations = ToPathMap(destinationFiles);
        var baselines = ToPathMap(lastSuccessfulDestinationFiles);
        var sources = ToPathMap(sourceFiles);
        var operations = sources.Values
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(source => Plan(
                source,
                destinations.GetValueOrDefault(source.RelativePath),
                baselines.GetValueOrDefault(source.RelativePath)))
            .Concat(destinations.Values.Where(destination => !sources.ContainsKey(destination.RelativePath))
                .Select(destination => new PlannedOperation(destination.RelativePath, PlannedOperationKind.Conflict,
                    "DestinationOnly", null, destination)))
            .ToArray();

        return DetectRenames(operations, baselines, sources, excludedPaths);
    }

    private static BackupPlan DetectRenames(PlannedOperation[] operations,
        IReadOnlyDictionary<string, FileSnapshot> baselines,
        IReadOnlyDictionary<string, FileSnapshot> sources, IReadOnlySet<string>? excludedPaths)
    {
        var newFiles = operations.Where(op => op.Kind == PlannedOperationKind.CopyNewFile &&
            !baselines.ContainsKey(op.RelativePath) && op.ExpectedSource?.Sha256 is not null)
            .GroupBy(op => ContentKey(op.ExpectedSource!)).ToDictionary(group => group.Key, group => group.ToArray());
        var oldFiles = operations.Where(op => op.Kind == PlannedOperationKind.Conflict && op.ExpectedSource is null &&
            !sources.ContainsKey(op.RelativePath) && excludedPaths?.Contains(op.RelativePath) != true &&
            op.ExpectedDestination?.Sha256 is not null && baselines.TryGetValue(op.RelativePath, out var previous) &&
            previous.Sha256 is not null && previous.HasSameContentMarkerAs(op.ExpectedDestination))
            .GroupBy(op => ContentKey(op.ExpectedDestination!)).ToDictionary(group => group.Key, group => group.ToArray());
        var replacements = new Dictionary<string, PlannedOperation>(StringComparer.OrdinalIgnoreCase);
        var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, candidates) in newFiles)
        {
            // Duplicate content has no unambiguous rename identity.
            if (candidates.Length != 1 || !oldFiles.TryGetValue(key, out var previous) || previous.Length != 1) continue;
            var next = candidates[0];
            var old = previous[0];
            replacements[next.RelativePath] = next with
            {
                Kind = PlannedOperationKind.RenameDestinationFile, Reason = "RenameDestinationFile",
                PreviousRelativePath = old.RelativePath, ExpectedDestination = old.ExpectedDestination,
            };
            removed.Add(old.RelativePath);
        }
        return new BackupPlan(operations.Where(op => !removed.Contains(op.RelativePath))
            .Select(op => replacements.GetValueOrDefault(op.RelativePath, op)).ToArray());
    }

    private static string ContentKey(FileSnapshot file) => $"{file.Length}:{file.Sha256!.ToUpperInvariant()}";

    public static PlannedOperation Plan(
        FileSnapshot source,
        FileSnapshot? destination,
        FileSnapshot? lastSuccessfulDestination)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (destination is null)
        {
            // One-way synchronization recreates missing destination files, even with a baseline.
            return new PlannedOperation(source.RelativePath, PlannedOperationKind.CopyNewFile,
                "Datei fehlt im Ziel und wird aus der Quelle kopiert.", source, destination);
        }

        if (source.HasSameContentMarkerAs(destination))
        {
            if (source.Sha256 is not null && destination.Sha256 is not null &&
                string.Equals(source.RelativePath, destination.RelativePath, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(source.RelativePath, destination.RelativePath, StringComparison.Ordinal) &&
                string.Equals(Path.GetDirectoryName(source.RelativePath), Path.GetDirectoryName(destination.RelativePath), StringComparison.Ordinal))
                return new PlannedOperation(source.RelativePath, PlannedOperationKind.RenameDestinationFile,
                    "RenameDestinationFile", source, destination) { PreviousRelativePath = destination.RelativePath };
            return new PlannedOperation(source.RelativePath, PlannedOperationKind.Unchanged, "Quelle und Ziel stimmen überein.", source, destination);
        }

        if (lastSuccessfulDestination is null)
        {
            return new PlannedOperation(source.RelativePath, PlannedOperationKind.Conflict, "Es gibt keine sichere Vergleichsbasis für unterschiedliche Dateien.", source, destination);
        }

        var sourceChanged = !source.HasSameContentMarkerAs(lastSuccessfulDestination);
        var destinationChanged = !destination.HasSameContentMarkerAs(lastSuccessfulDestination);

        return sourceChanged && !destinationChanged
            ? new PlannedOperation(source.RelativePath, PlannedOperationKind.UpdateDestinationFile, "Nur die Quelle wurde seit der letzten Sicherung geändert.", source, destination)
            : new PlannedOperation(source.RelativePath, PlannedOperationKind.Conflict, "Quelle und Ziel wurden unterschiedlich geändert.", source, destination);
    }

    private static Dictionary<string, FileSnapshot> ToPathMap(IEnumerable<FileSnapshot> files) =>
        files.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
}
