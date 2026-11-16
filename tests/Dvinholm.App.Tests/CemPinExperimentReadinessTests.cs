namespace Dvinholm.App.Tests;

/// <summary>
/// Offline blocker evidence. Never sends a frame, exposes candidate bytes or
/// treats source-profile membership as physical CEM compatibility proof.
/// </summary>
public sealed class CemPinExperimentReadinessTests
{
    private static CemReadOnlyPrerequisiteAssessment Ready() =>
        CemReadOnlyPrerequisiteReport.Assess(new(
            true, true, true, true, true, true, true, true));

    private static CemPinExperimentAssessment Assess(
        CemReadOnlyPrerequisiteAssessment? prerequisites = null,
        bool observed = true, bool source = true,
        bool range = true, bool running = false) =>
        CemPinExperimentReadiness.Assess(prerequisites ?? Ready(),
            observed, source, range, running);

    [Fact]
    public void FullPreflightStillDoesNotAuthorizePhysicalPinOrConfigWrites()
    {
        var result = Assess();
        Assert.True(result.AllLocalConditionsMet);
        Assert.Empty(result.Blockers);
        Assert.False(result.HasVerifiedPhysicalCem);
        Assert.False(result.HasVerifiedPin);
        Assert.False(result.MayWriteConfiguration);
        Assert.Contains("Physical CEM identity", CemPinExperimentReadiness.Format(result, true));
        Assert.Contains("не подтверждены", CemPinExperimentReadiness.Format(result, false));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    [InlineData(5, 5)]
    [InlineData(6, 6)]
    [InlineData(7, 7)]
    public void EachOfEightSessionConditionsBlocksLocalPreflight(
        int missing, int expected)
    {
        bool Flag(int index) => index != missing;
        var prereqs = CemReadOnlyPrerequisiteReport.Assess(new(
            Flag(0), Flag(1), Flag(2), Flag(3),
            Flag(4), Flag(5), Flag(6), Flag(7)));
        var result = Assess(prereqs);
        Assert.False(result.AllLocalConditionsMet);
        Assert.Equal((CemPinExperimentBlocker)expected, Assert.Single(result.Blockers));
    }

    [Fact]
    public void MissingCurrentPartObservationCannotBeReplacedByProfileText()
    {
        var result = Assess(observed: false, source: true);
        Assert.Contains(CemPinExperimentBlocker.NoCurrentPartNumber, result.Blockers);
        Assert.False(result.AllLocalConditionsMet);
    }

    [Fact]
    public void UnknownSourceProfileAndInvalidRangeHaveDistinctReasons()
    {
        var result = Assess(source: false, range: false);
        Assert.Contains(CemPinExperimentBlocker.NoSourceProfile, result.Blockers);
        Assert.Contains(CemPinExperimentBlocker.InvalidAttemptRange, result.Blockers);
        Assert.DoesNotContain("0x27", CemPinExperimentReadiness.Format(result, true));
    }

    [Fact]
    public void RunningExperimentCanNeverDisplayAsReady()
    {
        var result = Assess(running: true);
        Assert.False(result.AllLocalConditionsMet);
        Assert.Contains(CemPinExperimentBlocker.AttemptAlreadyRunning, result.Blockers);
        Assert.Contains("выполняется", CemPinExperimentReadiness.Format(result, false));
    }

    [Fact]
    public void InvalidPrerequisiteCollectionsAreRejectedInsteadOfTrusted()
    {
        var forged = new CemReadOnlyPrerequisiteAssessment(
            Array.AsReadOnly(Enumerable.Repeat(
                new CemReadOnlyPrerequisiteCheck(CemReadOnlyPrerequisiteKind.Connected, true), 8)
                .ToArray()));
        Assert.Throws<InvalidDataException>(() => Assess(forged));
        Assert.Throws<InvalidDataException>(() => Assess(
            new CemReadOnlyPrerequisiteAssessment(
                Array.Empty<CemReadOnlyPrerequisiteCheck>())));
    }

    [Fact]
    public void InvalidOrRepeatedBlockersCannotFormOfficialPreflightText()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CemPinExperimentReadiness.Format(null!, true));
        var repeated = new CemPinExperimentAssessment(Array.AsReadOnly(
            new[] { CemPinExperimentBlocker.Disconnected, CemPinExperimentBlocker.Disconnected }));
        Assert.Throws<InvalidDataException>(() =>
            CemPinExperimentReadiness.Format(repeated, false));
        var alien = new CemPinExperimentAssessment(Array.AsReadOnly(
            new[] { (CemPinExperimentBlocker)99 }));
        Assert.Throws<InvalidDataException>(() =>
            CemPinExperimentReadiness.Format(alien, true));
    }

    [Fact]
    public void NoDriverCommandsOrCredentialFieldsInOfflineAssessment()
    {
        var names = typeof(CemPinExperimentAssessment).GetProperties()
            .Select(property => property.Name).ToArray();
        Assert.DoesNotContain(names, value => value.Contains("PinBytes", StringComparison.Ordinal));
        Assert.All(typeof(CemPinExperimentAssessment).GetProperties(),
            property => Assert.NotEqual(typeof(byte[]), property.PropertyType));
        Assert.Equal(12, Enum.GetValues<CemPinExperimentBlocker>().Length);
    }

    [Fact]
    public void WinUiIsReadOnlyAndLocaleAndSessionRefreshReprojectBlockers()
    {
        static string Source(string path)
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
                 dir is not null; dir = dir.Parent)
            {
                var location = Path.Combine(dir.FullName,
                    path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(location)) return File.ReadAllText(location);
            }
            throw new FileNotFoundException(path);
        }
        var xaml = Source("src/Dvinholm.App/MainWindow.xaml");
        var code = Source("src/Dvinholm.App/MainWindow.CemPin.cs");
        var connection = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("x:Name=\"CemPinExperimentReadinessText\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"CemPinExperimentReadiness\"", xaml);
        Assert.Contains("RefreshCemPinExperimentReadiness();", code);
        Assert.Contains("InspectCemReadOnlyPrerequisites();", code);
        Assert.Contains("RefreshCemPartReadAvailability();", connection);
        Assert.Contains("CemPinProgressPresenter.Format", code);
        var start = code.IndexOf("private void RefreshCemPinExperimentReadiness()", StringComparison.Ordinal);
        var end = code.IndexOf("private void RefreshCemDevelopmentLabels()", start,
            StringComparison.Ordinal);
        Assert.True(start > 0 && end > start);
        var section = code[start..end];
        Assert.DoesNotContain("PassThru", section);
        Assert.DoesNotContain("Send(", section);
        Assert.DoesNotContain("ReadCemPin_Click(", section);
        Assert.DoesNotContain("OpenCemPinStore", section);
    }
}
