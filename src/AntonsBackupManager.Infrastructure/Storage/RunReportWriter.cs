using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.Infrastructure.Storage;

public sealed class RunReportWriter(string directory)
{
    public BackupRunRecord? ReadLatest(Guid taskId)
    {
        try
        {
            var path = Path.Combine(directory, "latest", taskId.ToString("N") + ".json");
            return File.Exists(path) ? System.Text.Json.JsonSerializer.Deserialize<BackupRunRecord>(File.ReadAllText(path)) : new JsonBackupRunReportStore().Load(directory, taskId).FirstOrDefault();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { return null; }
    }

    public bool Record(BackupTaskDefinition task, ManualBackupReport report, string outcome, string trigger, Exception? error = null)
    {
        try
        {
            var record = new BackupRunRecord(task.Id, DateTimeOffset.UtcNow, report.CopiedRelativePaths, report.SkippedConflicts)
                {
                    Outcome = outcome == "Completed" && report.DeferredRelativePaths.Count > 0 ? "RetryPending" : outcome, Trigger = trigger,
                    RenamedRelativePaths = report.RenamedRelativePaths, DeferredRelativePaths = report.DeferredRelativePaths,
                    ErrorCode = error is null ? null : $"{error.GetType().Name}:0x{error.HResult:X8}",
                };
            new JsonBackupRunReportStore().Save(directory, record);
            var latest = Path.Combine(directory, "latest", task.Id.ToString("N") + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(latest)!);
            var temporary = latest + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temporary, System.Text.Json.JsonSerializer.Serialize(record)); File.Move(temporary, latest, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
