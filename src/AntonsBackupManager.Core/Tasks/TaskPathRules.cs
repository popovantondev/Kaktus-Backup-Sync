namespace AntonsBackupManager.Core.Tasks;

public static class TaskPathRules
{
    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    public static bool Contains(string parent, string child)
    {
        parent = Normalize(parent); child = Normalize(child);
        return string.Equals(parent, child, StringComparison.OrdinalIgnoreCase) ||
            child.StartsWith(Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    public static bool Overlaps(string first, string second) => Contains(first, second) || Contains(second, first);

    public static void ValidatePair(BackupTaskDefinition task)
    {
        if (Overlaps(task.SourceDirectory, task.DestinationDirectory))
            throw new ArgumentException("Source and destination must not overlap.");
    }

    public static void ValidateAgainst(BackupTaskDefinition task, IEnumerable<BackupTaskDefinition> others)
    {
        ValidatePair(task);
        foreach (var other in others)
            if (Overlaps(task.DestinationDirectory, other.DestinationDirectory) ||
                Overlaps(task.SourceDirectory, other.DestinationDirectory) ||
                Overlaps(task.DestinationDirectory, other.SourceDirectory))
                throw new ArgumentException("The folders overlap another task's destination: " + other.Name);
    }
}
