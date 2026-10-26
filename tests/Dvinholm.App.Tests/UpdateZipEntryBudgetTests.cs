using System.IO.Compression;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline regression checks for ZIP directory-entry count limits.
/// No GitHub network, installed updater, WinUI, or J2534 adapter is used.
/// </summary>
public sealed class UpdateZipEntryBudgetTests
{
    [Fact]
    public void EmptyEntryListDoesNotExceedEntryBudget()
    {
        LatestBuildUpdater.ValidateArchiveEntryCount(0);
    }

    [Fact]
    public void SmallRealisticEntryCountIsAllowed()
    {
        LatestBuildUpdater.ValidateArchiveEntryCount(250);
    }

    [Fact]
    public void ExactMaximumEntryCountIsAllowed()
    {
        LatestBuildUpdater.ValidateArchiveEntryCount(LatestBuildUpdater.MaximumArchiveEntries);
    }

    [Fact]
    public void FirstExcessEntryIsRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateArchiveEntryCount(LatestBuildUpdater.MaximumArchiveEntries + 1));
    }

    [Fact]
    public void VeryLargeEntryCountIsRejectedWithoutOverflow()
    {
        Assert.Throws<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateArchiveEntryCount(int.MaxValue));
    }

    [Fact]
    public void NegativeCountCannotBypassBudget()
    {
        Assert.Throws<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateArchiveEntryCount(-1));
    }

    [Fact]
    public async Task ManyEmptyZipFilesAreRejectedWithoutCreatingStagingDirectory()
    {
        using var scratch = new TempZip();
        using (var archive = ZipFile.Open(scratch.ZipPath, ZipArchiveMode.Create))
        {
            for (var i = 0; i <= LatestBuildUpdater.MaximumArchiveEntries; i++)
                archive.CreateEntry($"empty-{i:D5}.txt");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(scratch.ZipPath, scratch.StagePath));

        // Zero-byte files have a negligible expanded-size total; this must
        // still reject their filesystem-entry amplification before any writes.
        Assert.False(Directory.Exists(scratch.StagePath));
    }

    [Fact]
    public async Task ValidSmallZipStillExtractsNestedFilesExactly()
    {
        using var scratch = new TempZip();
        using (var archive = ZipFile.Open(scratch.ZipPath, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "Dvinholm.App.exe", "fixture executable");
            WriteEntry(archive, "Assets/icon.txt", "fixture icon");
        }

        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(scratch.ZipPath, scratch.StagePath);
        Assert.Equal("fixture executable", File.ReadAllText(Path.Combine(scratch.StagePath, "Dvinholm.App.exe")));
        Assert.Equal("fixture icon", File.ReadAllText(Path.Combine(scratch.StagePath, "Assets", "icon.txt")));
    }

    private static void WriteEntry(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(text);
    }

    private sealed class TempZip : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "dvinholm-entry-budget-" + Guid.NewGuid().ToString("N"));
        internal string ZipPath => Path.Combine(_root, "test.zip");
        internal string StagePath => Path.Combine(_root, "stage");

        internal TempZip() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
