namespace AntonsBackupManager.Core.Tasks;

public static class BackupTaskValidator
{
    public static void Validate(BackupTaskDefinition task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.SourceBinding is { } sourceBinding) TaskVolumeResolver.Validate(sourceBinding);
        if (task.DestinationBinding is { } destinationBinding) TaskVolumeResolver.Validate(destinationBinding);
        if (task.Id == Guid.Empty) throw new ArgumentException("Task identity is missing.");
        ArgumentException.ThrowIfNullOrWhiteSpace(task.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(task.SourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(task.DestinationDirectory);
        if (task.VersionLimit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(task), "Version limit must be between 1 and 100.");
        if (task.FullCheckIntervalMinutes is < 1 or > 10080) throw new ArgumentOutOfRangeException(nameof(task), "Full check interval must be between 1 and 10080 minutes.");

        if (task.ConfirmedRunsRemaining is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(task), "Die Zahl der Bestätigungen muss zwischen 0 und 5 liegen.");
        }

        if (task.BackgroundCheckIntervalMinutes is < 1 or > 1440)
        {
            throw new ArgumentOutOfRangeException(nameof(task), "Das Prüfintervall muss zwischen 1 und 1440 Minuten liegen.");
        }
    }
}
