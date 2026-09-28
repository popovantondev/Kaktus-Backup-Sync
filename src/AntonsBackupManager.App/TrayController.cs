using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace AntonsBackupManager.App;

public sealed record TrayNotification(string Title, string MessageKey, params object[] Arguments)
{
    // Resolve when shown so queued notifications use the latest language.
    public string Message => UiText.Get(MessageKey, Arguments);
}
public enum TrayVisualState { Paused, Idle, Ready, Synchronizing, Attention }

internal sealed class TrayController : IDisposable
{
    private readonly Window window;
    private readonly Forms.NotifyIcon icon;
    private readonly Icon pausedIcon;
    private readonly Icon readyIcon;
    private readonly Icon idleIcon;
    private readonly Icon attentionIcon;
    private readonly Icon[] synchronizingIcons;
    private readonly Forms.Timer animationTimer;
    private readonly WindowsStartupController startup = new();
    private int animationFrame;
    private bool exiting;
    private bool disposed;
    internal TrayVisualState? CurrentState { get; private set; }
    internal int AnimationTicks { get; private set; }
    internal string TooltipText => icon.Text;
    internal string[] MenuLabels => icon.ContextMenuStrip!.Items.OfType<Forms.ToolStripMenuItem>().Select(item => item.Text ?? string.Empty).ToArray();

    public TrayController(Window window, Action exitApplication)
    {
        this.window = window;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(UiText.Get("TrayOpen"), null, (_, _) => ShowWindow());
        var startupItem = new Forms.ToolStripMenuItem(UiText.Get("StartWithWindows"))
        {
            Checked = startup.TryIsEnabled(),
            CheckOnClick = true,
        };
        startupItem.Click += (_, _) =>
        {
            try { startup.SetEnabled(startupItem.Checked); }
            catch (Exception)
            {
                startupItem.Checked = startup.TryIsEnabled();
                Notify(new TrayNotification(Brand.Name, "StartupFailed"));
            }
        };
        menu.Items.Add(startupItem);
        menu.Opening += (_, _) => RefreshLanguage();
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(UiText.Get("TrayExit"), null, async (_, _) =>
        {
            if (exiting) return;
            exiting = true;
            if (window is MainWindow main) await main.StopForExit();
            exitApplication();
        });
        icon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = Brand.Name,
            ContextMenuStrip = menu,
            Visible = true,
        };
        pausedIcon = LoadIcon("TrayPaused.ico");
        readyIcon = LoadIcon("TrayReady.ico");
        idleIcon = LoadIcon("TraySync0.ico");
        attentionIcon = LoadIcon("TrayAttention.ico");
        synchronizingIcons = Enumerable.Range(0, 12).Select(index => LoadIcon($"TraySync{index}.ico")).ToArray();
        animationTimer = new Forms.Timer { Interval = 80 };
        animationTimer.Tick += (_, _) =>
        {
            icon.Icon = synchronizingIcons[animationFrame++ % synchronizingIcons.Length];
            AnimationTicks++;
        };
        icon.DoubleClick += (_, _) => ShowWindow();
        window.StateChanged += (_, _) =>
        {
            if (window.WindowState == WindowState.Minimized) HideToTray();
        };
        window.Closing += (_, e) =>
        {
            if (exiting) return;
            e.Cancel = true;
            HideToTray();
        };
        UiText.LanguageChanged += OnLanguageChanged;
        RefreshLanguage();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (window.Dispatcher.CheckAccess()) RefreshLanguage();
        else window.Dispatcher.InvokeAsync(RefreshLanguage);
    }

    private void RefreshLanguage()
    {
        if (disposed) return;
        var menu = icon.ContextMenuStrip!;
        menu.Items[0].Text = UiText.Get("TrayOpen");
        menu.Items[1].Text = UiText.Get("StartWithWindows");
        menu.Items[menu.Items.Count - 1].Text = UiText.Get("TrayExit");
        SetState(CurrentState ?? TrayVisualState.Idle);
    }

    private void HideToTray()
    {
        window.Hide();
        window.WindowState = WindowState.Normal;
    }

    internal void ShowWindow()
    {
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    public void Notify(TrayNotification notification) =>
        icon.ShowBalloonTip(7000, notification.Title, notification.Message, Forms.ToolTipIcon.None);

    public void SetState(TrayVisualState state)
    {
        if (disposed) return;
        icon.Text = state switch
        {
            TrayVisualState.Paused => Brand.Name + " — " + UiText.Get("Paused"),
            TrayVisualState.Synchronizing => Brand.Name + " — " + UiText.Get("Copying"),
            TrayVisualState.Ready => Brand.Name + " — " + UiText.Get("TrayReadyState"),
            TrayVisualState.Attention => Brand.Name + " — " + UiText.Get("TrayAttention"),
            _ => Brand.Name + " — " + UiText.Get("TrayIdle"),
        };
        if (CurrentState == state) return;
        CurrentState = state;
        animationTimer.Stop();
        switch (state)
        {
            case TrayVisualState.Paused:
                icon.Icon = pausedIcon;
                break;
            case TrayVisualState.Synchronizing:
                animationFrame = 0;
                icon.Icon = synchronizingIcons[animationFrame++];
                animationTimer.Start();
                break;
            case TrayVisualState.Idle:
                icon.Icon = idleIcon;
                break;
            case TrayVisualState.Attention:
                icon.Icon = attentionIcon;
                break;
            default:
                icon.Icon = readyIcon;
                break;
        }
    }

    private static Icon LoadIcon(string name)
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri(
            $"pack://application:,,,/KaktusBackupSync;component/Assets/{name}"));
        if (resource is null) return (Icon)SystemIcons.Application.Clone();
        using var stream = resource.Stream;
        using var loaded = new Icon(stream, Forms.SystemInformation.SmallIconSize);
        return (Icon)loaded.Clone();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        UiText.LanguageChanged -= OnLanguageChanged;
        icon.Visible = false;
        animationTimer.Dispose();
        icon.ContextMenuStrip?.Dispose();
        icon.Dispose();
        pausedIcon.Dispose();
        readyIcon.Dispose();
        idleIcon.Dispose();
        attentionIcon.Dispose();
        foreach (var syncIcon in synchronizingIcons) syncIcon.Dispose();
    }
}
