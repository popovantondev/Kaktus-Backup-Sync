using System.Text.Json;
using AntonsBackupManager.Core.Execution;

namespace AntonsBackupManager.Infrastructure.Storage;

public sealed class JsonBackupRunReportStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public void Save(string reportsDirectory, BackupRunRecord report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportsDirectory);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.CopiedRelativePaths);

        Directory.CreateDirectory(reportsDirectory);
        var fileName = $"{report.CompletedAtUtc:yyyyMMddTHHmmssfffZ}-{report.TaskId:N}-{Guid.NewGuid():N}.json";
        var reportPath = Path.Combine(reportsDirectory, fileName);
        var temporaryPath = $"{reportPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(report, SerializerOptions));
            File.Move(temporaryPath, reportPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public IReadOnlyList<BackupRunRecord> Load(string reportsDirectory, Guid taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportsDirectory);
        if (!Directory.Exists(reportsDirectory)) return [];
        return Directory.EnumerateFiles(reportsDirectory, "*.json")
            .Select(path => JsonSerializer.Deserialize<BackupRunRecord>(File.ReadAllText(path), SerializerOptions)
                ?? throw new JsonException("Ungültiger Durchlaufbericht."))
            .Where(report => report.TaskId == taskId)
            .OrderByDescending(report => report.CompletedAtUtc)
            .ToArray();
    }
}
