using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline gates for real-byte determinate progress and indeterminate
/// lookup/verification/extraction/retry stages. Not a live WinUI GUI test.
/// </summary>
public sealed class UpdateDownloadPercentageTests
{
    [Fact]
    public void RecognizesTenPercentOfRealCopiedZipBytes()
    {
        Assert.True(UpdateDownloadPercentage.TryRead(
            "Downloading CI #3670 · 123abcd: 10% (1,000/10,000 bytes)", out var percent));
        Assert.Equal(10, percent);
    }

    [Fact]
    public void RecognizesHundredPercentWithoutClaimingInstallIsComplete()
    {
        Assert.True(UpdateDownloadPercentage.TryRead(
            "Downloading 0.2.4: 100% (10,000/10,000 bytes)", out var percent));
        Assert.Equal(100, percent);
    }

    [Fact]
    public void OtherUpdaterStagesNeverHaveAnInventedPercentage()
    {
        foreach (var status in new string?[]
        {
            null, "", "Downloading CI #3670…",
            "Checking selected update channel…",
            "Verifying the downloaded artifact checksum…",
            "Validating and extracting the build…",
            "Replacing application files with CI #3670…",
            "Cancelling download…"
        })
        {
            Assert.False(UpdateDownloadPercentage.TryRead(status, out var percent));
            Assert.Equal(0, percent);
        }
    }

    [Fact]
    public void RejectsUnsupportedOrOutOfRangePercentages()
    {
        foreach (var percentage in new[] { "0", "5", "11", "101", "999", "-10", "nan" })
        {
            Assert.False(UpdateDownloadPercentage.TryRead(
                $"Downloading CI #3670: {percentage}% (1/10 bytes)", out _));
        }
    }

    [Fact]
    public void RejectsMessagesWithoutExpectedCopiedBytesFormat()
    {
        foreach (var message in new[]
        {
            "Downloading CI #3670: 20%",
            "Downloading CI #3670: 20% (2/10)",
            "Downloading CI #3670: 20% (bytes)",
            "Downloading CI #3670: 20% (/10 bytes)",
            "Downloading CI #3670: 20% (2/ bytes)",
            "Installing CI #3670: 20% (2/10 bytes)"
        })
            Assert.False(UpdateDownloadPercentage.TryRead(message, out _));
    }

    [Fact]
    public void ProductionByteFormatterMessagesRoundTripWithoutGuesswork()
    {
        var producer = new UpdateDownloadProgress(100);
        var count = 0;
        for (var received = 1; received <= 100; received++)
        {
            var message = producer.ReportCopied(received, "CI #3670");
            if (message is null)
                continue;
            count++;
            Assert.True(UpdateDownloadPercentage.TryRead(message, out var percent));
            Assert.Equal(count * 10, percent);
        }

        Assert.Equal(10, count);
    }

    [Fact]
    public void XamlBarStartsHiddenAndIndeterminateAtZero()
    {
        var doc = XDocument.Parse(Read("src/Dvinholm.App/MainWindow.xaml"));
        var bar = Assert.Single(doc.Descendants(), element =>
            element.Name.LocalName == "ProgressBar" &&
            element.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "UpdateProgressBar"));
        Assert.Equal("Collapsed", (string?)bar.Attribute("Visibility"));
        Assert.Equal("True", (string?)bar.Attribute("IsIndeterminate"));
        Assert.Equal("0", (string?)bar.Attribute("Minimum"));
        Assert.Equal("100", (string?)bar.Attribute("Maximum"));
        Assert.Equal("0", (string?)bar.Attribute("Value"));
    }

    [Fact]
    public void FlyoutSetsRealPercentageAndClearsItForOtherStagesAndRetry()
    {
        var code = Read("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = code.IndexOf("private void ShowUpdateActivity(", StringComparison.Ordinal);
        var end = code.IndexOf("private bool UseStableUpdateSource", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = code[start..end];
        Assert.Contains("UpdateDownloadPercentage.TryRead(text, out var percent)", region);
        Assert.Contains("UpdateProgressBar.IsIndeterminate = false;", region);
        Assert.Contains("UpdateProgressBar.Value = percent;", region);
        Assert.Contains("UpdateProgressBar.IsIndeterminate = true;", region);
        Assert.Contains("UpdateProgressBar.Value = 0;", region);
        Assert.Contains("UpdateProgressBar.Visibility = Visibility.Collapsed;", region);

        var handler = code.IndexOf("DispatcherQueue.TryEnqueue(() =>", code.IndexOf("private async Task DownloadAndLaunchLatestBuildAsync", StringComparison.Ordinal), StringComparison.Ordinal);
        var update = code.IndexOf("ShowUpdateActivity(message);", handler, StringComparison.Ordinal);
        Assert.True(handler >= 0 && update > handler);
    }

    private static string Read(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        throw new FileNotFoundException("Update progress source was not found.", relative);
    }
}
