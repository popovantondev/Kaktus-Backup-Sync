namespace AntonsBackupManager.App;

internal sealed class WorkspacePreferences(string directory)
{
    public string ReadLanguage()
    {
        try
        {
            var path = Path.Combine(directory, "language.txt");
            var value = File.Exists(path) ? File.ReadAllText(path).Trim() : "de";
            return value is "en" or "ru" ? value : "de";
        }
        catch (IOException) { return "de"; }
        catch (UnauthorizedAccessException) { return "de"; }
    }
    public void SaveLanguage(string value)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "language.txt"), value);
    }
    public bool IsPaused => File.Exists(Path.Combine(directory, "all-paused.flag"));
    public void SavePaused(bool paused)
    {
        var path = Path.Combine(directory, "all-paused.flag");
        if (paused) { Directory.CreateDirectory(directory); File.WriteAllText(path, "paused"); }
        else File.Delete(path);
    }
}
