using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline compatibility checks for hexadecimal 0x prefixes in imported
/// ECU profile JSON. These are profile-only tests and do not access CAN.
/// </summary>
public sealed class EcuProfileJsonHexPrefixTests
{
    private static string Profile(
        string request = "7E0",
        string response = "7E8",
        bool extended = false,
        string dids = """["F190"]""") =>
        $$"""[{"id":"synthetic.ecm","name":"Synthetic ECM","requestId":"{{request}}","responseId":"{{response}}","isExtended":{{extended.ToString().ToLowerInvariant()}},"readableDataIdentifiers":{{dids}},"source":"Synthetic fixture only","isVerified":false}]""";

    [Fact]
    public void StandardRequestAndResponseAcceptMixedCaseHexPrefixes()
    {
        var profile = Assert.Single(EcuProfileCatalog.FromJson(
            Profile(request: "0x7E0", response: "0X7E8")).All);

        Assert.Equal((uint)0x7E0, profile.RequestId);
        Assert.Equal((uint)0x7E8, profile.ResponseId);
        Assert.False(profile.IsExtended);
    }

    [Fact]
    public void ExtendedRequestAndResponseAcceptPrefixedHex()
    {
        var profile = Assert.Single(EcuProfileCatalog.FromJson(
            Profile(request: "0x18DA10F1", response: "0X18DAF110", extended: true)).All);

        Assert.Equal((uint)0x18DA10F1, profile.RequestId);
        Assert.Equal((uint)0x18DAF110, profile.ResponseId);
        Assert.True(profile.IsExtended);
    }

    [Fact]
    public void CandidateDidsAcceptUppercaseAndLowercasePrefix()
    {
        var profile = Assert.Single(EcuProfileCatalog.FromJson(
            Profile(dids: """["0xF190","0XF187"]""")).All);

        Assert.Equal(new ushort[] { 0xF187, 0xF190 },
            profile.ReadableDataIdentifiers.Order().ToArray());
    }

    [Fact]
    public void PrefixedInputExportsCanonicalUnprefixedJson()
    {
        var original = EcuProfileCatalog.FromJson(Profile(
            request: "0x7E0", response: "0X7E8", dids: """["0xF190"]"""));

        var json = original.ToJson();

        Assert.DoesNotContain("0x", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"requestId\": \"7E0\"", json);
        Assert.Contains("\"responseId\": \"7E8\"", json);
        Assert.Contains("\"F190\"", json);
        Assert.Equal(original.ToJson(), EcuProfileCatalog.FromJson(json).ToJson());
    }

    [Fact]
    public void BarePrefixesStillFailAsInvalidCanIds()
    {
        Assert.Throws<InvalidDataException>(() => EcuProfileCatalog.FromJson(
            Profile(request: "0x")));
        Assert.Throws<InvalidDataException>(() => EcuProfileCatalog.FromJson(
            Profile(response: "0X")));
    }

    [Fact]
    public void RepeatedPrefixIsNotAcceptedForCanIdOrDid()
    {
        Assert.Throws<InvalidDataException>(() => EcuProfileCatalog.FromJson(
            Profile(request: "0x0x7E0")));
        Assert.Throws<InvalidDataException>(() => EcuProfileCatalog.FromJson(
            Profile(dids: """["0x0xF190"]""")));
    }

    [Fact]
    public void PrefixDoesNotBypassElevenOrTwentyNineBitLimits()
    {
        Assert.Throws<InvalidDataException>(() => EcuProfileCatalog.FromJson(
            Profile(request: "0x800")));
        Assert.Throws<InvalidDataException>(() => EcuProfileCatalog.FromJson(
            Profile(request: "0x20000000", response: "0x18DAF110", extended: true)));
        Assert.Throws<InvalidDataException>(() => EcuProfileCatalog.FromJson(
            Profile(dids: """["0x10000"]""")));
    }

    [Fact]
    public void PrefixedAndUnprefixedDuplicateDidStillAbortImport()
    {
        var persistCalls = 0;
        var json = Profile(dids: """["0xF190","F190"]""");

        var error = Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(json, _ => persistCalls++));

        Assert.Contains("duplicate hexadecimal DID", error.Message);
        Assert.Equal(0, persistCalls);
    }
}
