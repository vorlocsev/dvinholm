namespace Dvinholm.App.Tests;

/// <summary>
/// Eight entirely offline CEM research-browser checks: no PassThru, PIN,
/// vehicle, raw parameter values, authorization or stateful write operations.
/// </summary>
public sealed class AuroraCemConfigReferenceFilterTests
{
    private static IReadOnlyList<VolvoP2CemConfigOptionDefinition> Find(
        string? query = null, string? category = null,
        VolvoP2CemCatalogEvidenceTier tier = VolvoP2CemCatalogEvidenceTier.All) =>
        AuroraCemConfigReferenceFilter.Find(query, category, tier);

    [Fact]
    public void UnfilteredReferenceShowsExactlyFourteenAttributedLabels()
    {
        Assert.Equal(14, Find().Count);
        Assert.Equal(8, Find(tier: VolvoP2CemCatalogEvidenceTier.VendorAdvertisement).Count);
        Assert.Equal(6, Find(tier: VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu).Count);
        Assert.Equal(14, Find().Select(r => r.CanonicalId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void SearchUsesAllTokensAndIgnoresCase()
    {
        Assert.Equal("p2.xc90.headlamp-washers",
            Assert.Single(Find("HEADLAMP washers")).CanonicalId);
        Assert.Empty(Find("headlamp service"));
        Assert.Equal(14, Find("   ").Count);
    }

    [Fact]
    public void CategoryFilterIsExactAndCannotConflateCategories()
    {
        Assert.Equal("p2.xc90.speed-policy",
            Assert.Single(Find(category: "VehicleSpeedPolicy")).CanonicalId);
        Assert.Equal(6, Find(category: "XC90 CEM service menu").Count);
        Assert.Empty(Find(category: "vehicleSpeedPolicy"));
        Assert.Empty(Find(category: "unlisted"));
    }

    [Fact]
    public void EvidenceTierAndCategoryAreAppliedTogether()
    {
        Assert.Empty(Find(category: "VehicleSpeedPolicy",
            tier: VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu));
        Assert.Empty(Find(category: "XC90 CEM service menu",
            tier: VolvoP2CemCatalogEvidenceTier.VendorAdvertisement));
        Assert.Equal(6, Find(category: "XC90 CEM service menu",
            tier: VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu).Count);
    }

    [Fact]
    public void NoMatchCannotBeInterpretedAsCompatibleOrDefaultEnabled()
    {
        Assert.Empty(Find("no real vehicle value 999"));
        Assert.Empty(Find("headlamp washers", "XC90 CEM service menu"));
        Assert.All(Find(), r =>
        {
            Assert.Null(r.VerifiedPhysicalModuleOwner);
            Assert.Null(r.VerifiedRawValueMapping);
            Assert.Empty(r.VerifiedAllowedValues);
            Assert.Empty(r.VerifiedDependencies);
        });
    }

    [Fact]
    public void BadTextOrUnknownTierFailsClosedWithoutEchoingInput()
    {
        Assert.Throws<InvalidDataException>(() => Find(new string('x', 81)));
        Assert.Throws<InvalidDataException>(() => Find("secret\ninput"));
        Assert.Throws<InvalidDataException>(() => Find(category: new string('c', 81)));
        Assert.Throws<InvalidDataException>(() => Find(category: "private\ttext"));
        Assert.Throws<InvalidDataException>(() => Find(tier: (VolvoP2CemCatalogEvidenceTier)100));
    }

    [Fact]
    public void FilteredResultsAreImmutableAndNeverGrantVehicleCapabilities()
    {
        var rows = Find();
        var asList = Assert.IsAssignableFrom<IList<VolvoP2CemConfigOptionDefinition>>(rows);
        Assert.Throws<NotSupportedException>(() => asList[0] = rows[1]);
        Assert.All(rows, r =>
        {
            Assert.False(r.HasVerifiedPhysicalIdentity);
            Assert.False(r.MayReadVehicleConfiguration);
            Assert.False(r.MayEditVehicleConfiguration);
            Assert.False(r.MayAcquirePinOrAuthenticate);
            Assert.False(r.MayTransmitToVehicle);
        });
    }

    [Fact]
    public void ResultIdsAndProvenanceComeFromOriginalManifestOnly()
    {
        var allowed = VolvoP2CemConfigOptionManifest.Entries.ToDictionary(
            x => x.CanonicalId, StringComparer.Ordinal);
        Assert.True(Find().Count <= AuroraCemConfigReferenceFilter.MaximumRows);
        Assert.All(Find(), row =>
        {
            Assert.True(allowed.ContainsKey(row.CanonicalId));
            Assert.Same(allowed[row.CanonicalId], row);
        });
        Assert.All(Find(tier: VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu),
            row => Assert.Equal(VolvoP2CemOptionProvenance.VolvoPublishedCemServiceMenu,
                row.Provenance));
        Assert.All(Find(tier: VolvoP2CemCatalogEvidenceTier.VendorAdvertisement),
            row => Assert.Equal(VolvoP2CemOptionProvenance.ThirdPartyAdvertisement,
                row.Provenance));
    }
}
