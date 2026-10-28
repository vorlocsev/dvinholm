namespace Dvinholm.App.Tests;

/// <summary>Eight source-accuracy and fail-closed offline tests for MY2007.</summary>
public sealed class VolvoP2CemReadSurfaceEvidenceTests
{
    private static VolvoP2CemReadSurfaceAssessment Review(
        int? year, VolvoP2CemReadSurface surface) =>
        VolvoP2CemReadSurfaceEvidence.Assess(year, surface);

    [Fact]
    public void CatalogHasFiveDistinctReadSurfacesAndFixedPublishedExampleYear()
    {
        var refs = VolvoP2CemReadSurfaceEvidence.Entries;
        Assert.Equal(5, refs.Count);
        Assert.Equal(5, refs.Select(x => x.Surface).Distinct().Count());
        Assert.All(refs, x => Assert.Equal(2007, x.ExampleModelYear));
        var mutable = Assert.IsAssignableFrom<IList<VolvoP2CemReadSurfaceReference>>(refs);
        Assert.Throws<NotSupportedException>(() => mutable[0] = mutable[1]);
    }

    [Fact]
    public void ExactExampleYearIsOnlyWorkshopFunctionReference()
    {
        foreach (var source in VolvoP2CemReadSurfaceEvidence.Entries)
        {
            var result = Review(2007, source.Surface);
            Assert.Equal(VolvoP2CemReadSurfaceEvidenceStatus.WorkshopFunctionReferenceOnly,
                result.Status);
            Assert.False(result.HasAuthenticatedPhysicalModule);
            Assert.False(result.HasRevisionScopedWireEncoding);
            Assert.False(result.HasDocumentedDiagnosticAddressing);
        }
    }

    [Fact]
    public void OtherYearsMustNotInheritAnExactExampleProtocolOrFeature()
    {
        foreach (var year in new[] { 2003, 2005, 2006, 2008, 2013, 2014 })
            Assert.All(VolvoP2CemReadSurfaceEvidence.Entries, source =>
                Assert.Equal(VolvoP2CemReadSurfaceEvidenceStatus.OutsideExactPublishedExampleYear,
                    Review(year, source.Surface).Status));
    }

    [Fact]
    public void NullOrOutOfPlatformYearHasNoUsableSourceMatch()
    {
        foreach (int? year in new int?[] { null, 2002, 2015, 0 })
            Assert.All(VolvoP2CemReadSurfaceEvidence.Entries, source =>
                Assert.Equal(VolvoP2CemReadSurfaceEvidenceStatus.NoUsableClaimedYear,
                    Review(year, source.Surface).Status));
    }

    [Fact]
    public void IdentityLiveFrozenCustomerAndVehicleFileStaySeparate()
    {
        var refs = VolvoP2CemReadSurfaceEvidence.Entries;
        Assert.Contains(refs, x => x.Surface == VolvoP2CemReadSurface.ControlModuleIdentity);
        Assert.Contains(refs, x => x.Surface == VolvoP2CemReadSurface.LiveDiagnosticParameter);
        Assert.Contains(refs, x => x.Surface == VolvoP2CemReadSurface.FrozenDiagnosticContext);
        Assert.Contains(refs, x => x.Surface == VolvoP2CemReadSurface.CustomerProgrammedData);
        Assert.Contains(refs, x => x.Surface == VolvoP2CemReadSurface.VehicleConfigurationFile);
        Assert.True(Review(2007, VolvoP2CemReadSurface.VehicleConfigurationFile)
            .Reference.MayContainVehicleIdentity);
        Assert.False(Review(2007, VolvoP2CemReadSurface.CustomerProgrammedData)
            .Reference.MayContainVehicleIdentity);
    }

    [Fact]
    public void EveryResearchCategoryCannotEnableLiveReadPinOrWrites()
    {
        foreach (var source in VolvoP2CemReadSurfaceEvidence.Entries)
        {
            var assessment = Review(2007, source.Surface);
            Assert.False(assessment.MayReadLiveCemData);
            Assert.False(assessment.IsRestorableBackup);
            Assert.False(assessment.MayAcquirePin);
            Assert.False(assessment.MayWriteConfiguration);
        }
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        Assert.Empty(VolvoP2CemParameterEvidenceCatalog.ProductionRecords);
    }

    [Fact]
    public void UndefinedReadCategoryFailsClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Review(2007, (VolvoP2CemReadSurface)999));
    }

    [Fact]
    public void OfflineReportAndUiKeepDocumentedExamplesSeparateFromLiveProtocol()
    {
        var report = VolvoP2CemReadSurfaceEvidence.BuildOfflineReport(2007);
        Assert.Contains("FIVE DIFFERENT", report);
        Assert.Contains("MISSING", report);
        Assert.Contains("DISABLED", report);
        Assert.Contains("identifying data", report);
        Assert.DoesNotContain("7E0", report);
        Assert.DoesNotContain("7E8", report);
        Assert.DoesNotContain("0x27", report);
        Assert.True(report.Length < 3000);
        var ui = FindUi();
        Assert.Contains("VolvoP2CemReadSurfaceEvidence", ui);
        Assert.Contains(".BuildOfflineReport(manual.ClaimedModelYear)", ui);
    }

    private static string FindUi()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("MainWindow.xaml.cs");
    }
}
