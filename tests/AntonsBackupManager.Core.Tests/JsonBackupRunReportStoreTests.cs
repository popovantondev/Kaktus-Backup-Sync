using System.Text.Json;
using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Infrastructure.Storage;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class JsonBackupRunReportStoreTests
{
    [TestMethod]
    public void Save_WritesOneReadableReportWithoutTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var report = new BackupRunRecord(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 8, 12, 30, 0, TimeSpan.Zero),
            ["report.txt", "nested/checklist.txt"],
            SkippedConflicts: 1);

        new JsonBackupRunReportStore().Save(directory.Path, report);

        var reportPath = Directory.EnumerateFiles(directory.Path, "*.json").Single();
        var restored = JsonSerializer.Deserialize<BackupRunRecord>(File.ReadAllText(reportPath));
        Assert.IsNotNull(restored);
        Assert.AreEqual(report.TaskId, restored.TaskId);
        Assert.AreEqual(report.CompletedAtUtc, restored.CompletedAtUtc);
        Assert.AreEqual(report.SkippedConflicts, restored.SkippedConflicts);
        CollectionAssert.AreEqual(report.CopiedRelativePaths.ToArray(), restored.CopiedRelativePaths.ToArray());
        Assert.AreEqual(0, Directory.EnumerateFiles(directory.Path, "*.tmp").Count());
    }

    [TestMethod]
    public void Load_ReturnsOnlySelectedTaskInNewestOrder()
    {
        using var directory = new TemporaryDirectory();
        var task = Guid.NewGuid();
        var store = new JsonBackupRunReportStore();
        store.Save(directory.Path, new BackupRunRecord(task, new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero), ["old.txt"], 0));
        store.Save(directory.Path, new BackupRunRecord(task, new DateTimeOffset(2026, 9, 8, 11, 0, 0, TimeSpan.Zero), ["new.txt"], 1));
        store.Save(directory.Path, new BackupRunRecord(Guid.NewGuid(), new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero), ["other.txt"], 0));

        var reports = store.Load(directory.Path, task);

        Assert.HasCount(2, reports);
        Assert.AreEqual("new.txt", reports[0].CopiedRelativePaths.Single());
        Assert.AreEqual(1, reports[0].SkippedConflicts);
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
