using System.IO.Compression;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline gates for conflicting Windows ZIP targets. All failures
/// must happen before private staging is created; no updater is launched.
/// </summary>
public sealed class UpdateZipPathCollisionTests
{
    [Fact]
    public async Task DuplicateFileIsRejectedBeforeAnyStagingWrite() =>
        await RejectWithoutStagingAsync("Assets/icon.txt", "Assets/icon.txt");

    [Fact]
    public async Task SameFileWithDifferentWindowsCaseIsRejected() =>
        await RejectWithoutStagingAsync("Assets/Icon.txt", "assets/icon.TXT");

    [Fact]
    public async Task DuplicateExplicitDirectoryIsRejected() =>
        await RejectWithoutStagingAsync("Assets/", "Assets/");

    [Fact]
    public async Task FileAndExplicitDirectoryAreNeverTheSameTarget() =>
        await RejectWithoutStagingAsync("Assets", "assets/");

    [Fact]
    public async Task EarlierFileCannotBecomeImplicitDirectory() =>
        await RejectWithoutStagingAsync("Assets", "Assets/icon.txt");

    [Fact]
    public async Task LaterFileCannotReplaceRequiredImplicitDirectory() =>
        await RejectWithoutStagingAsync("Assets/icon.txt", "assets");

    [Fact]
    public async Task NormalizedAliasAtEndDoesNotLeaveEarlierFiles()
    {
        using var test = new IsolatedZip();
        using (var archive = ZipFile.Open(test.ZipPath, ZipArchiveMode.Create))
        {
            AddFile(archive, "Dvinholm.App.exe", "legitimate fixture");
            AddFile(archive, "Assets/icon.txt", "some bytes");
            AddFile(archive, "Assets/../Dvinholm.App.exe", "alias");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(test.ZipPath, test.StagePath));
        Assert.False(Directory.Exists(test.StagePath));
    }

    [Fact]
    public async Task NormalExplicitAndImplicitDirectoriesExtractWithoutFalseCollision()
    {
        using var test = new IsolatedZip();
        using (var archive = ZipFile.Open(test.ZipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("Assets/");
            archive.CreateEntry("Assets/Nested/");
            AddFile(archive, "Dvinholm.App.exe", "exe");
            AddFile(archive, "Assets/icon.txt", "icon");
            AddFile(archive, "Assets/Nested/info.txt", "details");
        }

        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(test.ZipPath, test.StagePath);
        Assert.Equal("exe", File.ReadAllText(Path.Combine(test.StagePath, "Dvinholm.App.exe")));
        Assert.Equal("icon", File.ReadAllText(Path.Combine(test.StagePath, "Assets", "icon.txt")));
        Assert.Equal("details", File.ReadAllText(Path.Combine(test.StagePath, "Assets", "Nested", "info.txt")));
    }

    private static async Task RejectWithoutStagingAsync(string first, string second)
    {
        using var test = new IsolatedZip();
        using (var archive = ZipFile.Open(test.ZipPath, ZipArchiveMode.Create))
        {
            AddFile(archive, "Dvinholm.App.exe", "valid fixture");
            AddEntry(archive, first);
            AddEntry(archive, second);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(test.ZipPath, test.StagePath));
        Assert.False(Directory.Exists(test.StagePath));
    }

    private static void AddEntry(ZipArchive archive, string name)
    {
        if (name.EndsWith("/", StringComparison.Ordinal))
            archive.CreateEntry(name);
        else
            AddFile(archive, name, "fixture");
    }

    private static void AddFile(ZipArchive archive, string name, string value)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(value);
    }

    private sealed class IsolatedZip : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), "dvinholm-zip-collision-" + Guid.NewGuid().ToString("N"));

        internal string ZipPath => Path.Combine(_root, "test.zip");
        internal string StagePath => Path.Combine(_root, "stage");

        internal IsolatedZip() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
