using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline regressions for literal text versus 0x-prefixed hexadecimal
/// ECU profile searches. Search must never mutate profile metadata.
/// </summary>
public sealed class EcuProfileSearchHexPrefixLiteralTests
{
    private static EcuProfileCatalog Catalog(bool includeLiteral = true)
    {
        var profiles = new List<EcuProfile>
        {
            new("plain.ecm", "Engine ECU", 0x7E0, 0x7E8, false,
                [0xF190], "Synthetic ECM note", false),
            new("verified.ext", "Extended ECU", 0x18DA10F1, 0x18DAF110, true,
                [0x1234], "Verified synthetic fixture", true)
        };
        if (includeLiteral)
        {
            profiles.Add(new EcuProfile("literal.note", "0xTAG endpoint", 0x710, 0x718,
                false, [0xF187], "Contains 0x label; ZZ Controller", false));
        }
        return new EcuProfileCatalog(profiles);
    }

    [Fact]
    public void BareHexPrefixIsNotAnEmptyWildcard()
    {
        var catalog = Catalog(includeLiteral: false);
        Assert.Empty(EcuProfileCatalogSearch.Filter(catalog, "0x"));
        Assert.Empty(EcuProfileCatalogSearch.Filter(catalog, "0X"));
    }

    [Fact]
    public void BareHexPrefixCanMatchActualLiteralMetadata()
    {
        var found = EcuProfileCatalogSearch.Filter(Catalog(), " 0x ");
        Assert.Equal("literal.note", Assert.Single(found).Id);
    }

    [Fact]
    public void NonHexSuffixDoesNotBecomeMetadataSubstringQuery()
    {
        // Querying "0xZZ" must not match "ZZ Controller" after stripping 0x.
        Assert.Empty(EcuProfileCatalogSearch.Filter(Catalog(), "0xZZ"));
        // Querying "0xECM" must not match "Synthetic ECM note" either.
        Assert.Empty(EcuProfileCatalogSearch.Filter(Catalog(), "0xECM"));
    }

    [Fact]
    public void LiteralNonHexPrefixRemainsSearchableByName()
    {
        Assert.Equal("literal.note", Assert.Single(
            EcuProfileCatalogSearch.Filter(Catalog(), "0xTAG")).Id);
    }

    [Fact]
    public void PrefixedStandardCanResponseStillMatches()
    {
        Assert.Equal("plain.ecm", Assert.Single(
            EcuProfileCatalogSearch.Filter(Catalog(), "0x7e8")).Id);
    }

    [Fact]
    public void PrefixedExtendedCanRequestStillMatches()
    {
        Assert.Equal("verified.ext", Assert.Single(
            EcuProfileCatalogSearch.Filter(Catalog(), "0X18da10f1")).Id);
    }

    [Fact]
    public void PrefixedCandidateDidStillMatchesWithoutMetadataAlias()
    {
        Assert.Equal("plain.ecm", Assert.Single(
            EcuProfileCatalogSearch.Filter(Catalog(), "0xF190")).Id);
    }

    [Fact]
    public void VerifiedFilterAndCatalogContentsRemainUnchanged()
    {
        var catalog = Catalog();
        var originalJson = catalog.ToJson();

        Assert.Empty(EcuProfileCatalogSearch.Filter(catalog, "0x", markedVerifiedOnly: true));
        Assert.Equal("verified.ext", Assert.Single(
            EcuProfileCatalogSearch.Filter(catalog, "0x1234", markedVerifiedOnly: true)).Id);
        Assert.Equal(catalog.All.Select(p => p.Id),
            EcuProfileCatalogSearch.Filter(catalog, "").Select(p => p.Id));
        Assert.Equal(originalJson, catalog.ToJson());
    }
}
