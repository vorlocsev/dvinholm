namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for rendering real downloaded-byte milestones
/// without displaying fake percentages for non-download phases.
/// </summary>
public sealed class UpdateDeterminateProgressTests
{
    [Fact]
    public void NullEmptyAndPhaseMessagesStayIndeterminate()
    {
        Assert.Null(UpdateDownloadProgress.ReadMilestone(null));
        Assert.Null(UpdateDownloadProgress.ReadMilestone(""));
        Assert.Null(UpdateDownloadProgress.ReadMilestone("Downloading CI #3…"));
        Assert.Null(UpdateDownloadProgress.ReadMilestone("Checking selected update channel…"));
        Assert.Null(UpdateDownloadProgress.ReadMilestone("Verifying the downloaded artifact checksum…"));
        Assert.Null(UpdateDownloadProgress.ReadMilestone("Validating and extracting the build…"));
        Assert.Null(UpdateDownloadProgress.ReadMilestone("Replacing application files…"));
    }

    [Fact]
    public void ActualFormatterTenPercentMilestoneIsParsed()
    {
        var counter = new UpdateDownloadProgress(10_000);
        var message = counter.ReportCopied(1_000, "CI #123 · aabbccd");
        Assert.NotNull(message);
        Assert.Equal(10, UpdateDownloadProgress.ReadMilestone(message));
    }

    [Fact]
    public void EveryRealMilestoneThroughHundredIsSupported()
    {
        var counter = new UpdateDownloadProgress(1_000);
        for (var milestone = 10; milestone <= 100; milestone += 10)
        {
            var message = counter.ReportCopied(milestone * 10, "v0.2.5");
            Assert.Equal(milestone, UpdateDownloadProgress.ReadMilestone(message));
        }
    }

    [Fact]
    public void ByteFormatterJumpIsDisplayedAtActualReachedBoundary()
    {
        var counter = new UpdateDownloadProgress(1_000);
        Assert.Equal(30, UpdateDownloadProgress.ReadMilestone(counter.ReportCopied(398, "Development")!));
        Assert.Null(counter.ReportCopied(399, "Development"));
        Assert.Equal(100, UpdateDownloadProgress.ReadMilestone(counter.ReportCopied(1_000, "Development")!));
    }

    [Fact]
    public void RejectsMalformedOrOutOfRangeMilestones()
    {
        var invalid = new[]
        {
            "Downloading CI: 0% (0/100 bytes)",
            "Downloading CI: 5% (5/100 bytes)",
            "Downloading CI: 110% (110/100 bytes)",
            "Downloading CI: -10% (10/100 bytes)",
            "Downloading CI: 10.0% (10/100 bytes)",
            "Downloading CI: ten% (10/100 bytes)",
            "Downloading CI: 10% (10/100)",
            "Download CI: 10% (10/100 bytes)"
        };
        foreach (var value in invalid)
            Assert.Null(UpdateDownloadProgress.ReadMilestone(value));
    }

    [Fact]
    public void VersionColonDoesNotAffectReadingLastMilestone()
    {
        var counter = new UpdateDownloadProgress(10_000);
        var message = counter.ReportCopied(7_000, "CI: #3621: aabbccd");
        Assert.Equal(70, UpdateDownloadProgress.ReadMilestone(message));
    }
}
