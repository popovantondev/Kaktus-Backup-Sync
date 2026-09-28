using System.Security.Cryptography;
using AntonsBackupManager.Core.Planning;

namespace AntonsBackupManager.Infrastructure.Scanning;

public static class FileSnapshotReader
{
    public static FileSnapshot Read(string relativePath, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        var file = new FileInfo(filePath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("Die Datei wurde nicht gefunden.", filePath);
        }

        var expectedLength = file.Length;
        var expectedLastWriteTime = file.LastWriteTimeUtc;
        using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 131072, useAsync: false);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[131072];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, read);
        }

        file.Refresh();
        if (file.Length != expectedLength || file.LastWriteTimeUtc != expectedLastWriteTime)
        {
            throw new IOException("Die Datei wurde während der Prüfung geändert.");
        }

        return new FileSnapshot(relativePath, expectedLength, expectedLastWriteTime, Convert.ToHexString(hash.GetHashAndReset()));
    }
}
