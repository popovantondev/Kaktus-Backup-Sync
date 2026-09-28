using AntonsBackupManager.Infrastructure.Planning;
using AntonsBackupManager.Core.Planning;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class DryRunServiceTests
{
    [TestMethod]
    public void CreatePlan_WhenFoldersAreSeparate_ReturnsReadOnlyPlan()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Source, "new.txt"), "new");
        var destinationFile = Path.Combine(directory.Destination, "same.txt");
        var sourceFile = Path.Combine(directory.Source, "same.txt");
        File.WriteAllText(destinationFile, "same");
        File.WriteAllText(sourceFile, "same");
        var sharedTimestamp = new DateTime(2026, 9, 8, 10, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(destinationFile, sharedTimestamp);
        File.SetLastWriteTimeUtc(sourceFile, sharedTimestamp);

        var plan = new DryRunService().CreatePlan(directory.Source, directory.Destination);

        Assert.AreEqual(1, plan.CopyNewFiles);
        Assert.AreEqual(1, plan.UnchangedFiles);
    }

    [TestMethod]
    public void CreatePlan_WhenDestinationIsInsideSource_RejectsUnsafeSetup()
    {
        using var directory = new TemporaryDirectory();
        var nestedDestination = Path.Combine(directory.Source, "destination");
        Directory.CreateDirectory(nestedDestination);

        Assert.ThrowsExactly<ArgumentException>(() => new DryRunService().CreatePlan(directory.Source, nestedDestination));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "AntonsBackupManagerTests", Guid.NewGuid().ToString("N"));
            Source = Path.Combine(Root, "source");
            Destination = Path.Combine(Root, "destination");
            Directory.CreateDirectory(Source);
            Directory.CreateDirectory(Destination);
        }

        public string Root { get; }
        public string Source { get; }
        public string Destination { get; }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
