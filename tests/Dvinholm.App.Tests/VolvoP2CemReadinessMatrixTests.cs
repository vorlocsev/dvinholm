namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contract checks for a source-limited P2/CEM research matrix.
/// No virtual or physical adapter, ECU, credential, CAN frame or DID is sent.
/// </summary>
public sealed class VolvoP2CemReadinessMatrixTests
{
    private static VolvoP2CemReadinessAssessment Review(
        int? year = 2007, bool checkedYear = true,
        VolvoP2NetworkKind network = VolvoP2NetworkKind.HighSpeedCan,
        uint? rate = 500_000,
        VolvoP2CemDiagnosticInterface adapter = VolvoP2CemDiagnosticInterface.DiCE) =>
        VolvoP2CemReadinessMatrix.Assess(year, checkedYear, network, rate, adapter);

    private static VolvoP2CemReadinessRow Row(
        VolvoP2CemReadinessAssessment review, VolvoP2CemReadinessDimension kind) =>
        Assert.Single(review.Rows, x => x.Dimension == kind);

    [Fact]
    public void AllEightDimensionsAreUniqueAndOnlyResearchMetadata()
    {
        var review = Review();
        Assert.Equal(8, review.Rows.Count);
        Assert.Equal(8, review.Rows.Select(x => x.Dimension).Distinct().Count());
        Assert.False(review.CanConnectToCem);
        Assert.False(review.CanReadConfiguration);
        Assert.False(review.CanGenerateRecoverableBackup);
        Assert.False(review.CanAcquireOrAuthorizePin);
        Assert.False(review.CanWriteConfiguration);
    }

    [Fact]
    public void ManufacturerP2ScopeIsNotAPhysicalModuleIdentity()
    {
        foreach (var year in new[] { 2003, 2014 })
        {
            var review = Review(year);
            Assert.Equal(VolvoP2CemReadinessStatus.PublishedReferenceOnly,
                Row(review, VolvoP2CemReadinessDimension.Xc90P2ModelYear).Status);
            Assert.Equal(VolvoP2CemReadinessStatus.Unconfirmed,
                Row(review, VolvoP2CemReadinessDimension.CemHardwareSoftwareIdentity).Status);
        }
        foreach (var year in new[] { 2002, 2015 })
            Assert.Equal(VolvoP2CemReadinessStatus.OutsideFirstGenerationScope,
                Row(Review(year), VolvoP2CemReadinessDimension.Xc90P2ModelYear).Status);
    }

    [Fact]
    public void UnknownOrUncheckedModelYearCannotConfirmAnyRate()
    {
        foreach (var review in new[] { Review(null), Review(2007, false), Review(2002) })
        {
            Assert.Equal(VolvoP2CemReadinessStatus.Unconfirmed,
                Row(review, VolvoP2CemReadinessDimension.HighSpeedCanReference).Status);
            Assert.False(review.CanConnectToCem);
        }
    }

    [Fact]
    public void HistoricalHsCanYearBoundaryIsOnlyAReference()
    {
        Assert.Equal(VolvoP2CemReadinessStatus.PublishedReferenceOnly,
            Row(Review(2004, rate: 250_000), VolvoP2CemReadinessDimension.HighSpeedCanReference).Status);
        Assert.Equal(VolvoP2CemReadinessStatus.PublishedReferenceOnly,
            Row(Review(2005, rate: 500_000), VolvoP2CemReadinessDimension.HighSpeedCanReference).Status);
        Assert.False(Review(2005).CanConnectToCem);
    }

    [Fact]
    public void WrongHsCanSpeedShowsConflictAndOtherBusesCannotInheritIt()
    {
        Assert.Equal(VolvoP2CemReadinessStatus.ConflictsWithPublishedReference,
            Row(Review(2004, rate: 500_000),
                VolvoP2CemReadinessDimension.HighSpeedCanReference).Status);
        foreach (var network in new[] { VolvoP2NetworkKind.Unknown,
                    VolvoP2NetworkKind.LowSpeedCan, VolvoP2NetworkKind.OtherOrKLine })
            Assert.Equal(VolvoP2CemReadinessStatus.Unconfirmed,
                Row(Review(network: network), VolvoP2CemReadinessDimension.HighSpeedCanReference).Status);
    }

    [Fact]
    public void J2534OneCannotBePromotedToVidaDiagnosticCapability()
    {
        var review = Review(adapter: VolvoP2CemDiagnosticInterface.J2534_1);
        Assert.Equal(VolvoP2CemReadinessStatus.NotSupportedForVidaDiagnostics,
            Row(review, VolvoP2CemReadinessDimension.VidaCommunicationInterface).Status);
        Assert.False(review.CanReadConfiguration);
    }

    [Fact]
    public void DiceAndJ2534TwoRemainGeneralGuidanceNotCemAcceptance()
    {
        foreach (var adapter in new[] { VolvoP2CemDiagnosticInterface.DiCE,
                                        VolvoP2CemDiagnosticInterface.J2534_2 })
        {
            var review = Review(adapter: adapter);
            Assert.Equal(VolvoP2CemReadinessStatus.PublishedReferenceOnly,
                Row(review, VolvoP2CemReadinessDimension.VidaCommunicationInterface).Status);
            Assert.Equal(VolvoP2CemReadinessStatus.Unconfirmed,
                Row(review, VolvoP2CemReadinessDimension.CemDiagnosticTransportAndAddresses).Status);
            Assert.False(review.CanConnectToCem);
        }
    }

    [Fact]
    public void ReportIsBoundedStaticAndNeverContainsGuessedIdsPinOrConfiguration()
    {
        var text = VolvoP2CemReadinessMatrix.BuildOfflineReport(
            2007, true, VolvoP2NetworkKind.HighSpeedCan, 500_000,
            VolvoP2CemDiagnosticInterface.DiCE);
        Assert.Contains("OFFLINE", text);
        Assert.Contains("BLOCKED", text);
        Assert.Contains("DID snapshot is NOT a restorable configuration backup", text);
        Assert.DoesNotContain("7E0", text);
        Assert.DoesNotContain("7E8", text);
        Assert.DoesNotContain("0x27", text);
        Assert.DoesNotContain("security seed", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(text.Length < 5000);

        var ui = ReadAppWindow();
        Assert.Contains("VolvoP2CemReadinessMatrix.BuildOfflineReport", ui);
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        Assert.Empty(VolvoP2CemParameterEvidenceCatalog.ProductionRecords);
    }

    private static string ReadAppWindow()
    {
        for (DirectoryInfo? path = new(AppContext.BaseDirectory);
             path is not null; path = path.Parent)
        {
            var candidate = Path.Combine(path.FullName, "src",
                "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
        }
        throw new FileNotFoundException("MainWindow.xaml.cs source unavailable");
    }
}
