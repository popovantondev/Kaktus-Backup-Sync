using AntonsBackupManager.Infrastructure.Scanning;
namespace AntonsBackupManager.Infrastructure.Storage;

public static class ApplicationDataDirectory
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AntonsBackupManager");

    // Call after acquiring the application instance lock. Preserve the complete old runtime,
    // including baseline/version blobs. An existing new catalog always wins, even when empty.
    public static string Prepare(string destination, string legacy)
    {
        if (Directory.Exists(destination)) return destination;
        if (!File.Exists(Path.Combine(legacy, "tasks.json")))
        {
            Directory.CreateDirectory(destination);
            return destination;
        }
        _ = new JsonBackupTaskStore().Load(Path.Combine(legacy, "tasks.json"));
        var staging = destination + ".migrate-" + Guid.NewGuid().ToString("N");
        try
        {
            CopyDirectory(legacy, staging);
            Directory.Move(staging, destination);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        return destination;
    }

    private static void CopyDirectory(string source, string destination)
    {
        AntonsBackupManager.Infrastructure.Scanning.FileSystemPathSafety.EnsureSafeExistingDirectory(source, "State");
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            AntonsBackupManager.Infrastructure.Scanning.FileSystemPathSafety.ThrowIfReparsePoint(new FileInfo(file), "State");
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
        foreach (var folder in Directory.EnumerateDirectories(source))
            CopyDirectory(folder, Path.Combine(destination, Path.GetFileName(folder)));
    }
}
