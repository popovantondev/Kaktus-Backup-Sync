using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AntonsBackupManager.App;

// View-only work: bindings, window gestures, layout and Windows message forwarding.
public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly DeviceArrivalMonitor deviceMonitor;
    private readonly DispatcherTimer displayTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? renderedLanguage;

    public event EventHandler<TrayNotification>? NotificationRequested
    { add => viewModel.NotificationRequested += value; remove => viewModel.NotificationRequested -= value; }
    public event EventHandler<TrayVisualState>? TrayStateRequested
    { add => viewModel.TrayStateRequested += value; remove => viewModel.TrayStateRequested -= value; }

    public MainWindow() : this(null) { }
    public MainWindow(string? runtimeDirectory) : this(runtimeDirectory, new FolderActions()) { }
    internal MainWindow(string? runtimeDirectory, IFolderActions folderActions)
    {
        InitializeComponent();
        var workspace = new BackupWorkspace(runtimeDirectory ?? Path.Combine(AppContext.BaseDirectory, "runtime"));
        viewModel = new MainViewModel(workspace, new WorkspaceDialogs(this, folderActions));
        DataContext = viewModel;
        viewModel.PropertyChanged += ModelChanged;
        viewModel.EditorReset += (_, _) => { TaskNameTextBox.Focus(); TaskNameTextBox.SelectAll(); };
        viewModel.Editor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TaskEditorViewModel.SafetyRuns)) ShowSafetySettings(this, new RoutedEventArgs());
        };
        IsVisibleChanged += (_, _) => viewModel.IsVisible = IsVisible;
        SizeChanged += (_, _) => RenderView();
        deviceMonitor = new DeviceArrivalMonitor(this);
        deviceMonitor.RemovableDeviceArrived += (_, arrival) => viewModel.DeviceArrived(arrival.Roots);
        displayTimer.Tick += (_, _) => viewModel.RefreshTiming();
        displayTimer.Start();
        Closed += (_, _) => { displayTimer.Stop(); viewModel.PropertyChanged -= ModelChanged; viewModel.Dispose(); };
        RenderView();
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs args) => RenderView();

    private void RenderView()
    {
        if (viewModel is null) return;
        if (renderedLanguage != UiText.Language)
        {
            renderedLanguage = UiText.Language;
            foreach (var key in UiText.Entries.Keys) Resources[key] = UiText.Get(key);
            Language = System.Windows.Markup.XmlLanguage.GetLanguage(UiText.Language);
            ActionColumn.Header = UiText.Get("Action"); PathColumn.Header = UiText.Get("Path");
            ShowSafetySettings(this, new RoutedEventArgs());
        }
        var extra = viewModel.Preview.Plan is { } plan && plan.Renames + plan.DeferredFiles > 0 ? 20 : 0;
        EditorScroll.MaxHeight = Math.Clamp(ActualHeight - 540 - extra, 130, 320);
        CopyButton.Style = (Style)FindResource(viewModel.CanSynchronize ? "Primary" : typeof(Button));
        PreviewButton.Style = (Style)FindResource(viewModel.CanSynchronize ? typeof(Button) : "Primary");
    }

    private void ShowSafetySettings(object sender, RoutedEventArgs e) =>
        Dispatcher.InvokeAsync(() =>
        {
            if (AdvancedExpander.IsExpanded && SafetySection.IsVisible) SafetySection.BringIntoView();
        }, DispatcherPriority.Loaded);

    private void PlanSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        viewModel?.SetSelection(PlanGrid.SelectedItems.OfType<PlanRow>().ToArray());

    private void ShowSelectedConflict(object sender, RoutedEventArgs e)
    {
        if (viewModel.CompareCommand.CanExecute(null)) viewModel.CompareCommand.Execute(null);
    }

    public void PublishTrayState() => viewModel.PublishTrayState();
    internal Task PollBackgroundNow() => viewModel.PollBackgroundNow();
    internal Task StopForExit() => viewModel.StopForExit();
}
