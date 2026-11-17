namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline RU/EN counter contracts. They format existing numeric
/// evidence only; no live J2534, CAN frames or WinUI screenshot is involved.
/// </summary>
public sealed class CanMonitorCounterLocalizationTests
{
    [Theory]
    [InlineData(0, "0 кадров/с", "0 frames/s")]
    [InlineData(37, "37 кадров/с", "37 frames/s")]
    public void FrameRateRetainsSameSampleAcrossLocales(int rate, string ru, string en)
    {
        Assert.Equal(ru, CanMonitorCounterCaptions.Rate(rate, english: false));
        Assert.Equal(en, CanMonitorCounterCaptions.Rate(rate, english: true));
    }

    [Theory]
    [InlineData(0, 0, "0 показано / 0 RX", "0 shown / 0 rx")]
    [InlineData(4, 27, "4 показано / 27 RX", "4 shown / 27 rx")]
    public void FilteredAndReceivedFrameTotalsRemainDistinct(
        int shown, long received, string ru, string en)
    {
        Assert.Equal(ru, CanMonitorCounterCaptions.Frames(shown, received, english: false));
        Assert.Equal(en, CanMonitorCounterCaptions.Frames(shown, received, english: true));
    }

    [Theory]
    [InlineData(0, "0 потеряно с очистки", "0 dropped since clear")]
    [InlineData(12, "12 потеряно с очистки", "12 dropped since clear")]
    public void ActualDroppedFrameCountDoesNotChangeOnLocaleSwitch(
        long drops, string ru, string en)
    {
        Assert.Equal(ru, CanMonitorCounterCaptions.Drops(drops, english: false));
        Assert.Equal(en, CanMonitorCounterCaptions.Drops(drops, english: true));
    }

    [Fact]
    public void UnknownDropsStayUnknownRatherThanInventingZero()
    {
        Assert.Equal("потери неизвестны",
            CanMonitorCounterCaptions.Drops(null, english: false));
        Assert.Equal("drops unknown",
            CanMonitorCounterCaptions.Drops(null, english: true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanMonitorCounterCaptions.Drops(-1, english: true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanMonitorCounterCaptions.Rate(-1, english: false));
    }

    [Fact]
    public void RealMonitorReprojectsExistingNumbersWithoutResetOrCanRequest()
    {
        var main = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("RefreshCanMonitorCounterLanguage();", locale);
        Assert.Contains("_lastMonitorRate = _rateFrames;", main);
        Assert.Contains("UpdateMonitorRateText();", main);
        Assert.Contains("CanMonitorCounterCaptions.Rate(_lastMonitorRate, _unifiedEnglish)", main);
        Assert.Contains("CanMonitorCounterCaptions.Frames(", main);
        Assert.Contains("CanMonitorCounterCaptions.Drops(drops, _unifiedEnglish)", main);

        var start = main.IndexOf("private void RefreshCanMonitorCounterLanguage()", StringComparison.Ordinal);
        var end = main.IndexOf("private void RefreshMonitorFilter()", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var refresh = main[start..end];
        Assert.Contains("UpdateMonitorRateText();", refresh);
        Assert.Contains("UpdateFrameCountText();", refresh);
        Assert.Contains("UpdateRxDropCountText();", refresh);
        Assert.DoesNotContain("_frames.Clear()", refresh);
        Assert.DoesNotContain("_total = 0", refresh);
        Assert.DoesNotContain("PassThru", refresh);
        Assert.DoesNotContain("ReadLoop(", refresh);
        Assert.DoesNotContain("Start_Click(", refresh);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        throw new FileNotFoundException(relative);
    }
}
