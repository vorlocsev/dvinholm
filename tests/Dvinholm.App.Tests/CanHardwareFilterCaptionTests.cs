namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline bilingual hardware-filter presentation checks. These never
/// invoke PassThru, start a monitor or interact with a vendor adapter.
/// </summary>
public sealed class CanHardwareFilterCaptionTests
{
    [Theory]
    [InlineData(false, false, "Применить аппаратный фильтр")]
    [InlineData(false, true, "Apply HW filter")]
    [InlineData(true, false, "Убрать аппаратный фильтр")]
    [InlineData(true, true, "Remove HW filter")]
    public void ActualFilterStateAndLocaleChooseOneTruthfulAction(
        bool active, bool english, string expected)
    {
        Assert.Equal(expected, CanHardwareFilterCaption.Text(active, english));
    }

    [Fact]
    public void SuccessfulHardwareToggleRefreshesCaptionAfterConfirmedFilterIdChange()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = source.IndexOf("private void HardwareFilter_Click(", StringComparison.Ordinal);
        var end = source.IndexOf("private void Filter_Changed(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var handler = source[start..end];

        var stop = handler.IndexOf("if (!bus.StopCanFilter(", StringComparison.Ordinal);
        var removed = handler.IndexOf("_activeHardwareFilterId = null;", stop, StringComparison.Ordinal);
        var refreshRemoved = handler.IndexOf("RefreshHardwareFilterCaption();", removed, StringComparison.Ordinal);
        var create = handler.IndexOf("if (!bus.StartCanIdFilter(", StringComparison.Ordinal);
        var applied = handler.IndexOf("_activeHardwareFilterId = filterId;", create, StringComparison.Ordinal);
        var refreshApplied = handler.IndexOf("RefreshHardwareFilterCaption();", applied, StringComparison.Ordinal);
        Assert.True(stop >= 0 && removed > stop && refreshRemoved > removed);
        Assert.True(create > refreshRemoved && applied > create && refreshApplied > applied);
        Assert.DoesNotContain("HardwareFilterButton.Content = \"Apply HW filter\"", handler);
        Assert.DoesNotContain("HardwareFilterButton.Content = \"Remove HW filter\"", handler);
    }

    [Fact]
    public void UnsuccessfulFilterOperationsDoNotSetActiveStateOrActionLabel()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = source.IndexOf("private void HardwareFilter_Click(", StringComparison.Ordinal);
        var end = source.IndexOf("private void Filter_Changed(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var handler = source[start..end];
        var failure = handler.IndexOf("if (!bus.StartCanIdFilter(", StringComparison.Ordinal);
        var accepted = handler.IndexOf("_activeHardwareFilterId = filterId;", failure, StringComparison.Ordinal);
        Assert.True(failure >= 0 && accepted > failure);
        var failedBranch = handler[failure..accepted];
        Assert.Contains("J2534PublicFailure.FilterApplyRejected", failedBranch);
        Assert.Contains("return;", failedBranch);
        Assert.DoesNotContain("RefreshHardwareFilterCaption();", failedBranch);
        Assert.DoesNotContain("_activeHardwareFilterId = filterId;", failedBranch);
    }

    [Fact]
    public void DisconnectAndFailedDisconnectReturnToLocalizedApplyCaption()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = source.IndexOf("private async void Connect_Click(", StringComparison.Ordinal);
        var end = source.IndexOf("private void Start_Click(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var handler = source[start..end];
        Assert.Equal(2, handler.Split("_activeHardwareFilterId = null;").Length - 1);
        Assert.Equal(2, handler.Split("RefreshHardwareFilterCaption();").Length - 1);
        Assert.DoesNotContain("HardwareFilterButton.Content = \"Apply HW filter\"", handler);
        Assert.Contains("HardwareFilterButton.IsEnabled = false;", handler);
    }

    [Fact]
    public void LocaleSwitchOnlyProjectsActiveActionWithoutConnectingOrWritingFrames()
    {
        var locale = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        var start = locale.IndexOf("private void RefreshHardwareFilterCaption()", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var method = locale[start..];
        Assert.Contains("_activeHardwareFilterId is not null, _unifiedEnglish", method);
        Assert.Contains("CanHardwareFilterCaption.Text(", method);
        Assert.DoesNotContain("StartCanIdFilter(", method);
        Assert.DoesNotContain("StopCanFilter(", method);
        Assert.DoesNotContain("PassThru", method);
        Assert.DoesNotContain("_session.Bus", method);
        Assert.Contains("RefreshHardwareFilterCaption();", locale);
        var language = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("ApplyAuroraPrimaryScreenLocalization();", language);
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
            dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }
}
