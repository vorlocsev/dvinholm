namespace VolvoJ2534.App.Tests;

public sealed class EcuProfileTests
{
    private static VolvoJ2534.App.EcuProfile CreateProfile(
        string id = "volvo.example.ecm",
        uint requestId = 0x7E0,
        uint responseId = 0x7E8,
        bool isExtended = false,
        IEnumerable<ushort>? dids = null,
        bool verified = false) =>
        new(id, "Example ECM", requestId, responseId, isExtended, dids, "Test fixture; not a vehicle-specific profile.", verified);

    [Fact]
    public void Constructor_StoresAddressingAndCopiesDids()
    {
        var dids = new HashSet<ushort> { 0xF190, 0xF187 };
        var profile = CreateProfile(dids: dids);

        dids.Add(0xF188);

        Assert.Equal((uint)0x7E0, profile.RequestId);
        Assert.Equal((uint)0x7E8, profile.ResponseId);
        Assert.False(profile.IsExtended);
        Assert.Contains((ushort)0xF190, profile.ReadableDataIdentifiers);
        Assert.DoesNotContain((ushort)0xF188, profile.ReadableDataIdentifiers);
        Assert.False(profile.IsVerified);
    }

    [Fact]
    public void Constructor_Accepts29BitAddressing()
    {
        var profile = CreateProfile(requestId: 0x18DA10F1, responseId: 0x18DAF110, isExtended: true);

        Assert.True(profile.IsExtended);
        Assert.Equal((uint)0x18DA10F1, profile.RequestId);
    }

    [Theory]
    [InlineData(0x800u, 0x7E8u, false)]
    [InlineData(0x7E0u, 0x20000000u, true)]
    public void Constructor_RejectsIdsOutsideAddressingRange(uint requestId, uint responseId, bool isExtended)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateProfile(requestId: requestId, responseId: responseId, isExtended: isExtended));
    }

    [Fact]
    public void Constructor_RejectsSameRequestAndResponseId()
    {
        Assert.Throws<ArgumentException>(() => CreateProfile(requestId: 0x7E0, responseId: 0x7E0));
    }

    [Fact]
    public void Constructor_RequiresSourceNote()
    {
        Assert.Throws<ArgumentException>(() =>
            new VolvoJ2534.App.EcuProfile("id", "name", 0x7E0, 0x7E8, false, null, " "));
    }

    [Fact]
    public void Catalog_LooksUpIdsCaseInsensitivelyAndSeparatesVerifiedProfiles()
    {
        var known = CreateProfile(id: "volvo.ecm", verified: true);
        var draft = CreateProfile(id: "volvo.tcm", verified: false);
        var catalog = new VolvoJ2534.App.EcuProfileCatalog([draft, known]);

        Assert.True(catalog.TryGet("VOLVO.ECM", out var found));
        Assert.Same(known, found);
        Assert.False(catalog.TryGet("missing", out _));
        Assert.Single(catalog.Verified);
        Assert.Equal("volvo.ecm", Assert.Single(catalog.Verified).Id);
        Assert.Equal(2, catalog.All.Count);
    }

    [Fact]
    public void Catalog_RejectsDuplicateIdsIgnoringCase()
    {
        var first = CreateProfile(id: "volvo.ecm");
        var duplicate = CreateProfile(id: "VOLVO.ECM");

        Assert.Throws<ArgumentException>(() => new VolvoJ2534.App.EcuProfileCatalog([first, duplicate]));
    }

    [Fact]
    public void Catalog_StartsEmptyWithoutInventedVehicleSpecificProfiles()
    {
        var catalog = new VolvoJ2534.App.EcuProfileCatalog();

        Assert.Empty(catalog.All);
        Assert.Empty(catalog.Verified);
    }
}
