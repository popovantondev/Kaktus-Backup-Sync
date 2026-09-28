using AntonsBackupManager.Infrastructure.Storage;
using System.Windows.Input;

namespace AntonsBackupManager.App;

internal sealed record VersionRow(PreviousVersion Version)
{
    public string Label => $"{Version.SavedAtUtc.LocalDateTime:g}   {Version.RelativePath}";
}

internal sealed class VersionsViewModel : ObservableModel
{
    private VersionRow? selected;
    private bool busy;
    private string statusKey;
    private readonly AsyncCommand export;
    public IReadOnlyList<VersionRow> Items { get; }
    public string Status => UiText.Get(statusKey);
    public bool CanSelect => !busy;
    public VersionRow? Selected { get => selected; set { Set(ref selected, value); export.Refresh(); } }
    public ICommand ExportCommand => export;

    public VersionsViewModel(IReadOnlyList<PreviousVersion> versions, Func<string, string?> choose,
        Func<PreviousVersion, string, Task> restore)
    {
        Items = versions.Select(version => new VersionRow(version)).ToArray();
        statusKey = versions.Count == 0 ? "NoVersions" : "Versions";
        export = new AsyncCommand(async () =>
        {
            if (Selected is not { } row) return;
            var path = choose(row.Version.RelativePath); if (path is null) return;
            busy = true; Changed(null);
            try { await restore(row.Version, path); statusKey = "Exported"; }
            finally { busy = false; Changed(null); }
        }, () => Selected is not null && !busy, error => { statusKey = ErrorPresentation.SummaryKey(error); Changed(null); });
    }
}
