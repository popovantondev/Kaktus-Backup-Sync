using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;

namespace AntonsBackupManager.App;

internal interface IFolderActions
{
    string? Choose(Window owner, string currentPath);
    void Open(string path);
}

internal sealed class FolderActions : IFolderActions
{
    internal static OpenFolderDialog CreatePicker(string currentPath)
    {
        var dialog = new OpenFolderDialog { Title = UiText.Get("Browse") };
        var path = currentPath.Trim().Trim('"');
        if (Directory.Exists(path))
        {
            dialog.InitialDirectory = Path.GetFullPath(path);
            dialog.DefaultDirectory = dialog.InitialDirectory;
        }
        return dialog;
    }

    public string? Choose(Window owner, string currentPath)
    {
        var dialog = CreatePicker(currentPath);
        return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
    }

    public void Open(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        start.ArgumentList.Add(Path.GetFullPath(path));
        Process.Start(start)?.Dispose();
    }
}
