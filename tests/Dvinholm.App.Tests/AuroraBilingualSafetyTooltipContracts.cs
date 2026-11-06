using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Offline verification that nine primary Aurora safety hints follow RU/EN
/// without changing J2534, UDS, diagnostics or update state.
/// Does not claim visual or keyboard acceptance.
/// </summary>
public sealed class AuroraBilingualSafetyTooltipContracts
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(relative);
    }

    private static string Screen() =>
        Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraBilingualSafetyTooltipContracts).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded MainWindow.xaml.");
        return XDocument.Load(stream);
    }

    [Theory]
    [InlineData("CanIdFormat")]
    [InlineData("AdapterMetadataText")]
    [InlineData("UdsRequestId")]
    [InlineData("UdsResponseId")]
    [InlineData("DtcRecordsView")]
    [InlineData("ClearDiagnosticResultButton")]
    [InlineData("CaptureSnapshotButton")]
    [InlineData("InspectSnapshotButton")]
    [InlineData("CompareSnapshotsButton")]
    public void EachVisibleSafetyHintHasOneLocaleDependentWriterAndRealControl(string id)
    {
        var screen = Screen();
        var writer = "ToolTipService.SetToolTip(" + id + ", L(";
        Assert.Equal(1, screen.Split(writer, StringSplitOptions.None).Length - 1);
        var begin = screen.IndexOf(writer, StringComparison.Ordinal);
        var end = screen.IndexOf("));", begin, StringComparison.Ordinal);
        Assert.True(begin >= 0 && end > begin);
        var projection = screen[begin..(end + 3)];
        Assert.Contains("L(", projection);
        Assert.Contains("\",", projection);
        Assert.Contains("));", projection);
        Assert.Single(Layout().Descendants().Where(e =>
            (string?)e.Attribute(X + "Name") == id));
    }

    [Fact]
    public void AllHintsRefreshWithShellLanguageWithoutTouchingSession()
    {
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("private void UnifiedLanguageSelector_SelectionChanged(", locale);
        Assert.Contains("_unifiedEnglish = UnifiedLanguageSelector.SelectedIndex == 1;", locale);
        Assert.Contains("ApplyUnifiedLocalization();", locale);
        Assert.Contains("ApplyAuroraPrimaryScreenLocalization();", locale);

        var screen = Screen();
        foreach (var forbidden in new[]
        {
            "PassThruOpen(", "PassThruConnect(", "ReadDtc_Click(",
            "ReadCemPin_Click(", "UnifiedStageUpdate_Click(",
            "Invoke-WebRequest", "Invoke-RestMethod"
        })
            Assert.DoesNotContain(forbidden, screen);
    }

    [Fact]
    public void ConnectionAndUdsHintsKeepHardwareAndAddressIdentityUnverified()
    {
        var screen = Screen();
        Assert.Contains("Reconnect to change the physical channel.", screen);
        Assert.Contains("does not establish ECU or vehicle identity or authorization", screen);
        Assert.Contains("No request ID is selected by default.", screen);
        Assert.Contains("passive discovery cannot verify it", screen);
        Assert.Contains("Observation alone does not identify an ECU.", screen);
        Assert.Contains("Непроверенные сведения драйвера J2534", screen);
    }

    [Fact]
    public void FaultAndResultHintsNeverSuggestAnEraseOrAutomaticVehicleRequest()
    {
        var screen = Screen();
        Assert.Contains("Only DTCs actually read from the selected ECU.", screen);
        Assert.Contains("No verified fault-cause catalog", screen);
        Assert.Contains("Clear the last local VIN/DID/DTC result", screen);
        Assert.Contains("Does not contact the ECU.", screen);
    }

    [Fact]
    public void SnapshotHintsDistinguishFileInspectionFromRestorableEcuBackup()
    {
        var screen = Screen();
        Assert.Contains("not a restorable ECU backup", screen);
        Assert.Contains("Inspect local DID JSON offline", screen);
        Assert.Contains("without CAN requests or ECU restoration", screen);
        Assert.Contains("Show changed DID numbers, not their payloads.", screen);
        Assert.Contains("Это не резервная копия для восстановления ECU.", screen);
    }
}
