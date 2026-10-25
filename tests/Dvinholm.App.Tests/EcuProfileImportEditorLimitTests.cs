using System.Text.Json;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks that imported profiles obey the same editable field
/// limits as profiles created in the editor. No hardware or WinUI required.
/// </summary>
public sealed class EcuProfileImportEditorLimitTests
{
    private static string Entry(
        string id = "synthetic.ecm",
        string name = "Synthetic ECM",
        string source = "Synthetic source note",
        string[]? dids = null) =>
        JsonSerializer.Serialize(new
        {
            id,
            name,
            requestId = "7E0",
            responseId = "7E8",
            isExtended = false,
            readableDataIdentifiers = dids ?? ["F190"],
            source,
            isVerified = false
        });

    private static string Catalog(string id = "synthetic.ecm",
        string name = "Synthetic ECM",
        string source = "Synthetic source note",
        string[]? dids = null) =>
        "[" + Entry(id, name, source, dids) + "]";

    private static void AssertImportLimit(string json, string expected)
    {
        var error = Assert.Throws<InvalidDataException>(() => EcuProfileCatalog.FromJson(json));
        Assert.Contains(expected, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdLongerThanEditorLimitIsRejected()
    {
        AssertImportLimit(Catalog(id: new string('I', 121)), "120-character ID");
    }

    [Fact]
    public void IdExactlyAtEditorLimitRemainsValid()
    {
        var id = new string('I', 120);
        Assert.Equal(id, Assert.Single(EcuProfileCatalog.FromJson(Catalog(id: id)).All).Id);
    }

    [Fact]
    public void NameLongerThanEditorLimitIsRejected()
    {
        AssertImportLimit(Catalog(name: new string('N', 161)), "160-character name");
    }

    [Fact]
    public void NameExactlyAtEditorLimitRemainsValid()
    {
        var name = new string('N', 160);
        Assert.Equal(name, Assert.Single(EcuProfileCatalog.FromJson(Catalog(name: name)).All).Name);
    }

    [Fact]
    public void SourceLongerThanEditorLimitIsRejected()
    {
        AssertImportLimit(Catalog(source: new string('S', 2001)), "2000-character source");
    }

    [Fact]
    public void SourceExactlyAtEditorLimitRemainsValid()
    {
        var source = new string('S', 2000);
        Assert.Equal(source, Assert.Single(EcuProfileCatalog.FromJson(Catalog(source: source)).All).Source);
    }

    [Fact]
    public void CandidateDidLimitAccepts128ButRejects129()
    {
        var oneHundredTwentyEight = Enumerable.Range(0, 128)
            .Select(value => value.ToString("X4")).ToArray();
        var oneHundredTwentyNine = Enumerable.Range(0, 129)
            .Select(value => value.ToString("X4")).ToArray();

        Assert.Equal(128, Assert.Single(
            EcuProfileCatalog.FromJson(Catalog(dids: oneHundredTwentyEight)).All)
            .ReadableDataIdentifiers.Count);
        AssertImportLimit(Catalog(dids: oneHundredTwentyNine), "128 candidate DID");
    }

    [Fact]
    public void InvalidSecondProfilePreventsEntireCatalogPersistence()
    {
        var json = "[" + Entry() + "," +
            Entry(id: "synthetic.tcm", source: new string('S', 2001)) + "]";
        var persistCalls = 0;

        Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(json, _ => persistCalls++));

        Assert.Equal(0, persistCalls);
    }
}
