using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Infrastructure.Storage;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class PortableStateSafetyTests
{
    [TestMethod]
    [DataRow(true, 0)]
    [DataRow(true, 1)]
    [DataRow(true, 2)]
    [DataRow(false, 0)]
    [DataRow(false, 1)]
    [DataRow(false, 2)]
    public void OverlappingState_ExplainsFolderAndRejectsPreviewAndWrites(bool isSource, int nesting)
    {
        var root = Path.Combine(Path.GetTempPath(), "KaktusStateTests", Guid.NewGuid().ToString("N"));
        var state = Path.Combine(root, "portable", "runtime", "sync");
        var selected = nesting switch { 0 => Path.Combine(root, "portable"), 1 => state, _ => Path.Combine(state, "child") };
        var separate = Path.Combine(root, "separate");
        Directory.CreateDirectory(selected);
        Directory.CreateDirectory(state);
        Directory.CreateDirectory(separate);
        try
        {
            var marker = Path.Combine(state, "sentinel.txt");
            File.WriteAllText(marker, "unchanged state");
            var filesBefore = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            var task = new BackupTaskDefinition(Guid.NewGuid(), "Synthetic portable task",
                isSource ? selected : separate, isSource ? separate : selected, 5);
            var service = new SynchronizationService(state);
            var error = Assert.ThrowsExactly<StateDirectoryOverlapException>(() => service.Preview(task));
            Assert.AreEqual(isSource, error.IsSource);
            Assert.AreEqual(selected, error.SelectedDirectory);
            Assert.AreEqual(state, error.StateDirectory);
            Assert.ThrowsExactly<StateDirectoryOverlapException>(() => service.Synchronize(task, new BackupPlan([])));
            CollectionAssert.AreEquivalent(filesBefore, Directory.GetFiles(root, "*", SearchOption.AllDirectories));
            Assert.AreEqual("unchanged state", File.ReadAllText(marker));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void SimilarSiblingName_IsAllowed()
    {
        var root = Path.Combine(Path.GetTempPath(), "KaktusStateTests", Guid.NewGuid().ToString("N"));
        var state = Path.Combine(root, "state");
        var source = Path.Combine(root, "state-files");
        var destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        try
        {
            File.WriteAllText(Path.Combine(source, "demo.txt"), "demo");
            var task = new BackupTaskDefinition(Guid.NewGuid(), "Synthetic sibling task", source, destination, 5);
            var service = new SynchronizationService(state);
            var plan = service.Preview(task);
            Assert.AreEqual(1, plan.CopyNewFiles);
            service.Synchronize(task, plan);
            Assert.AreEqual("demo", File.ReadAllText(Path.Combine(destination, "demo.txt")));
        }
        finally { Directory.Delete(root, true); }
    }
}
