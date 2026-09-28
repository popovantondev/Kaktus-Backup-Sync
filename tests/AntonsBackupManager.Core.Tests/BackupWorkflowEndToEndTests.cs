using AntonsBackupManager.Infrastructure.Planning;
using AntonsBackupManager.Infrastructure.Execution;
using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Core.Planning;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class BackupWorkflowEndToEndTests
{
    [TestMethod]
    public void PreviewCopyNewFilesAndPreviewAgain_CompletesSyntheticWorkflow()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteSourceFile("report.txt", "invented report");
        directory.WriteSourceFile("nested/checklist.txt", "invented checklist");
        var dryRun = new DryRunService();

        var firstPlan = dryRun.CreatePlan(directory.Source, directory.Destination);
        var report = ManualBackupExecutor.ExecuteNewFiles(directory.Source, directory.Destination, firstPlan, directory.Versions);
        var secondPlan = dryRun.CreatePlan(directory.Source, directory.Destination);

        Assert.AreEqual(2, firstPlan.CopyNewFiles);
        Assert.AreEqual(2, report.CopiedFiles);
        Assert.AreEqual(0, secondPlan.CopyNewFiles);
        Assert.AreEqual(2, secondPlan.UnchangedFiles);
        Assert.AreEqual("invented report", File.ReadAllText(Path.Combine(directory.Destination, "report.txt")));
        Assert.AreEqual("invented checklist", File.ReadAllText(Path.Combine(directory.Destination, "nested", "checklist.txt")));
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

        public void WriteSourceFile(string relativePath, string content)
        {
            var filePath = Path.Combine(Source, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, content);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
