namespace AntonsBackupManager.Core.Tasks;

public sealed record RemovableFolderBinding(string VolumeSerial, string RelativeDirectory);
public sealed record AvailableVolume(string RootDirectory, string Serial);

public enum VolumeProblem { Missing, Ambiguous, NeedsRebind }
public sealed class RemovableVolumeException(VolumeProblem problem)
    : IOException("The removable-volume binding cannot be resolved: " + problem)
{
    public VolumeProblem Problem { get; } = problem;
}

public static class TaskVolumeResolver
{
    public static BackupTaskDefinition Resolve(BackupTaskDefinition task, IReadOnlyList<AvailableVolume> volumes)
    {
        var sourceBinding = task.SourceBinding;
        var targetBinding = task.DestinationBinding;
        if (sourceBinding is null && targetBinding is null && task.MonitoredRemovableVolumeSerial is { } legacy)
        {
            // Old state did not record which endpoint belonged to the drive.
            // Upgrade it only while that endpoint can be verified at its original root.
            sourceBinding = Capture(task.SourceDirectory, volumes, legacy);
            targetBinding = Capture(task.DestinationDirectory, volumes, legacy);
            if (sourceBinding is null && targetBinding is null) throw new RemovableVolumeException(VolumeProblem.NeedsRebind);
        }
        var resolved = task with
        {
            SourceDirectory = Remap(task.SourceDirectory, sourceBinding, volumes),
            DestinationDirectory = Remap(task.DestinationDirectory, targetBinding, volumes),
            SourceBinding = sourceBinding,
            DestinationBinding = targetBinding,
        };
        TaskPathRules.ValidatePair(resolved);
        return resolved;
    }

    public static RemovableFolderBinding? Capture(string path, IReadOnlyList<AvailableVolume> volumes, string? expectedSerial = null)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path))!;
        var volume = volumes.SingleOrDefault(v => TaskPathRules.Normalize(v.RootDirectory).Equals(TaskPathRules.Normalize(root), StringComparison.OrdinalIgnoreCase));
        if (volume is null || expectedSerial is not null && !volume.Serial.Equals(expectedSerial, StringComparison.OrdinalIgnoreCase)) return null;
        return new(volume.Serial, Path.GetRelativePath(root, Path.GetFullPath(path)));
    }

    public static void EnsureAvailableAtCurrentPaths(BackupTaskDefinition task, IReadOnlyList<AvailableVolume> volumes)
    {
        var resolved = Resolve(task, volumes);
        if (!TaskPathRules.Normalize(task.SourceDirectory).Equals(TaskPathRules.Normalize(resolved.SourceDirectory), StringComparison.OrdinalIgnoreCase) ||
            !TaskPathRules.Normalize(task.DestinationDirectory).Equals(TaskPathRules.Normalize(resolved.DestinationDirectory), StringComparison.OrdinalIgnoreCase))
            throw new RemovableVolumeException(VolumeProblem.Missing);
    }

    private static string Remap(string previous, RemovableFolderBinding? binding, IReadOnlyList<AvailableVolume> volumes)
    {
        if (binding is null) return previous;
        Validate(binding);
        var candidates = volumes.Where(v => v.Serial.Equals(binding.VolumeSerial, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (candidates.Length != 1) throw new RemovableVolumeException(candidates.Length == 0 ? VolumeProblem.Missing : VolumeProblem.Ambiguous);
        var root = TaskPathRules.Normalize(candidates[0].RootDirectory);
        var path = TaskPathRules.Normalize(Path.Combine(root, binding.RelativeDirectory));
        if (!TaskPathRules.Contains(root, path)) throw new ArgumentException("Volume-relative folder escapes its root.");
        return path;
    }

    public static void Validate(RemovableFolderBinding binding)
    {
        if (string.IsNullOrWhiteSpace(binding.VolumeSerial) || binding.VolumeSerial.Length != 8 || binding.VolumeSerial.Any(c => !Uri.IsHexDigit(c)) ||
            string.IsNullOrWhiteSpace(binding.RelativeDirectory) || Path.IsPathRooted(binding.RelativeDirectory) ||
            binding.RelativeDirectory.Split('/', '\\').Any(part => part == "..") || binding.RelativeDirectory.Contains(':'))
            throw new ArgumentException("Invalid removable-folder binding.");
    }
}
