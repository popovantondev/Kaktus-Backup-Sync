using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AntonsBackupManager.Infrastructure.Storage;
using Microsoft.Win32;

namespace AntonsBackupManager.App;

internal sealed class VersionsWindow : Window
{
    public VersionsWindow(Window owner, IReadOnlyList<PreviousVersion> versions, Func<PreviousVersion, string, Task> restore)
    {
        Owner = owner; Title = UiText.Get("Versions");
        Width = 720; Height = 460; MinWidth = 600; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brand.Surface; FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        DataContext = new VersionsViewModel(versions, ChooseExportPath, restore);
        var panel = new DockPanel { Margin = new Thickness(24) };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,14) };
        status.SetBinding(TextBlock.TextProperty, new Binding(nameof(VersionsViewModel.Status)));
        DockPanel.SetDock(status, Dock.Top); panel.Children.Add(status);
        var button = new Button { Content = UiText.Get("Export"), Style = (Style)owner.FindResource("Primary"), Margin = new Thickness(0,16,0,0) };
        button.SetBinding(Button.CommandProperty, new Binding(nameof(VersionsViewModel.ExportCommand)));
        DockPanel.SetDock(button, Dock.Bottom); panel.Children.Add(button);
        var list = new ListBox { Background = Brushes.White, BorderThickness = new Thickness(0), DisplayMemberPath = nameof(VersionRow.Label) };
        list.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(VersionsViewModel.Items)));
        list.SetBinding(ListBox.SelectedItemProperty, new Binding(nameof(VersionsViewModel.Selected)) { Mode = BindingMode.TwoWay });
        list.SetBinding(IsEnabledProperty, new Binding(nameof(VersionsViewModel.CanSelect)));
        panel.Children.Add(list); Content = panel;
    }
    private string? ChooseExportPath(string relativePath)
    {
        var picker = new SaveFileDialog { Title = UiText.Get("Export"), FileName = Path.GetFileNameWithoutExtension(relativePath) + "-restored" + Path.GetExtension(relativePath) };
        return picker.ShowDialog(this) == true ? picker.FileName : null;
    }
}
