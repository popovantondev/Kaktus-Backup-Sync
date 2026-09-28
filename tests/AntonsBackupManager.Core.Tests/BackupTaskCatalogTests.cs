using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class BackupTaskCatalogTests
{
    [TestMethod]
    public void Add_WhenSourceAndDestinationAreNew_AddsTask()
    {
        var result = BackupTaskCatalog.Add([], Task("Dokumente", "C:\\Source", "D:\\Destination"));

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public void Add_WhenSourceAndDestinationAlreadyExist_RejectsDuplicateIgnoringCase()
    {
        var existing = Task("Dokumente", "C:\\Source", "D:\\Destination");

        Assert.ThrowsExactly<ArgumentException>(() => BackupTaskCatalog.Add([existing], Task("Kopie", "c:\\source", "d:\\destination")));
    }

    private static BackupTaskDefinition Task(string name, string source, string destination) =>
        new(Guid.NewGuid(), name, source, destination, 5);
}
