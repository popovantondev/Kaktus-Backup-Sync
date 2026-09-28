using System.IO;
using System.Diagnostics;
using AntonsBackupManager.App;
using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Infrastructure.Storage;

internal static class WorkspaceChecks
{
    public static async Task<bool> Run()
    {
        BackgroundNotificationChecks.Run();
        using var fixture = new Fixture();
        await CheckCommands(fixture);
        await CheckVolumeArrival(fixture);
        await CheckPauseDuringPreparation(fixture);
        return true;
    }

    private static async Task CheckCommands(Fixture f)
    {
        var workspace = new BackupWorkspace(f.Folder("manual-state"), () => []);
        var dialogs = new Dialogs();
        using var model = new MainViewModel(workspace, dialogs) { IsVisible = true };
        model.Editor.Name = "Windowless scenario";
        model.Editor.Source = f.Folder("manual-source"); model.Editor.Destination = f.Folder("manual-target");
        File.WriteAllText(Path.Combine(model.Editor.Source, "sample.txt"), "test content");
        model.PreviewCommand.Execute(null);
        await Until(() => model.CanEdit);
        Require(workspace.Tasks.Count == 1 && model.Preview.Plan?.CopyNewFiles == 1, "Preview did not persist its task.");
        Require(!File.Exists(Path.Combine(model.Editor.Destination, "sample.txt")), "Preview wrote a destination file.");
        model.SynchronizeCommand.Execute(null);
        await Until(() => model.CanEdit);
        Require(dialogs.Confirmations == 1 && File.ReadAllText(Path.Combine(model.Editor.Destination, "sample.txt")) == "test content", "Command did not confirm and synchronize.");
        Require(workspace.Tasks[0].ConfirmedRunsRemaining == 4, "A complete run did not advance safety state.");
        model.Editor.Name = "Unsaved edited name";
        model.PauseTaskCommand.Execute(null);
        await Until(() => model.CanEdit);
        Require(model.Editor.Name == "Unsaved edited name" && workspace.Tasks[0].Name == "Windowless scenario" && workspace.Tasks[0].BackgroundCheckEnabled,
            "Saving the schedule discarded or prematurely persisted a dirty draft.");
        model.Editor.Destination = model.Editor.Source;
        model.PreviewCommand.Execute(null);
        await Until(() => model.CanEdit);
        Require(model.Status.HasError && workspace.Tasks[0].DestinationDirectory != model.Editor.Source, "Invalid preview changed a saved task.");
        await model.StopForExit();
        var invalidState = f.Folder("invalid-state"); File.WriteAllText(Path.Combine(invalidState, "tasks.json"), "{invalid}");
        using var blocked = new MainViewModel(new BackupWorkspace(invalidState, () => []), dialogs);
        blocked.PreviewCommand.Execute(null);
        Require(File.ReadAllText(Path.Combine(invalidState, "tasks.json")) == "{invalid}", "Corrupt catalog was overwritten.");
        Console.WriteLine("PASS windowless commands, confirmation, draft preservation and invalid-state protection");
    }

    private static async Task CheckVolumeArrival(Fixture f)
    {
        // Folder roots simulate volumes. No host drive letters or real devices change.
        var oldRoot = f.Folder("old-volume"); var newRoot = f.Folder("new-volume");
        var source = Path.Combine(newRoot, "files"); Directory.CreateDirectory(source);
        var target = f.Folder("usb-target"); var state = f.Folder("usb-state");
        var task = new BackupTaskDefinition(Guid.NewGuid(), "Bound test task", Path.Combine(oldRoot, "files"), target, 0,
            BackgroundCheckEnabled: true, SourceBinding: new("A1B2C3D4", "files"));
        new JsonBackupTaskStore().Save(Path.Combine(state, "tasks.json"), [task]);
        IReadOnlyList<AvailableVolume> volumes = [new(newRoot, "00112233")];
        var workspace = new BackupWorkspace(state, () => volumes);
        var results = new List<BackgroundSyncResult>();
        using var poller = new TaskPoller(workspace.Synchronization, () => workspace.Tasks, items => results.AddRange(items), _ => { },
            reportWriter: workspace.Reports, prepareTask: workspace.PrepareAutomaticTaskAsync, volumes: () => volumes);
        File.WriteAllText(Path.Combine(source, "sample.txt"), "new letter");
        poller.CheckTasksForConnectedRemovableDrive([newRoot]);
        Require(!poller.IsRunning && results.Count == 0, "A different device triggered the task.");
        volumes = [new(newRoot, "A1B2C3D4")];
        poller.CheckTasksForConnectedRemovableDrive([newRoot]);
        await Until(() => !poller.IsRunning);
        Require(results.Count == 1 && results[0].Error is null && File.ReadAllText(Path.Combine(target, "sample.txt")) == "new letter", "Arrival at a changed root did not synchronize.");
        Require(new JsonBackupTaskStore().Load(Path.Combine(state, "tasks.json"))[0].SourceDirectory == source, "Resolved location was not persisted.");
        File.WriteAllText(Path.Combine(source, "second.txt"), "later");
        var preview = await workspace.PreviewAsync(workspace.Tasks[0], null, CancellationToken.None);
        volumes = [];
        var run = await workspace.SynchronizeAsync(preview.Task, preview.Plan, CancellationToken.None);
        Require(run.Failure is RemovableVolumeException && !File.Exists(Path.Combine(target, "second.txt")), "Missing bound device allowed a reviewed write.");
        Console.WriteLine("PASS arrival at a new volume root, wrong-device rejection, persisted remap and pre-write identity check");
    }

    private static async Task CheckPauseDuringPreparation(Fixture f)
    {
        var task = new BackupTaskDefinition(Guid.NewGuid(), "Pause during prepare", f.Folder("pause-source"), f.Folder("pause-target"), 0, BackgroundCheckEnabled: true);
        File.WriteAllText(Path.Combine(task.SourceDirectory, "sample.txt"), "wait");
        var gate = new TaskCompletionSource<BackupTaskDefinition>();
        using var poller = new TaskPoller(new SynchronizationService(f.Folder("pause-state")), () => [task], _ => { }, _ => { },
            prepareTask: _ => gate.Task, volumes: () => []);
        var run = poller.RunDue();
        poller.Pause(task.Id); gate.SetResult(task); await run;
        poller.CancelCurrent();
        Require(!File.Exists(Path.Combine(task.DestinationDirectory, "sample.txt")), "Pause during preparation still copied a file.");
        Console.WriteLine("PASS pause cancels preparation without writes or disposed-token errors");
    }

    private static async Task Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("Workflow did not finish."); await Task.Delay(15); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Dialogs : IWorkspaceDialogs
    {
        public int Confirmations { get; private set; }
        public bool Confirm(string title, string message) { Confirmations++; return true; }
        public string? ChooseFolder(string current) => null;
        public void OpenFolder(string path) { }
        public ConflictChoice Compare(BackupTaskDefinition task, PlannedOperation operation) => ConflictChoice.Skip;
        public void ShowHistory(BackupTaskDefinition task, IReadOnlyList<BackupRunRecord> records) { }
        public void ShowVersions(BackupTaskDefinition task, IReadOnlyList<PreviousVersion> versions, Func<PreviousVersion, string, Task> restore) { }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "KaktusWorkspaceCheck", Guid.NewGuid().ToString("N"));
        public string Folder(string name) { var path = Path.Combine(root, name); Directory.CreateDirectory(path); return path; }
        public void Dispose() => Directory.Delete(root, true);
    }
}
