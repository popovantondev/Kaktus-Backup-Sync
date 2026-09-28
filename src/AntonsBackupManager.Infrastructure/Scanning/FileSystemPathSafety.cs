namespace AntonsBackupManager.Infrastructure.Scanning;

public static class FileSystemPathSafety
{
    public static bool IsSameOrNestedPath(string parentDirectory, string candidatePath)
    {
        return AntonsBackupManager.Core.Tasks.TaskPathRules.Contains(parentDirectory, candidatePath);
    }

    public static void EnsureSafeExistingDirectory(string directoryPath, string label)
    {
        var directory = new DirectoryInfo(directoryPath);
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"{label}-Ordner wurde nicht gefunden.");
        }

        for (DirectoryInfo? ancestor = directory; ancestor is not null; ancestor = ancestor.Parent)
            ThrowIfReparsePoint(ancestor, label);
    }

    public static void EnsureNoReparsePointsBetween(string rootDirectory, string targetPath)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        EnsureSafeExistingDirectory(normalizedRoot, "Stamm");

        var relativePath = Path.GetRelativePath(normalizedRoot, Path.GetFullPath(targetPath));
        if (!IsSameOrNestedPath(normalizedRoot, targetPath) ||
            relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException("Der Dateipfad liegt außerhalb des zugelassenen Ordners.");
        }

        var currentPath = normalizedRoot;
        foreach (var segment in relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (string.IsNullOrWhiteSpace(segment) || segment == ".")
            {
                continue;
            }

            currentPath = Path.Combine(currentPath, segment);
            if (Directory.Exists(currentPath))
            {
                ThrowIfReparsePoint(new DirectoryInfo(currentPath), "Pfadbestandteil");
            }
            else if (File.Exists(currentPath))
            {
                ThrowIfReparsePoint(new FileInfo(currentPath), "Datei");
            }
            else
            {
                break;
            }
        }
    }

    public static void ThrowIfReparsePoint(FileSystemInfo entry, string label)
    {
        entry.Refresh();
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            if (OperatingSystem.IsWindows() && WindowsReparsePoint.IsCloudTag(WindowsReparsePoint.ReadTag(entry.FullName))) return;
            throw new UnsupportedLinkException(entry.FullName);
        }
    }
}
