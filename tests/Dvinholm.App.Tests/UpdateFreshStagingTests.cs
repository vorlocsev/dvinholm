using System.IO.Compression;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline ZIP fixtures for single-use staging. Existing files and
/// directories must never be admitted into a verified replacement tree.
/// </summary>
public sealed class UpdateFreshStagingTests
{
    [Fact]
    public async Task FreshStagingFolderExtractsValidArchive()
    {
        using var fixture = new IsolatedZip();
        fixture.WriteZip(("Dvinholm.App.exe", "safe"));
        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath);
        Assert.Equal("safe", File.ReadAllText(Path.Combine(fixture.StagePath, "Dvinholm.App.exe")));
    }

    [Fact]
    public async Task ExistingEmptyStagingFolderIsRejected()
    {
        using var fixture = new IsolatedZip();
        fixture.WriteZip(("Dvinholm.App.exe", "safe"));
        Directory.CreateDirectory(fixture.StagePath);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.StagePath));
    }

    [Fact]
    public async Task ExistingUnrelatedFileIsNotCarriedIntoUpdate()
    {
        using var fixture = new IsolatedZip();
        fixture.WriteZip(("Dvinholm.App.exe", "new"));
        Directory.CreateDirectory(fixture.StagePath);
        var marker = Path.Combine(fixture.StagePath, "untrusted.txt");
        File.WriteAllText(marker, "unchanged");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath));

        Assert.Equal("unchanged", File.ReadAllText(marker));
        Assert.False(File.Exists(Path.Combine(fixture.StagePath, "Dvinholm.App.exe")));
    }

    [Fact]
    public async Task ExistingExecutableIsNotOverwritten()
    {
        using var fixture = new IsolatedZip();
        fixture.WriteZip(("Dvinholm.App.exe", "new"));
        Directory.CreateDirectory(fixture.StagePath);
        var existing = Path.Combine(fixture.StagePath, "Dvinholm.App.exe");
        File.WriteAllText(existing, "old");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath));

        Assert.Equal("old", File.ReadAllText(existing));
    }

    [Fact]
    public async Task ExistingNestedDirectoryIsPreservedUntouched()
    {
        using var fixture = new IsolatedZip();
        fixture.WriteZip(("Dvinholm.App.exe", "safe"), ("Assets/new.txt", "new"));
        var oldFolder = Directory.CreateDirectory(Path.Combine(fixture.StagePath, "Assets"));
        var old = Path.Combine(oldFolder.FullName, "old.txt");
        File.WriteAllText(old, "original");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath));

        Assert.Equal("original", File.ReadAllText(old));
        Assert.False(File.Exists(Path.Combine(oldFolder.FullName, "new.txt")));
    }

    [Fact]
    public async Task ExistingFileAtStagingPathIsNotReplaced()
    {
        using var fixture = new IsolatedZip();
        fixture.WriteZip(("Dvinholm.App.exe", "safe"));
        File.WriteAllText(fixture.StagePath, "marker");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath));

        Assert.Equal("marker", File.ReadAllText(fixture.StagePath));
        Assert.False(Directory.Exists(fixture.StagePath));
    }

    [Fact]
    public async Task FreshNestedTreeWithExplicitDirectoriesStillExtracts()
    {
        using var fixture = new IsolatedZip();
        using (var archive = ZipFile.Open(fixture.ZipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("Assets/");
            AddText(archive, "Dvinholm.App.exe", "valid");
            AddText(archive, "Assets/icon.ico", "icon");
        }

        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(fixture.ZipPath, fixture.StagePath);

        Assert.Equal("valid", File.ReadAllText(Path.Combine(fixture.StagePath, "Dvinholm.App.exe")));
        Assert.Equal("icon", File.ReadAllText(Path.Combine(fixture.StagePath, "Assets", "icon.ico")));
    }

    [Fact]
    public void StagingCollisionGuardPrecedesAnyExtractionWrite()
    {
        var source = ReadUpdaterSource();
        var start = source.IndexOf("internal static Task ValidateAndExtractArchiveAsync(", StringComparison.Ordinal);
        var end = source.IndexOf("internal static void ValidateArchiveEntryType(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = source[start..end];
        var paths = method.IndexOf("if (fileTargets.Overlaps(requiredDirectories))", StringComparison.Ordinal);
        var exists = method.IndexOf("if (Directory.Exists(staging) || File.Exists(staging))", StringComparison.Ordinal);
        var create = method.IndexOf("Directory.CreateDirectory(staging);", StringComparison.Ordinal);
        var copy = method.IndexOf("await CopyArchiveEntryAsync(input, output, entry.Length, cancellationToken);", StringComparison.Ordinal);
        Assert.True(paths >= 0 && exists > paths && create > exists && copy > create);
    }

    private static void AddText(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
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
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "dvinholm-fresh-stage-" + Guid.NewGuid().ToString("N"));
        internal string ZipPath => Path.Combine(_root, "fixture.zip");
        internal string StagePath => Path.Combine(_root, "stage");
        internal IsolatedZip() => Directory.CreateDirectory(_root);
        internal void WriteZip(params (string name, string content)[] files)
        {
            using var archive = ZipFile.Open(ZipPath, ZipArchiveMode.Create);
            foreach (var (name, content) in files)
                AddText(archive, name, content);
        }
        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
