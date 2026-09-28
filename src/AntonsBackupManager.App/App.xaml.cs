using System.Windows;

namespace AntonsBackupManager.App;

public partial class App : System.Windows.Application
{
    private TrayController? tray;
    private SingleInstance? instance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        instance = new SingleInstance();
        if (!instance.IsPrimary) { Shutdown(); return; }
        var startInTray = e.Args.Contains("--tray", StringComparer.OrdinalIgnoreCase);
        StartupWindow? intro = null;
        try
        {
            Task animation = Task.CompletedTask;
            if (!startInTray)
            {
                intro = new StartupWindow();
                intro.Show();
                animation = intro.AnimateAsync();
            }
            var portable = File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.flag"));
            var legacy = Path.Combine(AppContext.BaseDirectory, "runtime");
            var dataDirectory = portable ? legacy : await Task.Run(() => Infrastructure.Storage.ApplicationDataDirectory.Prepare(
                Infrastructure.Storage.ApplicationDataDirectory.DefaultPath, legacy));
            var window = new MainWindow(dataDirectory);
            MainWindow = window;
            tray = new TrayController(window, Shutdown);
            window.NotificationRequested += (_, notification) => tray.Notify(notification);
            window.TrayStateRequested += (_, state) => tray.SetState(state);
            window.PublishTrayState();
            instance.Listen(() => Dispatcher.BeginInvoke(() => tray.ShowWindow()));
            await animation;
            if (!startInTray) window.Show();
        }
        catch (Exception error)
        {
            intro?.Close();
            MessageBox.Show(UiText.Get("AppStartFailed") + "\n\n" + ErrorPresentation.Details(error), Brand.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
        finally { intro?.Close(); }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
