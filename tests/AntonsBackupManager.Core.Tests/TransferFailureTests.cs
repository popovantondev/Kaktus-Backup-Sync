using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Infrastructure.Execution;
using AntonsBackupManager.Infrastructure.Storage;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class TransferFailureTests
{
    [TestMethod]
    [DataRow(unchecked((int)0x80070070))] // Disk full.
    [DataRow(unchecked((int)0x80070015))] // Device not ready.
    public void FailureDuringNewCopy_NeverCommitsPartialFileAndCanRetry(int code)
    {
        using var f = new Fixture();
        File.WriteAllText(f.Source, "complete source");
        var failing = new SynchronizationService(f.State, new FailingTransfer(f.Source, code));
        var error = Assert.ThrowsExactly<BackupExecutionException>(() => failing.Synchronize(f.Task, failing.Preview(f.Task)));
        Assert.AreEqual(0, error.Completed.CopiedFiles);
        Assert.IsFalse(File.Exists(f.Target));
        Assert.AreEqual("complete source", File.ReadAllText(f.Source));
        Assert.IsEmpty(Directory.GetFiles(f.Root, "*.pending", SearchOption.AllDirectories));
        f.Sync();
        Assert.AreEqual("complete source", File.ReadAllText(f.Target));
    }

    [TestMethod]
    public void FailedUpdate_PreservesOldTargetAndRecoveryVersion()
    {
        using var f = new Fixture();
        File.WriteAllText(f.Source, "old version"); f.Sync();
        File.WriteAllText(f.Source, "new version");
        var failing = new SynchronizationService(f.State, new FailingTransfer(f.Source, unchecked((int)0x80070070)));
        Assert.ThrowsExactly<BackupExecutionException>(() => failing.Synchronize(f.Task, failing.Preview(f.Task)));
        Assert.AreEqual("old version", File.ReadAllText(f.Target));
        Assert.IsNotEmpty(failing.ListVersions(f.Task));
        Assert.IsEmpty(Directory.GetFiles(f.Root, "*.pending", SearchOption.AllDirectories));
        f.Sync();
        Assert.AreEqual("new version", File.ReadAllText(f.Target));
    }

    [TestMethod]
    public void FailureOnLaterFile_ReportsCompletedFilesAndRetriesRemainingFile()
    {
        using var f = new Fixture();
        File.WriteAllText(f.Source, "first");
        var later = Path.Combine(f.Task.SourceDirectory, "z.txt");
        File.WriteAllText(later, "second");
        var failing = new SynchronizationService(f.State, new FailingTransfer(later, unchecked((int)0x80070015)));
        var error = Assert.ThrowsExactly<BackupExecutionException>(() => failing.Synchronize(f.Task, failing.Preview(f.Task)));
        Assert.AreEqual(1, error.Completed.CopiedFiles);
        Assert.AreEqual("first", File.ReadAllText(f.Target));
        Assert.IsFalse(File.Exists(Path.Combine(f.Task.DestinationDirectory, "z.txt")));
        f.Sync();
        Assert.AreEqual("second", File.ReadAllText(Path.Combine(f.Task.DestinationDirectory, "z.txt")));
    }

    [TestMethod]
    public void NativeCopy_CancelsInsideFileWithoutPublishingDestination()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("Windows copy callback required.");
        using var f = new Fixture();
        using (var file = File.Create(f.Source)) file.SetLength(16 * 1024 * 1024);
        using var cancel = new CancellationTokenSource();
        var callbacks = 0;
        var transfer = new LocalFileTransfer(new InlineProgress(bytes => { if (bytes > 0) { callbacks++; cancel.Cancel(); } }));
        Assert.ThrowsExactly<OperationCanceledException>(() => SafeFileCopier.CopyNewFile(f.Source, f.Target, cancellationToken: cancel.Token, transfer: transfer));
        Assert.IsGreaterThan(0, callbacks);
        Assert.IsFalse(File.Exists(f.Target));
        Assert.IsEmpty(Directory.GetFiles(f.Root, "*.pending", SearchOption.AllDirectories));
    }

    [TestMethod]
    public void NativeCopy_RetainsAlternateStreams()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("NTFS alternate streams required.");
        using var f = new Fixture();
        File.WriteAllText(f.Source, "main stream");
        File.WriteAllText(f.Source + ":metadata", "alternate stream");
        f.Sync();
        Assert.AreEqual("alternate stream", File.ReadAllText(f.Target + ":metadata"));
    }

    [TestMethod]
    public void CaseOnlyRename_ChangesNameWithoutCopyingOrMakingVersion()
    {
        using var f = new Fixture();
        File.WriteAllText(f.Source, "same content"); f.Sync();
        File.Move(f.Source, Path.Combine(f.Task.SourceDirectory, "DEMO.txt"));
        var plan = f.Service.Preview(f.Task);
        Assert.AreEqual(1, plan.Renames);
        f.Service.Synchronize(f.Task, plan);
        Assert.AreEqual("DEMO.txt", Path.GetFileName(Directory.GetFiles(f.Task.DestinationDirectory).Single()));
        Assert.IsEmpty(f.Service.ListVersions(f.Task));
        Assert.AreEqual(0, f.Service.Preview(f.Task).Renames);
    }

    [TestMethod]
    public void CaseOnlyRename_ChangedTargetIsNotOverwritten()
    {
        using var f = new Fixture();
        File.WriteAllText(f.Source, "before"); f.Sync();
        File.Move(f.Source, Path.Combine(f.Task.SourceDirectory, "DEMO.txt"));
        var plan = f.Service.Preview(f.Task);
        File.WriteAllText(f.Target, "external change");
        Assert.ThrowsExactly<IOException>(() => f.Service.Synchronize(f.Task, plan));
        Assert.AreEqual("external change", File.ReadAllText(f.Target));
    }

    [TestMethod]
    public void CaseOnlyRename_SourceRenamedAgainRequiresNewPreview()
    {
        using var f = new Fixture();
        File.WriteAllText(f.Source, "same content"); f.Sync();
        var upper = Path.Combine(f.Task.SourceDirectory, "DEMO.txt");
        File.Move(f.Source, upper);
        var plan = f.Service.Preview(f.Task);
        File.Move(upper, Path.Combine(f.Task.SourceDirectory, "Demo.txt"));
        Assert.ThrowsExactly<IOException>(() => f.Service.Synchronize(f.Task, plan));
        Assert.AreEqual("demo.txt", Path.GetFileName(Directory.GetFiles(f.Task.DestinationDirectory).Single()));
    }

    [TestMethod]
    public void UnownedWorkFolder_IsRejectedWithoutRemovingExistingData()
    {
        using var f = new Fixture();
        File.WriteAllText(f.Source, "source");
        var reserved = Path.Combine(f.Task.DestinationDirectory, ".SyncWork");
        Directory.CreateDirectory(reserved);
        var existing = Path.Combine(reserved, Guid.NewGuid().ToString("N") + ".pending");
        File.WriteAllText(existing, "unowned data");
        Assert.ThrowsExactly<IOException>(() => SafeFileCopier.CopyNewFile(f.Source, f.Target));
        Assert.AreEqual("unowned data", File.ReadAllText(existing));
        Assert.IsFalse(File.Exists(f.Target));
        Assert.ThrowsExactly<IOException>(() => f.Service.Preview(f.Task));
    }

    private sealed class InlineProgress(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }

    private sealed class FailingTransfer(string sourceToFail, int code) : IFileTransfer
    {
        public void Copy(string source, string destination, CancellationToken token)
        {
            if (source != sourceToFail) { new LocalFileTransfer().Copy(source, destination, token); return; }
            File.WriteAllText(destination, "incomplete");
            throw new IOException("Synthetic transfer failure.", code);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "KaktusTransferTests", Guid.NewGuid().ToString("N"));
        public BackupTaskDefinition Task { get; }
        public string State => Path.Combine(Root, "state");
        public string Source => Path.Combine(Task.SourceDirectory, "demo.txt");
        public string Target => Path.Combine(Task.DestinationDirectory, "demo.txt");
        public SynchronizationService Service => new(State);
        public Fixture()
        {
            Task = new(Guid.NewGuid(), "Synthetic transfer", Path.Combine(Root, "source"), Path.Combine(Root, "target"), 5);
            Directory.CreateDirectory(Task.SourceDirectory); Directory.CreateDirectory(Task.DestinationDirectory);
        }
        public void Sync() { var service = Service; service.Synchronize(Task, service.Preview(Task)); }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
