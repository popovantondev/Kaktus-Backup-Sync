namespace AntonsBackupManager.Core.Planning;

public sealed record BackupPlan(IReadOnlyList<PlannedOperation> Operations)
{
    public int CopyNewFiles => Operations.Count(operation => operation.Kind == PlannedOperationKind.CopyNewFile);

    public int Updates => Operations.Count(operation => operation.Kind == PlannedOperationKind.UpdateDestinationFile);

    public int UnchangedFiles => Operations.Count(operation => operation.Kind == PlannedOperationKind.Unchanged);

    public int Conflicts => Operations.Count(operation => operation.Kind == PlannedOperationKind.Conflict);
    public int KeptFiles => Operations.Count(operation => operation.Kind == PlannedOperationKind.KeepDestination);
    public int Renames => Operations.Count(operation => operation.Kind == PlannedOperationKind.RenameDestinationFile);
    public int DeferredFiles => Operations.Count(operation => operation.Kind == PlannedOperationKind.RetryLockedFile);
}
