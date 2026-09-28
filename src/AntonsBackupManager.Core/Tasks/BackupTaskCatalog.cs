namespace AntonsBackupManager.Core.Tasks;

public static class BackupTaskCatalog
{
    public static IReadOnlyList<BackupTaskDefinition> Add(
        IReadOnlyList<BackupTaskDefinition> existingTasks,
        BackupTaskDefinition newTask)
    {
        ArgumentNullException.ThrowIfNull(existingTasks);
        BackupTaskValidator.Validate(newTask);
        TaskPathRules.ValidateAgainst(newTask, existingTasks);

        if (existingTasks.Any(task =>
                string.Equals(task.SourceDirectory, newTask.SourceDirectory, StringComparison.OrdinalIgnoreCase)
                && string.Equals(task.DestinationDirectory, newTask.DestinationDirectory, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Für diese Quelle und dieses Ziel gibt es bereits eine Aufgabe.");
        }

        return existingTasks.Append(newTask).ToArray();
    }

    public static IReadOnlyList<BackupTaskDefinition> Replace(
        IReadOnlyList<BackupTaskDefinition> existingTasks,
        BackupTaskDefinition updatedTask)
    {
        ArgumentNullException.ThrowIfNull(existingTasks);
        BackupTaskValidator.Validate(updatedTask);
        if (!existingTasks.Any(task => task.Id == updatedTask.Id))
        {
            throw new ArgumentException("Die zu ändernde Aufgabe wurde nicht gefunden.");
        }

        var remaining = existingTasks.Where(task => task.Id != updatedTask.Id).ToArray();
        return Add(remaining, updatedTask);
    }

    public static IReadOnlyList<BackupTaskDefinition> Remove(IReadOnlyList<BackupTaskDefinition> existingTasks, Guid taskId) =>
        existingTasks.Where(task => task.Id != taskId).ToArray();
}
