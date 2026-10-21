using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Software-only tests of the JSON validate -> persist -> publish boundary.
/// No WinUI dialogs, J2534 device, registry modifications or permission races.
/// </summary>
public sealed class EcuProfileCatalogImporterTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "Dvinholm-atomic-import-tests",
        Guid.NewGuid().ToString("N"));

    public EcuProfileCatalogImporterTests() => Directory.CreateDirectory(_root);

    private string PathToCatalog => Path.Combine(_root, "ecu-profiles.json");

    private static EcuProfileCatalog OldCatalog() => new([
        new EcuProfile("existing.ecm", "Existing ECM", 0x7E0, 0x7E8,
            false, [0xF190], "Old synthetic source", isVerified: true)
    ]);

    private static EcuProfileCatalog ImportedCatalog() => new([
        new EcuProfile("import.one", "Imported 11-bit ECU", 0x7E1, 0x7E9,
            false, [0xF187, 0xF190], "Synthetic imported fixture"),
        new EcuProfile("import.two", "Imported 29-bit ECU",
            0x18DA10F1, 0x18DAF110, true, [0x1234],
            "Second imported fixture; author marked only", isVerified: true)
    ]);

    private void AssertNoTemporaryCatalogs() =>
        Assert.Empty(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories));

    [Fact]
    public void ValidImport_PersistsBeforeReturningCatalog()
    {
        var old = OldCatalog();
        var store = new EcuProfileCatalogStore(PathToCatalog);
        store.Save(old);
        var input = ImportedCatalog().ToJson();
        var calls = 0;

        var imported = EcuProfileCatalogImporter.ImportAndPersist(input, candidate =>
        {
            calls++;
            store.Save(candidate);
            Assert.Equal(input, File.ReadAllText(PathToCatalog));
        });

        Assert.Equal(1, calls);
        Assert.Equal(input, imported.ToJson());
        Assert.Equal(2, imported.All.Count);
        Assert.True(imported.All.Single(p => p.IsExtended).IsVerified);
        Assert.Equal(old.ToJson(), OldCatalog().ToJson());
        AssertNoTemporaryCatalogs();
    }

    [Fact]
    public void MalformedJson_DoesNotAttemptPersistence()
    {
        var calls = 0;
        Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist("{not json",
                _ => calls++));
        Assert.Equal(0, calls);
        Assert.False(File.Exists(PathToCatalog));
    }

    [Fact]
    public void InvalidCanId_DoesNotAttemptPersistence()
    {
        var input = ImportedCatalog().ToJson()
            .Replace("\"requestId\": \"7E1\"", "\"requestId\": \"800\"", StringComparison.Ordinal);
        var calls = 0;

        Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(input, _ => calls++));

        Assert.Equal(0, calls);
        Assert.False(File.Exists(PathToCatalog));
    }

    [Fact]
    public void DuplicateIds_DoNotAttemptPersistence()
    {
        var input = ImportedCatalog().ToJson()
            .Replace("\"id\": \"import.two\"", "\"id\": \"IMPORT.ONE\"", StringComparison.Ordinal);
        var calls = 0;

        Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(input, _ => calls++));

        Assert.Equal(0, calls);
        Assert.False(File.Exists(PathToCatalog));
    }

    [Fact]
    public void InvalidDidInSecondProfile_RejectsWholeCatalog()
    {
        var input = ImportedCatalog().ToJson()
            .Replace("\"1234\"", "\"ZZZZ\"", StringComparison.Ordinal);
        var calls = 0;

        Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(input, _ => calls++));

        Assert.Equal(0, calls);
        Assert.False(File.Exists(PathToCatalog));
    }

    [Fact]
    public void InterruptedSave_PreservesPreviousDiskCatalogAndActiveMemory()
    {
        var original = OldCatalog();
        var store = new EcuProfileCatalogStore(PathToCatalog);
        store.Save(original);
        var oldJson = original.ToJson();
        var brokenStore = new EcuProfileCatalogStore(PathToCatalog, (temp, json) =>
        {
            File.WriteAllText(temp, json[..Math.Min(json.Length, 18)]);
            throw new IOException("Simulated disk failure during import.");
        });

        Assert.Throws<IOException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(
                ImportedCatalog().ToJson(), brokenStore.Save));

        Assert.Equal(oldJson, original.ToJson());
        Assert.Equal(oldJson, store.Load().ToJson());
        Assert.True(Assert.Single(original.All).IsVerified);
        AssertNoTemporaryCatalogs();
    }

    [Fact]
    public void FirstImportFailedSave_DoesNotMaterializeCatalog()
    {
        var brokenStore = new EcuProfileCatalogStore(PathToCatalog, (temp, _) =>
        {
            File.WriteAllText(temp, "{partial");
            throw new IOException("Simulated first-write failure.");
        });

        Assert.Throws<IOException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(
                ImportedCatalog().ToJson(), brokenStore.Save));

        Assert.False(File.Exists(PathToCatalog));
        AssertNoTemporaryCatalogs();
    }

    [Fact]
    public void RetryAfterSaveFailure_ImportsCompleteReplacementWithoutChangingOldObject()
    {
        var original = OldCatalog();
        var store = new EcuProfileCatalogStore(PathToCatalog);
        store.Save(original);
        var oldJson = original.ToJson();
        var input = ImportedCatalog().ToJson();
        var attempts = 0;

        EcuProfileCatalog PersistWithOneFailure(EcuProfileCatalog candidate)
        {
            attempts++;
            if (attempts == 1)
                throw new UnauthorizedAccessException("Simulated read-only storage.");
            store.Save(candidate);
            return candidate;
        }

        Assert.Throws<UnauthorizedAccessException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(input,
                candidate => { PersistWithOneFailure(candidate); }));
        Assert.Equal(oldJson, File.ReadAllText(PathToCatalog));

        var imported = EcuProfileCatalogImporter.ImportAndPersist(input,
            candidate => { PersistWithOneFailure(candidate); });

        Assert.Equal(2, attempts);
        Assert.Equal(input, imported.ToJson());
        Assert.Equal(input, store.Load().ToJson());
        Assert.Equal(oldJson, original.ToJson());
        AssertNoTemporaryCatalogs();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
