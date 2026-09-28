using System.Windows;
using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Infrastructure.Storage;

namespace AntonsBackupManager.App;

internal sealed class WorkspaceDialogs(Window owner, IFolderActions folders) : IWorkspaceDialogs
{
    public bool Confirm(string title, string message) => ConfirmDialog.Ask(owner, title, message);
    public string? ChooseFolder(string current) => folders.Choose(owner, current);
    public void OpenFolder(string path) => folders.Open(path);
    public ConflictChoice Compare(BackupTaskDefinition task, PlannedOperation operation)
    {
        var dialog = new ConflictDetailsWindow(owner, task, operation); dialog.ShowDialog(); return dialog.Choice;
    }
    public void ShowHistory(BackupTaskDefinition task, IReadOnlyList<BackupRunRecord> records) => new HistoryWindow(owner, task, records).ShowDialog();
    public void ShowVersions(BackupTaskDefinition task, IReadOnlyList<PreviousVersion> versions, Func<PreviousVersion, string, Task> restore) =>
        new VersionsWindow(owner, versions, restore).ShowDialog();
}
