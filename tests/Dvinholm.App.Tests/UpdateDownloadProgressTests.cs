namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline gates for actionable download progress.
/// No GitHub access, installation, WinUI launch, or J2534 device is used.
/// </summary>
public sealed class UpdateDownloadProgressTests
{
    [Fact]
    public void RejectsUnknownOrInvalidExpectedArchiveSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UpdateDownloadProgress(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UpdateDownloadProgress(-1));
    }

    [Fact]
    public void RejectsNegativeCopiedByteCount()
    {
        var progress = new UpdateDownloadProgress(100);
        Assert.Throws<ArgumentOutOfRangeException>(() => progress.ReportCopied(-1, "CI 5"));
    }

    [Fact]
    public void SilentBelowFirstTenPercentBoundary()
    {
        var progress = new UpdateDownloadProgress(1000);
        Assert.Null(progress.ReportCopied(0, "CI 5"));
        Assert.Null(progress.ReportCopied(99, "CI 5"));
    }

    [Fact]
    public void FirstMilestoneHasAccurateCountAndInvariantFormatting()
    {
        var progress = new UpdateDownloadProgress(10_000);
        var message = progress.ReportCopied(1_000, "CI #123");
        Assert.NotNull(message);
        Assert.Contains("CI #123", message);
        Assert.Contains("10%", message);
        Assert.Contains("1,000/10,000 bytes", message);
    }

    [Fact]
    public void MultipleSmallReadsDoNotFloodUiProgressQueue()
    {
        var progress = new UpdateDownloadProgress(100);
        var reported = Enumerable.Range(1, 100)
            .Select(x => progress.ReportCopied(x, "test"))
            .Where(x => x is not null)
            .ToArray();
        Assert.Equal(10, reported.Length);
        for (var i = 0; i < 10; i++)
            Assert.Contains($"{(i + 1) * 10}%", reported[i]!);
    }

    [Fact]
    public void LargeReadJumpsForwardWithoutInventingMissingPercentEvents()
    {
        var progress = new UpdateDownloadProgress(100);
        Assert.Contains("30%", progress.ReportCopied(38, "test")!);
        Assert.Null(progress.ReportCopied(39, "test"));
        Assert.Contains("90%", progress.ReportCopied(98, "test")!);
        Assert.Contains("100%", progress.ReportCopied(100, "test")!);
        Assert.Null(progress.ReportCopied(100, "test"));
        Assert.Null(progress.ReportCopied(101, "test"));
    }

    [Fact]
    public void LargeByteCountsDoNotOverflowAndRemainBelowHundredUntilComplete()
    {
        var progress = new UpdateDownloadProgress(long.MaxValue);
        Assert.Contains("50%", progress.ReportCopied(long.MaxValue / 2 + 1, "test")!);
        Assert.Contains("100%", progress.ReportCopied(long.MaxValue, "test")!);
    }

    [Fact]
    public void ActualDownloaderReportsAfterWrittenBytesWithoutBypassingVerification()
    {
        var source = ReadUpdaterSource();
        var methodStart = source.IndexOf("internal static async Task<string> DownloadAndPrepareAsync(", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("internal static void TryCleanup(", methodStart, StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var method = source[methodStart..methodEnd];
        var progress = method.IndexOf("new UpdateDownloadProgress(build.ArtifactSize)", StringComparison.Ordinal);
        var copied = method.IndexOf("await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)", StringComparison.Ordinal);
        var update = method.IndexOf("downloadProgress.ReportCopied(total, build.DisplayVersion)", StringComparison.Ordinal);
        var report = method.IndexOf("progress?.Report(milestoneMessage);", StringComparison.Ordinal);
        var checkSize = method.IndexOf("downloadedSize != build.ArtifactSize", StringComparison.Ordinal);
        var checkDigest = method.IndexOf("SHA256.HashDataAsync(checksumStream, cancellationToken)", StringComparison.Ordinal);
        Assert.True(progress >= 0 && copied > progress && update > copied && report > update);
        Assert.True(checkSize > report && checkDigest > checkSize);
        Assert.Contains("MaximumDownloadBytes", method);
        Assert.Contains("Verifying the downloaded artifact checksum", method);
        Assert.Contains("Validating and extracting the build", method);
    }

    private static string ReadUpdaterSource()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "Dvinholm.App", "LatestBuildUpdater.cs");
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        throw new FileNotFoundException("LatestBuildUpdater.cs unavailable.");
    }
}
