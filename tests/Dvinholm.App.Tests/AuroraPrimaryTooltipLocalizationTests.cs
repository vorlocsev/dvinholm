namespace Dvinholm.App.Tests;

/// <summary>
/// Offline contracts for the eight visible RU/EN safety tooltips in the
/// approved Aurora workspaces. These are source checks, not UI screenshot PASS.
/// </summary>
public sealed class AuroraPrimaryTooltipLocalizationTests
{
    private static string Source()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var file = Path.Combine(directory.FullName, "src", "Dvinholm.App",
                "MainWindow.AuroraScreenLocalization.cs");
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException("MainWindow.AuroraScreenLocalization.cs");
    }

    [Theory]
    [InlineData("CemPartPrerequisiteButton")]
    [InlineData("EcuSelector")]
    [InlineData("EcusView")]
    [InlineData("DtcStatusMaskSelector")]
    [InlineData("ReadDtcButton")]
    [InlineData("DtcCheckAddressesButton")]
    [InlineData("CanRxSummaryButton")]
    [InlineData("CanTraceInspectButton")]
    public void PrimarySafetyTooltipAlwaysUsesRuEnProjection(string control)
    {
        var source = Source();
        var method = source.IndexOf("private void ApplyAuroraPrimaryScreenLocalization()",
            StringComparison.Ordinal);
        var call = source.IndexOf("ToolTipService.SetToolTip(" + control + ", L(",
            method, StringComparison.Ordinal);
        Assert.True(method >= 0 && call > method,
            $"Missing localized safety tooltip for {control}.");
        var closing = source.IndexOf("));", call, StringComparison.Ordinal);
        Assert.True(closing > call);
        var arguments = source[call..(closing + 3)];
        Assert.Contains("L(", arguments);
        Assert.Contains("\",", arguments);
        Assert.Contains("));", arguments);
    }

    [Fact]
    public void CemPrerequisiteTooltipNeverClaimsToReadPinOrVehicle()
    {
        var source = Source();
        Assert.Contains("Только локальная проверка состояния приложения", source);
        Assert.Contains("Inspect local application state only. No CAN or J2534 requests.", source);
        Assert.DoesNotContain("ReadCemPin_Click(", source);
    }

    [Fact]
    public void EcuPickerAndResponseTooltipsPreserveUnverifiedIdentity()
    {
        var source = Source();
        Assert.Contains("не подтверждает предполагаемый адрес TX", source);
        Assert.Contains("Selection does not send a request or verify the inferred TX address.", source);
        Assert.Contains("идентичность ECU и адрес TX не подтверждены", source);
        Assert.Contains("ECU identity and TX address are unverified.", source);
    }

    [Fact]
    public void DtcStatusTooltipNamesSingleReadOnlyUdsServiceInBothLanguages()
    {
        var source = Source();
        Assert.Contains("для одного чтения UDS 0x19/0x02", source);
        Assert.Contains("one read-only UDS 0x19/0x02 request", source);
        Assert.Contains("не стирает ошибки", source);
        Assert.Contains("does not verify ECU identity or clear faults", source);
    }

    [Fact]
    public void DtcReadTooltipRestrictsScopeToOneVerifiedEcu()
    {
        var source = Source();
        Assert.Contains("только выбранного ECU с проверенными адресами TX/RX", source);
        Assert.Contains("only from the selected ECU with verified TX/RX addresses", source);
        Assert.Contains("No clearing or full-vehicle scan.", source);
    }

    [Fact]
    public void DtcAddressShortcutTooltipIsNavigationOnly()
    {
        var source = Source();
        Assert.Contains("Только переход к параметрам диагностики", source);
        Assert.Contains("Navigate to diagnostic addressing only. No vehicle request is sent.", source);
    }

    [Fact]
    public void CanCounterAndTraceTooltipsKeepPayloadsPrivate()
    {
        var source = Source();
        Assert.Contains("без данных кадров", source);
        Assert.Contains("without payloads. Copy only with explicit approval.", source);
        Assert.Contains("Не подключает адаптер и не раскрывает содержимое кадров.", source);
        Assert.Contains("Does not connect or expose frame payloads.", source);
    }

    [Fact]
    public void PrimaryTooltipProjectionAddsNoCanOrUpdateAction()
    {
        var source = Source();
        foreach (var forbidden in new[]
        {
            "Connect_Click(", "PassThruOpen(", "ReadDtc_Click(",
            "UnifiedStageUpdate_Click(", "ReadCemPin_Click(",
            "Invoke-WebRequest", "Invoke-RestMethod"
        })
            Assert.DoesNotContain(forbidden, source);
        Assert.Contains("private void ApplyAuroraPrimaryScreenLocalization()", source);
        Assert.Contains("ToolTipService.SetToolTip(DtcStatusMaskSelector, L(", source);
    }
}
