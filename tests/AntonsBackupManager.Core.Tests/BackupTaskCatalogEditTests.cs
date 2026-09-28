using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class BackupTaskCatalogEditTests
{
    [TestMethod]
    public void Replace_UpdatesExistingTask()
    {
        var original = Task("Alt");
        var updated = original with { Name = "Neu" };

        var result = BackupTaskCatalog.Replace([original], updated);

        Assert.AreEqual("Neu", result.Single().Name);
    }

    [TestMethod]
    public void Remove_RemovesOnlyTheSelectedTask()
    {
        var first = Task("Erste");
        var second = Task("Zweite");

        var result = BackupTaskCatalog.Remove([first, second], first.Id);

        CollectionAssert.AreEqual(new[] { second }, result.ToArray());
    }

    private static BackupTaskDefinition Task(string name) =>
        new(Guid.NewGuid(), name, $"C:\\{name}", $"D:\\{name}", 5);
}
