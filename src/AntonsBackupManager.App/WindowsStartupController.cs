using Microsoft.Win32;

namespace AntonsBackupManager.App;

internal sealed class WindowsStartupController
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AntonsBackupManager";

    public bool TryIsEnabled()
    {
        try { return IsEnabled(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return false; }
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            var executable = Path.Combine(AppContext.BaseDirectory, Brand.ExecutableName);
            if (!File.Exists(executable)) throw new FileNotFoundException("Programmdatei nicht gefunden.", executable);
            key.SetValue(ValueName, $"\"{executable}\" --tray");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
