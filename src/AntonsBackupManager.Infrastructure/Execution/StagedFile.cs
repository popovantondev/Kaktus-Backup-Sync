using AntonsBackupManager.Infrastructure.Scanning;

namespace AntonsBackupManager.Infrastructure.Execution;

internal sealed class StagedFile : IDisposable
{
    public const string DirectoryName = ".SyncWork";
    private const string OwnerFile = "owner.txt";
    private const string Owner = "Kaktus Backup & Sync temporary files v1";
    private readonly FileStream directoryLock;
    public string Path { get; }

    public StagedFile(string destination)
    {
        var parent = System.IO.Path.GetDirectoryName(destination)!;
        var folder = System.IO.Path.Combine(parent, DirectoryName);
        FileSystemPathSafety.EnsureNoReparsePointsBetween(parent, folder);
        Directory.CreateDirectory(folder);
        var marker = System.IO.Path.Combine(folder, OwnerFile);
        if (!IsOwned(folder))
        {
            if (Directory.EnumerateFileSystemEntries(folder).Any()) throw new IOException("The temporary-work folder is not owned by this application.");
            using var output = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var bytes = System.Text.Encoding.UTF8.GetBytes(Owner);
            output.Write(bytes);
            output.Flush(true);
        }
        FileSystemPathSafety.EnsureNoReparsePointsBetween(folder, System.IO.Path.Combine(folder, "active.lock"));
        directoryLock = new FileStream(System.IO.Path.Combine(folder, "active.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            // An exclusive lock means no other active copy owns these pending files.
            // A previous process may have ended without running its finally block.
            foreach (var abandoned in Directory.EnumerateFiles(folder, "*.pending"))
                if (Guid.TryParseExact(System.IO.Path.GetFileNameWithoutExtension(abandoned), "N", out _)) DeletePending(abandoned);
            if (OperatingSystem.IsWindows()) File.SetAttributes(folder, File.GetAttributes(folder) | FileAttributes.Hidden);
            Path = System.IO.Path.Combine(folder, Guid.NewGuid().ToString("N") + ".pending");
        }
        catch { directoryLock.Dispose(); throw; }
    }

    public static bool IsOwned(string folder)
    {
        FileSystemPathSafety.EnsureSafeExistingDirectory(folder, "Temporary work");
        var marker = System.IO.Path.Combine(folder, OwnerFile);
        if (!File.Exists(marker)) return false;
        FileSystemPathSafety.ThrowIfReparsePoint(new FileInfo(marker), "Temporary work marker");
        return File.ReadAllText(marker) == Owner;
    }

    private static void DeletePending(string path)
    {
        if (!File.Exists(path)) return;
        FileSystemPathSafety.ThrowIfReparsePoint(new FileInfo(path), "Temporary file");
        File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        File.Delete(path);
    }

    public void Dispose()
    {
        try { DeletePending(Path); }
        finally { directoryLock.Dispose(); }
    }
}
