using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Infrastructure.Storage;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class TaskVolumeResolverTests
{
    private static BackupTaskDefinition Task() => new(Guid.NewGuid(), "Synthetic volume",
        @"D:\Photos", @"C:\Backups", 5, SourceBinding: new("1234ABCD", "Photos"));

    [TestMethod]
    public void MovedSource_UsesIdentityAndIgnoresReplacementAtOldLetter()
    {
        var resolved = TaskVolumeResolver.Resolve(Task(), [new(@"D:\", "87654321"), new(@"H:\", "1234abcd")]);
        Assert.AreEqual(@"H:\Photos", resolved.SourceDirectory);
        Assert.AreEqual(@"C:\Backups", resolved.DestinationDirectory);
        Assert.ThrowsExactly<RemovableVolumeException>(() => TaskVolumeResolver.EnsureAvailableAtCurrentPaths(Task(), [new(@"H:\", "1234ABCD")]));
        TaskVolumeResolver.EnsureAvailableAtCurrentPaths(resolved, [new(@"H:\", "1234ABCD")]);
    }

    [TestMethod]
    public void OldCatalog_LoadsAndMigratesWithoutLosingTaskSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "KaktusCatalogMigration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var task = Task() with { SourceBinding = null, MonitoredRemovableVolumeSerial = "1234ABCD" };
            var path = Path.Combine(root, "tasks.json");
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { SchemaVersion = 1, Tasks = new[] { task } }));
            var store = new JsonBackupTaskStore();
            Assert.AreEqual(task, store.Load(path).Single());
            store.Save(path, store.Load(path));
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            Assert.AreEqual(2, json.RootElement.GetProperty("SchemaVersion").GetInt32());
            Assert.AreEqual(task, store.Load(path).Single());
            Assert.HasCount(1, Directory.GetFiles(root, "tasks.json.backup-*.json"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void BothEndpoints_RemappedIndependently()
    {
        var task = Task() with { DestinationDirectory = @"E:\Archive", DestinationBinding = new("AABBCCDD", "Archive") };
        var resolved = TaskVolumeResolver.Resolve(task, [new(@"H:\", "1234ABCD"), new(@"J:\", "AABBCCDD")]);
        Assert.AreEqual(@"H:\Photos", resolved.SourceDirectory);
        Assert.AreEqual(@"J:\Archive", resolved.DestinationDirectory);
    }

    [TestMethod]
    public void MissingOrDuplicateIdentity_BlocksResolution()
    {
        Assert.AreEqual(VolumeProblem.Missing, Assert.ThrowsExactly<RemovableVolumeException>(() => TaskVolumeResolver.Resolve(Task(), [])).Problem);
        Assert.AreEqual(VolumeProblem.Ambiguous, Assert.ThrowsExactly<RemovableVolumeException>(() =>
            TaskVolumeResolver.Resolve(Task(), [new(@"H:\", "1234ABCD"), new(@"J:\", "1234ABCD")])).Problem);
    }

    [TestMethod]
    public void LegacyBinding_UpgradesOnlyAtVerifiedOriginalRoot()
    {
        var task = Task() with { SourceBinding = null, MonitoredRemovableVolumeSerial = "1234ABCD" };
        Assert.AreEqual(VolumeProblem.NeedsRebind, Assert.ThrowsExactly<RemovableVolumeException>(() =>
            TaskVolumeResolver.Resolve(task, [new(@"H:\", "1234ABCD")])).Problem);
        var upgraded = TaskVolumeResolver.Resolve(task, [new(@"D:\", "1234ABCD")]);
        Assert.AreEqual(new RemovableFolderBinding("1234ABCD", "Photos"), upgraded.SourceBinding);
        Assert.AreEqual(@"H:\Photos", TaskVolumeResolver.Resolve(upgraded, [new(@"H:\", "1234ABCD")]).SourceDirectory);
    }

    [TestMethod]
    [DataRow(@"..\outside")]
    [DataRow(@"C:\outside")]
    [DataRow(@"valid\..\outside")]
    [DataRow("file:stream")]
    public void UnsafeRelativeFolder_IsRejected(string relative)
    {
        Assert.ThrowsExactly<ArgumentException>(() => TaskVolumeResolver.Validate(new("1234ABCD", relative)));
    }

    [TestMethod]
    public void Relocation_RetainsBaselineAndPersistedBinding()
    {
        var root = Path.Combine(Path.GetTempPath(), "KaktusVolumeTests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "old-location");
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(source); Directory.CreateDirectory(target);
        try
        {
            var task = new BackupTaskDefinition(Guid.NewGuid(), "Synthetic relocation", source, target, 5,
                SourceBinding: new("1234ABCD", "Photos"));
            File.WriteAllText(Path.Combine(source, "demo.txt"), "before");
            var service = new SynchronizationService(Path.Combine(root, "state"));
            service.Synchronize(task, service.Preview(task));
            var moved = Path.Combine(root, "new-location");
            Directory.Move(source, moved);
            var relocated = task with { SourceDirectory = moved };
            service.RebindTaskLocation(task, relocated);
            File.WriteAllText(Path.Combine(moved, "demo.txt"), "after");
            var preview = service.Preview(relocated);
            Assert.AreEqual(1, preview.Updates);
            Assert.AreEqual(0, preview.Conflicts);
            var store = new JsonBackupTaskStore();
            var config = Path.Combine(root, "tasks.json");
            store.Save(config, [relocated]);
            Assert.AreEqual(relocated, store.Load(config).Single());
        }
        finally { Directory.Delete(root, true); }
    }
}
