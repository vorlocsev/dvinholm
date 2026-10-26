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

    [Fact]
    public void WindowRendersOnlyParsedBytesAsDeterminateAndResetsForOtherPhases()
    {
        var code = ReadWindowSource();
        var start = code.IndexOf("private void ShowUpdateActivity(", StringComparison.Ordinal);
        var end = code.IndexOf("private void FinishUpdateActivity(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = code[start..end];
        Assert.Contains("var percentage = UpdateDownloadProgress.ReadMilestone(text);", region);
        Assert.Contains("UpdateProgressBar.IsIndeterminate = percentage is null;", region);
        Assert.Contains("UpdateProgressBar.Value = percentage ?? 0;", region);
        Assert.Contains("UpdateProgressBar.Visibility = Visibility.Visible;", region);
        Assert.Contains("UpdateProgressText.Text = text;", region);
    }

    [Fact]
    public void CompletionClearsPercentageBeforeFutureUpdateChecksOrRetries()
    {
        var code = ReadWindowSource();
        var start = code.IndexOf("private void FinishUpdateActivity(", StringComparison.Ordinal);
        var end = code.IndexOf("private bool UseStableUpdateSource", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = code[start..end];
        Assert.Contains("UpdateProgressBar.IsIndeterminate = true;", region);
        Assert.Contains("UpdateProgressBar.Value = 0;", region);
        Assert.Contains("UpdateProgressBar.Visibility = Visibility.Collapsed;", region);
        Assert.Contains("message is null ? Visibility.Collapsed : Visibility.Visible", region);
    }

    private static string ReadWindowSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(file))
                return File.ReadAllText(file);
        }
        throw new FileNotFoundException("MainWindow.xaml.cs missing.");
    }
}
