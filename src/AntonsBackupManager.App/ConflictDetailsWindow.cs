using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AntonsBackupManager.Core.Planning;
using AntonsBackupManager.Core.Tasks;

namespace AntonsBackupManager.App;

internal enum ConflictChoice { Skip, UseSource, KeepDestination }

internal sealed class ConflictDetailsWindow : Window
{
    public ConflictChoice Choice { get; private set; }

    public ConflictDetailsWindow(Window owner, BackupTaskDefinition task, PlannedOperation operation)
    {
        NameScope.SetNameScope(this, new NameScope());
        Owner = owner; Title = UiText.Get("Compare"); Width = 820; Height = 440;
        MinWidth = 600; MinHeight = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brand.Surface;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        var panel = new DockPanel { Margin = new Thickness(24) };
        var heading = new TextBlock { Text = operation.RelativePath, FontSize = 20, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,16) };
        DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
        var footer = new StackPanel { Margin = new Thickness(0,16,0,0) };
        footer.Children.Add(new TextBlock { Text = UiText.Get("DecisionHint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) });
        var buttons = new WrapPanel();
        AddButton("SourceChoice", "ResolveConflicts", ConflictChoice.UseSource, operation.ExpectedSource is not null);
        AddButton("KeepChoice", "KeepDestination", ConflictChoice.KeepDestination, operation.ExpectedDestination is not null);
        AddButton("SkipChoice", "Cancel", ConflictChoice.Skip, true);
        footer.Children.Add(buttons); DockPanel.SetDock(footer, Dock.Bottom); panel.Children.Add(footer);
        var columns = new Grid(); columns.ColumnDefinitions.Add(new ColumnDefinition()); columns.ColumnDefinitions.Add(new ColumnDefinition());
        AddSide(0, "Source", task.SourceDirectory, operation.ExpectedSource);
        AddSide(1, "Destination", task.DestinationDirectory, operation.ExpectedDestination);
        panel.Children.Add(columns); Content = panel;

        void AddButton(string name, string key, ConflictChoice choice, bool enabled)
        {
            var button = new Button { Name = name, Content = UiText.Get(key), IsEnabled = enabled,
                Style = (Style)owner.FindResource(choice == ConflictChoice.UseSource ? "Primary" : typeof(Button)),
                IsCancel = choice == ConflictChoice.Skip, Margin = new Thickness(0,0,8,8) };
            RegisterName(name, button);
            button.Click += (_, _) => { Choice = choice; DialogResult = choice != ConflictChoice.Skip; };
            buttons.Children.Add(button);
        }
        void AddSide(int column, string title, string directory, FileSnapshot? snapshot)
        {
            var details = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.White,
                BorderThickness = new Thickness(0), Padding = new Thickness(14), Margin = new Thickness(column == 0 ? 0 : 8,0,0,0),
                Text = UiText.Get(title) + "\n\n" + Path.Combine(directory, operation.RelativePath) + "\n\n" +
                    (snapshot is null ? UiText.Get("Absent") : UiText.Get("FileDetails", snapshot.Length,
                        snapshot.LastWriteTimeUtc.LocalDateTime.ToString("g", CultureInfo.GetCultureInfo(UiText.Language)), snapshot.Sha256 ?? "—")) };
            Grid.SetColumn(details, column); columns.Children.Add(details);
        }
    }
}
