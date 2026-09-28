using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Infrastructure.Storage;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class JsonBackupTaskStoreTests
{
    [TestMethod]
    public void SaveAndLoad_RoundTripsTaskWithoutLeavingTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "tasks.json");
        var task = new BackupTaskDefinition(Guid.NewGuid(), "Dokumente", "C:\\Test\\Source", "D:\\Test\\Destination", 5);
        var store = new JsonBackupTaskStore();

        store.Save(path, [task]);
        var restored = store.Load(path);

        CollectionAssert.AreEqual(new[] { task }, restored.ToArray());
        Assert.AreEqual(0, Directory.EnumerateFiles(directory.Path, "*.tmp").Count());
    }

    [TestMethod]
    public void Load_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        using var directory = new TemporaryDirectory();

        var result = new JsonBackupTaskStore().Load(Path.Combine(directory.Path, "missing.json"));

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void Load_WhenJsonIsInvalid_ThrowsWithoutChangingTheFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "tasks.json");
        const string invalidJson = "{ not a task list";
        File.WriteAllText(path, invalidJson);

        Assert.Throws<System.Text.Json.JsonException>(() => new JsonBackupTaskStore().Load(path));

        Assert.AreEqual(invalidJson, File.ReadAllText(path));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AntonsBackupManagerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
