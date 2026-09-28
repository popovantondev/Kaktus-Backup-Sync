namespace AntonsBackupManager.Core.Planning;

public enum PlannedOperationKind
{
    CopyNewFile,
    UpdateDestinationFile,
    Unchanged,
    Conflict,
    KeepDestination,
    RenameDestinationFile,
    RetryLockedFile,
}

public sealed record PlannedOperation(
    string RelativePath,
    PlannedOperationKind Kind,
    string Reason,
    FileSnapshot? ExpectedSource = null,
    FileSnapshot? ExpectedDestination = null)
{
    public string? PreviousRelativePath { get; init; }
}
