using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Core.Safety;

namespace AntonsBackupManager.App;

internal sealed class TaskEditorViewModel : INotifyPropertyChanged
{
    private string name = "";
    private string source = "";
    private string destination = "";
    private string versionLimit = "7";
    private bool safetyEnabled = true;
    private int safetyRuns = SafetyRunPolicy.DefaultConfirmedRuns;
    private bool automatic;
    private int intervalIndex;
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Name { get => name; set => Set(ref name, value); }
    public string Source { get => source; set => Set(ref source, value); }
    public string Destination { get => destination; set => Set(ref destination, value); }
    public string VersionLimit { get => versionLimit; set => Set(ref versionLimit, value); }
    public bool SafetyEnabled { get => safetyEnabled; set => Set(ref safetyEnabled, value); }
    public int SafetyRuns { get => safetyRuns; set => Set(ref safetyRuns, value); }
    public bool Automatic { get => automatic; set => Set(ref automatic, value); }
    public int IntervalIndex { get => intervalIndex; set => Set(ref intervalIndex, value); }
    public static readonly int[] Intervals = [1, 5, 15, 30, 60];
    public int IntervalMinutes => Intervals[Math.Clamp(IntervalIndex, 0, Intervals.Length - 1)];
    public RemovableFolderBinding? SourceBinding { get; set; }
    public RemovableFolderBinding? DestinationBinding { get; set; }
    public string? LegacySerial { get; set; }
    public ICommand ResetSafetyCommand { get; }

    public TaskEditorViewModel() => ResetSafetyCommand = new RelayCommand(() => SafetyRuns = SafetyRunPolicy.DefaultConfirmedRuns);

    public BackupTaskDefinition Build(BackupTaskDefinition? selected)
    {
        var normalizedSource = Normalize(Source);
        var normalizedDestination = Normalize(Destination);
        var changed = selected is null || !SamePath(normalizedSource, selected.SourceDirectory) || !SamePath(normalizedDestination, selected.DestinationDirectory);
        var task = new BackupTaskDefinition(selected?.Id ?? Guid.NewGuid(), Name.Trim(), normalizedSource, normalizedDestination,
            changed ? SafetyRunPolicy.DefaultConfirmedRuns : SafetyRuns,
            Automatic, IntervalMinutes, LegacySerial,
            int.TryParse(VersionLimit, out var limit) ? limit : throw new ArgumentException(UiText.Get("VersionLimit")),
            SafetyEnabled, selected?.FullCheckIntervalMinutes ?? 60, SourceBinding, DestinationBinding);
        BackupTaskValidator.Validate(task);
        return task;
    }

    public void Load(BackupTaskDefinition? task)
    {
        Name = task?.Name ?? UiText.Get("NewName"); Source = task?.SourceDirectory ?? ""; Destination = task?.DestinationDirectory ?? "";
        SafetyEnabled = task?.SafetyModeEnabled ?? true; SafetyRuns = task?.ConfirmedRunsRemaining ?? 5;
        VersionLimit = (task?.VersionLimit ?? 7).ToString(); Automatic = task?.BackgroundCheckEnabled ?? false;
        IntervalIndex = Math.Max(0, Array.IndexOf(Intervals, task?.BackgroundCheckIntervalMinutes ?? 1));
        SourceBinding = task?.SourceBinding; DestinationBinding = task?.DestinationBinding; LegacySerial = task?.MonitoredRemovableVolumeSerial;
    }

    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return TaskPathRules.Normalize(path.Trim().Trim('"'));
    }
    public static bool SamePath(string first, string second) => string.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase);

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

internal sealed class RelayCommand(Action action) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
}
