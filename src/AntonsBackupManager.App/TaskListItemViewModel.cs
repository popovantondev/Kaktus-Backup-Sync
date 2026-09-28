using System.ComponentModel;
using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.App;

internal sealed class TaskListItemViewModel(BackupTaskDefinition task) : INotifyPropertyChanged
{
    public BackupTaskDefinition Task { get; } = task;
    public string Name => Task.Name;
    public bool BackgroundCheckEnabled => Task.BackgroundCheckEnabled;
    public string State { get; private set; } = "";
    public string Timing { get; private set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh(BackupRunRecord? last, DateTimeOffset? next, bool allPaused, bool running)
    {
        State = UiText.Get(allPaused || !Task.BackgroundCheckEnabled ? "Paused" : running ? "Copying" :
            Task.SafetyModeEnabled && Task.ConfirmedRunsRemaining > 0 ? "ApprovalShort" :
            last is { Outcome: "Failed" or "Interrupted" or "RetryPending" } || last?.SkippedConflicts > 0 ? "TrayAttention" : "Automatic");
        var lastText = last is null ? UiText.Get("NeverRun") : last.CompletedAtUtc.LocalDateTime.ToString("g", System.Globalization.CultureInfo.GetCultureInfo(UiText.Language));
        var nextText = allPaused || !Task.BackgroundCheckEnabled ? "—" : next is null || next <= DateTimeOffset.UtcNow
            ? UiText.Get("Soon") : next.Value.LocalDateTime.ToString("t", System.Globalization.CultureInfo.GetCultureInfo(UiText.Language));
        Timing = UiText.Get("TaskTiming", lastText, Task.BackgroundCheckIntervalMinutes, nextText);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Timing)));
    }
}
