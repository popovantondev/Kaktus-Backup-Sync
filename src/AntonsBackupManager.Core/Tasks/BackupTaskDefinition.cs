namespace AntonsBackupManager.Core.Tasks;

public sealed record BackupTaskDefinition(
    Guid Id,
    string Name,
    string SourceDirectory,
    string DestinationDirectory,
    int ConfirmedRunsRemaining,
    bool BackgroundCheckEnabled = false,
    int BackgroundCheckIntervalMinutes = 1,
    string? MonitoredRemovableVolumeSerial = null,
    int VersionLimit = 7,
    bool SafetyModeEnabled = true,
    int FullCheckIntervalMinutes = 60,
    RemovableFolderBinding? SourceBinding = null,
    RemovableFolderBinding? DestinationBinding = null);
