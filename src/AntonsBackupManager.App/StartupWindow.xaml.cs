using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace AntonsBackupManager.App;

internal partial class StartupWindow : Window
{
    private readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DispatcherTimer growthDeadline = new() { Interval = TimeSpan.FromMilliseconds(3500) };
    private readonly DispatcherTimer finalPause = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool holding;
    private readonly IStartupSound sound;
    internal double RevealProgress => GrowthScale.ScaleX;
    internal bool IsHolding => holding && !completed.Task.IsCompleted;

    public StartupWindow() : this(new StartupSound()) { }

    internal StartupWindow(IStartupSound sound)
    {
        this.sound = sound;
        InitializeComponent();
        // Rendering can be suspended; startup must still have a bounded lifetime.
        growthDeadline.Tick += (_, _) => BeginFinalPause();
        finalPause.Tick += (_, _) => Finish();
        Closed += (_, _) => { Finish(); sound.Dispose(); };
    }

    public Task AnimateAsync()
    {
        if (!SystemParameters.ClientAreaAnimation) { BeginFinalPause(); return completed.Task; }
        sound.Play();
        growthDeadline.Start();
        var growth = new DoubleAnimation(GrowthScale.ScaleX, 1, TimeSpan.FromSeconds(3))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        growth.Completed += (_, _) => BeginFinalPause();
        // One clock keeps width and height identical throughout the growth.
        var growthClock = growth.CreateClock();
        GrowthScale.ApplyAnimationClock(ScaleTransform.ScaleXProperty, growthClock);
        GrowthScale.ApplyAnimationClock(ScaleTransform.ScaleYProperty, growthClock);
        return completed.Task;
    }

    private void BeginFinalPause()
    {
        if (holding || completed.Task.IsCompleted) return;
        holding = true;
        growthDeadline.Stop();
        ShowFullCactus();
        finalPause.Start();
    }

    private void SkipIntro(object sender, RoutedEventArgs e) => Finish();

    internal void Finish()
    {
        sound.Stop();
        growthDeadline.Stop();
        finalPause.Stop();
        ShowFullCactus();
        completed.TrySetResult();
    }

    private void ShowFullCactus()
    {
        GrowthScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        GrowthScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        GrowthScale.ScaleX = GrowthScale.ScaleY = 1;
    }
}
