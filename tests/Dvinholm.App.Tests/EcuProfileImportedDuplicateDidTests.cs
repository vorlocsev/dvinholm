using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for imported candidate DID uniqueness. All inputs
/// are synthetic; the import never touches vehicle hardware.
/// </summary>
public sealed class EcuProfileImportedDuplicateDidTests
{
    private static string CatalogJson(string dids, string id = "synthetic.ecm") =>
        $$"""[{"id":"{{id}}","name":"Synthetic ECU","requestId":"7E0","responseId":"7E8","isExtended":false,"readableDataIdentifiers":{{dids}},"source":"Synthetic fixture only","isVerified":false}]""";

    private static void AssertDuplicate(string json, string expectedDid)
    {
        var error = Assert.Throws<InvalidDataException>(() => EcuProfileCatalog.FromJson(json));
        Assert.Contains("duplicate hexadecimal DID", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedDid, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExactRepeatedCandidateDidIsRejected()
    {
        AssertDuplicate(CatalogJson("""["F190","F190"]"""), "F190");
    }

    [Fact]
    public void CaseVariantCandidateDidIsRejected()
    {
        AssertDuplicate(CatalogJson("""["f190","F190"]"""), "F190");
    }

    [Fact]
    public void NumericEquivalentShortAndPaddedHexAreRejected()
    {
        AssertDuplicate(CatalogJson("""["FF","00FF"]"""), "00FF");
    }

    [Fact]
    public void DuplicateDidInSecondProfileRejectsWholeCatalog()
    {
        var first = CatalogJson("""["F187"]""", "synthetic.ecm");
        var second = CatalogJson("""["F190","F190"]""", "synthetic.tcm");
        var combined = first[..^1] + "," + second[1..];

        AssertDuplicate(combined, "F190");
    }

    [Fact]
    public void SameCandidateDidInDifferentProfilesIsAllowed()
    {
        var first = CatalogJson("""["F190"]""", "synthetic.ecm");
        var second = CatalogJson("""["F190"]""", "synthetic.tcm");
        var combined = first[..^1] + "," + second[1..];

        var imported = EcuProfileCatalog.FromJson(combined);
        Assert.Equal(2, imported.All.Count);
        Assert.All(imported.All, p => Assert.Contains((ushort)0xF190, p.ReadableDataIdentifiers));
    }

    [Fact]
    public void UniqueCandidateDidsSurviveRoundTrip()
    {
        var imported = EcuProfileCatalog.FromJson(CatalogJson("""["F190","F187","0019"]"""));
        var profile = Assert.Single(imported.All);
        Assert.Equal(new ushort[] { 0x0019, 0xF187, 0xF190 },
            profile.ReadableDataIdentifiers.Order().ToArray());

        var roundTrip = Assert.Single(EcuProfileCatalog.FromJson(imported.ToJson()).All);
        Assert.Equal(profile.ReadableDataIdentifiers.Order(),
            roundTrip.ReadableDataIdentifiers.Order());
    }

    [Fact]
    public void EmptyAndNullCandidateListsRemainValid()
    {
        Assert.Empty(Assert.Single(EcuProfileCatalog.FromJson(CatalogJson("[]")).All)
            .ReadableDataIdentifiers);
        Assert.Empty(Assert.Single(EcuProfileCatalog.FromJson(CatalogJson("null")).All)
            .ReadableDataIdentifiers);
    }

    [Fact]
    public void DuplicateDidImportDoesNotCallPersistenceCallback()
    {
        var persistCount = 0;
        var json = CatalogJson("""["F190","f190"]""");
        Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(json, _ => persistCount++));
        Assert.Equal(0, persistCount);
    }
}
