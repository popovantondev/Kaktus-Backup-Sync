using AntonsBackupManager.Core.Planning;

namespace AntonsBackupManager.Core.Tests;

[TestClass]
public sealed class BackupPlanBuilderTests
{
    private static readonly DateTimeOffset FirstVersion = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondVersion = FirstVersion.AddMinutes(5);

    [TestMethod]
    public void Plan_WhenDestinationDoesNotExistAndNoBaseline_CopiesNewFile()
    {
        var result = BackupPlanBuilder.Plan(File("notes.txt", 10, FirstVersion), destination: null, lastSuccessfulDestination: null);

        Assert.AreEqual(PlannedOperationKind.CopyNewFile, result.Kind);
        Assert.IsNotNull(result.ExpectedSource);
        Assert.AreEqual("notes.txt", result.ExpectedSource.RelativePath);
        Assert.AreEqual(10L, result.ExpectedSource.Length);
        Assert.AreEqual(FirstVersion, result.ExpectedSource.LastWriteTimeUtc);
        Assert.IsNull(result.ExpectedDestination);
    }

    [TestMethod]
    public void Plan_WhenSourceAndDestinationMatch_LeavesFileUnchanged()
    {
        var file = File("notes.txt", 10, FirstVersion);

        var result = BackupPlanBuilder.Plan(file, file, file);

        Assert.AreEqual(PlannedOperationKind.Unchanged, result.Kind);
    }

    [TestMethod]
    public void Plan_WhenOnlySourceChangedAfterSuccessfulBackup_UpdatesDestination()
    {
        var baseline = File("notes.txt", 10, FirstVersion);

        var result = BackupPlanBuilder.Plan(File("notes.txt", 20, SecondVersion), baseline, baseline);

        Assert.AreEqual(PlannedOperationKind.UpdateDestinationFile, result.Kind);
    }

    [TestMethod]
    public void Plan_WhenBothFilesDifferWithoutBaseline_ReportsConflict()
    {
        var result = BackupPlanBuilder.Plan(File("notes.txt", 20, SecondVersion), File("notes.txt", 10, FirstVersion), lastSuccessfulDestination: null);

        Assert.AreEqual(PlannedOperationKind.Conflict, result.Kind);
    }

    [TestMethod]
    public void Plan_WhenDestinationChangedAfterSuccessfulBackup_ReportsConflict()
    {
        var baseline = File("notes.txt", 10, FirstVersion);

        var result = BackupPlanBuilder.Plan(baseline, File("notes.txt", 20, SecondVersion), baseline);

        Assert.AreEqual(PlannedOperationKind.Conflict, result.Kind);
    }

    [TestMethod]
    public void PlanAll_WhenSourceContainsNewAndMatchingFiles_ReportsBothOperations()
    {
        var matching = File("matching.txt", 10, FirstVersion);
        var plan = BackupPlanBuilder.PlanAll(
            [matching, File("new.txt", 5, FirstVersion)],
            [matching],
            []);

        Assert.AreEqual(1, plan.CopyNewFiles);
        Assert.AreEqual(1, plan.UnchangedFiles);
        Assert.AreEqual(0, plan.Conflicts);
    }

    private static FileSnapshot File(string path, long length, DateTimeOffset modifiedAt) => new(path, length, modifiedAt);
}
