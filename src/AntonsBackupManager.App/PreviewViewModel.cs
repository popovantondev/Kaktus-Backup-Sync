using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.App;

internal sealed record PlanRow(PlannedOperation Operation)
{
    public string Action => UiText.Get(Operation.Kind.ToString());
    public string Path => Operation.PreviousRelativePath is { } previous ? $"{previous} → {Operation.RelativePath}" : Operation.RelativePath;
}

internal sealed class PreviewViewModel : ObservableModel
{
    public BackupPlan? Plan { get; private set; }
    public BackupTaskDefinition? Task { get; private set; }
    public IReadOnlyList<PlanRow> Rows { get; private set; } = [];
    public IReadOnlyList<PlanRow> Selection { get; set; } = [];
    public bool HasPlan => Plan is not null;
    public bool HasConflicts => Plan?.Conflicts > 0;
    public string Summary => Plan is not { } plan ? "" : UiText.Get("SyncSummary", plan.CopyNewFiles, plan.Updates, plan.UnchangedFiles, plan.Conflicts) +
        (plan.Renames + plan.DeferredFiles > 0 ? "\n" + UiText.Get("SyncExtra", plan.Renames, plan.DeferredFiles) : "");

    public void SetPlan(BackupTaskDefinition? task, BackupPlan? plan)
    {
        Task = task; Plan = plan; Refresh();
    }
    public void Clear() => SetPlan(null, null);
    public void Refresh()
    {
        Selection = [];
        Rows = Plan?.Operations.Select(operation => new PlanRow(operation)).ToArray() ?? [];
        Changed(null);
    }
}
