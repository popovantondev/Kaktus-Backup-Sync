using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Infrastructure.Storage;

namespace AntonsBackupManager.App;

internal sealed record PreviewResult(BackupTaskDefinition Task, BackupPlan Plan);
internal sealed record RunResult(ManualBackupReport Report, Exception? Failure, bool ReportSaved, bool TaskSaved);

// Owns persistent state and file workflows; it knows nothing about windows or controls.
internal sealed class BackupWorkspace
{
    private readonly JsonBackupTaskStore taskStore = new();
    private readonly JsonBackupRunReportStore reportStore = new();
    private readonly string taskFile;
    private readonly string reportsDirectory;
    private readonly Func<IReadOnlyList<AvailableVolume>> volumes;
    public SynchronizationService Synchronization { get; }
    public RunReportWriter Reports { get; }
    public WorkspacePreferences Preferences { get; }
    public IReadOnlyList<BackupTaskDefinition> Tasks { get; private set; } = [];
    public bool StoreAvailable { get; private set; } = true;
    public event EventHandler? TasksChanged;

    public BackupWorkspace(string runtime, Func<IReadOnlyList<AvailableVolume>>? volumes = null)
    {
        this.volumes = volumes ?? RemovableVolumeIdentity.AvailableVolumes;
        taskFile = Path.Combine(runtime, "tasks.json");
        reportsDirectory = Path.Combine(runtime, "reports");
        Reports = new RunReportWriter(reportsDirectory);
        Preferences = new WorkspacePreferences(runtime);
        Synchronization = new SynchronizationService(Path.Combine(runtime, "sync"), validateLocation: ValidateCurrentLocation);
        try { Tasks = taskStore.Load(taskFile); }
        catch (Exception) { StoreAvailable = false; }
    }

    public static bool HasBinding(BackupTaskDefinition task) => task.SourceBinding is not null ||
        task.DestinationBinding is not null || task.MonitoredRemovableVolumeSerial is not null;

    public IReadOnlyList<AvailableVolume> AvailableVolumes() => volumes();

    private void ValidateCurrentLocation(BackupTaskDefinition task)
    {
        if (HasBinding(task)) TaskVolumeResolver.EnsureAvailableAtCurrentPaths(task, volumes());
    }

    public async Task<PreviewResult> PreviewAsync(BackupTaskDefinition draft, IProgress<string>? progress, CancellationToken token)
    {
        var previous = Tasks.FirstOrDefault(task => task.Id == draft.Id);
        var resolved = await Task.Run(() => ResolveDraft(draft, previous), token);
        var tasks = ReplaceOrAdd(resolved);
        await Task.Run(() => RebindStateIfNeeded(previous, resolved), token);
        var plan = await Task.Run(() => Synchronization.Preview(resolved, token, progress), token);
        token.ThrowIfCancellationRequested();
        await SaveAsync(tasks);
        return new(resolved, plan);
    }

    public async Task<BackupTaskDefinition> PrepareAutomaticTaskAsync(BackupTaskDefinition task)
    {
        var resolved = await Task.Run(() => ResolveDraft(task, task));
        var tasks = ReplaceOrAdd(resolved);
        if (resolved != task)
        {
            await Task.Run(() => RebindStateIfNeeded(task, resolved));
            await SaveAsync(tasks);
        }
        return resolved;
    }

    private BackupTaskDefinition ResolveDraft(BackupTaskDefinition draft, BackupTaskDefinition? previous)
    {
        var available = volumes();
        if (previous is not null)
            draft = draft with
            {
                SourceBinding = AdjustEditedBinding(draft.SourceBinding, draft.SourceDirectory, previous.SourceDirectory, available),
                DestinationBinding = AdjustEditedBinding(draft.DestinationBinding, draft.DestinationDirectory, previous.DestinationDirectory, available),
            };
        var resolved = TaskVolumeResolver.Resolve(draft, available);
        var others = Tasks.Where(task => task.Id != draft.Id).Select(task =>
        {
            try { return TaskVolumeResolver.Resolve(task, available); }
            catch (RemovableVolumeException error) when (error.Problem != VolumeProblem.Ambiguous) { return task; }
        });
        TaskPathRules.ValidateAgainst(resolved, others);
        return resolved;
    }

    private static RemovableFolderBinding? AdjustEditedBinding(RemovableFolderBinding? binding,
        string current, string previous, IReadOnlyList<AvailableVolume> available)
    {
        if (binding is null || SamePath(current, previous)) return binding;
        return TaskVolumeResolver.Capture(current, available, binding.VolumeSerial)
            ?? throw new RemovableVolumeException(VolumeProblem.Missing);
    }

    private void RebindStateIfNeeded(BackupTaskDefinition? before, BackupTaskDefinition after)
    {
        if (before is null || SamePath(before.SourceDirectory, after.SourceDirectory) && SamePath(before.DestinationDirectory, after.DestinationDirectory)) return;
        if (SameLocation(before.SourceDirectory, after.SourceDirectory, before.SourceBinding, after.SourceBinding) &&
            SameLocation(before.DestinationDirectory, after.DestinationDirectory, before.DestinationBinding, after.DestinationBinding))
            Synchronization.RebindTaskLocation(before, after);
    }

    private static bool SameLocation(string before, string after, RemovableFolderBinding? oldBinding, RemovableFolderBinding? newBinding) =>
        SamePath(before, after) || oldBinding is not null && newBinding is not null &&
        oldBinding.VolumeSerial.Equals(newBinding.VolumeSerial, StringComparison.OrdinalIgnoreCase) &&
        oldBinding.RelativeDirectory.Equals(newBinding.RelativeDirectory, StringComparison.OrdinalIgnoreCase) &&
        Path.GetRelativePath(Path.GetPathRoot(before)!, before).Equals(newBinding.RelativeDirectory, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string first, string second) =>
        TaskPathRules.Normalize(first).Equals(TaskPathRules.Normalize(second), StringComparison.OrdinalIgnoreCase);

    private IReadOnlyList<BackupTaskDefinition> ReplaceOrAdd(BackupTaskDefinition task) =>
        Tasks.Any(existing => existing.Id == task.Id) ? BackupTaskCatalog.Replace(Tasks, task) : BackupTaskCatalog.Add(Tasks, task);

    public Task UpdateTaskAsync(BackupTaskDefinition task) => SaveAsync(BackupTaskCatalog.Replace(Tasks, task));
    public Task RemoveTaskAsync(Guid id) => SaveAsync(BackupTaskCatalog.Remove(Tasks, id));

    private async Task SaveAsync(IReadOnlyList<BackupTaskDefinition> tasks)
    {
        if (!StoreAvailable) throw new InvalidOperationException("The task catalog is unavailable.");
        await Task.Run(() => taskStore.Save(taskFile, tasks));
        Tasks = tasks;
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<RunResult> SynchronizeAsync(BackupTaskDefinition task, BackupPlan plan, CancellationToken token)
    {
        ManualBackupReport report;
        try { report = await Task.Run(() => Synchronization.Synchronize(task, plan, token), token); }
        catch (Exception error)
        {
            var partial = error is BackupExecutionException failure ? failure.Completed : new ManualBackupReport([], plan.Conflicts);
            var cause = error is BackupExecutionException wrapped ? wrapped.InnerException ?? wrapped : error;
            var outcome = cause is OperationCanceledException ? "Cancelled" : partial.SynchronizedFiles > 0 ? "Interrupted" : "Failed";
            var recorded = await Task.Run(() => Reports.Record(task, partial, outcome, "Manual", cause));
            return new(partial, cause, recorded, true);
        }
        var reportSaved = await Task.Run(() => Reports.Record(task, report, "Completed", "Manual"));
        var complete = report.SkippedConflicts == 0 && report.DeferredRelativePaths.Count == 0;
        var updated = task with { ConfirmedRunsRemaining = complete ? Math.Max(0, task.ConfirmedRunsRemaining - 1) : task.ConfirmedRunsRemaining };
        try { await UpdateTaskAsync(updated); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return new(report, null, reportSaved, false); }
        return new(report, null, reportSaved, true);
    }

    public Task<IReadOnlyList<BackupRunRecord>> ReadHistoryAsync(Guid taskId) => Task.Run(() => reportStore.Load(reportsDirectory, taskId));
    public Task<IReadOnlyList<PreviousVersion>> ReadVersionsAsync(BackupTaskDefinition task) => Task.Run(() => Synchronization.ListVersions(task));
    public Task ExportVersionAsync(BackupTaskDefinition task, PreviousVersion version, string destination) =>
        Task.Run(() => { ValidateCurrentLocation(task); Synchronization.ExportVersion(task, version, destination); });
}
