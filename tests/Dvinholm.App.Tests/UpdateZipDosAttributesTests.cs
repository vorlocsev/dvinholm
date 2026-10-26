using System.IO.Compression;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks of DOS ZIP metadata, before any staged file writes.
/// Tests use disposable ZIPs, no installed updater or J2534 adapter.
/// </summary>
public sealed class UpdateZipDosAttributesTests
{
    [Fact]
    public async Task DirectoryFlagOnRegularFileIsRejectedBeforeStaging() =>
        await RejectBeforeStageAsync("Assets/masked.txt", 0x10);

    [Fact]
    public async Task NormalDirectoryWithDosDirectoryFlagIsAccepted()
    {
        using var fixture = new IsolatedZip();
        using (var zip = ZipFile.Open(fixture.ZipPath, ZipArchiveMode.Create))
        {
            AddText(zip, "Dvinholm.App.exe", "valid");
            zip.CreateEntry("Assets/").ExternalAttributes = 0x10;
            AddText(zip, "Assets/icon.ico", "icon");
        }

        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath);
        Assert.Equal("icon", File.ReadAllText(Path.Combine(fixture.StagePath, "Assets", "icon.ico")));
    }

    [Fact]
    public async Task DirectoryWithoutDosFlagRemainsAccepted()
    {
        using var fixture = new IsolatedZip();
        using (var zip = ZipFile.Open(fixture.ZipPath, ZipArchiveMode.Create))
        {
            AddText(zip, "Dvinholm.App.exe", "valid");
            zip.CreateEntry("Assets/").ExternalAttributes = 0;
        }

        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath);
        Assert.True(Directory.Exists(Path.Combine(fixture.StagePath, "Assets")));
    }

    [Fact]
    public async Task DosVolumeLabelFileIsRejectedBeforeStaging() =>
        await RejectBeforeStageAsync("Assets/label.bin", 0x08);

    [Fact]
    public async Task DosVolumeLabelDirectoryIsRejectedBeforeStaging() =>
        await RejectBeforeStageAsync("Assets/", 0x18);

    [Fact]
    public async Task DosDeviceFlagIsRejectedBeforeStaging() =>
        await RejectBeforeStageAsync("Assets/device.dat", 0x40);

    [Fact]
    public async Task BenignArchiveHiddenAndSystemBitsRemainAccepted()
    {
        using var fixture = new IsolatedZip();
        using (var zip = ZipFile.Open(fixture.ZipPath, ZipArchiveMode.Create))
        {
            AddText(zip, "Dvinholm.App.exe", "valid", 0x20);
            AddText(zip, "Assets/hidden.ini", "safe", 0x02);
            AddText(zip, "Assets/system.txt", "safe", 0x04);
        }

        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath);
        Assert.Equal("safe", File.ReadAllText(Path.Combine(fixture.StagePath, "Assets", "hidden.ini")));
        Assert.Equal("safe", File.ReadAllText(Path.Combine(fixture.StagePath, "Assets", "system.txt")));
    }

    [Fact]
    public async Task LateInvalidDosMetadataCannotLeaveEarlierExtractedFiles()
    {
        using var fixture = new IsolatedZip();
        using (var zip = ZipFile.Open(fixture.ZipPath, ZipArchiveMode.Create))
        {
            AddText(zip, "Dvinholm.App.exe", "not written");
            AddText(zip, "Assets/good.txt", "not written");
            AddText(zip, "Assets/late.bin", "invalid", 0x10);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath));
        Assert.False(Directory.Exists(fixture.StagePath));

        var source = ReadUpdaterSource();
        var typeCheck = source.IndexOf("ValidateArchiveEntryType(entry);", StringComparison.Ordinal);
        var createStage = source.IndexOf("Directory.CreateDirectory(staging);", typeCheck, StringComparison.Ordinal);
        Assert.True(typeCheck >= 0 && createStage > typeCheck);
        Assert.Contains("dosVolumeLabel | dosDevice", source);
    }

    private static async Task RejectBeforeStageAsync(string badEntry, int attributes)
    {
        using var fixture = new IsolatedZip();
        using (var zip = ZipFile.Open(fixture.ZipPath, ZipArchiveMode.Create))
        {
            AddText(zip, "Dvinholm.App.exe", "preceding legitimate file");
            if (badEntry.EndsWith("/", StringComparison.Ordinal))
                zip.CreateEntry(badEntry).ExternalAttributes = attributes;
            else
                AddText(zip, badEntry, "unsafe", attributes);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath));
        Assert.False(Directory.Exists(fixture.StagePath));
    }

    private static void AddText(ZipArchive zip, string name, string text, int? attributes = null)
    {
        var entry = zip.CreateEntry(name);
        if (attributes.HasValue)
            entry.ExternalAttributes = attributes.Value;
        using var writer = new StreamWriter(entry.Open());
        writer.Write(text);
    }

    private static string ReadUpdaterSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "Dvinholm.App", "LatestBuildUpdater.cs");
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        throw new FileNotFoundException("LatestBuildUpdater.cs not found.");
    }

    private sealed class IsolatedZip : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "dvinholm-zip-dos-" + Guid.NewGuid().ToString("N"));
        internal string ZipPath => Path.Combine(_root, "fixture.zip");
        internal string StagePath => Path.Combine(_root, "stage");
        internal IsolatedZip() => Directory.CreateDirectory(_root);
        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
