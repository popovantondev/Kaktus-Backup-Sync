using System.IO;
using AntonsBackupManager.App;
using AntonsBackupManager.Core.Tasks;

internal static class BackgroundNotificationChecks
{
    public static void Run()
    {
        CheckDeduplicationAndRecovery();
        CheckWarningCategories();
        Console.WriteLine("PASS background notification policy, safety state and warning deduplication");
    }

    private static void CheckDeduplicationAndRecovery()
    {
        var task = CreateTask();
        var policy = new BackgroundNotificationPolicy();
        var conflict = new BackgroundSyncResult(task, 0, 2, null);

        var first = policy.Evaluate(conflict);
        var firstNotification = first.Notification ?? throw new InvalidOperationException("A conflict warning was not announced.");
        Require(first.State == TrayVisualState.Attention && firstNotification.MessageKey == "TrayConflicts",
            "A conflict warning was not announced.");
        Require(firstNotification.Arguments.Single() is int count && count == 2,
            "Conflict count was not passed to its notification.");
        Require(policy.Evaluate(conflict).Notification is null, "An identical warning was announced twice.");

        var approval = policy.Evaluate(new BackgroundSyncResult(task, 0, 0, null, NeedsApproval: true));
        Require(approval.NeedsApproval && approval.State == TrayVisualState.Attention && approval.Notification is null,
            "A task awaiting safety approval produced the wrong decision.");
        Require(policy.Evaluate(conflict).Notification is null,
            "A safety approval check unexpectedly reset warning deduplication.");

        var recovered = policy.Evaluate(new BackgroundSyncResult(task, 1, 0, null));
        Require(recovered.State == TrayVisualState.Ready && recovered.Notification is null,
            "A clean run did not restore the ready tray state.");
        Require(policy.Evaluate(conflict).Notification is not null,
            "A warning was not announced again after a clean recovery.");
    }

    private static void CheckWarningCategories()
    {
        var task = CreateTask();
        var policy = new BackgroundNotificationPolicy();

        RequireNotification(policy.Evaluate(new BackgroundSyncResult(task, 0, 0, null, ReportSaved: false)), "ReportWarning", null);
        RequireNotification(policy.Evaluate(new BackgroundSyncResult(task, 0, 0, new IOException("Synthetic error"))), "IoError", null);
        RequireNotification(policy.Evaluate(new BackgroundSyncResult(task, 0, 0, null, DeferredFiles: 1)), "TrayDeferred", 1);
    }

    private static void RequireNotification(BackgroundNotificationDecision decision, string key, int? count)
    {
        var notification = decision.Notification ?? throw new InvalidOperationException($"Expected notification {key}.");
        Require(notification.MessageKey == key, $"Expected notification {key}, got {notification.MessageKey}.");
        var expectedArguments = count.HasValue ? 1 : 0;
        Require(notification.Arguments.Length == expectedArguments, $"Notification {key} had an unexpected number of arguments.");
        if (count.HasValue)
            Require(notification.Arguments[0] is int actual && actual == count.Value, $"Notification {key} carried the wrong count.");
    }

    private static BackupTaskDefinition CreateTask() =>
        new(Guid.NewGuid(), "Synthetic notification task", "source", "destination", 0);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
