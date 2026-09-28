using AntonsBackupManager.Core.Execution;
using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Infrastructure.Storage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AntonsBackupManager.App;

internal sealed class HistoryWindow : Window
{
    public HistoryWindow(Window owner, BackupTaskDefinition task, IReadOnlyList<BackupRunRecord> reports)
    {
        Owner = owner;
        Title = UiText.Get("History");
        Width = 700; Height = 440; MinWidth = 560; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brand.Surface;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        var panel = new DockPanel { Margin = new Thickness(24) };
        var heading = new TextBlock { Text = UiText.Get("HistoryFor", task.Name), FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(heading, Dock.Top); panel.Children.Add(heading);
        var list = new ListBox { Background = Brushes.White, BorderThickness = new Thickness(0) };
        try
        {
            foreach (var report in reports)
                list.Items.Add(new ListBoxItem
                {
                    Content = Describe(report),
                    Padding = new Thickness(12),
                    Foreground = report.DeferredRelativePaths.Count > 0 ? new SolidColorBrush(Color.FromRgb(148, 88, 0)) : Brushes.Black,
                });
            if (reports.Count == 0) heading.Text = UiText.Get("NoHistory");
        }
        catch (Exception)
        {
            heading.Text = UiText.Get("IoError");
        }
        panel.Children.Add(list);
        Content = panel;
    }

    private static string Describe(BackupRunRecord report) =>
        $"{report.CompletedAtUtc.LocalDateTime:g}   {UiText.Get(report.Trigger == "Automatic" ? "AutomaticRun" : "ManualRun")}\n" +
        UiText.Get("HistoryEntry", UiText.Get(UiText.Entries.ContainsKey(report.Outcome) ? report.Outcome : "Failed"), report.CopiedRelativePaths.Count, report.SkippedConflicts) +
        (report.RenamedRelativePaths.Count + report.DeferredRelativePaths.Count == 0 ? "" :
            "\n" + UiText.Get("SyncExtra", report.RenamedRelativePaths.Count, report.DeferredRelativePaths.Count) +
            (report.DeferredRelativePaths.Count == 0 ? "" : "\n" + string.Join("\n", report.DeferredRelativePaths)));
}
