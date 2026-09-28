using System.Text.Json;
using AntonsBackupManager.Core.Planning;

namespace AntonsBackupManager.Infrastructure.Scanning;

public sealed class FileScanCache
{
    private sealed record Entry(string FullPath, FileSnapshot Snapshot);
    private sealed record Document(int SchemaVersion, DateTimeOffset LastFullCheckUtc, IReadOnlyList<Entry> Entries);
    private readonly Dictionary<string, FileSnapshot> previous = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileSnapshot> current = new(StringComparer.OrdinalIgnoreCase);
    private readonly string path;
    private readonly bool fullCheck;
    private readonly DateTimeOffset lastFullCheck;
    public int CachedFiles { get; private set; }

    public FileScanCache(string path, bool allowCache, int fullCheckIntervalMinutes)
    {
        this.path = path;
        Document? loaded = null;
        try { if (File.Exists(path)) loaded = JsonSerializer.Deserialize<Document>(File.ReadAllText(path)); }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException) { }
        if (loaded?.SchemaVersion == 1 && loaded.Entries is not null)
        {
            foreach (var entry in loaded.Entries)
                if (entry?.Snapshot?.Sha256 is not null && !string.IsNullOrWhiteSpace(entry.FullPath)) previous[entry.FullPath] = entry.Snapshot;
            lastFullCheck = loaded.LastFullCheckUtc;
        }
        fullCheck = !allowCache || lastFullCheck > DateTimeOffset.UtcNow || DateTimeOffset.UtcNow - lastFullCheck >= TimeSpan.FromMinutes(fullCheckIntervalMinutes);
    }

    public FileSnapshot Read(string relative, string filePath, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var info = new FileInfo(filePath);
        FileSnapshot snapshot;
        if (!fullCheck && previous.TryGetValue(filePath, out var cached) && info.Exists &&
            info.Length == cached.Length && info.LastWriteTimeUtc == cached.LastWriteTimeUtc.UtcDateTime)
        {
            snapshot = cached with { RelativePath = relative };
            CachedFiles++;
        }
        else snapshot = FileSnapshotReader.Read(relative, filePath, token);
        current[filePath] = snapshot;
        return snapshot;
    }

    public void Save()
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, JsonSerializer.Serialize(new Document(1, fullCheck ? DateTimeOffset.UtcNow : lastFullCheck,
                current.Select(pair => new Entry(pair.Key, pair.Value)).ToArray())));
            File.Move(temp, path, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* A cache failure must not invalidate a verified plan. */ }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }
}
