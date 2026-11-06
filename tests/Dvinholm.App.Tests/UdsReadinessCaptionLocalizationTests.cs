namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic RU/EN diagnostic-readiness presentation contracts.
/// Nothing in these tests opens J2534, transmits UDS or changes ECU state.
/// </summary>
public sealed class UdsReadinessCaptionLocalizationTests
{
    [Theory]
    [InlineData(false, false, false, "Чтение UDS отключено до подключения")]
    [InlineData(false, false, true, "Read-only UDS disabled until connected")]
    [InlineData(true, false, false, "Одного пассивно наблюдаемого RX недостаточно")]
    [InlineData(true, false, true, "Passive RX alone is not sufficient")]
    [InlineData(true, true, false, "Перед чтением подтвердите идентичность ECU")]
    [InlineData(true, true, true, "Verify ECU identity and service support before reading")]
    public void ReadinessCaptionMatchesEvaluatedGateAndLocale(
        bool connected, bool ready, bool english, string expected)
    {
        var text = UdsReadinessCaption.Text(connected, ready, english);
        Assert.Contains(expected, text);
        if (!ready)
            Assert.DoesNotContain("address syntax valid", text, StringComparison.OrdinalIgnoreCase);
        if (!connected)
            Assert.DoesNotContain("подключено", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InconsistentReadyFlagCannotAdvertiseDisconnectedBusAsReady()
    {
        var en = UdsReadinessCaption.Text(connected: false, ready: true, english: true);
        var ru = UdsReadinessCaption.Text(connected: false, ready: true, english: false);
        Assert.Equal(UdsReadinessCaption.Text(false, false, true), en);
        Assert.Equal(UdsReadinessCaption.Text(false, false, false), ru);
        Assert.Contains("disabled until connected", en);
        Assert.Contains("отключено до подключения", ru);
    }

    [Fact]
    public void LocaleRefreshUsesCachedActualGateWithoutArmingControlsOrTransmitting()
    {
        var main = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        var start = main.IndexOf("private void SetUdsEnabled(", StringComparison.Ordinal);
        var end = main.IndexOf("private long ClearDiagnosticReadResult(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = main[start..end];
        Assert.Contains("UdsReadAddressReadiness.CanRead(", region);
        Assert.Contains("ReadVinButton.IsEnabled = ready;", region);
        Assert.Contains("ReadDtcButton.IsEnabled = ready && TryGetSelectedDtcStatusMask(out _);", region);
        Assert.Contains("_udsReadinessConnected = connected;", region);
        Assert.Contains("_udsReadinessReady = ready;", region);
        Assert.Contains("RefreshUdsReadinessHint();", region);
        var refreshStart = region.IndexOf("private void RefreshUdsReadinessHint()", StringComparison.Ordinal);
        Assert.True(refreshStart > 0);
        var refresh = region[refreshStart..];
        Assert.Contains("UdsReadinessCaption.Text(", refresh);
        Assert.Contains("_udsReadinessConnected, _udsReadinessReady, _unifiedEnglish", refresh);
        Assert.DoesNotContain("ReadVinButton.IsEnabled =", refresh);
        Assert.DoesNotContain("ReadDtcButton.IsEnabled =", refresh);
        Assert.DoesNotContain("SetUdsEnabled(", refresh);
        Assert.DoesNotContain("_session.Bus", refresh);
        Assert.DoesNotContain("PassThru", refresh);
        Assert.Contains("RefreshUdsReadinessHint();", locale);
        Assert.Contains("ApplyAuroraPrimaryScreenLocalization();", locale);
        Assert.DoesNotContain("Connect_Click(", locale);
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
