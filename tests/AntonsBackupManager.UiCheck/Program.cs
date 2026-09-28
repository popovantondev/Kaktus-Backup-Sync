using AntonsBackupManager.Infrastructure.Scanning;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AntonsBackupManager.App;
using System.Diagnostics;

internal static class Program
{
    private static void CheckStartupAnimation(string output)
    {
        var sound = new TestStartupSound();
        var intro = new StartupWindow(sound);
        try
        {
            intro.Show();
            SaveIntroFrame(intro, Path.Combine(output, "intro-pot.png"));
            var plant = (FrameworkElement)intro.FindName("GrowingCactus");
            plant.Visibility = Visibility.Hidden;
            SaveIntroFrame(intro, Path.Combine(output, "intro-pot-only.png"));
            var firstFrame = BitmapFrame.Create(new Uri(Path.Combine(output, "intro-pot.png")));
            var potOnly = BitmapFrame.Create(new Uri(Path.Combine(output, "intro-pot-only.png")));
            var potArea = new Int32Rect(300, 244, 298, 74);
            var firstPixels = new byte[potArea.Width * potArea.Height * 4];
            var potPixels = new byte[firstPixels.Length];
            firstFrame.CopyPixels(potArea, firstPixels, potArea.Width * 4, 0);
            potOnly.CopyPixels(potArea, potPixels, potArea.Width * 4, 0);
            if (!firstPixels.SequenceEqual(potPixels))
                throw new InvalidOperationException("Growing cactus changed the pot or leaked beside it.");
            plant.Visibility = Visibility.Visible;
            var initialScale = intro.RevealProgress;
            if (initialScale <= 0 || initialScale >= 0.25)
                throw new InvalidOperationException("Cactus did not start as a small plant.");
            var animation = intro.AnimateAsync();
            if (sound.PlayCount != (SystemParameters.ClientAreaAnimation ? 1 : 0))
                throw new InvalidOperationException("Startup sound did not follow animation settings.");
            WaitForTask(Task.Delay(SystemParameters.ClientAreaAnimation ? 850 : 150).ContinueWith(_ => true));
            if (SystemParameters.ClientAreaAnimation && intro.RevealProgress <= initialScale)
                throw new InvalidOperationException("Cactus growth animation did not advance.");
            var transform = plant.RenderTransform.Value;
            var widthScale = Math.Sqrt(transform.M11 * transform.M11 + transform.M12 * transform.M12);
            var heightScale = Math.Sqrt(transform.M21 * transform.M21 + transform.M22 * transform.M22);
            if (Math.Abs(widthScale - heightScale) > 0.0001)
                throw new InvalidOperationException("Cactus was stretched instead of growing proportionally.");
            SaveIntroFrame(intro, Path.Combine(output, "intro-growing.png"));
            var holdSeen = new TaskCompletionSource<bool>();
            var holdObserver = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            holdObserver.Tick += (_, _) =>
            {
                if (intro.IsHolding || animation.IsCompleted)
                {
                    holdObserver.Stop();
                    holdSeen.TrySetResult(intro.IsHolding);
                }
            };
            holdObserver.Start();
            if (!WaitForTask(holdSeen.Task)) throw new InvalidOperationException("Completed cactus was not held on screen.");
            var pauseWatch = Stopwatch.StartNew();
            WaitForTask(animation.ContinueWith(_ => true));
            if (sound.IsPlaying) throw new InvalidOperationException("Growth sound continued after the intro ended.");
            if (SystemParameters.ClientAreaAnimation && pauseWatch.ElapsedMilliseconds < 700)
                throw new InvalidOperationException("The final cactus pause was too short.");
            if (intro.RevealProgress != 1) throw new InvalidOperationException("Cactus animation did not finish at full size.");
            SaveIntroFrame(intro, Path.Combine(output, "intro-ready.png"));
        }
        finally { intro.Close(); }
        var skippedSound = new TestStartupSound();
        var skipped = new StartupWindow(skippedSound);
        try
        {
            skipped.Show();
            var animation = skipped.AnimateAsync();
            skipped.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            if (!animation.IsCompleted || skipped.RevealProgress != 1)
                throw new InvalidOperationException("Clicking the startup screen did not skip it.");
            if (skippedSound.IsPlaying) throw new InvalidOperationException("Skipping the intro left its sound playing.");
        }
        finally { skipped.Close(); }
        var hidden = new StartupWindow(new TestStartupSound());
        try
        {
            WaitForTask(hidden.AnimateAsync().ContinueWith(_ => true));
            if (hidden.RevealProgress != 1) throw new InvalidOperationException("Hidden intro delayed startup indefinitely.");
        }
        finally { hidden.Close(); }
        if (!File.Exists(StartupSound.AudioPath))
        {
            Console.WriteLine("PASS startup animation without optional local audio");
            return;
        }
        var audioOpened = new TaskCompletionSource<bool>();
        var media = new MediaPlayer { Volume = 0 };
        media.MediaOpened += (_, _) => audioOpened.TrySetResult(media.HasAudio && media.NaturalDuration.HasTimeSpan);
        media.MediaFailed += (_, error) => audioOpened.TrySetException(error.ErrorException);
        try
        {
            media.Open(new Uri(StartupSound.AudioPath));
            if (!WaitForTask(audioOpened.Task)) throw new InvalidOperationException("Leaf rustle has no decodable audio.");
            Console.WriteLine($"PASS leaf MP3 decoder: {media.NaturalDuration.TimeSpan.TotalSeconds:F2} seconds");
        }
        finally { media.Close(); }
        Console.WriteLine("PASS proportional cactus growth, final pause, sound loading/lifecycle and skip");
    }

    private sealed class TestStartupSound : IStartupSound
    {
        public int PlayCount { get; private set; }
        public bool IsPlaying { get; private set; }
        public void Play() { PlayCount++; IsPlaying = true; }
        public void Stop() => IsPlaying = false;
        public void Dispose() => Stop();
    }

    private static void SaveIntroFrame(StartupWindow intro, string path)
    {
        intro.UpdateLayout();
        intro.Dispatcher.Invoke(() => intro.UpdateLayout(), DispatcherPriority.Render);
        var surface = (FrameworkElement)intro.Content;
        var bitmap = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--copy-crash-probe") return CrashRecoveryCheck.Child(args[1]);
        if (args.Length == 1 && args[0] == "--crash-check") return CrashRecoveryCheck.Run();
        if (args.Length == 2 && args[0] == "--instance-probe")
        {
            using var gate = new SingleInstance(args[1]);
            return gate.IsPrimary ? 1 : 0;
        }
        if (args.Length == 2 && args[0] == "--inspect-folder")
        {
            AntonsBackupManager.Infrastructure.Scanning.FileSystemPathSafety.EnsureSafeExistingDirectory(args[1], "Source");
            Console.WriteLine("PASS existing OneDrive folder metadata accepted; file contents were not read.");
            return 0;
        }
        var root = Path.Combine(Path.GetTempPath(), "AntonsBackupUiCheck", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(root, "screenshots");
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        using var bindingLog = new StringWriter();
        using var bindingTrace = new TextWriterTraceListener(bindingLog);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindingTrace);
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var folders = new TestFolderActions();
        var window = new MainWindow(root, folders) { ShowInTaskbar = false };
        try
        {
            WaitForTask(WorkspaceChecks.Run());
            if (args.Contains("--workflows-only")) return 0;
            if (args.Contains("--errors-only"))
            {
                CheckLocalizedErrors(root, output);
                return 0;
            }
            CheckStartupAnimation(output);
            CheckLocalizedErrors(root, output);
            window.Show();
            var identity = "AntonsBackupTest-" + Guid.NewGuid().ToString("N");
            using (var first = new SingleInstance(identity))
            {
                var activated = new ManualResetEventSlim();
                first.Listen(() => activated.Set());
                var probe = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") probe.ArgumentList.Add(typeof(Program).Assembly.Location);
                probe.ArgumentList.Add("--instance-probe"); probe.ArgumentList.Add(identity);
                using var second = Process.Start(probe)!;
                if (!second.WaitForExit(10000) || second.ExitCode != 0 || !activated.Wait(1000))
                    throw new InvalidOperationException("Second instance did not activate the first.");
            }
            Console.WriteLine("PASS cross-process single instance and activation");
            if (!DeviceArrivalMonitor.RootsFromMask((1U << 3) | (1U << 5)).SequenceEqual(new[] { @"D:\", @"F:\" }))
                throw new InvalidOperationException("Device arrival drive mask parsed incorrectly.");
            var language = (ComboBox)window.FindName("LanguageBox");
            foreach (var lang in new[] { 0, 1, 2 })
            {
                language.SelectedIndex = lang;
                foreach (var size in new[] { (900d, 700d), (1180d, 820d) })
                {
                    window.Width = size.Item1;
                    window.Height = size.Item2;
                    window.UpdateLayout();
                    window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    foreach (var name in new[] { "BackgroundCheckEnabledCheckBox", "IntervalComboBox", "BrowseSourceButton", "BrowseDestinationButton", "OpenSourceButton", "OpenDestinationButton", "PreviewButton", "CopyButton", "PauseTaskButton", "VersionsButton", "HistoryButton", "DeleteButton", "CancelPreviewButton", "StatusTextBlock", "LanguageBox", "PlanGrid" })
                    {
                        var element = (FrameworkElement)window.FindName(name);
                        var bounds = element.TransformToAncestor(window).TransformBounds(new Rect(element.RenderSize));
                        if (bounds.Left < 0 || bounds.Top < 0 || bounds.Right > window.ActualWidth + 1 || bounds.Bottom > window.ActualHeight + 1 || element.ActualWidth <= 0)
                            throw new InvalidOperationException($"Clipped control: {name}, language {lang}, size {size}");
                    }
                    var surface = (Grid)window.Content;
                    surface.Background = window.Background;
                    var bitmap = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(surface);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(output, $"ui-{lang}-{(int)size.Item1}.png"));
                    encoder.Save(stream);
                    Console.WriteLine($"PASS language={lang} size={size.Item1}x{size.Item2}");
                }
            }
            language.IsDropDownOpen = true;
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            var popup = (System.Windows.Controls.Primitives.Popup)language.Template.FindName("PART_Popup", language);
            if (!popup.IsOpen || popup.Child is not FrameworkElement popupSurface || popupSurface.ActualWidth < 100)
                throw new InvalidOperationException("Language dropdown did not open.");
            var popupBitmap = new RenderTargetBitmap((int)Math.Ceiling(popupSurface.ActualWidth), (int)Math.Ceiling(popupSurface.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            popupBitmap.Render(popupSurface);
            var popupEncoder = new PngBitmapEncoder();
            popupEncoder.Frames.Add(BitmapFrame.Create(popupBitmap));
            using (var popupStream = File.Create(Path.Combine(output, "language-dropdown.png"))) popupEncoder.Save(popupStream);
            language.IsDropDownOpen = false;
            Console.WriteLine("PASS styled language dropdown");
            var source = Path.Combine(root, "source");
            var target = Path.Combine(root, "target");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(target);
            SetText(window, "TaskNameTextBox", "Portfolio demo");
            SetText(window, "SourcePathTextBox", source);
            Click(((Button)window.FindName("PreviewButton")));
            WaitIdle(window);
            if (((Expander)window.FindName("ErrorDetails")).Visibility != Visibility.Visible ||
                ((ListBox)window.FindName("SavedTasksListBox")).Items.Count != 0)
                throw new InvalidOperationException("Invalid new task must explain the failure without saving.");
            SetText(window, "DestinationPathTextBox", target);
            Click(((Button)window.FindName("BrowseSourceButton")));
            if (folders.ChosenFrom != source || FolderActions.CreatePicker(source).InitialDirectory != source)
                throw new InvalidOperationException("Picker did not start in the current source folder.");
            Click(((Button)window.FindName("OpenSourceButton")));
            if (folders.Opened != source) throw new InvalidOperationException("Open source did not use the field's folder.");
            Click(((Button)window.FindName("BrowseDestinationButton")));
            if (folders.ChosenFrom != target) throw new InvalidOperationException("Destination picker used the wrong folder.");
            Click(((Button)window.FindName("OpenDestinationButton")));
            if (folders.Opened != target) throw new InvalidOperationException("Open destination used the wrong folder.");
            Console.WriteLine("PASS folder buttons and picker initial directory");
            Click(((Button)window.FindName("PreviewButton")));
            WaitIdle(window);
            if (((ListBox)window.FindName("SavedTasksListBox")).Items.Count != 1)
                throw new InvalidOperationException("Task save failed.");
            if (((TextBlock)window.FindName("StatusTextBlock")).Text != UiText.Get("EmptySaved"))
                throw new InvalidOperationException("Empty source must still create a task with a clear status.");
            File.WriteAllText(Path.Combine(source, "example.txt"), "Fictional portfolio data");
            Click(((Button)window.FindName("PreviewButton")));
            if (((TextBox)window.FindName("SourcePathTextBox")).IsEnabled)
                throw new InvalidOperationException("Editor is active during preview.");
            window.UpdateLayout();
            var busySurface = (Grid)window.Content;
            busySurface.Background = window.Background;
            var busyBitmap = new RenderTargetBitmap(220, (int)busySurface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            busyBitmap.Render(busySurface);
            var busyEncoder = new PngBitmapEncoder();
            busyEncoder.Frames.Add(BitmapFrame.Create(busyBitmap));
            using (var busyStream = File.Create(Path.Combine(output, "sidebar-busy.png"))) busyEncoder.Save(busyStream);
            WaitIdle(window);
            if (((DataGrid)window.FindName("PlanGrid")).Items.Count != 1 || !((Button)window.FindName("CopyButton")).IsEnabled)
                throw new InvalidOperationException("Preview did not enable copying.");
            var confirmTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            confirmTimer.Tick += (_, _) =>
            {
                var dialog = app.Windows.OfType<ConfirmDialog>().FirstOrDefault();
                if (dialog is null) return;
                confirmTimer.Stop();
                dialog.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)dialog.ActualWidth, (int)dialog.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(dialog);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(Path.Combine(output, "confirmation-ru.png"))) encoder.Save(stream);
                var panel = (StackPanel)dialog.Content;
                var buttons = panel.Children.OfType<StackPanel>().Single();
                Click(buttons.Children.OfType<Button>().Last());
            };
            confirmTimer.Start();
            Click(((Button)window.FindName("CopyButton")));
            WaitIdle(window);
            if (File.ReadAllText(Path.Combine(target, "example.txt")) != "Fictional portfolio data" ||
                Directory.GetFiles(Path.Combine(root, "reports"), "*.json").Length != 1)
                throw new InvalidOperationException("Confirmed UI copy or run report failed.");
            var historyVerified = false;
            var historyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            historyTimer.Tick += (_, _) =>
            {
                var history = app.Windows.OfType<HistoryWindow>().SingleOrDefault();
                if (history is null) return;
                historyTimer.Stop();
                historyVerified = history.Content is DockPanel historyPanel &&
                    historyPanel.Children.OfType<ListBox>().Single().Items.Count == 1;
                history.Close();
            };
            historyTimer.Start();
            Click(((Button)window.FindName("HistoryButton")));
            WaitIdle(window);
            if (!historyVerified) throw new InvalidOperationException("History did not show the saved run.");
            Console.WriteLine("PASS history shows saved runs");
            File.WriteAllText(Path.Combine(source, "example.txt"), "Updated fictional data");
            Click(((Button)window.FindName("PreviewButton")));
            WaitIdle(window);
            confirmTimer.Start();
            Click(((Button)window.FindName("CopyButton")));
            WaitIdle(window);
            if (File.ReadAllText(Path.Combine(target, "example.txt")) != "Updated fictional data")
                throw new InvalidOperationException("UI synchronization did not update the changed file.");
            var savedTask = new AntonsBackupManager.Infrastructure.Storage.JsonBackupTaskStore().Load(Path.Combine(root, "tasks.json")).Single();
            if (new AntonsBackupManager.Infrastructure.Storage.SynchronizationService(Path.Combine(root, "sync")).ListVersions(savedTask).Count != 1)
                throw new InvalidOperationException("UI synchronization did not preserve the previous version.");
            Console.WriteLine("PASS UI update and previous version preservation");
            File.Move(Path.Combine(source, "example.txt"), Path.Combine(source, "renamed.txt"));
            Click(((Button)window.FindName("PreviewButton")));
            WaitIdle(window);
            if (!((Button)window.FindName("CopyButton")).IsEnabled ||
                !((TextBlock)window.FindName("PlanTextBlock")).Text.Contains(UiText.Get("SyncExtra", 1, 0)))
                throw new InvalidOperationException("Rename preview is not clear or cannot be executed.");
            File.Move(Path.Combine(source, "renamed.txt"), Path.Combine(source, "example.txt"));
            using (var locked = new FileStream(Path.Combine(source, "example.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Click(((Button)window.FindName("PreviewButton")));
                WaitIdle(window);
                if (!((TextBlock)window.FindName("PlanTextBlock")).Text.Contains(UiText.Get("SyncExtra", 0, 1)))
                    throw new InvalidOperationException("Locked-file preview did not explain pending retry.");
                var planGrid = (DataGrid)window.FindName("PlanGrid");
                if (planGrid.Items.Count != 1) throw new InvalidOperationException("Locked file disappeared from preview.");
            }
            Console.WriteLine("PASS rename and locked-file preview states");
            File.WriteAllText(Path.Combine(target, "example.txt"), "External edit");
            Click(((Button)window.FindName("PreviewButton")));
            WaitIdle(window);
            var conflictGrid = (DataGrid)window.FindName("PlanGrid");
            conflictGrid.SelectedIndex = 0;
            var compared = false;
            var compareTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            compareTimer.Tick += (_, _) =>
            {
                var comparison = app.Windows.OfType<ConflictDetailsWindow>().SingleOrDefault();
                if (comparison is null) return;
                compareTimer.Stop();
                compared = ((Button)comparison.FindName("SourceChoice")).IsEnabled && ((Button)comparison.FindName("KeepChoice")).IsEnabled;
                Click(((Button)comparison.FindName("SkipChoice")));
            };
            compareTimer.Start();
            Click(((Button)window.FindName("CompareButton")));
            if (!compared || File.ReadAllText(Path.Combine(target,"example.txt")) != "External edit")
                throw new InvalidOperationException("Conflict comparison did not preserve files when skipped.");
            Console.WriteLine("PASS side-by-side conflict comparison and skip");
            var resolveButton = (Button)window.FindName("ResolveButton");
            if (!resolveButton.IsEnabled || resolveButton.Visibility != Visibility.Visible)
                throw new InvalidOperationException("Conflict resolution unavailable.");
            confirmTimer.Start();
            Click(resolveButton);
            if (!((Button)window.FindName("CopyButton")).IsEnabled ||
                File.ReadAllText(Path.Combine(target, "example.txt")) != "External edit")
                throw new InvalidOperationException("Resolution must change only the plan.");
            Console.WriteLine("PASS selected conflict resolution changes plan without copying");
            File.WriteAllText(Path.Combine(source, "second.txt"), "Second fictional file");
            Click(((Button)window.FindName("PreviewButton")));
            WaitIdle(window);
            SetText(window, "SourcePathTextBox", source + "-changed");
            if (((Button)window.FindName("CopyButton")).IsEnabled || ((DataGrid)window.FindName("PlanGrid")).Items.Count != 0)
                throw new InvalidOperationException("Stale preview was not invalidated.");
            var taskStore = new AntonsBackupManager.Infrastructure.Storage.JsonBackupTaskStore();
            if (taskStore.Load(Path.Combine(root, "tasks.json")).Single().ConfirmedRunsRemaining != 3)
                throw new InvalidOperationException("Successful copy did not persist safety counter.");
            var changedTarget = Path.Combine(root, "target2");
            Directory.CreateDirectory(changedTarget);
            SetText(window, "SourcePathTextBox", source);
            SetText(window, "DestinationPathTextBox", changedTarget);
            Click(((Button)window.FindName("PreviewButton")));
            WaitIdle(window);
            if (taskStore.Load(Path.Combine(root, "tasks.json")).Single().ConfirmedRunsRemaining != 5)
                throw new InvalidOperationException("Changing folders did not reset safety counter.");
            var automaticSource = Path.Combine(root, "automatic-source");
            var automaticTarget = Path.Combine(root, "automatic-target");
            Directory.CreateDirectory(automaticSource);
            Directory.CreateDirectory(automaticTarget);
            File.WriteAllText(Path.Combine(automaticSource, "automatic.txt"), "Automatic fictional data");
            var automaticTask = new AntonsBackupManager.Core.Tasks.BackupTaskDefinition(Guid.NewGuid(), "Automatic demo",
                automaticSource, automaticTarget, 5, true, 1, SafetyModeEnabled: false);
            var automaticResult = new TaskCompletionSource<BackgroundSyncResult>();
            var automaticPoller = new TaskPoller(
                new AntonsBackupManager.Infrastructure.Storage.SynchronizationService(Path.Combine(root, "automatic-sync")),
                () => [automaticTask],
                results => automaticResult.TrySetResult(results.Single()),
                _ => { }, reportWriter: new AntonsBackupManager.Infrastructure.Storage.RunReportWriter(Path.Combine(root,"automatic-reports")));
            automaticPoller.RunEnabledTasksNow();
            var automaticRun = WaitForTask(automaticResult.Task);
            if (automaticRun.Error is not null || automaticRun.SynchronizedFiles != 1 ||
                File.ReadAllText(Path.Combine(automaticTarget, "automatic.txt")) != "Automatic fictional data")
                throw new InvalidOperationException("Enabled task did not synchronize automatically.");
            Console.WriteLine("PASS enabled task synchronizes automatically");
            var autoHistory = new AntonsBackupManager.Infrastructure.Storage.JsonBackupRunReportStore().Load(Path.Combine(root,"automatic-reports"), automaticTask.Id);
            if (autoHistory.Count != 1 || autoHistory[0].Trigger != "Automatic" || autoHistory[0].CopiedRelativePaths.Count != 1)
                throw new InvalidOperationException("Automatic run not recorded with copied files.");
            Console.WriteLine("PASS automatic run history");
            automaticPoller.SetPaused(true);
            File.WriteAllText(Path.Combine(automaticSource,"global-pause.txt"), "wait");
            WaitForTask(automaticPoller.RunDue().ContinueWith(_ => true));
            if (File.Exists(Path.Combine(automaticTarget,"global-pause.txt"))) throw new InvalidOperationException("Global pause was ignored.");
            automaticPoller.SetPaused(false);
            var protectedTask = automaticTask with { Id = Guid.NewGuid(), SafetyModeEnabled = true };
            BackgroundSyncResult? protectedResult = null;
            using (var protectedPoller = new TaskPoller(new AntonsBackupManager.Infrastructure.Storage.SynchronizationService(Path.Combine(root,"protected-state")),
                () => [protectedTask], results => protectedResult = results.Single(), _ => { }))
                WaitForTask(protectedPoller.RunDue().ContinueWith(_ => true));
            if (protectedResult?.NeedsApproval != true || File.Exists(Path.Combine(automaticTarget,"global-pause.txt")))
                throw new InvalidOperationException("Safety mode wrote files without approval.");
            Console.WriteLine("PASS global pause and five-run safety gate prevent background writes");
            automaticPoller.Pause(automaticTask.Id);
            File.WriteAllText(Path.Combine(automaticSource, "paused.txt"), "must wait");
            automaticPoller.RunEnabledTasksNow();
            if (automaticPoller.IsRunning || File.Exists(Path.Combine(automaticTarget, "paused.txt")))
                throw new InvalidOperationException("Paused task still runs.");
            automaticPoller.Resume(automaticTask.Id);
            WaitForTask(automaticPoller.RunDue().ContinueWith(_ => true));
            if (!File.Exists(Path.Combine(automaticTarget, "paused.txt")))
                throw new InvalidOperationException("Resumed task did not run.");
            automaticPoller.Dispose();
            var queuedTarget = Path.Combine(root, "queued-target");
            Directory.CreateDirectory(queuedTarget);
            var queuedTask = automaticTask with { Id = Guid.NewGuid(), DestinationDirectory = queuedTarget };
            TaskPoller? queuedPoller = null;
            using (queuedPoller = new TaskPoller(
                new AntonsBackupManager.Infrastructure.Storage.SynchronizationService(Path.Combine(root,"queued-state")),
                () => [automaticTask, queuedTask],
                _ => queuedPoller!.Pause(queuedTask.Id), _ => { }))
            {
                WaitForTask(queuedPoller.RunDue().ContinueWith(_ => true));
                if (Directory.EnumerateFiles(queuedTarget).Any())
                    throw new InvalidOperationException("Queued paused task copied files.");
            }
            using (var gated = new TaskPoller(
                new AntonsBackupManager.Infrastructure.Storage.SynchronizationService(Path.Combine(root,"gated-state")),
                () => [queuedTask], _ => throw new InvalidOperationException("Gate bypassed."), _ => { }, () => false))
                WaitForTask(gated.RunDue().ContinueWith(_ => true));
            if (Directory.EnumerateFiles(queuedTarget).Any()) throw new InvalidOperationException("Busy gate copied files.");
            Console.WriteLine("PASS queued pause and manual/background exclusion");
            Click(((Button)window.FindName("PauseTaskButton")));
            WaitIdle(window);
            if (!taskStore.Load(Path.Combine(root, "tasks.json")).Single().BackgroundCheckEnabled)
                throw new InvalidOperationException("Resume not saved immediately.");
            Click(((Button)window.FindName("PauseTaskButton")));
            WaitIdle(window);
            if (taskStore.Load(Path.Combine(root, "tasks.json")).Single().BackgroundCheckEnabled)
                throw new InvalidOperationException("Pause not saved immediately.");
            Console.WriteLine("PASS pause/resume persists without preview and blocks automatic writes");
            Click(((Button)window.FindName("PauseTaskButton")));
            WaitIdle(window);
            ((ComboBox)window.FindName("IntervalComboBox")).SelectedIndex = 1;
            WaitIdle(window);
            if (taskStore.Load(Path.Combine(root,"tasks.json")).Single().BackgroundCheckIntervalMinutes != 5)
                throw new InvalidOperationException("Interval did not persist immediately.");
            Click(((Button)window.FindName("NewButton")));
            File.WriteAllText(Path.Combine(source, "review-gate.txt"), "Wait for the user");
            WaitForTask(window.PollBackgroundNow().ContinueWith(_ => true));
            if (File.Exists(Path.Combine(changedTarget,"review-gate.txt")))
                throw new InvalidOperationException("Background work interrupted new-task editing.");
            SetText(window, "TaskNameTextBox", "Second task");
            SetText(window, "SourcePathTextBox", automaticSource);
            SetText(window, "DestinationPathTextBox", queuedTarget);
            Click(((Button)window.FindName("PreviewButton")));
            WaitIdle(window);
            if (taskStore.Load(Path.Combine(root,"tasks.json")).Count != 2)
                throw new InvalidOperationException("Could not create another task while an automatic task exists.");
            var reviewedCount = ((DataGrid)window.FindName("PlanGrid")).Items.Count;
            WaitForTask(window.PollBackgroundNow().ContinueWith(_ => true));
            if (reviewedCount == 0 || ((DataGrid)window.FindName("PlanGrid")).Items.Count != reviewedCount)
                throw new InvalidOperationException("Background polling erased the reviewed plan.");
            Console.WriteLine("PASS new task with automatic task present, stable preview, immediate interval saving");
            foreach (var editorLanguage in new[] { 0, 1, 2 })
            {
            ((ComboBox)window.FindName("LanguageBox")).SelectedIndex = editorLanguage;
            foreach (var width in new[] { 900d, 1180d })
            {
                window.Width = width; window.Height = width == 900 ? 700 : 820;
                foreach (var expanded in new[] { false, true })
                {
                    ((Expander)window.FindName("AdvancedExpander")).IsExpanded = expanded;
                    window.UpdateLayout();
                    window.Dispatcher.Invoke(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                    CheckEditorSpacing(window, expanded);
                    if (expanded)
                    {
                        ((ScrollViewer)window.FindName("EditorScroll")).ScrollToTop();
                        Click(((Button)window.FindName("ResetSafetyButton")));
                        window.Dispatcher.Invoke(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                        CheckEditorSpacing(window, true);
                    }
                    if (((DataGrid)window.FindName("PlanGrid")).ActualHeight < 95)
                        throw new InvalidOperationException($"File preview has no usable body: {width}, expanded={expanded}, height={((DataGrid)window.FindName("PlanGrid")).ActualHeight}.");
                    foreach (var name in new[] { "PreviewButton", "CopyButton", "PauseAllButton", "CancelPreviewButton", "PlanGrid" })
                    {
                        var control = (FrameworkElement)window.FindName(name);
                        var bounds = control.TransformToAncestor(window).TransformBounds(new Rect(control.RenderSize));
                        if (bounds.Bottom > window.ActualHeight || control.ActualHeight < 1)
                            throw new InvalidOperationException("Populated or expanded layout clipped: " + name);
                    }
                    var surface = (Grid)window.Content;
                    var bitmap = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96,96,PixelFormats.Pbgra32);
                    bitmap.Render(surface);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var image = File.Create(Path.Combine(output,$"tasks-{editorLanguage}-{width}-{expanded}.png")); encoder.Save(image);
                }
            }
            }
            Console.WriteLine("PASS complete safety explanation after expand/reset and scrollbar gaps in all three languages at both sizes");
            ((Expander)window.FindName("AdvancedExpander")).IsExpanded = false;
            Click(((Button)window.FindName("PauseAllButton")));
            if (!File.Exists(Path.Combine(root,"all-paused.flag")) || ((Button)window.FindName("CopyButton")).IsEnabled)
                throw new InvalidOperationException("Global pause UI did not persist or prevent manual synchronization.");
            Click(((Button)window.FindName("PauseAllButton")));
            if (File.Exists(Path.Combine(root,"all-paused.flag"))) throw new InvalidOperationException("Global resume did not persist.");
            Console.WriteLine("PASS task save, async preview, busy controls, confirmed copy, run report, stale preview invalidation");
            window.Close();
            var reopened = new MainWindow(root) { ShowInTaskbar = false };
            reopened.Show();
            reopened.Dispatcher.Invoke(() => reopened.UpdateLayout(), DispatcherPriority.ApplicationIdle);
            if (((ComboBox)reopened.FindName("LanguageBox")).SelectedIndex != 2)
                throw new InvalidOperationException("Language preference was not restored.");
            reopened.Close();
            Console.WriteLine("PASS language persistence");
            var exitRequested = false;
            var iconNames = new[] { "TrayPaused", "TrayReady", "TrayAttention" }.Concat(Enumerable.Range(0,12).Select(i => $"TraySync{i}")).ToArray();
            var iconVisual = new DrawingVisual();
            var signatures = new HashSet<string>();
            using (var dc = iconVisual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0,0,480,96));
                for (var i = 0; i < iconNames.Length; i++)
                {
                    using var iconStream = Application.GetResourceStream(new Uri($"pack://application:,,,/KaktusBackupSync;component/Assets/{iconNames[i]}.ico"))!.Stream;
                    var frames = BitmapDecoder.Create(iconStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames;
                    var decoded = frames.First(frame => frame.PixelWidth == 32);
                    var rgba = new FormatConvertedBitmap(decoded, PixelFormats.Bgra32, null, 0);
                    var bytes = new byte[32*32*4]; rgba.CopyPixels(bytes,128,0);
                    var bright = Enumerable.Range(0,1024).Count(p => bytes[p*4+3] > 200 && bytes[p*4+1] > 180 && bytes[p*4+2] > 180);
                    if (bright < 15) throw new InvalidOperationException("Icon arrows are cropped or invisible: " + iconNames[i]);
                    if (i >= 3) signatures.Add(Convert.ToBase64String(bytes));
                    dc.DrawImage(decoded, new Rect(i*32,0,32,32));
                    if (i < 5) dc.DrawImage(frames.First(frame => frame.PixelWidth == 64), new Rect(i*64,32,64,64));
                }
            }
            if (signatures.Count != 12) throw new InvalidOperationException("Animation frames are identical.");
            var iconSheet = new RenderTargetBitmap(480,96,96,96,PixelFormats.Pbgra32); iconSheet.Render(iconVisual);
            var iconEncoder = new PngBitmapEncoder(); iconEncoder.Frames.Add(BitmapFrame.Create(iconSheet));
            using (var sheetStream = File.Create(Path.Combine(output,"tray-icons.png"))) iconEncoder.Save(sheetStream);
            Console.WriteLine("PASS all tray icons contain visible arrows and twelve distinct frames");
            using var tray = new TrayController(window, () => exitRequested = true);
            tray.SetState(TrayVisualState.Paused);
            var queuedNotice = new TrayNotification("Portfolio demo", "TrayDeferred", 2);
            var trayTranslations = new[] {
                (Index: 0, Open: "Kaktus öffnen", Exit: "Beenden", Message: "2 Dateien sind belegt. Wiederholung beim nächsten Lauf."),
                (Index: 1, Open: "Open Kaktus", Exit: "Exit", Message: "2 files are busy. Retrying on the next run."),
                (Index: 2, Open: "Открыть Kaktus", Exit: "Закрыть программу", Message: "Занято файлов: 2. Повторим при следующем запуске.") };
            var tooltips = new HashSet<string>();
            foreach (var translation in trayTranslations)
            {
                ((ComboBox)window.FindName("LanguageBox")).SelectedIndex = translation.Index;
                if (tray.MenuLabels[0] != translation.Open || tray.MenuLabels[2] != translation.Exit)
                    throw new InvalidOperationException("Tray menu did not update immediately with the language picker.");
                if (queuedNotice.Message != translation.Message)
                    throw new InvalidOperationException("A pending notification retained the old language.");
                tooltips.Add(tray.TooltipText);
            }
            if (tooltips.Count != 3) throw new InvalidOperationException("Unchanged tray state retained the old tooltip language.");
            tray.Notify(queuedNotice);
            Console.WriteLine("PASS immediate tray menu/tooltip translation and pending notifications in DE/EN/RU");
            tray.SetState(TrayVisualState.Paused);
            tray.SetState(TrayVisualState.Ready);
            tray.SetState(TrayVisualState.Synchronizing);
            WaitForTask(Task.Delay(650).ContinueWith(_ => true));
            if (tray.AnimationTicks < 3) throw new InvalidOperationException("Tray animation timer did not advance.");
            tray.SetState(TrayVisualState.Ready);
            var stoppedTicks = tray.AnimationTicks;
            WaitForTask(Task.Delay(200).ContinueWith(_ => true));
            if (tray.AnimationTicks != stoppedTicks) throw new InvalidOperationException("Animation did not stop after success.");
            Console.WriteLine("PASS tray animation advances and stops on success");
            tray.SetState(TrayVisualState.Paused);
            window.Close();
            if (window.IsVisible || exitRequested)
                throw new InvalidOperationException("Closing should hide to tray without exiting.");
            Console.WriteLine("PASS close hides to tray and notification is accepted");
            if (bindingLog.ToString().Length > 0) throw new InvalidOperationException("WPF binding errors: " + bindingLog);
            Console.WriteLine("PASS view-model bindings without runtime errors");
            return 0;
        }
        finally
        {
            window.Close();
            app.Shutdown();
            PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingTrace);
            // Only this check's generated GUID folder is removed.
            Directory.Delete(root, true);
        }
    }

    private static void CheckLocalizedErrors(string root, string output)
    {
        var portable = Path.Combine(root, "portable-error-demo");
        var runtime = Path.Combine(portable, "runtime");
        var destination = Path.Combine(root, "portable-error-target");
        Directory.CreateDirectory(portable);
        Directory.CreateDirectory(destination);
        var window = new MainWindow(runtime) { Width = 900, Height = 700, ShowInTaskbar = false };
        try
        {
            window.Show();
            var language = (ComboBox)window.FindName("LanguageBox");
            language.SelectedIndex = 2;
            SetText(window, "TaskNameTextBox", "Synthetic portable check");
            SetText(window, "SourcePathTextBox", portable);
            SetText(window, "DestinationPathTextBox", destination);
            Click(((Button)window.FindName("PreviewButton")));
            WaitIdle(window);
            var details = (TextBox)window.FindName("ErrorDetailsText");
            string[] phrases = ["Die Anwendung verändert", "The app changes", "Программа изменяет"];
            var distinctDetails = new HashSet<string>();
            var interruptedReason = new UiText.Reference("IoError");
            foreach (var index in new[] { 2, 0, 1, 2 })
            {
                language.SelectedIndex = index;
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                if (!details.Text.Contains(phrases[index]) || !details.Text.Contains(Path.Combine(runtime, "sync")) ||
                    ((Expander)window.FindName("ErrorDetails")).Visibility != Visibility.Visible ||
                    ((Border)window.FindName("WelcomePanel")).Visibility != Visibility.Collapsed)
                    throw new InvalidOperationException("An existing portable-folder error did not translate or became hidden.");
                distinctDetails.Add(details.Text);
                if (!UiText.Get("Partial", 2, interruptedReason).Contains(UiText.Get("IoError")))
                    throw new InvalidOperationException("Interrupted status retained its old explanation language.");
                foreach (var error in new Exception[] { new IOException("Deutscher interner Fehler"),
                    new UnauthorizedAccessException("Deutscher interner Fehler"), new ArgumentException("Deutscher interner Fehler"),
                    new InvalidOperationException("Deutscher interner Fehler") })
                {
                    var explanation = ErrorPresentation.Details(error);
                    if (explanation.Contains(error.Message) || !explanation.Contains(UiText.Get(ErrorPresentation.SummaryKey(error))))
                        throw new InvalidOperationException("Raw component error leaked through localized guidance.");
                }
            }
            if (distinctDetails.Count != 3 || ((ListBox)window.FindName("SavedTasksListBox")).Items.Count != 0 ||
                ((Button)window.FindName("CopyButton")).IsEnabled || Directory.EnumerateFileSystemEntries(destination).Any())
                throw new InvalidOperationException("Rejected portable task was saved, copied or not translated into three languages.");
            var surface = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)surface.ActualWidth, (int)surface.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual();
            using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(surface.RenderSize));
            bitmap.Render(background);
            bitmap.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(output, "portable-error-ru.png"))) encoder.Save(stream);
            // The editor must recover normally once the user chooses a separate data folder.
            Click(((Button)window.FindName("NewButton")));
            if (((Border)window.FindName("WelcomePanel")).Visibility != Visibility.Visible ||
                ((Expander)window.FindName("ErrorDetails")).Visibility != Visibility.Collapsed)
                throw new InvalidOperationException("Starting a new task left a stale error or blank welcome panel.");
            SetText(window, "SourcePathTextBox", destination);
            var safeTarget = Path.Combine(root, "portable-safe-target");
            Directory.CreateDirectory(safeTarget);
            SetText(window, "DestinationPathTextBox", safeTarget);
            Click(((Button)window.FindName("PreviewButton")));
            WaitIdle(window);
            if (((ListBox)window.FindName("SavedTasksListBox")).Items.Count != 1 ||
                ((Expander)window.FindName("ErrorDetails")).Visibility != Visibility.Collapsed)
                throw new InvalidOperationException("Correcting the folders did not recover task creation.");
            Console.WriteLine("PASS portable-folder error, live DE/EN/RU details, component fallback and corrected task creation");
        }
        finally { window.Close(); }
    }

    private static void CheckEditorSpacing(MainWindow window, bool expanded)
    {
        var scroll = (ScrollViewer)window.FindName("EditorScroll");
        var viewport = (FrameworkElement)scroll.Template.FindName("PART_ScrollContentPresenter", scroll);
        foreach (var name in new[] { "OpenSourceButton", "OpenDestinationButton" })
        {
            var control = (FrameworkElement)window.FindName(name);
            var bounds = control.TransformToAncestor(viewport).TransformBounds(new Rect(control.RenderSize));
            if (viewport.ActualWidth - bounds.Right < 12)
                throw new InvalidOperationException("Folder button touches the scrollbar: " + name);
        }
        if (!expanded) return;
        var hint = (FrameworkElement)window.FindName("SafetyCountTextBlock");
        var hintBounds = hint.TransformToAncestor(viewport).TransformBounds(new Rect(hint.RenderSize));
        if (hintBounds.Top < 0 || hintBounds.Bottom > viewport.ActualHeight - 4)
            throw new InvalidOperationException($"Safety explanation is clipped: top={hintBounds.Top}, bottom={hintBounds.Bottom}, viewport={viewport.ActualHeight}.");
    }

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (button.Command?.CanExecute(button.CommandParameter) == true) button.Command.Execute(button.CommandParameter);
    }

    private static void SetText(MainWindow window, string name, string value)
    {
        var box = (TextBox)window.FindName(name);
        box.SetCurrentValue(TextBox.TextProperty, value);
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private static void WaitIdle(MainWindow window)
    {
        var frame = new DispatcherFrame();
        var watch = Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            if (((Button)window.FindName("PreviewButton")).IsEnabled || watch.Elapsed.TotalSeconds > 20)
                frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        if (watch.Elapsed.TotalSeconds > 20) throw new TimeoutException("UI operation timed out.");
    }

    private sealed class TestFolderActions : IFolderActions
    {
        public string? ChosenFrom { get; private set; }
        public string? Opened { get; private set; }
        public string? Choose(Window owner, string currentPath) { ChosenFrom = currentPath; return null; }
        public void Open(string path) => Opened = path;
    }

    private static T WaitForTask<T>(Task<T> task)
    {
        var deadline = Stopwatch.StartNew();
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) => frame.Continue = !task.IsCompleted && deadline.Elapsed.TotalSeconds < 20;
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        if (!task.IsCompleted) throw new TimeoutException("Asynchronous UI check timed out.");
        return task.GetAwaiter().GetResult();
    }
}
