using System.Text.Json;
using AntonsBackupManager.Infrastructure.Scanning;
using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Infrastructure.Storage;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class ReleaseRegressionTests
{
    [TestMethod]
    public void CloudTags_AreAllowedWithoutAllowingJunctionsOrUnknownTags()
    {
        for (uint index = 0; index < 16; index++) Assert.IsTrue(WindowsReparsePoint.IsCloudTag(0x9000001aU | (index << 12)));
        foreach (var tag in new uint[] { 0xa0000003, 0xa000000c, 0x80000021, 0x900000ff, 0 })
            Assert.IsFalse(WindowsReparsePoint.IsCloudTag(tag));
    }

    [TestMethod]
    public void Catalog_RejectsCrossTaskWritesAndAllowsNestedSources()
    {
        var first = Task(@"C:\Data\Source", @"D:\Backup\First");
        Assert.Throws<ArgumentException>(() => BackupTaskCatalog.Add([first], Task(@"C:\Other", @"D:\Backup\First\Child")));
        Assert.Throws<ArgumentException>(() => BackupTaskCatalog.Add([first], Task(@"D:\Backup\First\Child", @"E:\Target")));
        Assert.Throws<ArgumentException>(() => BackupTaskCatalog.Add([first], Task(@"E:\Other", @"C:\Data")));
        Assert.HasCount(2, BackupTaskCatalog.Add([first], Task(@"C:\Data\Source\Child", @"E:\Backup")));
    }

    [TestMethod]
    public void Catalog_UpgradesLegacyAndPreservesUnsupportedVersion()
    {
        WithDirectory(root =>
        {
            var file = Path.Combine(root, "tasks.json");
            var task = Task(@"C:\Source", @"D:\Destination");
            var legacy = JsonSerializer.Serialize(new[] { task });
            File.WriteAllText(file, legacy);
            var store = new JsonBackupTaskStore();
            store.Save(file, store.Load(file));
            Assert.AreEqual(task, store.Load(file).Single());
            Assert.AreEqual(legacy, File.ReadAllText(Directory.GetFiles(root, "*.backup-*.json").Single()));
            for (var i = 0; i < 5; i++) store.Save(file, [task with { Name = "Revision " + i }]);
            Assert.HasCount(3, Directory.GetFiles(root, "*.backup-*.json"));
            var future = "{\"SchemaVersion\":99,\"Tasks\":[]}";
            File.WriteAllText(file, future);
            Assert.Throws<JsonException>(() => store.Load(file));
            Assert.AreEqual(future, File.ReadAllText(file));
        });
    }

    [TestMethod]
    public void Migration_PreservesRecoveryFilesAndDoesNotReviveDeletedTasks()
    {
        WithDirectory(root =>
        {
            var legacy = Path.Combine(root, "legacy");
            var current = Path.Combine(root, "current");
            var store = new JsonBackupTaskStore();
            store.Save(Path.Combine(legacy, "tasks.json"), [Task(@"C:\Source", @"D:\Destination")]);
            Directory.CreateDirectory(Path.Combine(legacy, "sync", "versions"));
            File.WriteAllText(Path.Combine(legacy, "sync", "versions", "sample.previous"), "recovery");
            ApplicationDataDirectory.Prepare(current, legacy);
            Assert.AreEqual("recovery", File.ReadAllText(Path.Combine(current, "sync", "versions", "sample.previous")));
            store.Save(Path.Combine(current, "tasks.json"), []);
            ApplicationDataDirectory.Prepare(current, legacy);
            Assert.IsEmpty(store.Load(Path.Combine(current, "tasks.json")));
        });
    }

    [TestMethod]
    public void Reports_InSameMillisecondDoNotOverwriteEachOther()
    {
        WithDirectory(root =>
        {
            var store = new JsonBackupRunReportStore();
            var report = new BackupRunRecord(Guid.NewGuid(), DateTimeOffset.UtcNow, ["sample.txt"], 0);
            store.Save(root, report); store.Save(root, report with { Outcome = "Interrupted" });
            Assert.HasCount(2, store.Load(root, report.TaskId));
        });
    }

    private static BackupTaskDefinition Task(string source, string destination) => new(Guid.NewGuid(), "Example", source, destination, 5);
    [TestMethod]
    public void Junction_IsRejectedEvenForDirectCopyExport()
    {
        if (!OperatingSystem.IsWindows()) return;
        WithDirectory(root =>
        {
            var real = Directory.CreateDirectory(Path.Combine(root,"real")).FullName;
            var junction = Path.Combine(root,"junction");
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(junction); start.ArgumentList.Add(real);
            using var process = System.Diagnostics.Process.Start(start)!;
            if (!process.WaitForExit(10000) || process.ExitCode != 0) Assert.Fail("Could not create the disposable junction fixture.");
            try
            {
                var source = Path.Combine(root,"sample.txt"); File.WriteAllText(source,"sample");
                Assert.Throws<IOException>(() => FileSystemPathSafety.EnsureSafeExistingDirectory(junction,"Test"));
                Assert.Throws<IOException>(() => AntonsBackupManager.Infrastructure.Execution.SafeFileCopier.CopyNewFile(source,Path.Combine(junction,"copy.txt")));
                Assert.IsEmpty(Directory.GetFiles(real));
            }
            finally { Directory.Delete(junction); }
        });
    }
    [TestMethod]
    public void KeepDestination_IsRememberedUntilContentChangesWithoutTouchingSource()
    {
        WithDirectory(root =>
        {
            var source = Directory.CreateDirectory(Path.Combine(root,"source")).FullName;
            var target = Directory.CreateDirectory(Path.Combine(root,"target")).FullName;
            File.WriteAllText(Path.Combine(source,"sample.txt"), "source");
            File.WriteAllText(Path.Combine(target,"sample.txt"), "target");
            var task = Task(source, target);
            var service = new SynchronizationService(Path.Combine(root,"state"));
            var keep = AntonsBackupManager.Core.Planning.ConflictResolution.KeepDestination(service.Preview(task), ["sample.txt"]);
            service.Synchronize(task, keep);
            Assert.AreEqual(1, service.Preview(task).KeptFiles);
            Assert.AreEqual("source", File.ReadAllText(Path.Combine(source,"sample.txt")));
            Assert.AreEqual("target", File.ReadAllText(Path.Combine(target,"sample.txt")));
            File.WriteAllText(Path.Combine(target,"sample.txt"), "changed target");
            Assert.AreEqual(1, service.Preview(task).Conflicts);
            Assert.Throws<IOException>(() => service.Synchronize(task, keep));
            Assert.AreEqual("source", File.ReadAllText(Path.Combine(source,"sample.txt")));
        });
    }
    [TestMethod]
    public void ScanCache_ReusesHashesButManualCheckDetectsHiddenContentChange()
    {
        WithDirectory(root =>
        {
            var source = Directory.CreateDirectory(Path.Combine(root,"source")).FullName;
            var target = Directory.CreateDirectory(Path.Combine(root,"target")).FullName;
            var task = Task(source, target);
            var path = Path.Combine(source,"example.txt");
            File.WriteAllText(path, "first");
            var service = new SynchronizationService(Path.Combine(root,"state"));
            service.Synchronize(task, service.Preview(task));
            service.Preview(task, allowCache: true);
            service.Preview(task, allowCache: true);
            Assert.AreEqual(2, service.CachedFilesInLastPreview);
            var modified = File.GetLastWriteTimeUtc(path);
            File.WriteAllText(path, "other");
            File.SetLastWriteTimeUtc(path, modified);
            Assert.AreEqual(1, service.Preview(task).Updates);
            Assert.AreEqual(0, service.CachedFilesInLastPreview);
        });
    }

    [TestMethod]
    public void MissingDestination_CannotBeReportedSuccessfulWithAnEmptyPlan()
    {
        WithDirectory(root =>
        {
            var source = Directory.CreateDirectory(Path.Combine(root,"source")).FullName;
            var destination = Directory.CreateDirectory(Path.Combine(root,"target")).FullName;
            var task = Task(source, destination);
            var service = new SynchronizationService(Path.Combine(root,"state"));
            var plan = service.Preview(task);
            Directory.Delete(destination);
            Assert.Throws<DirectoryNotFoundException>(() => service.Synchronize(task, plan));
        });
    }
    [TestMethod]
    public void Versions_LiveAtDestinationAreBoundedAndRemainRecoverable()
    {
        WithDirectory(root =>
        {
            var source = Directory.CreateDirectory(Path.Combine(root,"source")).FullName;
            var target = Directory.CreateDirectory(Path.Combine(root,"target")).FullName;
            var task = Task(source, target);
            var service = new SynchronizationService(Path.Combine(root,"state"));
            for (var index = 0; index < 10; index++)
            {
                File.WriteAllText(Path.Combine(source,"sample.txt"), "Version " + index);
                service.Synchronize(task, service.Preview(task));
            }
            var versions = service.ListVersions(task);
            Assert.HasCount(7, versions);
            Assert.IsTrue(Directory.Exists(Path.Combine(target,".SyncVersions", task.Id.ToString("N"))));
            Assert.AreEqual(0, service.Preview(task).Conflicts);
            service.ExportVersion(task, versions[0], Path.Combine(root,"restored.txt"));
            Assert.AreEqual("Version 8", File.ReadAllText(Path.Combine(root,"restored.txt")));
            File.Delete(Path.Combine(source,"sample.txt"));
            Assert.AreEqual(1, service.Preview(task).Conflicts);
            Assert.AreEqual("Version 9", File.ReadAllText(Path.Combine(target,"sample.txt")));
        });
    }
    private static void WithDirectory(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "AntonsBackupRegression", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { Directory.Delete(root, true); }
    }
}
