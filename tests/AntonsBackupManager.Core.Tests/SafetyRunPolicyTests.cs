using AntonsBackupManager.Core.Safety;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class SafetyRunPolicyTests
{
    [TestMethod]
    public void ForNewTask_RequiresFiveConfirmedRuns()
    {
        var policy = SafetyRunPolicy.ForNewTask();

        Assert.IsTrue(policy.RequiresConfirmation);
        Assert.AreEqual(5, policy.ConfirmedRunsRemaining);
    }

    [TestMethod]
    public void RegisterConfirmedSuccess_DecreasesOnlyAfterConfirmedSuccess()
    {
        var policy = SafetyRunPolicy.ForNewTask();

        var updated = policy.RegisterConfirmedSuccess();

        Assert.AreEqual(5, policy.ConfirmedRunsRemaining);
        Assert.AreEqual(4, updated.ConfirmedRunsRemaining);
    }

    [TestMethod]
    public void RegisterConfirmedSuccess_AfterFiveRuns_DoesNotGoBelowZero()
    {
        var policy = SafetyRunPolicy.ForNewTask();
        for (var index = 0; index < 5; index++)
        {
            policy = policy.RegisterConfirmedSuccess();
        }

        var updated = policy.RegisterConfirmedSuccess();

        Assert.IsFalse(policy.RequiresConfirmation);
        Assert.AreEqual(0, updated.ConfirmedRunsRemaining);
    }
}
