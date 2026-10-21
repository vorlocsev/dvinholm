using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Deterministic backup-stage failure injection. All cases use private temp
/// folders and virtual data; there is no J2534 or WinUI dependency.
/// </summary>
public sealed class EcuProfileBackupStagingTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "Dvinholm-ecu-backup-staging",
        Guid.NewGuid().ToString("N"));

    public EcuProfileBackupStagingTests() => Directory.CreateDirectory(_directory);

    private string PrimaryPath => Path.Combine(_directory, "ecu-profiles.json");
    private string BackupPath => PrimaryPath + ".bak";

    private static EcuProfileCatalog Catalog(string id) => new([
        new EcuProfile(id, "Synthetic " + id, 0x7E0, 0x7E8, false,
            [0xF187, 0xF190], "Synthetic backup test; no vehicle confirmation.")
    ]);

    private EcuProfileCatalogStore NormalStore() => new(PrimaryPath);

    private EcuProfileCatalogStore StoreWithBackupWriter(Action<string, string> writer) =>
        new(PrimaryPath, copyCommittedCatalogToBackup: writer);

    private void AssertNoNewTemporaryFiles() =>
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp", SearchOption.AllDirectories));

    [Fact]
    public void FirstSave_BackupCopyThrows_PrimaryRemainsCommitted()
    {
        var candidate = Catalog("first");
        var store = StoreWithBackupWriter((source, dest) =>
        {
            File.WriteAllText(dest, "{partial backup");
            throw new IOException("Simulated interrupted backup copy.");
        });

        store.Save(candidate);

        Assert.Equal(candidate.ToJson(), File.ReadAllText(PrimaryPath));
        Assert.False(File.Exists(BackupPath));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void FirstSave_BackupWriterReturnsWithoutCreatingFile_DoesNotCreateBackup()
    {
        var candidate = Catalog("first");
        StoreWithBackupWriter((_, _) => { }).Save(candidate);

        Assert.Equal(candidate.ToJson(), File.ReadAllText(PrimaryPath));
        Assert.False(File.Exists(BackupPath));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void SecondSave_TruncatedBackupStage_CannotReplacePreviousValidBackup()
    {
        var store = NormalStore();
        var oldCatalog = Catalog("old");
        var newCatalog = Catalog("new");
        store.Save(oldCatalog);

        StoreWithBackupWriter((source, dest) =>
        {
            var bytes = File.ReadAllBytes(source);
            File.WriteAllBytes(dest, bytes[..(bytes.Length / 2)]);
        }).Save(newCatalog);

        Assert.Equal(newCatalog.ToJson(), File.ReadAllText(PrimaryPath));
        Assert.Equal(oldCatalog.ToJson(), File.ReadAllText(BackupPath));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void SecondSave_EmptyBackupStage_LeavesPriorBackupIntact()
    {
        var store = NormalStore();
        store.Save(Catalog("old"));
        var oldBackup = File.ReadAllBytes(BackupPath);
        var candidate = Catalog("new");

        StoreWithBackupWriter((_, dest) => File.WriteAllBytes(dest, []))
            .Save(candidate);

        Assert.Equal(candidate.ToJson(), File.ReadAllText(PrimaryPath));
        Assert.Equal(oldBackup, File.ReadAllBytes(BackupPath));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void SecondSave_EqualLengthButCorruptedBackupStage_IsRejected()
    {
        var store = NormalStore();
        store.Save(Catalog("old"));
        var oldBackup = File.ReadAllBytes(BackupPath);
        var candidate = Catalog("new");

        StoreWithBackupWriter((source, dest) =>
        {
            var bytes = File.ReadAllBytes(source);
            bytes[bytes.Length / 2] ^= 0x01;
            File.WriteAllBytes(dest, bytes);
        }).Save(candidate);

        Assert.Equal(candidate.ToJson(), File.ReadAllText(PrimaryPath));
        Assert.Equal(oldBackup, File.ReadAllBytes(BackupPath));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void SecondSave_InterruptedBackupCopy_DoesNotReplacePreviousBackup()
    {
        var store = NormalStore();
        store.Save(Catalog("old"));
        var previousBackup = File.ReadAllBytes(BackupPath);
        var candidate = Catalog("new");

        StoreWithBackupWriter((source, dest) =>
        {
            var bytes = File.ReadAllBytes(source);
            File.WriteAllBytes(dest, bytes[..16]);
            throw new IOException("Simulated disk interruption.");
        }).Save(candidate);

        Assert.Equal(candidate.ToJson(), File.ReadAllText(PrimaryPath));
        Assert.Equal(previousBackup, File.ReadAllBytes(BackupPath));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void SecondSave_BackupAccessDenied_DoesNotTurnCommittedSaveIntoFailure()
    {
        var store = NormalStore();
        store.Save(Catalog("old"));
        var previousBackup = File.ReadAllBytes(BackupPath);
        var candidate = Catalog("new");

        StoreWithBackupWriter((_, dest) =>
        {
            File.WriteAllText(dest, "incomplete");
            throw new UnauthorizedAccessException("Simulated backup ACL failure.");
        }).Save(candidate);

        Assert.Equal(candidate.ToJson(), File.ReadAllText(PrimaryPath));
        Assert.Equal(previousBackup, File.ReadAllBytes(BackupPath));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void RetryAfterStagingMismatch_RefreshesBackupAndRestoresAfterPrimaryDamage()
    {
        var store = NormalStore();
        store.Save(Catalog("old"));
        var current = Catalog("recovered");

        StoreWithBackupWriter((source, dest) =>
        {
            var bytes = File.ReadAllBytes(source);
            File.WriteAllBytes(dest, bytes[..10]);
        }).Save(current);
        Assert.NotEqual(current.ToJson(), File.ReadAllText(BackupPath));

        // The next successful save repairs the stale backup; if the primary
        // becomes corrupt afterwards, recovery returns the last valid catalog.
        store.Save(current);
        Assert.Equal(current.ToJson(), File.ReadAllText(BackupPath));
        File.WriteAllText(PrimaryPath, "{unreadable");

        var (loaded, usedBackup) = store.LoadWithRecovery();
        Assert.True(usedBackup);
        Assert.Equal(current.ToJson(), loaded.ToJson());
        Assert.Equal("{unreadable", File.ReadAllText(PrimaryPath));
        AssertNoNewTemporaryFiles();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
