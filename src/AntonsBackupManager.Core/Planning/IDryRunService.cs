namespace AntonsBackupManager.Core.Planning;

public interface IDryRunService
{
    BackupPlan CreatePlan(string sourceDirectory, string destinationDirectory);
}
