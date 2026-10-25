using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight read-only JSON import regressions. Duplicate object properties must
/// not silently replace ECU addressing, DID selection or verification state.
/// </summary>
public sealed class EcuProfileDuplicateJsonPropertyTests
{
    private const string OriginalJson =
        """[{"id":"synthetic.ecm","name":"Synthetic ECM","requestId":"7E0","responseId":"7E8","isExtended":false,"readableDataIdentifiers":["F190"],"source":"Synthetic test fixture","isVerified":false}]""";

    private static string WithExtra(string property) =>
        OriginalJson.Replace("}]", "," + property + "}]", StringComparison.Ordinal);

    private static void AssertDuplicate(string property, string extra)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalog.FromJson(WithExtra(extra)));
        Assert.Contains("index 0", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("duplicate JSON property", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(property, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepeatedRequestIdCannotSilentlyRedirectAnEcu()
    {
        AssertDuplicate("requestId", "\"requestId\":\"7E1\"");
    }

    [Fact]
    public void CaseVariantOfRequestIdIsAlsoRejected()
    {
        AssertDuplicate("REQUESTID", "\"REQUESTID\":\"7E1\"");
    }

    [Fact]
    public void RepeatedResponseIdCannotSilentlyChangeTheReadEndpoint()
    {
        AssertDuplicate("responseId", "\"responseId\":\"7E9\"");
    }

    [Fact]
    public void RepeatedAddressingWidthCannotSilentlyChangeTheCanFrameType()
    {
        AssertDuplicate("isExtended", "\"isExtended\":true");
    }

    [Fact]
    public void RepeatedVerifiedFlagCannotSilentlyPromoteAProfile()
    {
        AssertDuplicate("isVerified", "\"isVerified\":true");
    }

    [Fact]
    public void RepeatedDidListCannotSilentlyDiscardCandidateIdentifiers()
    {
        AssertDuplicate("readableDataIdentifiers",
            "\"readableDataIdentifiers\":[\"F187\"]");
    }

    [Fact]
    public void DuplicateFieldInSecondProfileRejectsImportWithoutPersistence()
    {
        const string second =
            """{"id":"synthetic.tcm","name":"Synthetic TCM","requestId":"7E1","responseId":"7E9","source":"First source","source":"Second source"}""";
        var json = OriginalJson[..^1] + "," + second + "]";
        var persistCalls = 0;

        var error = Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(json, _ => persistCalls++));

        Assert.Contains("index 1", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, persistCalls);
    }

    [Fact]
    public void DistinctForwardCompatibleFieldsStillAllowValidRoundTrip()
    {
        var input = WithExtra("\"futureAnnotation\":\"unverified fixture\"");
        var catalog = EcuProfileCatalog.FromJson(input);
        var profile = Assert.Single(catalog.All);

        Assert.Equal((uint)0x7E0, profile.RequestId);
        Assert.Equal((uint)0x7E8, profile.ResponseId);
        Assert.False(profile.IsVerified);
        Assert.Contains((ushort)0xF190, profile.ReadableDataIdentifiers);

        var reloaded = Assert.Single(EcuProfileCatalog.FromJson(catalog.ToJson()).All);
        Assert.Equal(profile.Id, reloaded.Id);
        Assert.Equal(profile.RequestId, reloaded.RequestId);
        Assert.False(reloaded.IsVerified);
    }
}
