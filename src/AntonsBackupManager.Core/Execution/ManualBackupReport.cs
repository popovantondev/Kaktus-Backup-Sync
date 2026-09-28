namespace AntonsBackupManager.Core.Execution;

public sealed record ManualBackupReport(IReadOnlyList<string> CopiedRelativePaths, int SkippedConflicts)
{
    public int CopiedFiles => CopiedRelativePaths.Count;
    public IReadOnlyList<string> RenamedRelativePaths { get; init; } = [];
    public IReadOnlyList<string> DeferredRelativePaths { get; init; } = [];
    public int SynchronizedFiles => CopiedFiles + RenamedRelativePaths.Count;
}

public sealed class BackupExecutionException(ManualBackupReport completed, Exception innerException)
    : IOException("Backup interrupted; see completed files.", innerException)
{
    public ManualBackupReport Completed { get; } = completed;
}
