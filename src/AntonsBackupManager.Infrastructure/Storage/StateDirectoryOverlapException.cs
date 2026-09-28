namespace AntonsBackupManager.Infrastructure.Storage;

// Keep the reason and paths separate from the language used by the interface.
public sealed class StateDirectoryOverlapException(string selectedDirectory, string stateDirectory, bool isSource)
    : ArgumentException("The selected folder overlaps synchronization state.")
{
    public string SelectedDirectory { get; } = selectedDirectory;
    public string StateDirectory { get; } = stateDirectory;
    public bool IsSource { get; } = isSource;
}
