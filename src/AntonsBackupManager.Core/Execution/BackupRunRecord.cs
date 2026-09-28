namespace AntonsBackupManager.Core.Execution;

public sealed record BackupRunRecord(
    Guid TaskId,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<string> CopiedRelativePaths,
    int SkippedConflicts)
{
    public int SchemaVersion { get; init; } = 1;
    public string Trigger { get; init; } = "Manual";
    public string? ErrorCode { get; init; }
    public string Outcome { get; init; } = "Completed";
    public IReadOnlyList<string> RenamedRelativePaths { get; init; } = [];
    public IReadOnlyList<string> DeferredRelativePaths { get; init; } = [];
}
