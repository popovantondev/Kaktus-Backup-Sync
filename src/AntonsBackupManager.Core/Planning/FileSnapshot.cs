namespace AntonsBackupManager.Core.Planning;

public sealed record FileSnapshot(string RelativePath, long Length, DateTimeOffset LastWriteTimeUtc, string? Sha256 = null)
{
    public bool HasSameContentMarkerAs(FileSnapshot other) =>
        Sha256 is not null && other.Sha256 is not null
            ? string.Equals(Sha256, other.Sha256, StringComparison.OrdinalIgnoreCase)
            : Length == other.Length && LastWriteTimeUtc == other.LastWriteTimeUtc;

}
