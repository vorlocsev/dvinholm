using System.IO.Compression;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks of ZIP external file-type metadata. Reject hostile
/// records before creating any private stage; never touch installed files.
/// </summary>
public sealed class UpdateZipSpecialFileTests
{
    [Fact]
    public async Task UnixSymbolicLinkIsRejectedBeforeStaging() =>
        await RejectBeforeStageAsync("Assets/link.txt", UnixMode(0xA1FF));

    [Fact]
    public async Task UnixFifoAndSocketAreRejectedBeforeStaging()
    {
        await RejectBeforeStageAsync("Assets/pipe", UnixMode(0x11A4));
        await RejectBeforeStageAsync("Assets/socket", UnixMode(0xC1FF));
    }

    [Fact]
    public async Task UnixCharacterAndBlockDevicesAreRejected()
    {
        await RejectBeforeStageAsync("Assets/tty", UnixMode(0x21A4));
        await RejectBeforeStageAsync("Assets/block", UnixMode(0x61A4));
    }

    [Fact]
    public async Task UnixDirectoryMetadataOnFileNameIsRejected() =>
        await RejectBeforeStageAsync("Assets/looks-like-a-file", UnixMode(0x41ED));

    [Fact]
    public async Task UnixRegularFileMetadataOnDirectoryNameIsRejected() =>
        await RejectBeforeStageAsync("Assets/", UnixMode(0x81A4));

    [Fact]
    public async Task ReparsePointFlagIsRejectedBeforeStaging() =>
        await RejectBeforeStageAsync("Assets/junction", 0x400);

    [Fact]
    public async Task NormalWindowsAndUnixRegularEntriesExtract()
    {
        using var test = new IsolatedZip();
        using (var archive = ZipFile.Open(test.ZipPath, ZipArchiveMode.Create))
        {
            AddText(archive, "Dvinholm.App.exe", "valid", UnixMode(0x81A4));
            archive.CreateEntry("Assets/").ExternalAttributes = UnixMode(0x41ED);
            AddText(archive, "Assets/config.json", "safe", UnixMode(0x81A4));
            AddText(archive, "Assets/default.bin", "default", 0);
        }

        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(test.ZipPath, test.StagePath);

        Assert.Equal("valid", File.ReadAllText(Path.Combine(test.StagePath, "Dvinholm.App.exe")));
        Assert.Equal("safe", File.ReadAllText(Path.Combine(test.StagePath, "Assets", "config.json")));
        Assert.Equal("default", File.ReadAllText(Path.Combine(test.StagePath, "Assets", "default.bin")));
    }

    [Fact]
    public async Task PreflightRejectsLateSpecialEntryBeforeWritingEarlierFiles()
    {
        using var test = new IsolatedZip();
        using (var archive = ZipFile.Open(test.ZipPath, ZipArchiveMode.Create))
        {
            AddText(archive, "Dvinholm.App.exe", "should not be written");
            AddText(archive, "Assets/good.txt", "good");
            AddText(archive, "Assets/late-link", "bad", UnixMode(0xA1FF));
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(test.ZipPath, test.StagePath));
        Assert.False(Directory.Exists(test.StagePath));

        var code = FindUpdaterSource();
        var check = code.IndexOf("ValidateArchiveEntryType(entry);", StringComparison.Ordinal);
        var create = code.IndexOf("Directory.CreateDirectory(staging);", check, StringComparison.Ordinal);
        Assert.True(check >= 0 && create > check);
    }

    private static async Task RejectBeforeStageAsync(string name, int attributes)
    {
        using var test = new IsolatedZip();
        using (var archive = ZipFile.Open(test.ZipPath, ZipArchiveMode.Create))
        {
            AddText(archive, "Dvinholm.App.exe", "legitimate");
            if (name.EndsWith("/", StringComparison.Ordinal))
                archive.CreateEntry(name).ExternalAttributes = attributes;
            else
                AddText(archive, name, "hostile", attributes);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(test.ZipPath, test.StagePath));
        Assert.False(Directory.Exists(test.StagePath));
    }

    private static int UnixMode(int mode) => mode << 16;

    private static void AddText(ZipArchive archive, string name, string text, int? attributes = null)
    {
        var entry = archive.CreateEntry(name);
        if (attributes.HasValue)
            entry.ExternalAttributes = attributes.Value;
        using var writer = new StreamWriter(entry.Open());
        writer.Write(text);
    }

    private static string FindUpdaterSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "Dvinholm.App", "LatestBuildUpdater.cs");
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        throw new FileNotFoundException("LatestBuildUpdater.cs unavailable.");
    }

    private sealed class IsolatedZip : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "dvinholm-zip-kind-" + Guid.NewGuid().ToString("N"));
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
