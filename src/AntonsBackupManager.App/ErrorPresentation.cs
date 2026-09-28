using System.Globalization;
using System.IO;
using AntonsBackupManager.Infrastructure.Scanning;
using AntonsBackupManager.Infrastructure.Storage;
using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.App;

internal static class ErrorPresentation
{
    public static string SummaryKey(Exception error) => error switch
    {
        StateDirectoryOverlapException => "StateOverlap",
        RemovableVolumeException volume => volume.Problem switch
        {
            VolumeProblem.Ambiguous => "VolumeAmbiguous",
            VolumeProblem.NeedsRebind => "VolumeRebind",
            _ => "VolumeMissing",
        },
        UnauthorizedAccessException => "Denied",
        UnsupportedLinkException => "LinkBlocked",
        DirectoryNotFoundException or FileNotFoundException => "Missing",
        ArgumentException => "Invalid",
        IOException => "IoError",
        _ => "Error",
    };

    public static string Details(Exception error) => error switch
    {
        StateDirectoryOverlapException overlap => UiText.Get("StateOverlapDetails",
            UiText.Get(overlap.IsSource ? "Source" : "Destination"), overlap.SelectedDirectory, overlap.StateDirectory),
        UnsupportedLinkException link => UiText.Get("UnsupportedLink", link.EntryPath),
        // OS and library messages can use a different language or contain internal data.
        // Give localized guidance and a stable diagnostic code instead of raw text.
        _ => UiText.Get(SummaryKey(error)) + "\n\n" + UiText.Get("ErrorCode",
            error.GetType().Name, error.HResult.ToString("X8", CultureInfo.InvariantCulture)),
    };
}
