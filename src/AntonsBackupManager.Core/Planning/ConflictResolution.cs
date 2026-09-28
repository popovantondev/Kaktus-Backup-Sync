namespace AntonsBackupManager.Core.Planning;

public static class ConflictResolution
{
    public static BackupPlan KeepDestination(BackupPlan plan, IEnumerable<string> selectedPaths)
    {
        var selected = selectedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new BackupPlan(plan.Operations.Select(op => op.Kind == PlannedOperationKind.Conflict &&
            selected.Contains(op.RelativePath) && op.ExpectedDestination?.Sha256 is not null
                ? op with { Kind = PlannedOperationKind.KeepDestination, Reason = "KeepDestination" } : op).ToArray());
    }
    // Resolves only explicitly selected files. Execution must still validate both snapshots.
    public static BackupPlan UseSource(BackupPlan plan, IEnumerable<string> selectedPaths)
    {
        var selected = selectedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new BackupPlan(plan.Operations.Select(op =>
            op.Kind == PlannedOperationKind.Conflict && selected.Contains(op.RelativePath)
                && op.ExpectedSource?.Sha256 is not null && op.ExpectedDestination?.Sha256 is not null
                ? op with { Kind = PlannedOperationKind.UpdateDestinationFile, Reason = "Quelle ausdrücklich ausgewählt." }
                : op).ToArray());
    }
}
