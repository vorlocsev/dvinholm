namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline gates for six manufacturer-documented XC90 CEM service
/// menu *names*, without byte mappings, PINs, a vehicle or J2534 traffic.
/// </summary>
public sealed class VolvoXc90OemCemMenuCatalogTests
{
    [Fact]
    public void ListsExactlySixDistinctDocumentedXc90CemMenuCategories()
    {
        var entries = VolvoXc90OemCemMenuCatalog.Entries;
        Assert.Equal(6, entries.Count);
        Assert.Equal(6, entries.Select(e => e.ResearchId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(entries, entry => Assert.StartsWith("xc90.oem.cem.", entry.ResearchId));
        Assert.Contains(entries, e => e.Name == "Daytime running lamps");
        Assert.Contains(entries, e => e.Name == "Three-flash turn indicator");
    }

    [Fact]
    public void EachCategoryReferencesExactManufacturerBulletinAndXc90Page()
    {
        foreach (var entry in VolvoXc90OemCemMenuCatalog.Entries)
        {
            Assert.Equal("Volvo Technical Journal 20278.6.11, 2016-09-06",
                entry.ManufacturerDocument);
            Assert.Equal(
                "https://static.nhtsa.gov/odi/tsbs/2016/SB-10087303-5448.pdf",
                entry.PublicSource);
            Assert.Equal(16, entry.PdfPage);
        }
    }

    [Fact]
    public void TailgateWiperAndBixenonCategoryDiscloseConditionalEquipment()
    {
        var entries = VolvoXc90OemCemMenuCatalog.Entries;
        Assert.True(entries.Single(e => e.ResearchId.EndsWith("tailgate-wiper")).ConditionalEquipment);
        Assert.True(entries.Single(e => e.ResearchId.EndsWith("bixenon-lowbeam-position-warning"))
            .ConditionalEquipment);
        Assert.False(entries.Single(e => e.ResearchId.EndsWith("daytime-running-lamps"))
            .ConditionalEquipment);
    }

    [Fact]
    public void KnownManufacturerCategoryDoesNotEstablishVehicleOrFirmwareSupport()
    {
        foreach (var entry in VolvoXc90OemCemMenuCatalog.Entries)
        {
            var result = VolvoXc90OemCemMenuCatalog.Assess(entry.ResearchId);
            Assert.Equal(VolvoXc90OemFeatureEvidenceKind.ManufacturerServiceMenuDocumented,
                result.Kind);
            Assert.Contains("particular model year", result.Explanation);
            Assert.Contains("No byte offset", result.Explanation);
            Assert.All(new[]
            {
                result.IsExactVehicleOrFirmwareCompatible,
                result.HasKnownReadMapping, result.HasKnownWriteMapping,
                result.MayRequestEcu, result.MayEditConfiguration,
                result.MaySearchOrAuthorizePin
            }, enabled => Assert.False(enabled));
        }
    }

    [Fact]
    public void UnknownOrCaseChangedResearchIdentifiersAreRejected()
    {
        foreach (var id in new string?[] { null, "", "xc90.oem.cem.TAILGATE-WIPER",
            "p2.xc90.headlamp-washers", "0x50" })
        {
            var result = VolvoXc90OemCemMenuCatalog.Assess(id);
            Assert.Equal(VolvoXc90OemFeatureEvidenceKind.UnknownFeature, result.Kind);
            Assert.Null(result.Feature);
            Assert.False(result.MayRequestEcu);
            Assert.False(result.MaySearchOrAuthorizePin);
        }
    }

    [Fact]
    public void DocumentedMenuCategoryIsNotAManufacturerFirmwareParameterId()
    {
        Assert.All(VolvoXc90OemCemMenuCatalog.Entries, entry =>
        {
            Assert.DoesNotContain("0x", entry.ResearchId, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DID", entry.Name, StringComparison.OrdinalIgnoreCase);
            Assert.True(entry.Name.Length > 6);
            Assert.NotEmpty(entry.ManufacturerDocument);
        });
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
        Assert.Empty(VolvoP2CemParameterEvidenceCatalog.ProductionRecords);
    }

    [Fact]
    public void EntriesAreImmutableAndNeverOverwriteVendorOnlyCategoryList()
    {
        var original = VolvoXc90OemCemMenuCatalog.Entries[0];
        var list = (IList<VolvoXc90DocumentedCemFeature>)
            VolvoXc90OemCemMenuCatalog.Entries;
        Assert.Throws<NotSupportedException>(() => list[0] = original with
        {
            Name = "Should not edit published source"
        });
        Assert.Same(original, VolvoXc90OemCemMenuCatalog.Entries[0]);
        Assert.Equal(8, VolvoP2ConfigOptionCatalog.Entries.Count);
        Assert.DoesNotContain(VolvoP2ConfigOptionCatalog.Entries,
            x => x.CanonicalId == original.ResearchId);
    }

    [Fact]
    public void OfflinePreviewKeepsOemAndVendorEvidenceVisiblySeparate()
    {
        var overview = VolvoP2ConfigCandidateDisplay.BuildOverview();
        Assert.Contains("SectorV public P2 advertisement", overview);
        Assert.Contains("Volvo-documented XC90 CEM service menu", overview);
        Assert.Contains("Technical Journal 20278.6.11", overview);
        Assert.Contains("https://static.nhtsa.gov/odi/tsbs/2016/SB-10087303-5448.pdf",
            overview);
        Assert.Contains("Daytime running lamps", overview);
        Assert.Contains("Three-flash turn indicator", overview);
        Assert.Contains("manufacturer service-menu names only", overview);
        Assert.Contains("PIN/service authentication: unavailable", overview);
        Assert.Contains("ECU writing: disabled", overview);
        foreach (var e in VolvoXc90OemCemMenuCatalog.Entries)
            Assert.Equal(1, Occurrences(overview, "• OEM: " + e.Name));
    }

    private static int Occurrences(string text, string needle)
    {
        int count = 0, start = 0;
        while (true)
        {
            var index = text.IndexOf(needle, start, StringComparison.Ordinal);
            if (index < 0) return count;
            count++;
            start = index + needle.Length;
        }
    }
}
