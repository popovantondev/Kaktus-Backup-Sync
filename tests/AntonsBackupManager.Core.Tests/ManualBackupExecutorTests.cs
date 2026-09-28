using AntonsBackupManager.Infrastructure.Execution;
using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Core.Planning;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class ManualBackupExecutorTests
{
    [TestMethod]
    public void ExecuteNewFiles_CopiesOnlyNewFilesAndLeavesConflictsUntouched()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.Write("source/new.txt", "new");
        var destinationConflict = directory.Write("destination/conflict.txt", "keep this");
        var sourceSnapshot = new FileInfo(source);
        var plan = new BackupPlan([
            new PlannedOperation("new.txt", PlannedOperationKind.CopyNewFile, "new", new FileSnapshot("new.txt", sourceSnapshot.Length, sourceSnapshot.LastWriteTimeUtc)),
            new PlannedOperation("conflict.txt", PlannedOperationKind.Conflict, "conflict"),
        ]);

        var report = ManualBackupExecutor.ExecuteNewFiles(directory.Source, directory.Destination, plan, directory.Versions);

        Assert.AreEqual(1, report.CopiedFiles);
        Assert.AreEqual(1, report.SkippedConflicts);
        Assert.AreEqual(File.ReadAllText(source), File.ReadAllText(Path.Combine(directory.Destination, "new.txt")));
        Assert.AreEqual("keep this", File.ReadAllText(destinationConflict));
    }

    [TestMethod]
    public void ExecuteNewFiles_WhenDestinationAppearsAfterPreview_StopsWithoutOverwritingIt()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.Write("source/new.txt", "source version");
        var plan = CreateNewFilePlan(source);
        var destination = directory.Write("destination/new.txt", "created later");

        Assert.ThrowsExactly<IOException>(() => ManualBackupExecutor.ExecuteNewFiles(directory.Source, directory.Destination, plan, directory.Versions));

        Assert.AreEqual("created later", File.ReadAllText(destination));
    }

    [TestMethod]
    public void ExecuteNewFiles_WhenSourceChangesAfterPreview_StopsWithoutCreatingDestination()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.Write("source/new.txt", "first source version");
        var plan = CreateNewFilePlan(source);
        File.WriteAllText(source, "changed source version");

        Assert.ThrowsExactly<IOException>(() => ManualBackupExecutor.ExecuteNewFiles(directory.Source, directory.Destination, plan, directory.Versions));

        Assert.IsFalse(File.Exists(Path.Combine(directory.Destination, "new.txt")));
    }

    private static BackupPlan CreateNewFilePlan(string source)
    {
        var snapshot = new FileInfo(source);
        return new BackupPlan([
            new PlannedOperation("new.txt", PlannedOperationKind.CopyNewFile, "new", new FileSnapshot("new.txt", snapshot.Length, snapshot.LastWriteTimeUtc)),
        ]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "AntonsBackupManagerTests", Guid.NewGuid().ToString("N"));
            Source = Path.Combine(Root, "source");
            Destination = Path.Combine(Root, "destination");
            Versions = Path.Combine(Root, "versions");
            Directory.CreateDirectory(Source);
            Directory.CreateDirectory(Destination);
        }

        public string Root { get; }
        public string Source { get; }
        public string Destination { get; }
        public string Versions { get; }

        public string Write(string relativePath, string text)
        {
            var fullPath = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, text);
            return fullPath;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
