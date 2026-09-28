using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Infrastructure.Storage;

namespace AntonsBackupManager.App;

internal interface IWorkspaceDialogs
{
    bool Confirm(string title, string message);
    string? ChooseFolder(string current);
    void OpenFolder(string path);
    ConflictChoice Compare(BackupTaskDefinition task, PlannedOperation operation);
    void ShowHistory(BackupTaskDefinition task, IReadOnlyList<BackupRunRecord> records);
    void ShowVersions(BackupTaskDefinition task, IReadOnlyList<PreviousVersion> versions, Func<PreviousVersion, string, Task> restore);
}
