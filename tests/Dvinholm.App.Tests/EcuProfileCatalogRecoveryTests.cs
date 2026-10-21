using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Offline data-recovery acceptance. All eight tests use isolated local files,
/// with no J2534 hardware, WinUI automation or system permission changes.
/// </summary>
public sealed class EcuProfileCatalogRecoveryTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "Dvinholm-ecu-catalog-recovery",
        Guid.NewGuid().ToString("N"));

    public EcuProfileCatalogRecoveryTests() => Directory.CreateDirectory(_directory);

    private string PrimaryPath => Path.Combine(_directory, "ecu-profiles.json");
    private string BackupPath => PrimaryPath + ".bak";
    private EcuProfileCatalogStore Store => new(PrimaryPath);

    private static EcuProfileCatalog Catalog(string id = "synthetic.ecm") => new([
        new EcuProfile(id, "Synthetic ECM", 0x7E0, 0x7E8, false,
            [0xF187, 0xF190],
            "Test-only fixture; not verified against any vehicle.")
    ]);

    private void AssertNoTemporaryFiles() =>
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp", SearchOption.AllDirectories));

    [Fact]
    public void FirstSave_CreatesReadableAtomicBackup()
    {
        var expected = Catalog();

        Store.Save(expected);

        Assert.Equal(expected.ToJson(), File.ReadAllText(PrimaryPath));
        Assert.Equal(expected.ToJson(), File.ReadAllText(BackupPath));
        var (loaded, recovered) = Store.LoadWithRecovery();
        Assert.False(recovered);
        Assert.Equal(expected.ToJson(), loaded.ToJson());
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void SubsequentSave_RefreshesBackupToLatestCommittedCatalog()
    {
        Store.Save(Catalog("old.ecm"));
        var newer = Catalog("new.ecm");

        Store.Save(newer);

        Assert.Equal(newer.ToJson(), File.ReadAllText(PrimaryPath));
        Assert.Equal(newer.ToJson(), File.ReadAllText(BackupPath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void MalformedMain_RecoversFromBackupWithoutEditingEitherFile()
    {
        var original = Catalog();
        Store.Save(original);
        const string damaged = "{ this is not JSON";
        File.WriteAllText(PrimaryPath, damaged);

        var (loaded, recovered) = Store.LoadWithRecovery();

        Assert.True(recovered);
        Assert.Equal(original.ToJson(), loaded.ToJson());
        Assert.Equal(damaged, File.ReadAllText(PrimaryPath));
        Assert.Equal(original.ToJson(), File.ReadAllText(BackupPath));
        Assert.Empty(Directory.GetFiles(_directory, "*.corrupt-*.json"));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void TruncatedMain_RecoversAllDidsAndSourceFromBackup()
    {
        var catalog = Catalog();
        Store.Save(catalog);
        File.WriteAllText(PrimaryPath, catalog.ToJson()[..26]);

        var (recoveredCatalog, recovered) = Store.LoadWithRecovery();

        Assert.True(recovered);
        var profile = Assert.Single(recoveredCatalog.All);
        Assert.Equal(catalog.All[0].Source, profile.Source);
        Assert.Equal(new ushort[] { 0xF187, 0xF190 },
            profile.ReadableDataIdentifiers.Order().ToArray());
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void ValidMain_IgnoresDamagedBackup()
    {
        var catalog = Catalog();
        Store.Save(catalog);
        File.WriteAllText(BackupPath, "[broken backup");

        var (loaded, recovered) = Store.LoadWithRecovery();

        Assert.False(recovered);
        Assert.Equal(catalog.ToJson(), loaded.ToJson());
        Assert.Equal("[broken backup", File.ReadAllText(BackupPath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void BothCopiesInvalid_FailsWithoutReplacingUserData()
    {
        File.WriteAllText(PrimaryPath, "{broken");
        File.WriteAllText(BackupPath, "[broken");

        var error = Assert.Throws<InvalidDataException>(() => Store.LoadWithRecovery());

        Assert.Contains("both invalid", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("{broken", File.ReadAllText(PrimaryPath));
        Assert.Equal("[broken", File.ReadAllText(BackupPath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void InvalidMainWithNoBackup_FailsAndPreservesOriginalBytes()
    {
        var damagedBytes = new byte[] { 0x5B, 0x7B, 0x00, 0x01, 0x4E };
        File.WriteAllBytes(PrimaryPath, damagedBytes);

        Assert.ThrowsAny<Exception>(() => Store.LoadWithRecovery());

        Assert.Equal(damagedBytes, File.ReadAllBytes(PrimaryPath));
        Assert.False(File.Exists(BackupPath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void SavingRecoveredCatalog_ArchivesDamagedOriginalBeforeReplacingIt()
    {
        var original = Catalog();
        Store.Save(original);
        var damagedBytes = new byte[] { 0x7B, 0x01, 0xFF, 0x7D };
        File.WriteAllBytes(PrimaryPath, damagedBytes);
        var (recoveredCatalog, usedBackup) = Store.LoadWithRecovery();
        Assert.True(usedBackup);

        Store.Save(recoveredCatalog);

        var archive = Assert.Single(Directory.GetFiles(
            _directory, "ecu-profiles.json.corrupt-*.json"));
        Assert.Equal(damagedBytes, File.ReadAllBytes(archive));
        Assert.Equal(original.ToJson(), File.ReadAllText(PrimaryPath));
        Assert.Equal(original.ToJson(), File.ReadAllText(BackupPath));
        var (afterRestart, againRecovered) = Store.LoadWithRecovery();
        Assert.False(againRecovered);
        Assert.Equal(original.ToJson(), afterRestart.ToJson());
        AssertNoTemporaryFiles();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
