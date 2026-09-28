using System.Text.Json;
using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.Infrastructure.Storage;

internal sealed class ConflictDecisionStore(string path)
{
    public void Rebind(BackupTaskDefinition previous, BackupTaskDefinition current)
    {
        if (!File.Exists(path)) return;
        var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path)) ?? throw new JsonException("Invalid conflict decisions.");
        if (document.SchemaVersion != 1) throw new JsonException("Unsupported conflict decisions.");
        if (!string.Equals(document.Source, previous.SourceDirectory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(document.Destination, previous.DestinationDirectory, StringComparison.OrdinalIgnoreCase)) return;
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(document with { Source = current.SourceDirectory, Destination = current.DestinationDirectory }));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private sealed record Decision(string RelativePath, string? SourceHash, string DestinationHash);
    private sealed record Document(int SchemaVersion, string Source, string Destination, IReadOnlyList<Decision> Decisions);

    private Dictionary<string, Decision> Read(BackupTaskDefinition task)
    {
        if (!File.Exists(path)) return new(StringComparer.OrdinalIgnoreCase);
        var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path)) ?? throw new JsonException("Invalid conflict decisions.");
        if (document.SchemaVersion != 1 || document.Decisions is null) throw new JsonException("Unsupported conflict decisions.");
        if (!string.Equals(document.Source, task.SourceDirectory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(document.Destination, task.DestinationDirectory, StringComparison.OrdinalIgnoreCase))
            return new(StringComparer.OrdinalIgnoreCase);
        return document.Decisions.ToDictionary(item => item.RelativePath, StringComparer.OrdinalIgnoreCase);
    }

    public BackupPlan Apply(BackupTaskDefinition task, BackupPlan plan)
    {
        var decisions = Read(task);
        var operations = new List<PlannedOperation>();
        foreach (var operation in plan.Operations)
        {
            // A remembered choice to retain an orphan destination outranks rename detection.
            if (operation.Kind == PlannedOperationKind.RenameDestinationFile &&
                decisions.TryGetValue(operation.PreviousRelativePath!, out var retained) &&
                retained.SourceHash is null && retained.DestinationHash == operation.ExpectedDestination?.Sha256)
            {
                operations.Add(operation with { Kind = PlannedOperationKind.CopyNewFile, Reason = "CopyNewFile", ExpectedDestination = null, PreviousRelativePath = null });
                operations.Add(new PlannedOperation(operation.PreviousRelativePath!, PlannedOperationKind.KeepDestination,
                    "KeepDestination", null, operation.ExpectedDestination));
            }
            else operations.Add(operation);
        }
        return new BackupPlan(operations.Select(op => op.Kind == PlannedOperationKind.Conflict &&
            decisions.TryGetValue(op.RelativePath, out var saved) &&
            op.ExpectedSource?.Sha256 == saved.SourceHash && op.ExpectedDestination?.Sha256 == saved.DestinationHash
                ? op with { Kind = PlannedOperationKind.KeepDestination, Reason = "KeepDestination" } : op).ToArray());
    }

    public void Save(BackupTaskDefinition task, BackupPlan plan)
    {
        var decisions = Read(task);
        foreach (var op in plan.Operations)
        {
            if (op.Kind == PlannedOperationKind.KeepDestination && op.ExpectedDestination?.Sha256 is not null)
                decisions[op.RelativePath] = new Decision(op.RelativePath, op.ExpectedSource?.Sha256, op.ExpectedDestination.Sha256);
            else if (op.Kind != PlannedOperationKind.Conflict) decisions.Remove(op.RelativePath);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(new Document(1, task.SourceDirectory, task.DestinationDirectory, decisions.Values.ToArray())));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
