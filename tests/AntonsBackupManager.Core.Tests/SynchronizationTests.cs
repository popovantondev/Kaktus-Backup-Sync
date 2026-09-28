using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Infrastructure.Storage;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class SynchronizationTests
{
    [TestMethod]
    public void Sync_ExplicitConflictResolution_PreservesDestinationVersion()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "source");
        File.WriteAllText(fixture.TargetFile, "destination");
        var conflict = fixture.Service.Preview(fixture.Task);
        Assert.AreEqual(1, conflict.Conflicts);
        Assert.AreEqual(1, ConflictResolution.UseSource(conflict, []).Conflicts);
        var resolved = ConflictResolution.UseSource(conflict, ["demo.txt"]);
        Assert.AreEqual(1, resolved.Updates);
        Assert.AreEqual(1, conflict.Conflicts);
        fixture.Service.Synchronize(fixture.Task, resolved);
        Assert.AreEqual("source", File.ReadAllText(fixture.TargetFile));
        var version = fixture.Service.ListVersions(fixture.Task).Single();
        var export = Path.Combine(fixture.Root, "old.txt");
        fixture.Service.ExportVersion(fixture.Task, version, export);
        Assert.AreEqual("destination", File.ReadAllText(export));
    }

    [TestMethod]
    public void Sync_ResolvedConflictChangedAfterPreview_RefusesOverwrite()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "source");
        File.WriteAllText(fixture.TargetFile, "destination");
        var plan = ConflictResolution.UseSource(fixture.Service.Preview(fixture.Task), ["demo.txt"]);
        File.WriteAllText(fixture.TargetFile, "later edit");
        Assert.ThrowsExactly<IOException>(() => fixture.Service.Synchronize(fixture.Task, plan));
        Assert.AreEqual("later edit", File.ReadAllText(fixture.TargetFile));
    }

    [TestMethod]
    public void Sync_DeletedDestination_RecreatesFileWithPersistedBaseline()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "first");
        fixture.Sync();
        File.Delete(fixture.TargetFile);
        var restarted = new SynchronizationService(Path.Combine(fixture.Root, "state"));
        var plan = restarted.Preview(fixture.Task);
        Assert.AreEqual(1, plan.CopyNewFiles);
        Assert.AreEqual(0, plan.Conflicts);
        restarted.Synchronize(fixture.Task, plan);
        Assert.AreEqual("first", File.ReadAllText(fixture.TargetFile));
        File.Delete(fixture.TargetFile);
        File.WriteAllText(fixture.SourceFile, "updated source");
        restarted.Synchronize(fixture.Task, restarted.Preview(fixture.Task));
        Assert.AreEqual("updated source", File.ReadAllText(fixture.TargetFile));
    }

    [TestMethod]
    public void Sync_DeletedDestinationReappearsAfterPreview_DoesNotOverwrite()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "first");
        fixture.Sync();
        File.Delete(fixture.TargetFile);
        var plan = fixture.Service.Preview(fixture.Task);
        File.WriteAllText(fixture.TargetFile, "external file");
        Assert.ThrowsExactly<IOException>(() => fixture.Service.Synchronize(fixture.Task, plan));
        Assert.AreEqual("external file", File.ReadAllText(fixture.TargetFile));
    }

    [TestMethod]
    public void Sync_NewThenChangedSource_SavesPreviousVersionAndRestoresSeparately()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "first version");
        fixture.Sync();
        File.WriteAllText(fixture.SourceFile, "second version");
        var plan = fixture.Service.Preview(fixture.Task);
        Assert.AreEqual(1, plan.Updates);
        fixture.Service.Synchronize(fixture.Task, plan);
        Assert.AreEqual("second version", File.ReadAllText(fixture.TargetFile));
        var version = fixture.Service.ListVersions(fixture.Task).Single();
        var restored = Path.Combine(fixture.Root, "restored.txt");
        fixture.Service.ExportVersion(fixture.Task, version, restored);
        Assert.AreEqual("first version", File.ReadAllText(restored));
        Assert.AreEqual("second version", File.ReadAllText(fixture.TargetFile));
        Assert.ThrowsExactly<IOException>(() => fixture.Service.ExportVersion(fixture.Task, version, restored));
    }

    [TestMethod]
    public void Sync_ChangedDestination_IsConflictAndRemainsUntouched()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "first");
        fixture.Sync();
        File.WriteAllText(fixture.SourceFile, "source edit");
        File.WriteAllText(fixture.TargetFile, "target edit");
        var plan = fixture.Service.Preview(fixture.Task);
        Assert.AreEqual(1, plan.Conflicts);
        fixture.Service.Synchronize(fixture.Task, plan);
        Assert.AreEqual("target edit", File.ReadAllText(fixture.TargetFile));
    }

    [TestMethod]
    public void Sync_TargetChangedAfterPreview_StopsBeforeReplacement()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "first");
        fixture.Sync();
        File.WriteAllText(fixture.SourceFile, "second");
        var plan = fixture.Service.Preview(fixture.Task);
        File.WriteAllText(fixture.TargetFile, "external change");
        Assert.ThrowsExactly<IOException>(() => fixture.Service.Synchronize(fixture.Task, plan));
        Assert.AreEqual("external change", File.ReadAllText(fixture.TargetFile));
        Assert.IsEmpty(fixture.Service.ListVersions(fixture.Task));
    }

    [TestMethod]
    public void Sync_ExistingIdenticalFile_BecomesBaselineOnlyAfterConfirmation()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "same");
        File.WriteAllText(fixture.TargetFile, "same");
        fixture.Sync();
        File.WriteAllText(fixture.SourceFile, "changed");
        Assert.AreEqual(1, fixture.Service.Preview(fixture.Task).Updates);
    }

    [TestMethod]
    public void Sync_Cancelled_DoesNotWriteFiles()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "first");
        var plan = fixture.Service.Preview(fixture.Task);
        Assert.ThrowsExactly<OperationCanceledException>(() => fixture.Service.Synchronize(fixture.Task, plan, new CancellationToken(true)));
        Assert.IsFalse(File.Exists(fixture.TargetFile));
    }

    [TestMethod]
    public void Sync_LockedDestination_PreservesOriginalAndRecoveryCopy()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "first");
        fixture.Sync();
        File.WriteAllText(fixture.SourceFile, "second");
        var plan = fixture.Service.Preview(fixture.Task);
        using (var locked = new FileStream(fixture.TargetFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var report = fixture.Service.Synchronize(fixture.Task, plan);
            CollectionAssert.AreEqual(new[] { "demo.txt" }, report.DeferredRelativePaths.ToArray());
            Assert.AreEqual("first", File.ReadAllText(fixture.TargetFile));
        }
        var version = fixture.Service.ListVersions(fixture.Task).Single();
        var restored = Path.Combine(fixture.Root, "recovered.txt");
        fixture.Service.ExportVersion(fixture.Task, version, restored);
        Assert.AreEqual("first", File.ReadAllText(restored));
    }

    [TestMethod]
    public void Sync_LockedSourceInPreview_CopiesOtherFilesAndRetriesAfterUnlock()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "busy");
        File.WriteAllText(Path.Combine(fixture.Task.SourceDirectory, "other.txt"), "available");
        using (var locked = new FileStream(fixture.SourceFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var plan = fixture.Service.Preview(fixture.Task);
            Assert.AreEqual(1, plan.DeferredFiles);
            Assert.AreEqual(0, plan.Conflicts);
            var report = fixture.Service.Synchronize(fixture.Task, plan);
            Assert.AreEqual(1, report.CopiedFiles);
            Assert.HasCount(1, report.DeferredRelativePaths);
            Assert.IsFalse(File.Exists(fixture.TargetFile));
            Assert.AreEqual("available", File.ReadAllText(Path.Combine(fixture.Task.DestinationDirectory, "other.txt")));
            var writer = new RunReportWriter(Path.Combine(fixture.Root, "reports"));
            Assert.IsTrue(writer.Record(fixture.Task, report, "Completed", "Automatic"));
            Assert.AreEqual("RetryPending", writer.ReadLatest(fixture.Task.Id)!.Outcome);
        }
        var retry = fixture.Service.Preview(fixture.Task);
        Assert.AreEqual(1, retry.CopyNewFiles);
        Assert.AreEqual(0, retry.DeferredFiles);
        fixture.Service.Synchronize(fixture.Task, retry);
        Assert.AreEqual("busy", File.ReadAllText(fixture.TargetFile));
    }

    [TestMethod]
    public void Sync_DestinationLockedAfterPreview_DoesNotAdvanceItsBaseline()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "before");
        fixture.Sync();
        File.WriteAllText(fixture.SourceFile, "after");
        File.WriteAllText(Path.Combine(fixture.Task.SourceDirectory, "other.txt"), "available");
        var plan = fixture.Service.Preview(fixture.Task);
        using (var locked = new FileStream(fixture.TargetFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var report = fixture.Service.Synchronize(fixture.Task, plan);
            Assert.AreEqual(1, report.CopiedFiles);
            Assert.HasCount(1, report.DeferredRelativePaths);
        }
        Assert.AreEqual("before", File.ReadAllText(fixture.TargetFile));
        var retry = fixture.Service.Preview(fixture.Task);
        Assert.AreEqual(1, retry.Updates);
        Assert.AreEqual(0, retry.Conflicts);
        fixture.Service.Synchronize(fixture.Task, retry);
        Assert.AreEqual("after", File.ReadAllText(fixture.TargetFile));
    }

    [TestMethod]
    public void Sync_RenameMovesVerifiedDestinationAndTracksFutureUpdate()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "original");
        fixture.Sync();
        Directory.CreateDirectory(Path.Combine(fixture.Task.SourceDirectory, "new"));
        var newSource = Path.Combine(fixture.Task.SourceDirectory, "new", "renamed.txt");
        var newTarget = Path.Combine(fixture.Task.DestinationDirectory, "new", "renamed.txt");
        File.Move(fixture.SourceFile, newSource);
        var plan = fixture.Service.Preview(fixture.Task);
        Assert.AreEqual(1, plan.Renames);
        Assert.AreEqual(0, plan.CopyNewFiles);
        Assert.AreEqual(0, plan.Conflicts);
        var report = fixture.Service.Synchronize(fixture.Task, plan);
        Assert.AreEqual(0, report.CopiedFiles);
        Assert.HasCount(1, report.RenamedRelativePaths);
        Assert.IsFalse(File.Exists(fixture.TargetFile));
        Assert.AreEqual("original", File.ReadAllText(newTarget));
        Assert.IsEmpty(fixture.Service.ListVersions(fixture.Task));
        File.WriteAllText(newSource, "edited");
        Assert.AreEqual(1, fixture.Service.Preview(fixture.Task).Updates);
        fixture.Sync();
        Assert.AreEqual("edited", File.ReadAllText(newTarget));
        Assert.HasCount(1, fixture.Service.ListVersions(fixture.Task));
    }

    [TestMethod]
    public void Sync_RenameRefusesChangedOrReappearedPaths()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "original");
        fixture.Sync();
        File.Move(fixture.SourceFile, Path.Combine(fixture.Task.SourceDirectory, "renamed.txt"));
        var plan = fixture.Service.Preview(fixture.Task);
        Assert.AreEqual(1, plan.Renames);
        File.WriteAllText(fixture.TargetFile, "external change");
        Assert.ThrowsExactly<IOException>(() => fixture.Service.Synchronize(fixture.Task, plan));
        Assert.AreEqual("external change", File.ReadAllText(fixture.TargetFile));
        File.WriteAllText(fixture.TargetFile, "original");
        File.WriteAllText(fixture.SourceFile, "reappeared");
        Assert.ThrowsExactly<IOException>(() => fixture.Service.Synchronize(fixture.Task, plan));
        File.Delete(fixture.SourceFile);
        var newTarget = Path.Combine(fixture.Task.DestinationDirectory, "renamed.txt");
        File.WriteAllText(newTarget, "unrelated target");
        Assert.ThrowsExactly<IOException>(() => fixture.Service.Synchronize(fixture.Task, plan));
        Assert.AreEqual("unrelated target", File.ReadAllText(newTarget));
        Assert.AreEqual("original", File.ReadAllText(fixture.TargetFile));
    }

    [TestMethod]
    public void Sync_AmbiguousIdenticalCopies_AreNotRenamed()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "same content");
        File.WriteAllText(Path.Combine(fixture.Task.SourceDirectory, "second.txt"), "same content");
        fixture.Sync();
        File.Move(fixture.SourceFile, Path.Combine(fixture.Task.SourceDirectory, "renamed.txt"));
        File.Delete(Path.Combine(fixture.Task.SourceDirectory, "second.txt"));
        var plan = fixture.Service.Preview(fixture.Task);
        Assert.AreEqual(0, plan.Renames);
        Assert.AreEqual(1, plan.CopyNewFiles);
        Assert.AreEqual(2, plan.Conflicts);
    }

    [TestMethod]
    public void Sync_LockedOldSource_IsNotMistakenForRename()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "same content");
        fixture.Sync();
        File.Copy(fixture.SourceFile, Path.Combine(fixture.Task.SourceDirectory, "new.txt"));
        using var locked = new FileStream(fixture.SourceFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var plan = fixture.Service.Preview(fixture.Task);
        Assert.AreEqual(0, plan.Renames);
        Assert.AreEqual(1, plan.DeferredFiles);
        Assert.AreEqual(1, plan.CopyNewFiles);
    }

    [TestMethod]
    public void Sync_RememberedKeepDestination_OverridesRenameDetection()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.SourceFile, "keep this copy");
        fixture.Sync();
        File.Delete(fixture.SourceFile);
        var conflict = fixture.Service.Preview(fixture.Task);
        var kept = new BackupPlan(conflict.Operations.Select(op => op with { Kind = PlannedOperationKind.KeepDestination }).ToArray());
        fixture.Service.Synchronize(fixture.Task, kept);
        File.WriteAllText(Path.Combine(fixture.Task.SourceDirectory, "new.txt"), "keep this copy");
        var plan = fixture.Service.Preview(fixture.Task);
        Assert.AreEqual(0, plan.Renames);
        Assert.AreEqual(1, plan.CopyNewFiles);
        Assert.AreEqual(1, plan.KeptFiles);
        fixture.Service.Synchronize(fixture.Task, plan);
        Assert.AreEqual("keep this copy", File.ReadAllText(fixture.TargetFile));
        Assert.AreEqual("keep this copy", File.ReadAllText(Path.Combine(fixture.Task.DestinationDirectory, "new.txt")));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "AntonsBackupManagerTests", Guid.NewGuid().ToString("N"));
        public BackupTaskDefinition Task { get; }
        public SynchronizationService Service { get; }
        public string SourceFile => Path.Combine(Task.SourceDirectory, "demo.txt");
        public string TargetFile => Path.Combine(Task.DestinationDirectory, "demo.txt");
        public Fixture()
        {
            Task = new BackupTaskDefinition(Guid.NewGuid(), "Demo", Path.Combine(Root, "source"), Path.Combine(Root, "target"), 5);
            Directory.CreateDirectory(Task.SourceDirectory);
            Directory.CreateDirectory(Task.DestinationDirectory);
            Service = new SynchronizationService(Path.Combine(Root, "state"));
        }
        public void Sync() => Service.Synchronize(Task, Service.Preview(Task));
        public void Dispose() => Directory.Delete(Root, true);
    }
}
