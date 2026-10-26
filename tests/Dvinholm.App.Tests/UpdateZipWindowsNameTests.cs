using System.IO.Compression;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline Windows ZIP-name guards. Invalid metadata must be rejected
/// before creating a private staging folder or replacing any application files.
/// </summary>
public sealed class UpdateZipWindowsNameTests
{
    [Fact]
    public async Task NtfsAlternateDataStreamIsRejectedBeforeStaging() =>
        await RejectBeforeStageAsync("Assets/config.txt:stream");

    [Fact]
    public async Task DeviceNameWithExtensionIsRejectedBeforeStaging() =>
        await RejectBeforeStageAsync("CON.txt");

    [Fact]
    public async Task NestedCaseInsensitiveDeviceNameIsRejected() =>
        await RejectBeforeStageAsync("Assets/aux.log");

    [Fact]
    public async Task ComLptAndSuperscriptDeviceNumbersAreRejected()
    {
        foreach (var name in new[] { "COM1.log", "lpt9.bin", "Assets/COM¹.txt", "LPT²", "conout$.txt" })
            await RejectBeforeStageAsync(name);
    }

    [Fact]
    public async Task Win32TrailingDotAndSpaceAliasesAreRejected()
    {
        await RejectBeforeStageAsync("Assets/icon.");
        await RejectBeforeStageAsync("Assets/icon ");
    }

    [Fact]
    public async Task WildcardOrControlCharacterAndQuoteAreRejected()
    {
        foreach (var name in new[] { "Assets/file?.txt", "Assets/bad|name.txt", "Assets/bad\"name.txt", "Assets/bad\u0001name" })
            await RejectBeforeStageAsync(name);
    }

    [Fact]
    public async Task BackslashAndSafeLookingDotAliasesAreRejected()
    {
        foreach (var name in new[] { @"Assets\icon.txt", "Assets/./icon.txt", "Assets/../icon.txt", "Assets//icon.txt" })
            await RejectBeforeStageAsync(name);
    }

    [Fact]
    public async Task NormalNestedWindowsZipExtractsAndPreflightPrecedesFirstWrite()
    {
        using var fixture = new IsolatedZip();
        using (var archive = ZipFile.Open(fixture.ZipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("Assets/");
            AddText(archive, "Dvinholm.App.exe", "fixture executable");
            AddText(archive, "Assets/.manifest", "hidden name is legal");
            AddText(archive, "Assets/safe-file.txt", "hello");
        }

        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath);
        Assert.Equal("fixture executable", File.ReadAllText(Path.Combine(fixture.StagePath, "Dvinholm.App.exe")));
        Assert.Equal("hidden name is legal", File.ReadAllText(Path.Combine(fixture.StagePath, "Assets", ".manifest")));
        Assert.Equal("hello", File.ReadAllText(Path.Combine(fixture.StagePath, "Assets", "safe-file.txt")));

        var source = FindUpdaterSource();
        var preflight = source.IndexOf("ValidateWindowsArchiveEntryName(entry.FullName);", StringComparison.Ordinal);
        var stageCreation = source.IndexOf("Directory.CreateDirectory(staging);", preflight, StringComparison.Ordinal);
        Assert.True(preflight >= 0 && stageCreation > preflight);
    }

    private static async Task RejectBeforeStageAsync(string badEntry)
    {
        using var fixture = new IsolatedZip();
        using (var archive = ZipFile.Open(fixture.ZipPath, ZipArchiveMode.Create))
        {
            AddText(archive, "Dvinholm.App.exe", "valid preceding entry");
            AddText(archive, badEntry, "unsafe");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath));
        Assert.False(Directory.Exists(fixture.StagePath));
    }

    private static void AddText(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(text);
    }

    private static string FindUpdaterSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var source = Path.Combine(dir.FullName, "src", "Dvinholm.App", "LatestBuildUpdater.cs");
            if (File.Exists(source))
                return File.ReadAllText(source);
        }
        throw new FileNotFoundException("LatestBuildUpdater.cs missing from repository.");
    }

    private sealed class IsolatedZip : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "dvinholm-zip-winname-" + Guid.NewGuid().ToString("N"));
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
