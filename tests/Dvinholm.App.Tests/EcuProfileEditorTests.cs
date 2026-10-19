namespace Dvinholm.App.Tests;

public sealed class EcuProfileEditorTests
{
    private static Dvinholm.App.EcuProfileDraft Draft(
        string id = "user.test.ecm",
        string name = "User ECM",
        string request = "0x7E0",
        string response = "7e8",
        bool extended = false,
        string dids = "F187, 0xF190",
        string source = "Locally documented test only") =>
        new(id, name, request, response, extended, dids, source);

    [Fact]
    public void Create_FromEmptyCatalogPreservesEditableJsonSchema()
    {
        var original = new Dvinholm.App.EcuProfileCatalog();

        var (catalog, profile) = Dvinholm.App.EcuProfileEditor.Save(original, Draft());
        Assert.Empty(original.All);
        Assert.False(profile.IsVerified);
        Assert.Equal((uint)0x7E0, profile.RequestId);
        Assert.Equal((uint)0x7E8, profile.ResponseId);
        Assert.Equal(new ushort[] { 0xF187, 0xF190 }, profile.ReadableDataIdentifiers.Order());
        Assert.Contains("\"id\": \"user.test.ecm\"", catalog.ToJson());

        var roundTrip = Dvinholm.App.EcuProfileCatalog.FromJson(catalog.ToJson());
        Assert.Equal("User ECM", Assert.Single(roundTrip.All).Name);
    }

    [Fact]
    public void Create_29BitCanIdAndOptionalDids()
    {
        var (_, profile) = Dvinholm.App.EcuProfileEditor.Save(
            new Dvinholm.App.EcuProfileCatalog(),
            Draft(request: "18DA10F1", response: "0x18daf110", extended: true, dids: "  "));

        Assert.True(profile.IsExtended);
        Assert.Equal((uint)0x18DAF110, profile.ResponseId);
        Assert.Empty(profile.ReadableDataIdentifiers);
    }

    [Fact]
    public void Create_DuplicateIdIsRejectedCaseInsensitivelyWithoutMutatingCatalog()
    {
        var existing = new Dvinholm.App.EcuProfileCatalog();
        var (catalog, _) = Dvinholm.App.EcuProfileEditor.Save(existing, Draft());
        var previousJson = catalog.ToJson();

        var ex = Assert.Throws<ArgumentException>(() =>
            Dvinholm.App.EcuProfileEditor.Save(catalog, Draft(id: "USER.TEST.ECM")));

        Assert.Contains("already exists", ex.Message);
        Assert.Equal(previousJson, catalog.ToJson());
    }

    [Theory]
    [InlineData("800", "7E8", false)]
    [InlineData("7E0", "800", false)]
    [InlineData("20000000", "18DAF110", true)]
    [InlineData("18DA10F1", "20000000", true)]
    [InlineData("XY", "7E8", false)]
    [InlineData("", "7E8", false)]
    [InlineData("7E0", "7E0", false)]
    public void Create_RejectsInvalidOrOverlappingCanIds(string request, string response, bool extended)
    {
        Assert.Throws<ArgumentException>(() =>
            Dvinholm.App.EcuProfileEditor.Save(
                new Dvinholm.App.EcuProfileCatalog(),
                Draft(request: request, response: response, extended: extended)));
    }

    [Theory]
    [InlineData("ZZ99")]
    [InlineData("0x")]
    [InlineData("F190 F190")]
    [InlineData("0xF190, F190")]
    [InlineData("10000")]
    public void Create_RejectsInvalidOrDuplicateDids(string dids)
    {
        Assert.Throws<ArgumentException>(() =>
            Dvinholm.App.EcuProfileEditor.Save(
                new Dvinholm.App.EcuProfileCatalog(),
                Draft(dids: dids)));
    }

    [Fact]
    public void Create_AcceptsSemicolonAndWhitespaceSeparatedDids()
    {
        var (_, profile) = Dvinholm.App.EcuProfileEditor.Save(
            new Dvinholm.App.EcuProfileCatalog(),
            Draft(dids: "F190;\nF187\t1234"));
        Assert.Equal(new ushort[] { 0x1234, 0xF187, 0xF190 }, profile.ReadableDataIdentifiers.Order());
    }

    [Theory]
    [InlineData("", "User ECM", "source")]
    [InlineData("id", "", "source")]
    [InlineData("id", "User ECM", " ")]
    public void Create_RejectsMissingMetadata(string id, string name, string source)
    {
        Assert.Throws<ArgumentException>(() =>
            Dvinholm.App.EcuProfileEditor.Save(
                new Dvinholm.App.EcuProfileCatalog(),
                Draft(id: id, name: name, source: source)));
    }

    [Fact]
    public void Edit_ReplacesOneProfileWithoutChangingOthersOrOriginalCatalog()
    {
        var catalog = new Dvinholm.App.EcuProfileCatalog();
        var (first, _) = Dvinholm.App.EcuProfileEditor.Save(catalog, Draft());
        var (two, _) = Dvinholm.App.EcuProfileEditor.Save(
            first, Draft(id: "other", name: "Other", request: "7E1", response: "7E9"));
        var unchanged = two.ToJson();

        var (updated, edited) = Dvinholm.App.EcuProfileEditor.Save(two,
            Draft(name: "Edited ECM", request: "7D0", response: "7D8", dids: "F190",
                source: "Edited test note"), editingId: "user.test.ecm");

        Assert.Equal(2, updated.All.Count);
        Assert.Equal(2, two.All.Count);
        Assert.Equal(unchanged, two.ToJson());
        Assert.Equal("Edited ECM", edited.Name);
        Assert.Equal((uint)0x7D0, edited.RequestId);
        Assert.Equal("Other", updated.All.Single(p => p.Id == "other").Name);
        Assert.Equal("Edited test note", edited.Source);
    }

    [Fact]
    public void Edit_ClearsPreviousAuthorMarkedVerificationFlag()
    {
        var verified = new Dvinholm.App.EcuProfile(
            "author.verified", "Author-verified example", 0x7E0, 0x7E8, false,
            [0xF190], "External source note", isVerified: true);
        var catalog = new Dvinholm.App.EcuProfileCatalog([verified]);

        var (updated, changed) = Dvinholm.App.EcuProfileEditor.Save(
            catalog, Draft(id: verified.Id, name: "Renamed example"),
            editingId: verified.Id);

        Assert.True(verified.IsVerified);
        Assert.False(changed.IsVerified);
        Assert.Empty(updated.Verified);
        Assert.Single(catalog.Verified);
    }

    [Fact]
    public void Edit_CannotChangeStableProfileIdOrEditMissingProfile()
    {
        var (catalog, _) = Dvinholm.App.EcuProfileEditor.Save(
            new Dvinholm.App.EcuProfileCatalog(), Draft());

        Assert.Throws<ArgumentException>(() =>
            Dvinholm.App.EcuProfileEditor.Save(catalog, Draft(id: "renamed"),
                editingId: "user.test.ecm"));
        Assert.Throws<ArgumentException>(() =>
            Dvinholm.App.EcuProfileEditor.Save(catalog, Draft(),
                editingId: "nonexistent"));
    }

    [Fact]
    public void Save_RejectsNullInputs()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Dvinholm.App.EcuProfileEditor.Save(null!, Draft()));
        Assert.Throws<ArgumentNullException>(() =>
            Dvinholm.App.EcuProfileEditor.Save(
                new Dvinholm.App.EcuProfileCatalog(), null!));
    }
}
