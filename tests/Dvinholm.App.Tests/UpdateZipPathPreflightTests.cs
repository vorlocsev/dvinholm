using System.IO.Compression;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks: reject hostile ZIP paths before touching private
/// staging, while retaining normal nested-file extraction.
/// </summary>
public sealed class UpdateZipPathPreflightTests
{
    [Fact]
    public void SourceValidatesEveryPathBeforeCreatingStage()
    {
        var source = ReadUpdaterSource();
        var start = source.IndexOf("internal static Task ValidateAndExtractArchiveAsync(", StringComparison.Ordinal);
        var end = source.IndexOf("internal static async Task CopyArchiveEntryAsync(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = source[start..end];

        var entryCount = region.IndexOf("ValidateArchiveEntryCount(", StringComparison.Ordinal);
        var expanded = region.IndexOf("ValidateExpandedSize(", StringComparison.Ordinal);
        var fullRoot = region.IndexOf("var fullRoot = Path.GetFullPath(staging)", StringComparison.Ordinal);
        var pathCheck = region.IndexOf("The artifact contains an unsafe archive path.", StringComparison.Ordinal);
        var createStage = region.IndexOf("Directory.CreateDirectory(staging);", StringComparison.Ordinal);
        var extraction = region.IndexOf("Directory.CreateDirectory(target);", StringComparison.Ordinal);
        Assert.True(entryCount >= 0 && expanded > entryCount && fullRoot > expanded);
        Assert.True(pathCheck > fullRoot && createStage > pathCheck && extraction > createStage);
    }

    [Fact]
    public async Task ParentTraversalDoesNotCreateStagingDirectory() =>
        await RejectBeforeStageAsync("../outside.txt");

    [Fact]
    public async Task NestedParentTraversalDoesNotCreateStagingDirectory() =>
        await RejectBeforeStageAsync("Assets/../../outside.txt");

    [Fact]
    public async Task SiblingDirectorySharingStagingPrefixIsRejected() =>
        await RejectBeforeStageAsync("../stage-sibling/forged.txt");

    [Fact]
    public async Task AbsoluteZipPathIsRejectedBeforeStaging() =>
        await RejectBeforeStageAsync("/absolute-outside.txt");

    [Fact]
    public async Task DotOnlyEntryCannotResolveToStagingRoot() =>
        await RejectBeforeStageAsync(".");

    [Fact]
    public async Task BadEntryAtEndPreventsAnyEarlierGoodFileFromBeingCreated()
    {
        using var test = new IsolatedZip();
        using (var archive = ZipFile.Open(test.ZipPath, ZipArchiveMode.Create))
        {
            AddText(archive, "Dvinholm.App.exe", "legitimate fixture");
            AddText(archive, "Assets/icon.txt", "icon fixture");
            AddText(archive, "../../escape.txt", "hostile fixture");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(test.ZipPath, test.StagePath));
        Assert.False(Directory.Exists(test.StagePath));
        Assert.False(File.Exists(Path.Combine(test.Root, "escape.txt")));
    }

    [Fact]
    public async Task NormalNestedFilesAndExplicitDirectoriesStillExtract()
    {
        using var test = new IsolatedZip();
        using (var archive = ZipFile.Open(test.ZipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("Assets/");
            AddText(archive, "Dvinholm.App.exe", "exe fixture");
            AddText(archive, "Assets/icon.txt", "icon fixture");
            AddText(archive, "Assets/Nested/info.txt", "nested fixture");
        }

        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(test.ZipPath, test.StagePath);
        Assert.Equal("exe fixture", File.ReadAllText(Path.Combine(test.StagePath, "Dvinholm.App.exe")));
        Assert.Equal("icon fixture", File.ReadAllText(Path.Combine(test.StagePath, "Assets", "icon.txt")));
        Assert.Equal("nested fixture", File.ReadAllText(Path.Combine(test.StagePath, "Assets", "Nested", "info.txt")));
    }

    private static async Task RejectBeforeStageAsync(string entry)
    {
        using var test = new IsolatedZip();
        using (var archive = ZipFile.Open(test.ZipPath, ZipArchiveMode.Create))
            AddText(archive, entry, "fixture");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(test.ZipPath, test.StagePath));
        Assert.False(Directory.Exists(test.StagePath));
    }

    private static void AddText(ZipArchive archive, string entry, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(entry).Open());
        writer.Write(text);
    }

    private static string ReadUpdaterSource()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory);
             folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName, "src", "Dvinholm.App", "LatestBuildUpdater.cs");
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        throw new FileNotFoundException("LatestBuildUpdater.cs missing for ZIP source check.");
    }

    private sealed class IsolatedZip : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "dvinholm-zip-preflight-" + Guid.NewGuid().ToString("N"));
        internal string ZipPath => Path.Combine(Root, "archive.zip");
        internal string StagePath => Path.Combine(Root, "stage");

        internal IsolatedZip() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
