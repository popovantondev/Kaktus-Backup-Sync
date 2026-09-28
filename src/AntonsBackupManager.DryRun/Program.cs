using AntonsBackupManager.Infrastructure.Planning;
using AntonsBackupManager.Core.Planning;

if (args.Length != 2)
{
    Console.Error.WriteLine("Verwendung: AntonsBackupManager.DryRun <Quelle> <Ziel>");
    return 1;
}

try
{
    var plan = new DryRunService().CreatePlan(args[0], args[1]);

    foreach (var operation in plan.Operations)
    {
        Console.WriteLine($"{operation.Kind}: {operation.RelativePath} — {operation.Reason}");
    }

    Console.WriteLine();
    Console.WriteLine($"Neu: {plan.CopyNewFiles}; Aktualisieren: {plan.Updates}; Unverändert: {plan.UnchangedFiles}; Konflikte: {plan.Conflicts}");
    Console.WriteLine("Dry run abgeschlossen. Es wurden keine Dateien geändert.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Dry run nicht möglich: {exception.Message}");
    return 2;
}
