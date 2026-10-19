namespace Dvinholm.App.Tests;

public sealed class EcuProfileCatalogSearchTests
{
    private static Dvinholm.App.EcuProfileCatalog CreateCatalog() => new(
    [
        new Dvinholm.App.EcuProfile(
            "demo.engine", "Example ECM", 0x7E0, 0x7E8, false,
            [0xF190, 0xF187], "Synthetic fixture only", false),
        new Dvinholm.App.EcuProfile(
            "verified.generic", "Generic test endpoint", 0x18DA10F1, 0x18DAF110, true,
            [0x1234], "Documented fixture reference", true),
        new Dvinholm.App.EcuProfile(
            "demo.gearbox", "Example TCM", 0x7E1, 0x7E9, false,
            [0xF190], "Unverified user note", false)
    ]);

    [Fact]
    public void Filter_EmptySearchReturnsSortedCatalog()
    {
        var catalog = CreateCatalog();
        Assert.Equal(
            catalog.All.Select(x => x.Id),
            Dvinholm.App.EcuProfileCatalogSearch.Filter(catalog, "  ").Select(x => x.Id));
    }

    [Fact]
    public void Filter_SearchesNameIgnoringCaseAndWhitespace()
    {
        var found = Dvinholm.App.EcuProfileCatalogSearch.Filter(CreateCatalog(), "  eCm ");
        Assert.Equal("demo.engine", Assert.Single(found).Id);
    }

    [Fact]
    public void Filter_SearchesIdentifierAndSource()
    {
        var catalog = CreateCatalog();
        Assert.Equal("demo.gearbox", Assert.Single(
            Dvinholm.App.EcuProfileCatalogSearch.Filter(catalog, "GEARBOX")).Id);
        Assert.Equal("verified.generic", Assert.Single(
            Dvinholm.App.EcuProfileCatalogSearch.Filter(catalog, "documented")).Id);
    }

    [Theory]
    [InlineData("7E0", "demo.engine")]
    [InlineData("0x7e8", "demo.engine")]
    [InlineData("18DA10F1", "verified.generic")]
    [InlineData("0x18daf110", "verified.generic")]
    public void Filter_SearchesRequestAndResponseCanIds(string query, string expectedId)
    {
        Assert.Equal(expectedId, Assert.Single(
            Dvinholm.App.EcuProfileCatalogSearch.Filter(CreateCatalog(), query)).Id);
    }

    [Fact]
    public void Filter_SearchesCandidateDidWithoutClaimingSupport()
    {
        var results = Dvinholm.App.EcuProfileCatalogSearch.Filter(CreateCatalog(), "0xf187");
        Assert.Equal("demo.engine", Assert.Single(results).Id);
    }

    [Fact]
    public void Filter_MarkedVerifiedOnlyUsesCatalogFlagNotName()
    {
        var catalog = CreateCatalog();
        var results = Dvinholm.App.EcuProfileCatalogSearch.Filter(catalog, null, markedVerifiedOnly: true);
        Assert.Equal("verified.generic", Assert.Single(results).Id);
    }

    [Fact]
    public void Filter_SearchAndVerifiedFlagAreConjunctive()
    {
        var catalog = CreateCatalog();
        Assert.Empty(Dvinholm.App.EcuProfileCatalogSearch.Filter(catalog, "F190", true));
        Assert.Equal("verified.generic", Assert.Single(
            Dvinholm.App.EcuProfileCatalogSearch.Filter(catalog, "1234", true)).Id);
    }

    [Fact]
    public void Filter_NoMatchesReturnsEmptyWithoutChangingCatalog()
    {
        var catalog = CreateCatalog();
        Assert.Empty(Dvinholm.App.EcuProfileCatalogSearch.Filter(catalog, "does-not-exist"));
        Assert.Equal(3, catalog.All.Count);
    }

    [Fact]
    public void Filter_DoesNotMarkSyntheticProfileAsVerified()
    {
        var catalog = CreateCatalog();
        var before = catalog.ToJson();

        var filtered = Dvinholm.App.EcuProfileCatalogSearch.Filter(catalog, "ECM", true);

        Assert.Empty(filtered);
        Assert.Equal(before, catalog.ToJson());
        Assert.False(catalog.All.Single(x => x.Id == "demo.engine").IsVerified);
    }

    [Fact]
    public void Describe_UnverifiedProfileShowsSourceDidAndHardwareCaveat()
    {
        var profile = CreateCatalog().All.Single(x => x.Id == "demo.engine");

        var info = Dvinholm.App.EcuProfileCatalogSearch.Describe(profile);

        Assert.Contains("Example ECM", info);
        Assert.Contains("demo.engine", info);
        Assert.Contains("unverified", info);
        Assert.Contains("Synthetic fixture only", info);
        Assert.Contains("F187, F190", info);
        Assert.Contains("7E0", info);
        Assert.Contains("7E8", info);
        Assert.Contains("11-bit", info);
        Assert.Contains("not been established", info);
    }

    [Fact]
    public void Describe_MarkedVerifiedProfileStillDisclaimsVehicleCompatibility()
    {
        var profile = CreateCatalog().All.Single(x => x.Id == "verified.generic");

        var info = Dvinholm.App.EcuProfileCatalogSearch.Describe(profile);

        Assert.Contains("marked verified by profile author", info);
        Assert.Contains("18DA10F1", info);
        Assert.Contains("18DAF110", info);
        Assert.Contains("29-bit", info);
        Assert.Contains("1234", info);
        Assert.Contains("not been established", info);
    }

    [Fact]
    public void Describe_ProfileWithNoCandidateDidsSaysNoneListed()
    {
        var profile = new Dvinholm.App.EcuProfile(
            "empty", "Empty sample", 0x700, 0x708, false,
            Array.Empty<ushort>(), "No DIDs documented.");

        var info = Dvinholm.App.EcuProfileCatalogSearch.Describe(profile);

        Assert.Contains("Candidate readable DIDs: none listed", info);
    }

    [Fact]
    public void Describe_NullProfileThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Dvinholm.App.EcuProfileCatalogSearch.Describe(null!));
    }

    [Fact]
    public void Filter_NullCatalogThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Dvinholm.App.EcuProfileCatalogSearch.Filter(null!, string.Empty));
    }
}
