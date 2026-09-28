namespace AntonsBackupManager.Core.Safety;

public sealed record SafetyRunPolicy(int ConfirmedRunsRemaining)
{
    public const int DefaultConfirmedRuns = 5;

    public static SafetyRunPolicy ForNewTask() => new(DefaultConfirmedRuns);

    public bool RequiresConfirmation => ConfirmedRunsRemaining > 0;

    public SafetyRunPolicy RegisterConfirmedSuccess() =>
        ConfirmedRunsRemaining <= 0 ? this : this with { ConfirmedRunsRemaining = ConfirmedRunsRemaining - 1 };
}
