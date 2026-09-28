using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AntonsBackupManager.Core.Tasks;
using AntonsBackupManager.Infrastructure.Execution;
using AntonsBackupManager.Infrastructure.Scanning;
using AntonsBackupManager.Infrastructure.Storage;

internal static class CrashRecoveryCheck
{
    private static readonly string Parent = Path.Combine(Path.GetTempPath(), "KaktusCrashCheck");

    public static int Child(string directory)
    {
        var root = ValidateRoot(directory);
        var task = JsonSerializer.Deserialize<BackupTaskDefinition>(File.ReadAllText(Path.Combine(root, "task.json")))!;
        if (!TaskPathRules.Contains(root, task.SourceDirectory) || !TaskPathRules.Contains(root, task.DestinationDirectory))
            throw new InvalidOperationException("Probe paths escaped their generated root.");
        var service = new SynchronizationService(Path.Combine(root, "state"), new BlockingTransfer(root));
        service.Synchronize(task, service.Preview(task));
        throw new InvalidOperationException("The crash probe was not interrupted.");
    }

    public static int Run()
    {
        foreach (var update in new[] { false, true }) RunCase(update);
        return 0;
    }

    private static void RunCase(bool update)
    {
        var root = ValidateRoot(Path.Combine(Parent, Guid.NewGuid().ToString("N")));
        var source = Path.Combine(root, "source"); var destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(source); Directory.CreateDirectory(destination);
        Process? child = null;
        try
        {
            var task = new BackupTaskDefinition(Guid.NewGuid(), "Synthetic crash recovery", source, destination, 5);
            File.WriteAllText(Path.Combine(root, "task.json"), JsonSerializer.Serialize(task));
            var input = Path.Combine(source, "demo.bin"); var output = Path.Combine(destination, "demo.bin");
            var service = new SynchronizationService(Path.Combine(root, "state"));
            if (update)
            {
                File.WriteAllText(input, "previous intact version");
                service.Synchronize(task, service.Preview(task));
            }
            using (var file = File.Create(input)) file.SetLength(8 * 1024 * 1024);
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardError = true, RedirectStandardOutput = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(typeof(CrashRecoveryCheck).Assembly.Location);
            start.ArgumentList.Add("--copy-crash-probe"); start.ArgumentList.Add(root);
            child = Process.Start(start)!;
            var ready = Path.Combine(root, "ready");
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(ready) && !child.HasExited && deadline.Elapsed < TimeSpan.FromSeconds(15)) Thread.Sleep(50);
            if (!File.Exists(ready) || child.HasExited) throw new InvalidOperationException("The owned crash probe did not reach a partial copy.");
            // Only the Process object created above is terminated; no application lookup.
            child.Kill();
            if (!child.WaitForExit(10000)) throw new TimeoutException("The owned probe did not stop.");
            if (update ? File.ReadAllText(output) != "previous intact version" : File.Exists(output))
                throw new InvalidOperationException("Abrupt termination committed an incomplete destination.");
            if (!Directory.GetFiles(root, "*.pending", SearchOption.AllDirectories).Any())
                throw new InvalidOperationException("Probe did not leave the interrupted staged copy.");
            var retry = service.Preview(task);
            if (retry.Conflicts != 0 || retry.Operations.Any(op => op.RelativePath.Contains(".SyncWork")))
                throw new InvalidOperationException("An abandoned staged copy leaked into the user's plan.");
            service.Synchronize(task, retry);
            if (!FileSnapshotReader.Read("demo.bin", input).HasSameContentMarkerAs(FileSnapshotReader.Read("demo.bin", output)) ||
                Directory.GetFiles(root, "*.pending", SearchOption.AllDirectories).Any())
                throw new InvalidOperationException("Restart did not recover the copy and clean its abandoned data.");
            Console.WriteLine($"PASS actual owned-process termination and restart (update={update})");
        }
        finally
        {
            if (child is not null) { if (!child.HasExited) { child.Kill(); child.WaitForExit(10000); } child.Dispose(); }
            Directory.Delete(ValidateRoot(root), true);
        }
    }

    private static string ValidateRoot(string path)
    {
        var root = Path.GetFullPath(path);
        if (!root.StartsWith(Path.GetFullPath(Parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(Path.GetFileName(root), "N", out _)) throw new InvalidOperationException("Invalid crash-test root.");
        return root;
    }

    private sealed class BlockingTransfer(string root) : IFileTransfer
    {
        public void Copy(string source, string destination, CancellationToken token)
        {
            var progress = source.Equals(Path.Combine(root, "source", "demo.bin"), StringComparison.OrdinalIgnoreCase) ? new BlockingProgress(root) : null;
            new LocalFileTransfer(progress).Copy(source, destination, token);
        }
    }
    private sealed class BlockingProgress(string root) : IProgress<long>
    {
        public void Report(long bytes)
        {
            if (bytes == 0) return;
            File.WriteAllText(Path.Combine(root, "ready"), "synthetic staged copy reached");
            Thread.Sleep(20000);
            throw new IOException("Parent did not interrupt its probe.");
        }
    }
}
