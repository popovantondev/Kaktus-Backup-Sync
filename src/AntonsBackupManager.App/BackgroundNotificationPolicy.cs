using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.App;

internal sealed record BackgroundNotificationDecision(
    TrayVisualState State,
    bool NeedsApproval,
    TrayNotification? Notification);

internal sealed class BackgroundNotificationPolicy
{
    private readonly Dictionary<Guid, string> announcedWarnings = [];

    public BackgroundNotificationDecision Evaluate(BackgroundSyncResult result)
    {
        if (result.NeedsApproval)
            return new(TrayVisualState.Attention, true, null);

        if (IsCleanSuccess(result))
        {
            announcedWarnings.Remove(result.Task.Id);
            return new(TrayVisualState.Ready, false, null);
        }

        var messageKey = GetMessageKey(result);
        var fingerprint = $"{messageKey}:{result.Conflicts}:{result.DeferredFiles}";
        if (announcedWarnings.GetValueOrDefault(result.Task.Id) == fingerprint)
            return new(TrayVisualState.Attention, false, null);

        announcedWarnings[result.Task.Id] = fingerprint;
        object[] arguments = result.Error is null && result.ReportSaved
            ? [result.DeferredFiles > 0 ? result.DeferredFiles : result.Conflicts]
            : [];
        return new(TrayVisualState.Attention, false,
            new TrayNotification(result.Task.Name, messageKey, arguments));
    }

    private static bool IsCleanSuccess(BackgroundSyncResult result) =>
        result.Error is null && result.Conflicts == 0 && result.DeferredFiles == 0 && result.ReportSaved;

    private static string GetMessageKey(BackgroundSyncResult result) =>
        !result.ReportSaved ? "ReportWarning" :
        result.Error is { } error ? ErrorPresentation.SummaryKey(error) :
        result.DeferredFiles > 0 ? "TrayDeferred" : "TrayConflicts";
}
