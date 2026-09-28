using AntonsBackupManager.Infrastructure.Execution;
using AntonsBackupManager.Infrastructure.Scanning;
using AntonsBackupManager.Core.Execution;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class SafeFileCopierTests
{
    [TestMethod]
    public void Copy_WhenDestinationDoesNotExist_CreatesMatchingFileWithoutVersion()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.WriteFile("source/report.txt", "new content");
        var destination = Path.Combine(directory.Path, "destination", "report.txt");

        var result = SafeFileCopier.CopyNewFile(source, destination);

        Assert.AreEqual("new content", File.ReadAllText(destination));
        Assert.IsNull(result.PreviousVersionPath);
    }

    [TestMethod]
    public void CopyNewFile_WhenSourceChangesWithSameMetadata_RejectsStaleHash()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.WriteFile("source/report.txt", "new content");
        var destination = Path.Combine(directory.Path, "destination/report.txt");
        var expected = FileSnapshotReader.Read("report.txt", source);
        File.WriteAllText(source, "bad content");
        File.SetLastWriteTimeUtc(source, expected.LastWriteTimeUtc.UtcDateTime);
        Assert.ThrowsExactly<IOException>(() => SafeFileCopier.CopyNewFile(source, destination, expected));
        Assert.IsFalse(File.Exists(destination));
    }

    [TestMethod]
    public void CopyNewFile_WhenDestinationAlreadyExists_LeavesExistingFileUntouched()
    {
        using var directory = new TemporaryDirectory();
        var source = directory.WriteFile("source/report.txt", "new content");
        var destination = directory.WriteFile("destination/report.txt", "keep this");

        Assert.ThrowsExactly<IOException>(() => SafeFileCopier.CopyNewFile(source, destination));

        Assert.AreEqual("keep this", File.ReadAllText(destination));
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

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
