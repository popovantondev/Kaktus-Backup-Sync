using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Core.Tasks;
using System.Windows.Input;

namespace AntonsBackupManager.App;

internal sealed class MainViewModel : ObservableModel, IDisposable
{
    private readonly BackupWorkspace workspace;
    private readonly IWorkspaceDialogs dialogs;
    private readonly TaskPoller poller;
    private readonly BackgroundNotificationPolicy backgroundNotificationPolicy = new();
    private readonly List<AsyncCommand> commands = [];
    private readonly Dictionary<Guid, BackupRunRecord?> lastReports = [];
    private BackupTaskDefinition? selectedTask;
    private bool loadingEditor, refreshingTasks, busy, savingSchedule, dialogOpen, editorDirty, manualRun, backgroundRun;
    private CancellationTokenSource? cancellation;
    private TrayVisualState lastRunState;
    public TaskEditorViewModel Editor { get; } = new();
    public PreviewViewModel Preview { get; } = new();
    public OperationStatusViewModel Status { get; } = new();
    public IReadOnlyList<TaskListItemViewModel> Tasks { get; private set; } = [];
    public bool IsVisible { get; set; }
    public bool AllPaused { get; private set; }
    public bool CanEdit => !busy && !savingSchedule && !dialogOpen;
    public bool IsBusy => busy;
    public bool ShowWelcome => !Preview.HasPlan && !Status.HasError;
    public bool CanManageTask => CanEdit && workspace.StoreAvailable && SelectedTask is not null;
    public bool CanSynchronize => CanManageTask && !AllPaused && !backgroundRun && Preview.Plan is { } plan &&
        Preview.Task?.Id == SelectedTask?.Id && plan.CopyNewFiles + plan.Updates + plan.UnchangedFiles + plan.KeptFiles + plan.Renames + plan.DeferredFiles > 0;
    public string PauseAllText => UiText.Get(AllPaused ? "ResumeAll" : "PauseAll");
    public string PauseTaskText => UiText.Get(SelectedTask?.BackgroundCheckEnabled == true ? "PauseTask" : "ResumeTask");
    public string SafetyText => UiText.Get("SafetyCount", Editor.SafetyRuns);
    public string UsbText => UiText.Get(Editor.SourceBinding is not null || Editor.DestinationBinding is not null || Editor.LegacySerial is not null ? "UsbBound" : "UsbNotBound");
    public BackupTaskDefinition? SelectedTask
    {
        get => selectedTask;
        set
        {
            if (!Set(ref selectedTask, value) || refreshingTasks) return;
            LoadEditor(value); Preview.Clear(); Status.SetStatus("Ready"); Refresh();
        }
    }
    public int LanguageIndex
    {
        get => UiText.Language == "ru" ? 2 : UiText.Language == "en" ? 1 : 0;
        set
        {
            var previousName = UiText.Get("NewName");
            UiText.Language = value == 2 ? "ru" : value == 1 ? "en" : "de";
            if (SelectedTask is null && Editor.Name == previousName)
            { loadingEditor = true; Editor.Name = UiText.Get("NewName"); loadingEditor = false; }
            try { workspace.Preferences.SaveLanguage(UiText.Language); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Status.SetStatus("LanguageWarning"); }
            Refresh();
        }
    }

    public ICommand NewCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand SynchronizeCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand PauseTaskCommand { get; }
    public ICommand PauseAllCommand { get; }
    public ICommand BrowseSourceCommand { get; }
    public ICommand BrowseDestinationCommand { get; }
    public ICommand OpenSourceCommand { get; }
    public ICommand OpenDestinationCommand { get; }
    public ICommand BindUsbCommand { get; }
    public ICommand UnbindUsbCommand { get; }
    public ICommand CompareCommand { get; }
    public ICommand ResolveCommand { get; }
    public ICommand HistoryCommand { get; }
    public ICommand VersionsCommand { get; }
    public event EventHandler<TrayNotification>? NotificationRequested;
    public event EventHandler<TrayVisualState>? TrayStateRequested;
    public event EventHandler? EditorReset;

    public MainViewModel(BackupWorkspace workspace, IWorkspaceDialogs dialogs)
    {
        this.workspace = workspace; this.dialogs = dialogs;
        UiText.Language = workspace.Preferences.ReadLanguage();
        Editor.Load(null);
        poller = new TaskPoller(workspace.Synchronization, () => workspace.Tasks, BackgroundCompleted, BackgroundActivity,
            () => CanEdit && workspace.StoreAvailable && !(IsVisible && (editorDirty || Preview.HasPlan)), workspace.Reports,
            workspace.PrepareAutomaticTaskAsync, workspace.AvailableVolumes);
        NewCommand = Action(() => { ResetEditor(); EditorReset?.Invoke(this, EventArgs.Empty); }, () => CanEdit);
        PreviewCommand = Command(CreatePreviewAsync, () => CanEdit);
        SynchronizeCommand = Command(SynchronizeAsync, () => CanSynchronize);
        CancelCommand = Action(() => cancellation?.Cancel(), () => busy);
        DeleteCommand = Command(DeleteAsync, () => CanManageTask);
        PauseTaskCommand = Command(() => SaveScheduleAsync(!SelectedTask!.BackgroundCheckEnabled, Editor.IntervalMinutes), () => CanManageTask);
        PauseAllCommand = Action(ToggleAllPause, () => !savingSchedule && !dialogOpen);
        BrowseSourceCommand = Action(() => Browse(true), () => CanEdit);
        BrowseDestinationCommand = Action(() => Browse(false), () => CanEdit);
        OpenSourceCommand = Action(() => dialogs.OpenFolder(TaskEditorViewModel.Normalize(Editor.Source)), () => CanEdit);
        OpenDestinationCommand = Action(() => dialogs.OpenFolder(TaskEditorViewModel.Normalize(Editor.Destination)), () => CanEdit);
        BindUsbCommand = Action(BindUsb, () => CanEdit);
        UnbindUsbCommand = Action(() => { Editor.SourceBinding = null; Editor.DestinationBinding = null; Editor.LegacySerial = null; MarkEdited(); }, () => CanEdit);
        CompareCommand = Action(Compare, () => CanManageTask && Preview.Selection.FirstOrDefault()?.Operation.Kind == PlannedOperationKind.Conflict);
        ResolveCommand = Action(Resolve, () => CanEdit && SelectedConflicts().Length > 0);
        HistoryCommand = Command(ShowHistoryAsync, () => CanManageTask);
        VersionsCommand = Command(ShowVersionsAsync, () => CanManageTask);
        Editor.PropertyChanged += EditorChanged;
        Status.PropertyChanged += (_, _) => Refresh();
        Preview.PropertyChanged += (_, _) => Refresh();
        workspace.TasksChanged += TasksChanged;
        UiText.LanguageChanged += LanguageChanged;
        AllPaused = workspace.Preferences.IsPaused;
        poller.SetPaused(AllPaused);
        RefreshTasks();
        LanguageIndex = LanguageIndex;
        if (!workspace.StoreAvailable) Status.SetStatus("StoreBlocked");
        poller.Start();
    }

    private AsyncCommand Command(Func<Task> execute, Func<bool> allowed)
    {
        var command = new AsyncCommand(execute, allowed, ShowError); commands.Add(command); return command;
    }
    private AsyncCommand Action(Action execute, Func<bool> allowed) => Command(() => { execute(); return Task.CompletedTask; }, allowed);
    private void Refresh() { Changed(null); foreach (var command in commands) command.Refresh(); }
    private void ShowError(Exception error) { lastRunState = TrayVisualState.Attention; Preview.Clear(); Status.ShowError(error); PublishTrayState(); }
    private void LanguageChanged(object? sender, EventArgs e) { Status.Refresh(); Preview.Refresh(); RefreshTiming(); Refresh(); }
    private void TasksChanged(object? sender, EventArgs e) => RefreshTasks();

    private void LoadEditor(BackupTaskDefinition? task)
    {
        loadingEditor = true;
        try { Editor.Load(task); }
        finally { loadingEditor = false; editorDirty = false; }
    }
    private void ResetEditor()
    {
        SelectedTask = null; LoadEditor(null); editorDirty = true;
        Preview.Clear(); Status.SetStatus("Ready"); Refresh();
    }
    private async void EditorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (loadingEditor) return;
        if (args.PropertyName is nameof(TaskEditorViewModel.Automatic) or nameof(TaskEditorViewModel.IntervalIndex))
        {
            Preview.Clear();
            if (SelectedTask is null) editorDirty = true;
            else await SaveScheduleAsync(Editor.Automatic, Editor.IntervalMinutes);
            Refresh();
        }
        else MarkEdited();
    }
    private void MarkEdited() { editorDirty = true; Preview.Clear(); Status.SetStatus("Changed"); Refresh(); }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (!CanEdit) return;
        busy = true;
        using var cancel = new CancellationTokenSource(); cancellation = cancel;
        Refresh(); PublishTrayState();
        try
        {
            if (poller.IsRunning)
            {
                Status.SetStatus("WaitingForFile"); poller.CancelCurrent();
                while (poller.IsRunning) await Task.Delay(50, cancel.Token);
            }
            await action(cancel.Token);
        }
        catch (OperationCanceledException) { Preview.Clear(); Status.SetStatus("Cancelled"); }
        catch (Exception error) { ShowError(error); }
        finally { busy = false; manualRun = false; cancellation = null; Refresh(); PublishTrayState(); }
    }

    private Task CreatePreviewAsync()
    {
        if (!workspace.StoreAvailable) { Status.SetStatus("StoreBlocked"); return Task.CompletedTask; }
        return RunAsync(async token =>
        {
            Preview.Clear(); Status.SetStatus("Running");
            var watch = System.Diagnostics.Stopwatch.StartNew(); long last = -250;
            var progress = new Progress<string>(path =>
            {
                if (busy && !token.IsCancellationRequested && watch.ElapsedMilliseconds - last >= 250)
                { last = watch.ElapsedMilliseconds; Status.SetStatus("ScanningFile", Path.GetFileName(path)); }
            });
            var result = await workspace.PreviewAsync(Editor.Build(SelectedTask), progress, token);
            SelectedTask = result.Task; LoadEditor(result.Task);
            if (result.Task.BackgroundCheckEnabled) poller.Resume(result.Task.Id); else poller.Pause(result.Task.Id);
            Preview.SetPlan(result.Task, result.Plan);
            if (result.Plan.Conflicts > 0) Status.SetStatus("ConflictHelp", result.Plan.Conflicts);
            else Status.SetStatus(result.Plan.Operations.Count == 0 ? "EmptySaved" : "NextSync");
        });
    }

    private async Task SynchronizeAsync()
    {
        if (!CanSynchronize || Preview.Task is not { } task || Preview.Plan is not { } plan) return;
        if (!Confirm(UiText.Get("ConfirmTitle"), UiText.Get("SyncConfirm", plan.CopyNewFiles, plan.Updates, task.DestinationDirectory) +
            "\n" + UiText.Get("SyncExtra", plan.Renames, plan.DeferredFiles))) return;
        await RunAsync(async token =>
        {
            manualRun = true; Status.SetStatus("Copying"); PublishTrayState();
            var result = await workspace.SynchronizeAsync(task, plan, token);
            Preview.Clear();
            if (result.Failure is { } error)
            {
                lastRunState = TrayVisualState.Attention;
                if (result.Report.SynchronizedFiles > 0) Status.SetStatus("Partial", result.Report.SynchronizedFiles, new UiText.Reference(ErrorPresentation.SummaryKey(error)));
                else if (error is OperationCanceledException) Status.SetStatus("Cancelled");
                else Status.ShowError(error);
            }
            else
            {
                var report = result.Report;
                lastRunState = report.SkippedConflicts == 0 && report.DeferredRelativePaths.Count == 0 ? TrayVisualState.Ready : TrayVisualState.Attention;
                if (!result.ReportSaved || !result.TaskSaved) Status.SetStatus("ReportWarning");
                else Status.SetStatus("SyncDone", report.CopiedFiles, report.RenamedRelativePaths.Count, report.SkippedConflicts, report.DeferredRelativePaths.Count);
            }
            RefreshTasks();
        });
    }

    private async Task SaveScheduleAsync(bool enabled, int interval)
    {
        if (!CanManageTask || SelectedTask is not { } task) return;
        savingSchedule = true; if (!enabled) poller.Pause(task.Id); Refresh();
        try
        {
            // Finish an in-flight volume remap before changing the saved schedule.
            poller.CancelCurrent();
            while (poller.IsRunning) await Task.Delay(50);
            task = workspace.Tasks.First(current => current.Id == task.Id);
            var updated = task with { BackgroundCheckEnabled = enabled, BackgroundCheckIntervalMinutes = interval };
            await workspace.UpdateTaskAsync(updated);
            loadingEditor = true; Editor.Automatic = enabled; Editor.IntervalIndex = Math.Max(0, Array.IndexOf(TaskEditorViewModel.Intervals, interval)); loadingEditor = false;
            if (enabled) poller.Resume(task.Id);
            lastRunState = TrayVisualState.Idle; Preview.Clear(); Status.SetStatus(enabled ? "TaskResumed" : "TaskPaused");
        }
        catch (Exception error)
        {
            if (task.BackgroundCheckEnabled) poller.Resume(task.Id);
            loadingEditor = true; Editor.Automatic = task.BackgroundCheckEnabled; loadingEditor = false; ShowError(error);
        }
        finally { savingSchedule = false; Refresh(); PublishTrayState(); }
    }

    private async Task DeleteAsync()
    {
        if (SelectedTask is not { } task || !Confirm(UiText.Get("Delete"), UiText.Get("RemoveConfirm", task.Name))) return;
        await RunAsync(async _ => { await workspace.RemoveTaskAsync(task.Id); poller.ResetSchedule(); ResetEditor(); Status.SetStatus("Removed"); });
    }
    private void ToggleAllPause()
    {
        workspace.Preferences.SavePaused(!AllPaused); AllPaused = !AllPaused; poller.SetPaused(AllPaused);
        if (AllPaused) cancellation?.Cancel(); Status.SetStatus(AllPaused ? "AllPaused" : "AllResumed"); Refresh(); PublishTrayState();
    }
    private bool Confirm(string title, string text)
    {
        dialogOpen = true; Refresh();
        try { return dialogs.Confirm(title, text); }
        finally { dialogOpen = false; Refresh(); }
    }
    private void Browse(bool source)
    {
        dialogOpen = true;
        try { var chosen = dialogs.ChooseFolder(source ? Editor.Source : Editor.Destination); if (chosen is not null) { if (source) Editor.Source = chosen; else Editor.Destination = chosen; } }
        finally { dialogOpen = false; Refresh(); }
    }
    private void BindUsb()
    {
        var volumes = workspace.AvailableVolumes();
        var source = TaskVolumeResolver.Capture(Editor.Source, volumes);
        var target = TaskVolumeResolver.Capture(Editor.Destination, volumes);
        if (source is null && target is null) { Status.SetStatus("UsbUnavailable"); return; }
        Editor.SourceBinding = source; Editor.DestinationBinding = target; Editor.LegacySerial = null; MarkEdited();
    }
    public void SetSelection(IReadOnlyList<PlanRow> rows) { Preview.Selection = rows; Refresh(); }
    private string[] SelectedConflicts() => Preview.Selection.Where(row => row.Operation.Kind == PlannedOperationKind.Conflict && row.Operation.ExpectedSource is not null).Select(row => row.Operation.RelativePath).ToArray();
    private void Resolve()
    {
        if (Preview.Plan is not { } plan) return;
        var paths = SelectedConflicts();
        if (Confirm(UiText.Get("ResolveConflicts"), UiText.Get("ResolveConfirm", string.Join("\n", paths))))
        { Preview.SetPlan(Preview.Task, ConflictResolution.UseSource(plan, paths)); Status.SetStatus("NextSync"); }
    }
    private void Compare()
    {
        if (Preview.Task is not { } task || Preview.Plan is not { } plan || Preview.Selection.FirstOrDefault() is not { } row) return;
        dialogOpen = true;
        try
        {
            var choice = dialogs.Compare(task, row.Operation);
            if (choice == ConflictChoice.Skip) return;
            Preview.SetPlan(task, choice == ConflictChoice.UseSource ? ConflictResolution.UseSource(plan, [row.Operation.RelativePath]) : ConflictResolution.KeepDestination(plan, [row.Operation.RelativePath]));
            Status.SetStatus("DecisionHint");
        }
        finally { dialogOpen = false; Refresh(); }
    }
    private async Task ShowHistoryAsync()
    {
        if (SelectedTask is not { } task) return;
        dialogOpen = true; Refresh();
        try { dialogs.ShowHistory(task, await workspace.ReadHistoryAsync(task.Id)); }
        finally { dialogOpen = false; Refresh(); }
    }
    private async Task ShowVersionsAsync()
    {
        if (SelectedTask is not { } task) return;
        dialogOpen = true; Refresh();
        try { dialogs.ShowVersions(task, await workspace.ReadVersionsAsync(task), (version, path) => workspace.ExportVersionAsync(task, version, path)); }
        finally { dialogOpen = false; Refresh(); }
    }

    private void RefreshTasks()
    {
        var id = SelectedTask?.Id; refreshingTasks = true;
        try
        {
            Tasks = workspace.Tasks.Select(task => new TaskListItemViewModel(task)).ToArray();
            foreach (var task in workspace.Tasks) lastReports[task.Id] = workspace.Reports.ReadLatest(task.Id);
            Changed(nameof(Tasks)); selectedTask = workspace.Tasks.FirstOrDefault(task => task.Id == id); Changed(nameof(SelectedTask));
        }
        finally { refreshingTasks = false; }
        if (!editorDirty && SelectedTask is not null) LoadEditor(SelectedTask);
        RefreshTiming(); Refresh();
    }
    public void RefreshTiming()
    {
        foreach (var item in Tasks) item.Refresh(lastReports.GetValueOrDefault(item.Task.Id), poller.NextRun(item.Task.Id), AllPaused, poller.CurrentTaskId == item.Task.Id);
    }
    private void BackgroundActivity(bool running) { backgroundRun = running; if (running) Preview.Clear(); Refresh(); PublishTrayState(); }
    private void BackgroundCompleted(IReadOnlyList<BackgroundSyncResult> results)
    {
        foreach (var result in results)
        {
            lastReports[result.Task.Id] = workspace.Reports.ReadLatest(result.Task.Id);
            var decision = backgroundNotificationPolicy.Evaluate(result);
            lastRunState = decision.State;
            if (decision.NeedsApproval)
            { if (SelectedTask?.Id == result.Task.Id) Status.SetStatus("NeedsApproval", result.Task.ConfirmedRunsRemaining); continue; }
            if (decision.Notification is { } notification)
                NotificationRequested?.Invoke(this, notification);
        }
        RefreshTiming();
    }
    public void PublishTrayState()
    {
        var attention = workspace.Tasks.Where(task => task.BackgroundCheckEnabled).Any(task =>
            task.SafetyModeEnabled && task.ConfirmedRunsRemaining > 0 || lastReports.GetValueOrDefault(task.Id) is { Outcome: "Failed" or "Interrupted" or "RetryPending" } || lastReports.GetValueOrDefault(task.Id)?.SkippedConflicts > 0);
        var state = AllPaused && !manualRun && !backgroundRun ? TrayVisualState.Paused : manualRun || backgroundRun ? TrayVisualState.Synchronizing :
            attention ? TrayVisualState.Attention : lastRunState is TrayVisualState.Ready or TrayVisualState.Attention ? lastRunState :
            workspace.Tasks.Any(task => task.BackgroundCheckEnabled) ? TrayVisualState.Idle : TrayVisualState.Paused;
        TrayStateRequested?.Invoke(this, state);
    }
    public Task PollBackgroundNow() => poller.RunDue();
    public void DeviceArrived(IReadOnlyList<string> roots) => poller.CheckTasksForConnectedRemovableDrive(roots);
    public async Task StopForExit()
    {
        poller.Dispose(); cancellation?.Cancel();
        while (busy || savingSchedule || poller.IsRunning) await Task.Delay(50);
    }
    public void Dispose()
    {
        UiText.LanguageChanged -= LanguageChanged; workspace.TasksChanged -= TasksChanged; Editor.PropertyChanged -= EditorChanged;
        poller.Dispose(); cancellation?.Cancel();
    }
}
