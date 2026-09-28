using AntonsBackupManager.Infrastructure.Planning;
using AntonsBackupManager.Infrastructure.Execution;
using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Infrastructure.Scanning;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class AuditRegressionTests
{
    [TestMethod]
    public void Containment_NormalizesTrailingSeparatorAndRejectsSiblingPrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "audit-source");
        Assert.IsTrue(FileSystemPathSafety.IsSameOrNestedPath(root + Path.DirectorySeparatorChar, root));
        Assert.IsFalse(FileSystemPathSafety.IsSameOrNestedPath(root, root + "-other"));
    }

    [TestMethod]
    public void Execute_WhenLaterFileIsStale_PreflightPreventsPartialWrites()
    {
        var root = Path.Combine(Path.GetTempPath(), "AntonsBackupManagerTests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        try
        {
            File.WriteAllText(Path.Combine(source, "a.txt"), "safe");
            File.WriteAllText(Path.Combine(source, "b.txt"), "before");
            var plan = new DryRunService().CreatePlan(source, target);
            File.WriteAllText(Path.Combine(source, "b.txt"), "changed");
            Assert.ThrowsExactly<IOException>(() => ManualBackupExecutor.ExecuteNewFiles(source, target, plan, Path.Combine(root, "versions")));
            Assert.IsEmpty(Directory.GetFiles(target));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void Execute_RejectsOverlappingRootsEvenWithEmptyPlan()
    {
        var root = Path.GetTempPath();
        Assert.ThrowsExactly<ArgumentException>(() => ManualBackupExecutor.ExecuteNewFiles(root, root, new BackupPlan([]), root));
    }
}
