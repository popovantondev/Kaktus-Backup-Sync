using System.Text.Json;
using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.Infrastructure.Storage;

public sealed class JsonBackupTaskStore
{
    private sealed record Catalog(int SchemaVersion, IReadOnlyList<BackupTaskDefinition> Tasks);
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public IReadOnlyList<BackupTaskDefinition> Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string json;
        try { json = File.ReadAllText(filePath); }
        catch (FileNotFoundException) { return []; }
        catch (DirectoryNotFoundException) { return []; }
        using var document = JsonDocument.Parse(json);
        IReadOnlyList<BackupTaskDefinition> tasks;
        if (document.RootElement.ValueKind == JsonValueKind.Array)
            tasks = document.RootElement.Deserialize<List<BackupTaskDefinition>>(SerializerOptions)
                ?? throw new JsonException("Empty catalog.");
        else
        {
            var catalog = document.RootElement.Deserialize<Catalog>(SerializerOptions)
                ?? throw new JsonException("Invalid catalog.");
            if (catalog.SchemaVersion is not (1 or 2) || catalog.Tasks is null)
                throw new JsonException("Unsupported catalog version. The original file is preserved.");
            tasks = catalog.Tasks;
        }
        ValidateCatalog(tasks);

        return tasks;
    }

    public void Save(string filePath, IReadOnlyList<BackupTaskDefinition> tasks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(tasks);
        ValidateCatalog(tasks);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new Catalog(2, tasks), SerializerOptions));
            if (File.Exists(filePath))
                File.Copy(filePath, filePath + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N") + ".json");
            File.Move(temporaryPath, filePath, overwrite: true);
            foreach (var backup in Directory.EnumerateFiles(Path.GetDirectoryName(Path.GetFullPath(filePath))!, Path.GetFileName(filePath) + ".backup-*.json")
                .OrderByDescending(path => path, StringComparer.Ordinal).Skip(3)) File.Delete(backup);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ValidateCatalog(IReadOnlyList<BackupTaskDefinition> tasks)
    {
        var ids = new HashSet<Guid>();
        IReadOnlyList<BackupTaskDefinition> validated = [];
        foreach (var task in tasks)
        {
            BackupTaskValidator.Validate(task);
            if (!ids.Add(task.Id)) throw new JsonException("Duplicate task identity.");
            validated = BackupTaskCatalog.Add(validated, task);
        }
    }
}
