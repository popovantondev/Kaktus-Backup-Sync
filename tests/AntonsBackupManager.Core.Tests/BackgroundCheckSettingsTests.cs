using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class BackgroundCheckSettingsTests
{
    [TestMethod]
    public void Task_DefaultsToDisabledOneMinuteBackgroundCheck()
    {
        var task = new BackupTaskDefinition(Guid.NewGuid(), "Demo", @"C:\Source", @"D:\Destination", 5);

        Assert.IsFalse(task.BackgroundCheckEnabled);
        Assert.AreEqual(1, task.BackgroundCheckIntervalMinutes);
        Assert.IsNull(task.MonitoredRemovableVolumeSerial);
        BackupTaskValidator.Validate(task);
    }

    [TestMethod]
    public void Task_RejectsTooShortBackgroundCheckInterval()
    {
        var task = new BackupTaskDefinition(Guid.NewGuid(), "Demo", @"C:\Source", @"D:\Destination", 5, true, 0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => BackupTaskValidator.Validate(task));
    }

    [TestMethod]
    public void Task_RejectsTooLongBackgroundCheckInterval()
    {
        var task = new BackupTaskDefinition(Guid.NewGuid(), "Demo", @"C:\Source", @"D:\Destination", 5, true, 1441);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => BackupTaskValidator.Validate(task));
    }
}
