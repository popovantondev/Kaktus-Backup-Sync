using AntonsBackupManager.Infrastructure.Scanning;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class FileTreeScannerTests
{
    [TestMethod]
    public void Scan_WhenDirectoryContainsNestedFiles_ReturnsNormalizedRelativePaths()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("notes.txt", "hello");
        directory.WriteFile("nested/todo.txt", "buy milk");

        var result = FileTreeScanner.Scan(directory.Path);

        CollectionAssert.AreEqual(new[] { "nested/todo.txt", "notes.txt" }, result.Select(file => file.RelativePath).ToArray());
        CollectionAssert.AreEqual(new[] { 8L, 5L }, result.Select(file => file.Length).ToArray());
    }

    [TestMethod]
    public void Scan_WhenCancellationWasAlreadyRequested_StopsBeforeReadingFiles()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteFile("notes.txt", "hello");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => FileTreeScanner.Scan(directory.Path, cancellation.Token));
    }

    [TestMethod]
    public void Scan_WhenFilesHaveSameLengthAndTimestampButDifferentContent_ProducesDifferentHashes()
    {
        using var directory = new TemporaryDirectory();
        var firstPath = directory.WriteFile("first.txt", "aaaa");
        var secondPath = directory.WriteFile("second.txt", "bbbb");
        var timestamp = new DateTime(2026, 9, 8, 10, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(firstPath, timestamp);
        File.SetLastWriteTimeUtc(secondPath, timestamp);

        var snapshots = FileTreeScanner.Scan(directory.Path);

        Assert.AreNotEqual(snapshots[0].Sha256, snapshots[1].Sha256);
        Assert.IsFalse(snapshots[0].HasSameContentMarkerAs(snapshots[1]));
    }

    [TestMethod]
    public void IsSameOrNestedPath_WhenParentIsDriveRoot_AcceptsChildPath()
    {
        var driveRoot = Path.GetPathRoot(Path.GetTempPath())!;
        var childPath = Path.Combine(driveRoot, "AntonsBackupManagerTests", "child.txt");

        Assert.IsTrue(FileSystemPathSafety.IsSameOrNestedPath(driveRoot, childPath));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AntonsBackupManagerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string WriteFile(string relativePath, string content)
        {
            var fullPath = System.IO.Path.Combine(Path, relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
