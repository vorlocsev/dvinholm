namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline groups. This suite never opens a native driver or sends a
/// diagnostic request and contains no PIN, firmware signature or ECU payload.
/// </summary>
public sealed class CemReadOnlyPrerequisiteReportTests
{
    private static CemReadOnlyPrerequisiteInput Ready() =>
        new(true, true, true, true, true, true, true, true);

    private static CemReadOnlyPrerequisiteAssessment Assess(
        CemReadOnlyPrerequisiteInput input) => CemReadOnlyPrerequisiteReport.Assess(input);

    [Fact]
    public void DisconnectedDefaultDoesNotOfferAnExperimentalRead()
    {
        var result = Assess(new(false, false, false, false, false, false, false, false));
        Assert.Equal(8, result.Checks.Count);
        Assert.All(result.Checks, x => Assert.False(x.Satisfied));
        Assert.False(result.EligibleForSourceDefinedPartReadOnly);
        Assert.False(result.HasAuthenticatedPhysicalCem);
    }

    [Fact]
    public void FullLocalSessionStateStillCannotAuthorizePinOrConfiguration()
    {
        var result = Assess(Ready());
        Assert.True(result.EligibleForSourceDefinedPartReadOnly);
        Assert.All(result.Checks, x => Assert.True(x.Satisfied));
        Assert.False(result.HasAuthenticatedPhysicalCem);
        Assert.False(result.MayAcquirePin);
        Assert.False(result.MayReadConfiguration);
        Assert.False(result.MayWriteConfiguration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void EverySingleMissingConditionBlocksTheExperimentalPartRead(int missing)
    {
        bool Yes(int index) => index != missing;
        var result = Assess(new(Yes(0), Yes(1), Yes(2), Yes(3),
            Yes(4), Yes(5), Yes(6), Yes(7)));
        Assert.False(result.EligibleForSourceDefinedPartReadOnly);
        Assert.Single(result.Checks, item => !item.Satisfied);
        Assert.False(result.Checks[missing].Satisfied);
    }

    [Fact]
    public void ExactlyEightNamedDistinctChecksAreReportedInStableOrder()
    {
        var result = Assess(Ready());
        Assert.Equal(Enum.GetValues<CemReadOnlyPrerequisiteKind>(),
            result.Checks.Select(x => x.Kind));
        Assert.Equal(8, result.Checks.Select(x => x.Kind).Distinct().Count());
    }

    [Fact]
    public void ChecklistCannotBeMutatedToOverrideSessionFailures()
    {
        var result = Assess(new(false, true, true, true, true, true, true, true));
        var list = Assert.IsAssignableFrom<IList<CemReadOnlyPrerequisiteCheck>>(result.Checks);
        Assert.Throws<NotSupportedException>(() => list[0] =
            new(CemReadOnlyPrerequisiteKind.Connected, true));
        Assert.False(result.EligibleForSourceDefinedPartReadOnly);
    }

    [Fact]
    public void LocalizedReportsDiscloseOnlyStatusAndExplicitSafetyLimit()
    {
        var result = Assess(Ready());
        var en = CemReadOnlyPrerequisiteReport.Format(result, english: true);
        var ru = CemReadOnlyPrerequisiteReport.Format(result, english: false);
        Assert.Contains("No request was sent", en);
        Assert.Contains("Запросов к автомобилю не было", ru);
        Assert.Contains("PIN", en);
        Assert.Contains("PIN", ru);
        Assert.Contains("500 kbit/s", en);
        Assert.Contains("500 кбит/с", ru);
        Assert.DoesNotContain("0x27", en);
        Assert.DoesNotContain("PassThruOpen", en);
    }

    [Fact]
    public void ForgedOrIncompleteChecklistIsRefusedByFormatter()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CemReadOnlyPrerequisiteReport.Format(null!, true));
        var forged = new CemReadOnlyPrerequisiteAssessment(
            Array.AsReadOnly(Enumerable.Repeat(
                new CemReadOnlyPrerequisiteCheck(CemReadOnlyPrerequisiteKind.Connected, true), 8)
                .ToArray()));
        Assert.False(forged.EligibleForSourceDefinedPartReadOnly);
        Assert.Throws<InvalidDataException>(() =>
            CemReadOnlyPrerequisiteReport.Format(forged, true));
        var empty = new CemReadOnlyPrerequisiteAssessment(
            Array.Empty<CemReadOnlyPrerequisiteCheck>());
        Assert.False(empty.EligibleForSourceDefinedPartReadOnly);
    }

    [Fact]
    public void SixPageXamlUsesLocalOnlyButtonAndStableDisablesExperimentalRoute()
    {
        string Read(string file)
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
                 dir is not null; dir = dir.Parent)
            {
                var path = Path.Combine(dir.FullName,
                    file.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            throw new FileNotFoundException(file);
        }

        var xaml = Read("src/Dvinholm.App/MainWindow.xaml");
        var handler = Read("src/Dvinholm.App/MainWindow.CemPin.cs");
        var locale = Read("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("x:Name=\"CemPartPrerequisiteButton\"", xaml);
        Assert.Contains("Click=\"CheckCemPrerequisites_Click\"", xaml);
        Assert.Contains("x:Name=\"CemPartPrerequisiteExpander\"", xaml);
        Assert.Contains("IsExpanded=\"False\"", xaml);
        Assert.Contains("CemReadOnlyPrerequisiteReport.Assess(new(", handler);
        Assert.Contains("const bool safeResearchContext = false", handler);
        Assert.Contains("RefreshCemPrerequisitePresentation();", locale);
        var start = handler.IndexOf("private void CheckCemPrerequisites_Click", StringComparison.Ordinal);
        var stop = handler.IndexOf("private void RefreshCemPrerequisitePresentation", start,
            StringComparison.Ordinal);
        Assert.True(start > 0 && stop > start);
        var body = handler[start..stop];
        Assert.DoesNotContain("Task.Run", body);
        Assert.DoesNotContain("ReadCemPartNumber_Click", body);
        Assert.DoesNotContain("NativeLibrary", body);
        Assert.DoesNotContain("Send(", body);
    }
}
