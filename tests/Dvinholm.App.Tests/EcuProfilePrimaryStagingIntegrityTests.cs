using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline primary-staging integrity regressions. A temporary JSON
/// writer that returns early must never replace the last committed catalog.
/// </summary>
public sealed class EcuProfilePrimaryStagingIntegrityTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "Dvinholm-primary-staging-integrity", Guid.NewGuid().ToString("N"));

    public EcuProfilePrimaryStagingIntegrityTests() => Directory.CreateDirectory(_directory);

    private string PrimaryPath => Path.Combine(_directory, "ecu-profiles.json");
    private string BackupPath => PrimaryPath + ".bak";
    private EcuProfileCatalogStore NormalStore => new(PrimaryPath);

    private static EcuProfileCatalog Catalog(string id = "new.ecm") => new([
        new EcuProfile(id, "Synthetic ECM", 0x7E0, 0x7E8, false,
            [0xF187, 0xF190], "Synthetic integrity fixture only", false)
    ]);

    private EcuProfileCatalogStore WithWriter(Action<string, string> writer) =>
        new(PrimaryPath, writeTemporaryFile: writer);

    private void AssertNoStagingFiles() =>
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp", SearchOption.AllDirectories));

    private (string Primary, string Backup) SaveOldAndCapture()
    {
        NormalStore.Save(Catalog("old.ecm"));
        return (File.ReadAllText(PrimaryPath), File.ReadAllText(BackupPath));
    }

    private void AssertUnchanged((string Primary, string Backup) before)
    {
        Assert.Equal(before.Primary, File.ReadAllText(PrimaryPath));
        Assert.Equal(before.Backup, File.ReadAllText(BackupPath));
        AssertNoStagingFiles();
    }

    [Fact]
    public void TruncatedNonemptyStageReturningNormallyPreservesLastCatalog()
    {
        var before = SaveOldAndCapture();
        var faulty = WithWriter((path, json) => File.WriteAllText(path, json[..20]));

        Assert.Throws<IOException>(() => faulty.Save(Catalog()));
        AssertUnchanged(before);
        Assert.Equal("old.ecm", Assert.Single(NormalStore.Load().All).Id);
    }

    [Fact]
    public void WellFormedButIncorrectEmptyCatalogStageDoesNotEraseProfiles()
    {
        var before = SaveOldAndCapture();
        var faulty = WithWriter((path, _) => File.WriteAllText(path, "[]"));

        Assert.Throws<IOException>(() => faulty.Save(Catalog()));
        AssertUnchanged(before);
        Assert.Single(NormalStore.Load().All);
    }

    [Fact]
    public void EqualLengthModifiedJsonIsRejectedEvenWhenItCanBeParsed()
    {
        var before = SaveOldAndCapture();
        var faulty = WithWriter((path, json) =>
        {
            Assert.Contains("new.ecm", json);
            File.WriteAllText(path, json.Replace("new.ecm", "bad.ecm", StringComparison.Ordinal));
        });

        Assert.Throws<IOException>(() => faulty.Save(Catalog()));
        AssertUnchanged(before);
    }

    [Fact]
    public void FirstSaveWithTruncatedNonemptyStageCreatesNeitherPrimaryNorBackup()
    {
        var faulty = WithWriter((path, json) => File.WriteAllText(path, json[..10]));

        Assert.Throws<IOException>(() => faulty.Save(Catalog()));
        Assert.False(File.Exists(PrimaryPath));
        Assert.False(File.Exists(BackupPath));
        AssertNoStagingFiles();
    }

    [Fact]
    public void MissingStageReturningNormallyPreservesPrimaryAndBackup()
    {
        var before = SaveOldAndCapture();

        Assert.Throws<IOException>(() => WithWriter((_, _) => { }).Save(Catalog()));
        AssertUnchanged(before);
    }

    [Fact]
    public void EmptyStageReturningNormallyPreservesPrimaryAndBackup()
    {
        var before = SaveOldAndCapture();

        Assert.Throws<IOException>(() =>
            WithWriter((path, _) => File.WriteAllText(path, "")).Save(Catalog()));
        AssertUnchanged(before);
    }

    [Fact]
    public void SuccessfulRetryAfterStageMismatchUpdatesPrimaryAndBackup()
    {
        var before = SaveOldAndCapture();
        Assert.Throws<IOException>(() =>
            WithWriter((path, _) => File.WriteAllText(path, "[]")).Save(Catalog()));
        AssertUnchanged(before);

        var newCatalog = Catalog();
        NormalStore.Save(newCatalog);

        Assert.Equal(newCatalog.ToJson(), File.ReadAllText(PrimaryPath));
        Assert.Equal(newCatalog.ToJson(), File.ReadAllText(BackupPath));
        var (loaded, recovered) = NormalStore.LoadWithRecovery();
        Assert.False(recovered);
        Assert.Equal("new.ecm", Assert.Single(loaded.All).Id);
        AssertNoStagingFiles();
    }

    [Fact]
    public void CompleteUnicodeCatalogStageRoundTripsWithoutFalseRejection()
    {
        var catalog = new EcuProfileCatalog([
            new EcuProfile("first.ecm", "Северная Двина", 0x7E0, 0x7E8, false,
                [0xF190, 0xF187], "Синтетический тестовый профиль", false),
            new EcuProfile("second.tcm", "Transmission", 0x7E1, 0x7E9, false,
                [0x1234], "Independent test-only source", false)
        ]);

        NormalStore.Save(catalog);

        Assert.Equal(catalog.ToJson(), File.ReadAllText(PrimaryPath));
        Assert.Equal(catalog.ToJson(), File.ReadAllText(BackupPath));
        Assert.Equal(2, NormalStore.Load().All.Count);
        AssertNoStagingFiles();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
