namespace Dvinholm.App.Tests;

public sealed class EcuProfileTests
{
    private static Dvinholm.App.EcuProfile CreateProfile(
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
            new Dvinholm.App.EcuProfile("id", "name", 0x7E0, 0x7E8, false, null, " "));
    }

    [Fact]
    public void Catalog_LooksUpIdsCaseInsensitivelyAndSeparatesVerifiedProfiles()
    {
        var known = CreateProfile(id: "volvo.ecm", verified: true);
        var draft = CreateProfile(id: "volvo.tcm", verified: false);
        var catalog = new Dvinholm.App.EcuProfileCatalog([draft, known]);

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

        Assert.Throws<ArgumentException>(() => new Dvinholm.App.EcuProfileCatalog([first, duplicate]));
    }

    [Fact]
    public void Catalog_StartsEmptyWithoutInventedVehicleSpecificProfiles()
    {
        var catalog = new Dvinholm.App.EcuProfileCatalog();

        Assert.Empty(catalog.All);
        Assert.Empty(catalog.Verified);
    }

    [Fact]
    public void JsonRoundTrip_PreservesProfilesAndUsesHexadecimalIds()
    {
        var original = new Dvinholm.App.EcuProfileCatalog([
            CreateProfile(id: "volvo.ecm", dids: [0xF190, 0xF187], verified: true),
            CreateProfile(id: "volvo.extended", requestId: 0x18DA10F1,
                responseId: 0x18DAF110, isExtended: true)
        ]);

        var json = original.ToJson();
        Assert.Contains("\"requestId\": \"7E0\"", json);
        using (var document = System.Text.Json.JsonDocument.Parse(json))
        {
            var dids = document.RootElement[0].GetProperty("readableDataIdentifiers")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray();
            Assert.Equal(new string?[] { "F187", "F190" }, dids);
        }

        var imported = Dvinholm.App.EcuProfileCatalog.FromJson(json);

        Assert.Equal(2, imported.All.Count);
        Assert.True(imported.TryGet("VOLVO.ECM", out var ecm));
        Assert.True(ecm!.IsVerified);
        Assert.Contains((ushort)0xF190, ecm.ReadableDataIdentifiers);
        Assert.True(imported.TryGet("volvo.extended", out var extended));
        Assert.True(extended!.IsExtended);
        Assert.Equal((uint)0x18DAF110, extended.ResponseId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{not json}")]
    [InlineData("{}")]
    public void FromJson_RejectsEmptyMalformedOrNonArrayDocuments(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            Assert.Throws<ArgumentException>(() => Dvinholm.App.EcuProfileCatalog.FromJson(json));
        else
            Assert.Throws<InvalidDataException>(() => Dvinholm.App.EcuProfileCatalog.FromJson(json));
    }

    [Fact]
    public void FromJson_RejectsInvalidCanIdAndDid()
    {
        const string invalidCanId = """[{"id":"x","name":"X","requestId":"800","responseId":"7E8","isExtended":false,"source":"test"}]""";
        const string invalidDid = """[{"id":"x","name":"X","requestId":"7E0","responseId":"7E8","isExtended":false,"readableDataIdentifiers":["XYZ"],"source":"test"}]""";

        Assert.Throws<InvalidDataException>(() => Dvinholm.App.EcuProfileCatalog.FromJson(invalidCanId));
        Assert.Throws<InvalidDataException>(() => Dvinholm.App.EcuProfileCatalog.FromJson(invalidDid));
    }

    [Fact]
    public void FromJson_RejectsDuplicateProfileIdsIgnoringCase()
    {
        const string json = """
        [
          {"id":"volvo.ecm","name":"A","requestId":"7E0","responseId":"7E8","isExtended":false,"source":"test"},
          {"id":"VOLVO.ECM","name":"B","requestId":"7E1","responseId":"7E9","isExtended":false,"source":"test"}
        ]
        """;

        Assert.Throws<InvalidDataException>(() => Dvinholm.App.EcuProfileCatalog.FromJson(json));
    }
    [Fact]
    public void FromJson_RejectsNullEntriesInsteadOfSilentlySkippingThem()
    {
        const string json = """
        [
          {"id":"volvo.ecm","name":"ECM","requestId":"7E0","responseId":"7E8","isExtended":false,"source":"documented fixture"},
          null
        ]
        """;

        var exception = Assert.Throws<InvalidDataException>(() =>
            Dvinholm.App.EcuProfileCatalog.FromJson(json));

        Assert.Contains("index 1", exception.Message);
    }

    [Fact]
    public void FromJson_RejectsRequestAndResponseIdsThatAreTheSame()
    {
        const string json = """[{"id":"volvo.ecm","name":"ECM","requestId":"7E0","responseId":"7E0","isExtended":false,"source":"documented fixture"}]""";

        var exception = Assert.Throws<InvalidDataException>(() =>
            Dvinholm.App.EcuProfileCatalog.FromJson(json));

        Assert.Contains("Request and response CAN IDs must be different", exception.Message);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAnEmptyCatalog()
    {
        var original = new Dvinholm.App.EcuProfileCatalog();

        var imported = Dvinholm.App.EcuProfileCatalog.FromJson(original.ToJson());

        Assert.Empty(imported.All);
        Assert.Empty(imported.Verified);
    }

    [Theory]
    [InlineData("7E0", "800", false)]
    [InlineData("20000000", "18DAF110", true)]
    public void FromJson_RejectsCanIdsOutsideSelectedAddressingRange(
        string requestId, string responseId, bool isExtended)
    {
        var json = $$"""[{"id":"volvo.ecm","name":"ECM","requestId":"{{requestId}}","responseId":"{{responseId}}","isExtended":{{isExtended.ToString().ToLowerInvariant()}},"source":"documented fixture"}]""";

        var exception = Assert.Throws<InvalidDataException>(() =>
            Dvinholm.App.EcuProfileCatalog.FromJson(json));

        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

}
