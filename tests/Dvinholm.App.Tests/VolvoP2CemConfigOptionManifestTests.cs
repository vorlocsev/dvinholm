namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic checks of the future P2 configuration editor's
/// source-separated offline option definitions. No J2534, PIN, CAN requests,
/// firmware claims or physical ECU actions are involved.
/// </summary>
public sealed class VolvoP2CemConfigOptionManifestTests
{
    [Fact]
    public void ExactlyFourteenDistinctCanonicalOptionsAreRegistered()
    {
        var entries = VolvoP2CemConfigOptionManifest.Entries;
        Assert.Equal(14, entries.Count);
        Assert.Equal(entries.Count, entries.Select(x => x.CanonicalId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(entries, x => Assert.False(string.IsNullOrWhiteSpace(x.DisplayName)));
    }

    [Fact]
    public void EveryVendorOptionRetainsItsIndependentCanonicalIdentifier()
    {
        var manifest = VolvoP2CemConfigOptionManifest.Entries
            .Where(x => x.Provenance == VolvoP2CemOptionProvenance.ThirdPartyAdvertisement)
            .ToArray();
        Assert.Equal(8, manifest.Length);
        foreach (var candidate in VolvoP2ConfigOptionCatalog.Entries)
        {
            var entry = Assert.Single(manifest, x => x.CanonicalId == candidate.CanonicalId);
            Assert.Equal(candidate.Group.ToString(), entry.Category);
            Assert.Equal(candidate.Source, entry.EvidenceReference);
            Assert.Equal(candidate.DisplayName, entry.DisplayName);
            Assert.Equal(candidate.SafetyOrComplianceReviewRequired,
                entry.AdditionalSafetyOrEquipmentReviewRequired);
        }
    }

    [Fact]
    public void VendorAdvertisingYearsAreClaimsNotVerifiedCompatibility()
    {
        var entries = VolvoP2CemConfigOptionManifest.Entries
            .Where(x => x.Provenance == VolvoP2CemOptionProvenance.ThirdPartyAdvertisement);
        Assert.All(entries, x =>
        {
            Assert.Equal(2006, x.ClaimedModelYearFrom);
            Assert.Equal(2014, x.ClaimedModelYearThrough);
            Assert.Contains("vendor advertisement", x.EvidenceReference);
            Assert.False(x.HasVerifiedPhysicalIdentity);
        });
    }

    [Fact]
    public void AllSixVolvoMenuLabelsRetainManufacturerSourceWithoutInventedYears()
    {
        var original = VolvoXc90OemCemMenuCatalog.Entries;
        var manifest = VolvoP2CemConfigOptionManifest.Entries
            .Where(x => x.Provenance == VolvoP2CemOptionProvenance.VolvoPublishedCemServiceMenu)
            .ToArray();
        Assert.Equal(6, manifest.Length);
        foreach (var feature in original)
        {
            var entry = Assert.Single(manifest, x => x.CanonicalId == feature.ResearchId);
            Assert.Equal(feature.Name, entry.DisplayName);
            Assert.Equal(feature.PublicSource, entry.EvidenceReference);
            Assert.Null(entry.ClaimedModelYearFrom);
            Assert.Null(entry.ClaimedModelYearThrough);
            Assert.Null(entry.VerifiedPhysicalModuleOwner);
        }
    }

    [Fact]
    public void NoCandidateHasRawMappingFirmwareAllowedValuesOrExecutableCapability()
    {
        Assert.All(VolvoP2CemConfigOptionManifest.Entries, entry =>
        {
            Assert.Null(entry.VerifiedPhysicalModuleOwner);
            Assert.Null(entry.VerifiedCemHardwareRevision);
            Assert.Null(entry.VerifiedCemSoftwareRevision);
            Assert.Null(entry.VerifiedRawValueMapping);
            Assert.Empty(entry.VerifiedAllowedValues);
            Assert.Empty(entry.VerifiedDependencies);
            Assert.False(entry.HasVerifiedConfigurationReadMapping);
            Assert.False(entry.HasVerifiedConfigurationWriteMapping);
            Assert.False(entry.MayReadVehicleConfiguration);
            Assert.False(entry.MayEditVehicleConfiguration);
            Assert.False(entry.MayAcquirePinOrAuthenticate);
            Assert.False(entry.MayTransmitToVehicle);
        });
    }

    [Fact]
    public void CatalogAndNestedCollectionsAreImmutable()
    {
        var entries = (IList<VolvoP2CemConfigOptionDefinition>)
            VolvoP2CemConfigOptionManifest.Entries;
        var original = entries[0];
        Assert.Throws<NotSupportedException>(() =>
            entries[0] = original with { DisplayName = "forged" });
        Assert.Same(original, VolvoP2CemConfigOptionManifest.Find(original.CanonicalId));
        var values = (IList<string>)original.VerifiedAllowedValues;
        Assert.Throws<NotSupportedException>(() => values.Add("0xFF"));
        var dependencies = (IList<string>)original.VerifiedDependencies;
        Assert.Throws<NotSupportedException>(() => dependencies.Add("unsafe"));
    }

    [Fact]
    public void LookupIsExactAndUnknownOrOversizedIdentifiersDoNotMatch()
    {
        Assert.NotNull(VolvoP2CemConfigOptionManifest.Find("p2.xc90.speed-policy"));
        foreach (var id in new string?[]
        {
            null, "", "P2.XC90.SPEED-POLICY", "xc90.oem.cem.not-a-feature",
            new string('x', 129)
        })
            Assert.Null(VolvoP2CemConfigOptionManifest.Find(id));
        Assert.Equal(14, VolvoP2CemConfigOptionManifest.Entries.Count);
    }

    [Fact]
    public void ExistingOfflineFinderUsesManifestAndNeverPromotesOptions()
    {
        var all = VolvoP2CemCatalogSearch.Find(null, VolvoP2CemCatalogEvidenceTier.All);
        Assert.Equal(14, all.Count);
        Assert.Equal(8, all.Count(x =>
            x.Tier == VolvoP2CemCatalogEvidenceTier.VendorAdvertisement));
        Assert.Equal(6, all.Count(x =>
            x.Tier == VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu));
        foreach (var row in all)
        {
            var entry = Assert.IsType<VolvoP2CemConfigOptionDefinition>(
                VolvoP2CemConfigOptionManifest.Find(row.ResearchId));
            Assert.Equal(entry.DisplayName, row.DisplayName);
            Assert.False(row.MayReadCem);
            Assert.False(row.MayAcquirePin);
            Assert.False(row.MayWriteCem);
        }

        var report = VolvoP2CemCatalogSearch.BuildReadOnlyReport(
            "headlamp washers", VolvoP2CemCatalogEvidenceTier.All);
        Assert.Contains("Structured option: physical owner UNKNOWN", report);
        Assert.Contains("Raw value mapping UNKNOWN", report);
        Assert.Contains("Write: DISABLED", report);
        Assert.Contains("No CAN/J2534 requests", report);
        Assert.DoesNotContain("0x27", report);
    }
}
