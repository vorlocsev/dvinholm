using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Deterministic failure injection at the temporary-file write boundary.
/// No permissions changes, Windows dialogs or physical J2534 are required.
/// </summary>
public sealed class EcuProfileSaveFailureTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "Dvinholm-profile-failure", Guid.NewGuid().ToString("N"));

    public EcuProfileSaveFailureTests() => Directory.CreateDirectory(_directory);

    private string CatalogPath => Path.Combine(_directory, "ecu-profiles.json");

    private static EcuProfileCatalog Original() => new([
        new EcuProfile("test.ecm", "Original ECM", 0x7E0, 0x7E8, false,
            [0xF187, 0xF190], "Synthetic example only", isVerified: true)
    ]);

    private static EcuProfileDraft EditDraft(string dids = "F190") =>
        new("test.ecm", "Edited ECM", "7E0", "7E8", false,
            dids, "Edited synthetic example");

    private void AssertNoTemporaryFiles() =>
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp", SearchOption.AllDirectories));

    [Fact]
    public void PartialStagingWrite_LeavesPreviouslySavedCatalogUnchanged()
    {
        var original = Original();
        var normal = new EcuProfileCatalogStore(CatalogPath);
        normal.Save(original);
        var saved = File.ReadAllBytes(CatalogPath);
        var broken = new EcuProfileCatalogStore(CatalogPath, (temporaryPath, _) =>
        {
            File.WriteAllText(temporaryPath, "{ partial JSON");
            throw new IOException("Simulated disk failure during staging.");
        });

        Assert.Throws<IOException>(() => broken.Save(new EcuProfileCatalog()));
        Assert.Equal(saved, File.ReadAllBytes(CatalogPath));
        Assert.Equal(original.ToJson(), normal.Load().ToJson());
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void PartialStagingWrite_WithNoExistingCatalog_CreatesNoDestination()
    {
        var broken = new EcuProfileCatalogStore(CatalogPath, (temporaryPath, _) =>
        {
            File.WriteAllText(temporaryPath, "{");
            throw new IOException("Interrupted new catalog.");
        });

        Assert.Throws<IOException>(() => broken.Save(Original()));
        Assert.False(File.Exists(CatalogPath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void MissingStagingFile_NeverReplacesExistingCatalog()
    {
        var normal = new EcuProfileCatalogStore(CatalogPath);
        normal.Save(Original());
        var old = File.ReadAllText(CatalogPath);
        var broken = new EcuProfileCatalogStore(CatalogPath, (_, _) => { });

        Assert.Throws<IOException>(() => broken.Save(new EcuProfileCatalog()));
        Assert.Equal(old, File.ReadAllText(CatalogPath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void EmptyStagingFile_NeverReplacesExistingCatalog()
    {
        var normal = new EcuProfileCatalogStore(CatalogPath);
        normal.Save(Original());
        var old = File.ReadAllText(CatalogPath);
        var broken = new EcuProfileCatalogStore(CatalogPath,
            (temporaryPath, _) => File.WriteAllText(temporaryPath, string.Empty));

        Assert.Throws<IOException>(() => broken.Save(new EcuProfileCatalog()));
        Assert.Equal(old, File.ReadAllText(CatalogPath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void FailedFinalMove_CleansTemporaryFileAndPreservesTargetDirectory()
    {
        // A directory at the destination guarantees the rename cannot replace
        // it, independently of account permissions or disk exhaustion.
        Directory.CreateDirectory(CatalogPath);
        var sentinel = Path.Combine(CatalogPath, "keep.txt");
        File.WriteAllText(sentinel, "preserve directory");

        var exception = Record.Exception(() =>
            new EcuProfileCatalogStore(CatalogPath).Save(Original()));

        Assert.NotNull(exception);
        Assert.True(exception is IOException or UnauthorizedAccessException);
        Assert.Equal("preserve directory", File.ReadAllText(sentinel));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void InvalidEditorDraft_DoesNotAttemptPersistence()
    {
        var original = Original();
        var json = original.ToJson();
        var writes = 0;

        Assert.Throws<ArgumentException>(() =>
            EcuProfileEditor.SaveAndPersist(original, EditDraft("F190, 0xF190"),
                _ => writes++, editingId: "test.ecm"));

        Assert.Equal(0, writes);
        Assert.Equal(json, original.ToJson());
    }

    [Fact]
    public void FailedEditorPersistence_LeavesExistingMemoryAndFileUntouched()
    {
        var original = Original();
        var store = new EcuProfileCatalogStore(CatalogPath);
        store.Save(original);
        var previousMemory = original.ToJson();
        var previousFile = File.ReadAllText(CatalogPath);

        Assert.Throws<IOException>(() =>
            EcuProfileEditor.SaveAndPersist(original, EditDraft(),
                _ => throw new IOException("Simulated permission or storage error"),
                editingId: "test.ecm"));

        Assert.Equal(previousMemory, original.ToJson());
        Assert.Equal(previousFile, File.ReadAllText(CatalogPath));
        Assert.True(Assert.Single(store.Load().All).IsVerified);
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void AfterFailedEdit_RetryCommitsCompleteCatalogAndClearsVerification()
    {
        var original = Original();
        var store = new EcuProfileCatalogStore(CatalogPath);
        store.Save(original);
        var old = File.ReadAllText(CatalogPath);
        var broken = new EcuProfileCatalogStore(CatalogPath, (temporaryPath, json) =>
        {
            File.WriteAllText(temporaryPath, json[..Math.Min(12, json.Length)]);
            throw new IOException("Simulated interrupted staging.");
        });

        Assert.Throws<IOException>(() =>
            EcuProfileEditor.SaveAndPersist(original, EditDraft(),
                broken.Save, editingId: "test.ecm"));
        Assert.Equal(old, File.ReadAllText(CatalogPath));
        Assert.True(Assert.Single(original.All).IsVerified);

        var (savedCatalog, savedProfile) = EcuProfileEditor.SaveAndPersist(
            original, EditDraft(), store.Save, editingId: "test.ecm");
        Assert.False(savedProfile.IsVerified);
        Assert.Equal("Edited ECM", Assert.Single(savedCatalog.All).Name);
        Assert.Equal(savedCatalog.ToJson(), store.Load().ToJson());
        Assert.True(Assert.Single(original.All).IsVerified);
        AssertNoTemporaryFiles();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
