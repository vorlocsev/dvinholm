namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic/offline source-scope gates; no communication with an
/// adapter or claim of an actual Volvo CEM diagnostic implementation.
/// </summary>
public sealed class VolvoP2CemThirdPartyScannerCoverageTests
{
    private static VolvoP2CemPublishedCoverageAssessment Assess(
        int? year, bool confirmed, VolvoP2CemPublishedModule module,
        VolvoP2CemPublishedFunction function) =>
        VolvoP2CemThirdPartyScannerCoverage.Assess(year, confirmed, module, function);

    [Fact]
    public void SourceAndPdfPageReferencesArePinnedAndBounded()
    {
        var entries = VolvoP2CemThirdPartyScannerCoverage.Entries;
        Assert.Equal(9, entries.Count);
        Assert.Equal("V1.10.001", VolvoP2CemThirdPartyScannerCoverage.ListedSoftwareVersion);
        Assert.Contains("NT530_Volvo_functions.pdf", VolvoP2CemThirdPartyScannerCoverage.Source);
        Assert.All(entries, e => Assert.InRange(e.SourcePdfPage, 1, 189));
        Assert.Equal(9, entries.DistinctBy(e => (e.ModelYear, e.Module, e.Function)).Count());
    }

    [Fact]
    public void Year2005AdvertisesConfigurationReadWithoutExecutableSupport()
    {
        var assessment = Assess(2005, true, VolvoP2CemPublishedModule.Cem,
            VolvoP2CemPublishedFunction.VehicleConfigurationDataReading);
        Assert.Equal(VolvoP2CemPublishedCoverageKind.PublishedScannerListingOnly,
            assessment.Kind);
        Assert.False(assessment.MayReadRealConfiguration);
        Assert.False(assessment.HasVerifiedDiagnosticCommand);
        Assert.False(assessment.MayWriteConfiguration);
        Assert.Contains(VolvoP2CemThirdPartyScannerCoverage.Entries,
            e => e.ModelYear == 2005 &&
                 e.PublishedLabel == "Reading data for vehicle configuration" &&
                 e.SourcePdfPage == 184);
    }

    [Fact]
    public void ImmobilizerMenuDoesNotGivePinOrCredentials()
    {
        var assessment = Assess(2005, true, VolvoP2CemPublishedModule.Cem,
            VolvoP2CemPublishedFunction.ImmobilizerParameterReading);
        Assert.Equal(VolvoP2CemPublishedCoverageKind.PublishedScannerListingOnly,
            assessment.Kind);
        Assert.False(assessment.MayAcquirePin);
        Assert.False(assessment.MayConnectCem);
        Assert.False(assessment.MayReadRealConfiguration);
    }

    [Fact]
    public void Year2007ProgrammedValuesAreNotConfigurationReadMapping()
    {
        Assert.Equal(VolvoP2CemPublishedCoverageKind.PublishedScannerListingOnly,
            Assess(2007, true, VolvoP2CemPublishedModule.Cem,
                VolvoP2CemPublishedFunction.ProgrammedValues).Kind);
        Assert.Equal(VolvoP2CemPublishedCoverageKind.NoListingForExactYearAndModule,
            Assess(2007, true, VolvoP2CemPublishedModule.Cem,
                VolvoP2CemPublishedFunction.VehicleConfigurationDataReading).Kind);
        Assert.Contains("NOT equivalent",
            VolvoP2CemThirdPartyScannerCoverage.BuildOfflineOverview());
    }

    [Fact]
    public void Year2008IsExplicitlySeparateFromYear2005()
    {
        Assert.Equal(VolvoP2CemPublishedCoverageKind.PublishedScannerListingOnly,
            Assess(2008, true, VolvoP2CemPublishedModule.Cem,
                VolvoP2CemPublishedFunction.ProgrammedValues).Kind);
        Assert.Equal(VolvoP2CemPublishedCoverageKind.NoListingForExactYearAndModule,
            Assess(2008, true, VolvoP2CemPublishedModule.Cem,
                VolvoP2CemPublishedFunction.ImmobilizerParameterReading).Kind);
        Assert.Contains(VolvoP2CemThirdPartyScannerCoverage.Entries,
            e => e.ModelYear == 2008 && e.Module == VolvoP2CemPublishedModule.CemHs);
    }

    [Fact]
    public void CemHsIsSeparateFromCemAndNeverBecomesBusAddress()
    {
        Assert.Equal(VolvoP2CemPublishedCoverageKind.PublishedScannerListingOnly,
            Assess(2007, true, VolvoP2CemPublishedModule.CemHs,
                VolvoP2CemPublishedFunction.ControlUnitIdentification).Kind);
        Assert.Equal(VolvoP2CemPublishedCoverageKind.NoListingForExactYearAndModule,
            Assess(2007, true, VolvoP2CemPublishedModule.CemHs,
                VolvoP2CemPublishedFunction.ProgrammedValues).Kind);
        var report = VolvoP2CemThirdPartyScannerCoverage.BuildOfflineOverview();
        Assert.Contains("SEPARATE listed module", report);
        Assert.Contains("NOT a synonym or confirmed bus address", report);
    }

    [Fact]
    public void UnconfirmedAndUnlistedYearsFailClosedWithoutInferences()
    {
        foreach (var year in new int?[] { null, 2005, 2007 })
            Assert.Equal(VolvoP2CemPublishedCoverageKind.ModelYearNotConfirmed,
                Assess(year, false, VolvoP2CemPublishedModule.Cem,
                    VolvoP2CemPublishedFunction.ControlUnitIdentification).Kind);
        foreach (var year in new int?[] { 2002, 2006, 2014, 2099 })
            Assert.Equal(VolvoP2CemPublishedCoverageKind.NoListingForExactYearAndModule,
                Assess(year, true, VolvoP2CemPublishedModule.Cem,
                    VolvoP2CemPublishedFunction.VehicleConfigurationDataReading).Kind);
    }

    [Fact]
    public void EntireListingIsImmutableOfflineAndNeverGrantsAnyCapability()
    {
        var entries = Assert.IsAssignableFrom<IList<VolvoP2CemPublishedFunctionEntry>>(
            VolvoP2CemThirdPartyScannerCoverage.Entries);
        Assert.True(entries.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => entries.Clear());

        foreach (var e in entries)
        {
            var result = Assess(e.ModelYear, true, e.Module, e.Function);
            Assert.False(result.MayConnectCem);
            Assert.False(result.HasVerifiedRequestOrResponseCanId);
            Assert.False(result.HasVerifiedDiagnosticCommand);
            Assert.False(result.MayReadRealConfiguration);
            Assert.False(result.MayAcquirePin);
            Assert.False(result.MayWriteConfiguration);
        }

        var report = VolvoP2CemThirdPartyScannerCoverage.BuildOfflineOverview();
        Assert.Contains("OFFLINE", report);
        Assert.Contains("No function can be called", report);
        Assert.Contains("No PIN/security access", report);
    }
}
