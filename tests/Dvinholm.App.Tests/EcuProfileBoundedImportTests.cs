using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic bounded profile-catalog import tests. No adapter,
/// diagnostic session or vehicle transmission is created.
/// </summary>
public sealed class EcuProfileBoundedImportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(),
        "Dvinholm-bounded-profile-import-" + Guid.NewGuid().ToString("N"));

    public EcuProfileBoundedImportTests() => Directory.CreateDirectory(_directory);

    private string PathFor(string file) => Path.Combine(_directory, file);

    [Fact]
    public async Task ValidExistingUtf8BomAndPlainCatalogsStillLoad()
    {
        var json = Catalog(2).ToJson();
        var path = PathFor("bom.json");
        File.WriteAllBytes(path,
            new UTF8Encoding(true).GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(json)).ToArray());
        Assert.Equal(json, EcuProfileCatalogInput.ReadFile(path));
        Assert.Equal(json, await EcuProfileCatalogInput.ReadFileAsync(path));
        Assert.Equal(2, EcuProfileCatalog.FromJson(
            EcuProfileCatalogInput.ReadFile(path)).All.Count);

        File.WriteAllText(path, json, new UTF8Encoding(false));
        Assert.Equal(json, await EcuProfileCatalogInput.ReadFileAsync(path));
    }

    [Fact]
    public async Task OversizedFileIsRejectedBeforeReadingOrPersisting()
    {
        var path = PathFor("oversized.json");
        File.WriteAllBytes(path,
            new byte[EcuProfileCatalogInput.MaximumJsonBytes + 1]);
        Assert.Throws<InvalidDataException>(() => EcuProfileCatalogInput.ReadFile(path));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => EcuProfileCatalogInput.ReadFileAsync(path));
        var calls = 0;
        Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(
                new string(' ', EcuProfileCatalogInput.MaximumJsonBytes + 1),
                _ => calls++));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task MalformedUtf8CannotEnterPrimaryOrUserChosenJson()
    {
        var path = PathFor("invalid-utf8.json");
        File.WriteAllBytes(path, [0x5B, 0x22, 0xC3, 0x28, 0x22, 0x5D]);
        Assert.Throws<InvalidDataException>(() => EcuProfileCatalogInput.ReadFile(path));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => EcuProfileCatalogInput.ReadFileAsync(path));
        Assert.Equal(6, new FileInfo(path).Length);
    }

    [Fact]
    public void MoreThanMaximumProfilesFailsBeforeAnyPersistence()
    {
        var json = "[" + string.Join(",",
            Enumerable.Repeat("{}", EcuProfileCatalogInput.MaximumProfiles + 1)) + "]";
        var calls = 0;
        Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(json, _ => calls++));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ExactlyMaximumProfilesCanRoundTripWithoutDataLoss()
    {
        var catalog = Catalog(EcuProfileCatalogInput.MaximumProfiles);
        var json = catalog.ToJson();
        Assert.InRange(Encoding.UTF8.GetByteCount(json),
            1, EcuProfileCatalogInput.MaximumJsonBytes);
        var imported = EcuProfileCatalog.FromJson(json);
        Assert.Equal(EcuProfileCatalogInput.MaximumProfiles, imported.All.Count);
        Assert.Equal(json, imported.ToJson());
        Assert.True(imported.TryGet("synthetic.511", out _));
    }

    [Fact]
    public void TooDeepOrOversizedJsonCannotReachThePersistCallback()
    {
        var calls = 0;
        var tooDeep = new string('[', EcuProfileCatalogInput.MaximumJsonDepth + 3) +
            "0" + new string(']', EcuProfileCatalogInput.MaximumJsonDepth + 3);
        Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(tooDeep, _ => calls++));
        Assert.Throws<InvalidDataException>(() =>
            EcuProfileCatalogImporter.ImportAndPersist(
                Catalog(1).ToJson() + new string(' ', EcuProfileCatalogInput.MaximumJsonBytes),
                _ => calls++));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void OversizedDamagedPrimaryRecoversFromKnownGoodBackup()
    {
        var path = PathFor("saved.json");
        var store = new EcuProfileCatalogStore(path);
        store.Save(Catalog(2));
        var before = File.ReadAllText(path);
        Assert.Equal(before, File.ReadAllText(path + ".bak"));

        File.WriteAllBytes(path,
            new byte[EcuProfileCatalogInput.MaximumJsonBytes + 1]);
        var (recovered, recoveredFromBackup) = store.LoadWithRecovery();
        Assert.True(recoveredFromBackup);
        Assert.Equal(2, recovered.All.Count);
        Assert.Equal(before, recovered.ToJson());
        Assert.Equal(EcuProfileCatalogInput.MaximumJsonBytes + 1,
            new FileInfo(path).Length);
    }

    [Fact]
    public async Task CancelledReadAndExistingWinUiUseTheSameBoundedReader()
    {
        var path = PathFor("profiles.json");
        File.WriteAllText(path, Catalog(1).ToJson());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => EcuProfileCatalogInput.ReadFileAsync(path, cancelled.Token));

        var source = ReadSource("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("EcuProfileCatalogInput.ReadFileAsync(file.Path)", source);
        Assert.DoesNotContain("File.ReadAllTextAsync(file.Path)", source);
        var store = ReadSource("src/Dvinholm.App/EcuProfileCatalogStore.cs");
        Assert.Contains("EcuProfileCatalogInput.ReadFile(_path)", store);
        Assert.Contains("EcuProfileCatalogInput.ReadFile(BackupPath)", store);
    }

    private static EcuProfileCatalog Catalog(int count) =>
        new(Enumerable.Range(0, count).Select(i =>
            new EcuProfile("synthetic." + i, "Synthetic ECU " + i,
                0x7E0, 0x7E8, false, [0xF187],
                "Offline test fixture only", false)));

    private static string ReadSource(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(relative);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
