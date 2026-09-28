using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Infrastructure.Storage;
using System.Windows.Threading;

namespace AntonsBackupManager.App;

internal sealed record BackgroundSyncResult(BackupTaskDefinition Task, int SynchronizedFiles, int Conflicts, Exception? Error, bool ReportSaved = true, bool NeedsApproval = false, int DeferredFiles = 0);

internal sealed class TaskPoller : IDisposable
{
    private readonly DispatcherTimer timer;
    private readonly SynchronizationService synchronization;
    private readonly Func<IReadOnlyList<BackupTaskDefinition>> tasks;
    private readonly Action<IReadOnlyList<BackgroundSyncResult>> completed;
    private readonly Action<bool> activityChanged;
    private readonly Func<bool> canRun;
    private readonly RunReportWriter? reportWriter;
    private readonly Func<BackupTaskDefinition, Task<BackupTaskDefinition>>? prepareTask;
    private readonly Func<IReadOnlyList<AvailableVolume>> volumes;
    private readonly Dictionary<Guid, DateTimeOffset> nextRuns = [];
    private readonly HashSet<Guid> paused = [];
    private CancellationTokenSource? cancellation;
    private Guid? currentTask;
    private bool disposed;
    public bool IsRunning { get; private set; }
    public Guid? CurrentTaskId => currentTask;
    public bool IsPaused { get; private set; }
    public DateTimeOffset? NextRun(Guid id) => nextRuns.TryGetValue(id, out var next) ? next : null;
    public void SetPaused(bool value) { IsPaused = value; if (value) CancelCurrent(); else ResetSchedule(); }

    public TaskPoller(SynchronizationService synchronization, Func<IReadOnlyList<BackupTaskDefinition>> tasks,
        Action<IReadOnlyList<BackgroundSyncResult>> completed, Action<bool> activityChanged, Func<bool>? canRun = null, RunReportWriter? reportWriter = null,
        Func<BackupTaskDefinition, Task<BackupTaskDefinition>>? prepareTask = null, Func<IReadOnlyList<AvailableVolume>>? volumes = null)
    {
        this.synchronization = synchronization;
        this.tasks = tasks;
        this.completed = completed;
        this.activityChanged = activityChanged;
        this.canRun = canRun ?? (() => true);
        this.reportWriter = reportWriter;
        this.prepareTask = prepareTask;
        this.volumes = volumes ?? RemovableVolumeIdentity.AvailableVolumes;
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += async (_, _) => await RunDue();
    }

    public void Start() => timer.Start();
    public void ResetSchedule() => nextRuns.Clear();
    public void Pause(Guid id)
    {
        paused.Add(id);
        if (currentTask == id) cancellation?.Cancel();
    }
    public void Resume(Guid id) { paused.Remove(id); nextRuns.Remove(id); }
    public void CancelCurrent() => cancellation?.Cancel();
    public void RunEnabledTasksNow()
    {
        ResetSchedule();
        _ = RunDue();
    }
    public void CheckTasksForConnectedRemovableDrive(IReadOnlyList<string>? arrivingRoots = null)
    {
        var matched = false;
        var available = volumes();
        foreach (var task in tasks().Where(task => task.BackgroundCheckEnabled && available.Any(volume =>
            (arrivingRoots is null || arrivingRoots.Any(root => root.Equals(volume.RootDirectory, StringComparison.OrdinalIgnoreCase))) &&
            new[] { task.SourceBinding?.VolumeSerial, task.DestinationBinding?.VolumeSerial, task.MonitoredRemovableVolumeSerial }
                .Any(serial => serial is not null && serial.Equals(volume.Serial, StringComparison.OrdinalIgnoreCase)))))
        {
            nextRuns.Remove(task.Id);
            matched = true;
        }
        if (matched) _ = RunDue();
    }

    internal async Task RunDue()
    {
        if (disposed || IsPaused || IsRunning || !canRun()) return;
        var due = tasks().Where(t => t.BackgroundCheckEnabled && !paused.Contains(t.Id) &&
            (!nextRuns.TryGetValue(t.Id, out var next) || next <= DateTimeOffset.UtcNow)).Select(t => t.Id).ToArray();
        if (due.Length == 0) return;
        IsRunning = true;
        activityChanged(true);
        try
        {
            foreach (var id in due)
            {
                var task = tasks().FirstOrDefault(t => t.Id == id && t.BackgroundCheckEnabled);
                if (disposed || IsPaused || task is null || paused.Contains(id) || !canRun()) continue;
                currentTask = id;
                using var cancel = new CancellationTokenSource();
                cancellation = cancel;
                try
                {
                    task = prepareTask is null ? TaskVolumeResolver.Resolve(task, volumes()) : await prepareTask(task);
                    if (cancel.IsCancellationRequested || disposed || IsPaused || paused.Contains(id) || !canRun())
                    { cancellation = null; currentTask = null; continue; }
                }
                catch (Exception error)
                {
                    var recorded = reportWriter?.Record(task, new ManualBackupReport([], 0), "Failed", "Automatic", error) ?? true;
                    nextRuns[id] = DateTimeOffset.UtcNow.AddMinutes(task.BackgroundCheckIntervalMinutes);
                    if (!disposed && !cancel.IsCancellationRequested) completed([new(task, 0, 0, error, recorded)]);
                    cancellation = null;
                    currentTask = null;
                    continue;
                }
                var result = await Task.Run(() =>
                {
                    var report = new ManualBackupReport([], 0);
                    Exception? failure = null;
                    try
                    {
                        if (BackupWorkspace.HasBinding(task)) TaskVolumeResolver.EnsureAvailableAtCurrentPaths(task, volumes());
                        var plan = synchronization.Preview(task, cancel.Token, allowCache: true);
                        if (task.SafetyModeEnabled && task.ConfirmedRunsRemaining > 0)
                            return new BackgroundSyncResult(task, 0, plan.Conflicts, null, NeedsApproval: true);
                        report = synchronization.Synchronize(task, plan, cancel.Token);
                    }
                    catch (BackupExecutionException error) { report = error.Completed; failure = error.InnerException ?? error; }
                    catch (Exception error) { failure = error; }
                    var outcome = failure is OperationCanceledException ? "Cancelled" : failure is null ? "Completed" : report.CopiedFiles > 0 ? "Interrupted" : "Failed";
                    var recorded = reportWriter?.Record(task, report, outcome, "Automatic", failure) ?? true;
                    return new BackgroundSyncResult(task, report.SynchronizedFiles, report.SkippedConflicts, failure, recorded, DeferredFiles: report.DeferredRelativePaths.Count);
                });
                cancellation = null;
                currentTask = null;
                nextRuns[id] = DateTimeOffset.UtcNow.AddMinutes(task.BackgroundCheckIntervalMinutes);
                if (!disposed && !cancel.IsCancellationRequested) completed([result]);
            }
        }
        finally
        {
            cancellation = null;
            currentTask = null;
            IsRunning = false;
            activityChanged(false);
        }
    }
    public void Dispose()
    {
        disposed = true;
        timer.Stop();
        cancellation?.Cancel();
    }
}
