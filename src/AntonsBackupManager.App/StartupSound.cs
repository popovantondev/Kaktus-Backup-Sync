using System.Windows;
using System.Windows.Media;

namespace AntonsBackupManager.App;

internal interface IStartupSound : IDisposable
{
    void Play();
    void Stop();
}

internal sealed class StartupSound : IStartupSound
{
    private MediaPlayer? player;
    internal static string AudioPath => Path.Combine(AppContext.BaseDirectory, "Assets", "StartupLeaves.mp3");

    public void Play()
    {
        Stop();
        try
        {
            if (!File.Exists(AudioPath)) return;
            player = new MediaPlayer { Volume = 0.3 };
            player.MediaOpened += OnMediaOpened;
            player.MediaFailed += OnMediaFailed;
            player.Open(new Uri(AudioPath));
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or NotSupportedException)
        {
            // An optional sound must never prevent the backup application from opening.
            Stop();
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, player)) player?.Play();
    }

    private void OnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        if (ReferenceEquals(sender, player)) Stop();
    }

    public void Stop()
    {
        var active = player;
        player = null;
        if (active is null) return;
        active.MediaOpened -= OnMediaOpened;
        active.MediaFailed -= OnMediaFailed;
        active.Close();
    }

    public void Dispose() => Stop();
}
