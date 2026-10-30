namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic, offline contract tests for the selected research-label
/// card. No vehicle, VirtualCar, driver, CAN, PIN, secrets or config writes.
/// </summary>
public sealed class VolvoP2CemCandidateDetailTests
{
    private static string Detail(string? id,
        VolvoP2CemCatalogEvidenceTier tier = VolvoP2CemCatalogEvidenceTier.All) =>
        VolvoP2CemCandidateDetail.BuildReadOnlyReport(id, tier);

    [Fact]
    public void EveryVendorLabelHasAnAttributedOfflineCard()
    {
        foreach (var item in VolvoP2ConfigOptionCatalog.Entries)
        {
            var report = Detail(item.CanonicalId,
                VolvoP2CemCatalogEvidenceTier.VendorAdvertisement);
            Assert.Contains(item.DisplayName, report);
            Assert.Contains(item.CanonicalId, report);
            Assert.Contains("VendorAdvertisement", report);
            Assert.Contains("third-party vendor advertised", report);
            Assert.Contains("NOT independently verified", report);
        }
        Assert.Equal(8, VolvoP2ConfigOptionCatalog.Entries.Count);
    }

    [Fact]
    public void EveryOemServiceMenuLabelHasIndependentManufacturerSource()
    {
        foreach (var item in VolvoXc90OemCemMenuCatalog.Entries)
        {
            var report = Detail(item.ResearchId,
                VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu);
            Assert.Contains(item.Name, report);
            Assert.Contains(item.ResearchId, report);
            Assert.Contains("VolvoPublishedServiceMenu", report);
            Assert.Contains(VolvoXc90OemCemMenuCatalog.Journal, report);
            Assert.Contains("PDF page: 16", report);
            Assert.DoesNotContain("third-party vendor advertised", report);
        }
        Assert.Equal(6, VolvoXc90OemCemMenuCatalog.Entries.Count);
    }

    [Fact]
    public void VendorYearRangeIsOnlyAnAdvertisingScope()
    {
        var report = Detail("p2.xc90.automatic-locking");
        Assert.Contains("Advertised research scope: MY2006–2014", report);
        Assert.Contains("NOT independently verified for any individual XC90/CEM", report);
        Assert.Contains("Physical CEM identity / firmware: UNKNOWN", report);
        Assert.DoesNotContain("Current vehicle setting: KNOWN", report);
    }

    [Fact]
    public void SafetyAndConditionalEquipmentWarningsArePreservedByTier()
    {
        var speed = Detail("p2.xc90.speed-policy");
        Assert.Contains("safety / legal compliance review is required", speed);
        var maintenance = Detail("p2.xc90.service-interval");
        Assert.DoesNotContain("Further safety / legal compliance review", maintenance);
        var tailgate = Detail("xc90.oem.cem.tailgate-wiper");
        Assert.Contains("Equipment-dependent category", tailgate);
        var homesafe = Detail("xc90.oem.cem.homesafe-lighting");
        Assert.DoesNotContain("Equipment-dependent category", homesafe);
    }

    [Fact]
    public void TierMismatchAndUnknownIdsNeverPromoteAResearchLabel()
    {
        var id = "xc90.oem.cem.three-flash-indicator";
        Assert.Contains("VolvoPublishedServiceMenu", Detail(id));
        var mismatch = Detail(id, VolvoP2CemCatalogEvidenceTier.VendorAdvertisement);
        Assert.Contains("No matching research label", mismatch);
        Assert.DoesNotContain("three-flash-indicator", mismatch);
        Assert.Contains("No matching research label", Detail("xc90.oem.cem.UNKNOWN"));
        Assert.Contains("No matching research label",
            Detail("P2.XC90.SPEED-POLICY"));
    }

    [Fact]
    public void BadIdsAndInvalidTiersCannotBeEchoedOrGrantAccess()
    {
        const string privateValue = "PRIVATE-SECRET-RESEARCH-LABEL";
        foreach (var input in new string?[]
        {
            null, "", " ", privateValue,
            new string('Q', VolvoP2CemCandidateDetail.MaximumResearchIdCharacters + 1),
            "sensitive\nvalue"
        })
        {
            var report = Detail(input);
            Assert.Contains("No matching research label", report);
            Assert.DoesNotContain(privateValue, report);
            Assert.DoesNotContain("sensitive", report);
            Assert.Contains("No CAN/J2534 request", report);
        }
        Assert.Throws<InvalidDataException>(() =>
            Detail("p2.xc90.speed-policy", (VolvoP2CemCatalogEvidenceTier)999));
    }

    [Fact]
    public void AllFourteenCardsExplicitlyDenyUnimplementedVehicleOperations()
    {
        var rows = VolvoP2CemCatalogSearch.Find(null, VolvoP2CemCatalogEvidenceTier.All);
        Assert.Equal(14, rows.Count);
        foreach (var row in rows)
        {
            var report = Detail(row.ResearchId);
            Assert.Contains("Current vehicle setting: UNKNOWN", report);
            Assert.Contains("Value encoding / supported range: UNKNOWN", report);
            Assert.Contains("Diagnostic transport, CAN ID and read mapping: UNKNOWN", report);
            Assert.Contains("Read CEM configuration: DISABLED", report);
            Assert.Contains("PIN retrieval / authorization: DISABLED", report);
            Assert.Contains("Preview or write vehicle configuration: DISABLED", report);
            Assert.Contains("No CAN/J2534/UDS request or ECU change occurs here", report);
        }
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        Assert.Empty(VolvoP2CemParameterEvidenceCatalog.ProductionRecords);
    }

    [Fact]
    public void MainWindowRebuildsSelectedDetailsAndInvalidatesStaleSearchResults()
    {
        var source = ReadMainWindow();
        var start = source.IndexOf("var candidateSearch = new TextBox", StringComparison.Ordinal);
        var end = source.IndexOf("// Preserve the complete legacy notes", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = source[start..end];
        Assert.Contains("candidateDetailPicker.SelectionChanged += (_, _) =>", region);
        Assert.Contains("VolvoP2CemCandidateDetail.BuildReadOnlyReport(", region);
        Assert.Contains("Tag = row.ResearchId", region);
        Assert.Contains("candidateDetailPicker.Items.Clear()", region);
        Assert.Contains("candidateDetailPicker.SelectedIndex = -1", region);
        Assert.Contains("candidateSearch.TextChanged += (_, _) => RefreshCandidates()", region);
        Assert.Contains("candidateEvidenceTier.SelectionChanged += (_, _) => RefreshCandidates()", region);
        Assert.Contains("IsExpanded = false", region);
        Assert.DoesNotContain("_session.", region);
        Assert.DoesNotContain("Connect_Click(", region);
        Assert.DoesNotContain("RunUdsOperationAsync", region);
    }

    private static string ReadMainWindow()
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
