namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline checks. No adapter, VirtualCar, PIN, ECU,
/// J2534 command, file import or physical XC90 is involved.
/// </summary>
public sealed class VolvoP2CemCatalogSearchTests
{
    private static IReadOnlyList<VolvoP2CemCatalogSearchRow> Find(
        string? query = null,
        VolvoP2CemCatalogEvidenceTier tier = VolvoP2CemCatalogEvidenceTier.All) =>
        VolvoP2CemCatalogSearch.Find(query, tier);

    [Fact]
    public void AllResultsRetainEightVendorAndSixManufacturerLabelsWithoutDuplicates()
    {
        var rows = Find();
        Assert.Equal(14, rows.Count);
        Assert.Equal(8, rows.Count(x =>
            x.Tier == VolvoP2CemCatalogEvidenceTier.VendorAdvertisement));
        Assert.Equal(6, rows.Count(x =>
            x.Tier == VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu));
        Assert.Equal(14, rows.Select(x => x.ResearchId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EmptyAndWhitespaceQueryBothShowAllFourteen()
    {
        Assert.Equal(14, Find(string.Empty).Count);
        Assert.Equal(14, Find("   ").Count);
        Assert.Equal(14, Find(null).Count);
        Assert.Equal(8, Find(null, VolvoP2CemCatalogEvidenceTier.VendorAdvertisement).Count);
        Assert.Equal(6, Find(null, VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu).Count);
    }

    [Fact]
    public void VendorCategoryAndCanonicalIdSearchIsCaseInsensitive()
    {
        var byName = Assert.Single(Find("HeAdLaMp Washers",
            VolvoP2CemCatalogEvidenceTier.VendorAdvertisement));
        Assert.Equal("p2.xc90.headlamp-washers", byName.ResearchId);
        var byCategory = Assert.Single(Find("VEHICLESPEEDPOLICY"));
        Assert.Equal("p2.xc90.speed-policy", byCategory.ResearchId);
        Assert.Equal(byName.ResearchId,
            Assert.Single(Find("P2.XC90.HEADLAMP-WASHERS")).ResearchId);
    }

    [Fact]
    public void OemLabelsNeverBecomeVendorVerifiedOptions()
    {
        var oem = Assert.Single(Find("three-flash turn",
            VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu));
        Assert.StartsWith("xc90.oem.cem.", oem.ResearchId);
        Assert.Contains("no vehicle-year or revision proof", oem.ClaimedScope);
        Assert.Empty(Find("three-flash turn",
            VolvoP2CemCatalogEvidenceTier.VendorAdvertisement));
        Assert.Empty(Find("parking assistance",
            VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu));
    }

    [Fact]
    public void MultipleSearchTokensMustAllMatchVisibleResearchFields()
    {
        Assert.Single(Find("service interval"));
        Assert.Empty(Find("service headlamp"));
        var oem = Find("XC90 service", VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu);
        Assert.Equal(6, oem.Count);
        var vendor = Assert.Single(Find("XC90 service",
            VolvoP2CemCatalogEvidenceTier.VendorAdvertisement));
        Assert.Equal("p2.xc90.service-interval", vendor.ResearchId);
    }

    [Fact]
    public void MalformedOrUnboundedQueriesFailClosedAndDoNotEchoUserInput()
    {
        Assert.Throws<InvalidDataException>(() => Find(new string('x', 81)));
        Assert.Throws<InvalidDataException>(() => Find("line\nsecret"));
        Assert.Throws<InvalidDataException>(() =>
            Find(null, (VolvoP2CemCatalogEvidenceTier)1234));
        const string privateText = "PRIVATE-SENSITIVE-INPUT-123";
        var report = VolvoP2CemCatalogSearch.BuildReadOnlyReport(
            privateText, VolvoP2CemCatalogEvidenceTier.All);
        Assert.DoesNotContain(privateText, report);
        Assert.Contains("No matching research labels", report);
    }

    [Fact]
    public void ResultsAreImmutableAndCannotEnableAnyVehicleCapabilities()
    {
        var rows = Find();
        var mutable = Assert.IsAssignableFrom<IList<VolvoP2CemCatalogSearchRow>>(rows);
        Assert.Throws<NotSupportedException>(() =>
            mutable[0] = mutable[0] with { DisplayName = "forged" });
        Assert.All(rows, r =>
        {
            Assert.False(r.HasAuthenticatedVehicleOrFirmware);
            Assert.False(r.MayReadCem);
            Assert.False(r.MayAcquirePin);
            Assert.False(r.MayWriteCem);
        });
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        Assert.Empty(VolvoP2CemParameterEvidenceCatalog.ProductionRecords);
    }

    [Fact]
    public void UiUsesEphemeralLiveSearchTierAndKeepsOriginalDetailsCollapsed()
    {
        var report = VolvoP2CemCatalogSearch.BuildReadOnlyReport(
            "locking", VolvoP2CemCatalogEvidenceTier.All);
        Assert.Contains("OFFLINE", report);
        Assert.Contains("Write: DISABLED", report);
        Assert.Contains("No CAN/J2534 requests", report);
        Assert.DoesNotContain("7E0", report);
        Assert.DoesNotContain("0x27", report);
        Assert.True(report.Length < 4000);

        var ui = ReadUi();
        Assert.Contains("candidateSearch.TextChanged += (_, _) => RefreshCandidates()", ui);
        Assert.Contains("candidateEvidenceTier.SelectionChanged += (_, _) => RefreshCandidates()", ui);
        Assert.Contains("VolvoP2CemCatalogSearch.BuildReadOnlyReport(", ui);
        Assert.Contains("new Expander", ui);
        Assert.Contains("IsExpanded = false", ui);
        Assert.Contains("VolvoP2ConfigCandidateDisplay.BuildOverview()", ui);
    }

    private static string ReadUi()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException("MainWindow.xaml.cs");
    }
}
