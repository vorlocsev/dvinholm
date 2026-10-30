using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline read-only readiness checks, without adapter,
/// VirtualCar, ECU frames, PIN access or any diagnostic transmission.
/// </summary>
public sealed class UdsReadAddressReadinessTests
{
    [Fact]
    public void DisconnectedOrClosingReadIsNeverArmed()
    {
        Assert.False(UdsReadAddressReadiness.CanRead(false, false, false, 0, "7E0", "7E8"));
        Assert.False(UdsReadAddressReadiness.CanRead(false, false, false, 1, "18DA10F1", "18DAF110"));
    }

    [Fact]
    public void MissingRequestOrResponseIdIsNotAnImplicitDefault()
    {
        Assert.False(UdsReadAddressReadiness.CanRead(true, false, false, 0, "", "7E8"));
        Assert.False(UdsReadAddressReadiness.CanRead(true, false, false, 0, "7E0", ""));
        Assert.False(UdsReadAddressReadiness.CanRead(true, false, false, 0, null, "7E8"));
        Assert.False(UdsReadAddressReadiness.CanRead(true, false, false, 0, "7E0", null));
        Assert.False(UdsReadAddressReadiness.CanRead(true, false, false, 0, " ", " "));
    }

    [Fact]
    public void ExplicitStandardAndExtendedPairsAreSyntacticallyEligible()
    {
        Assert.True(UdsReadAddressReadiness.CanRead(true, false, false, 0, "7e0", "7e8"));
        Assert.True(UdsReadAddressReadiness.CanRead(true, false, false, 0, "0x7E0", "0x7E8"));
        Assert.True(UdsReadAddressReadiness.CanRead(true, false, false, 1, "18DA10F1", "18DAF110"));
    }

    [Fact]
    public void InvalidRangesFormatsOrSameIdFailClosed()
    {
        Assert.False(UdsReadAddressReadiness.CanRead(true, false, false, -1, "7E0", "7E8"));
        Assert.False(UdsReadAddressReadiness.CanRead(true, false, false, 2, "7E0", "7E8"));
        Assert.False(UdsReadAddressReadiness.CanRead(true, false, false, 0, "800", "7E8"));
        Assert.False(UdsReadAddressReadiness.CanRead(true, false, false, 1, "20000000", "18DAF110"));
        Assert.False(UdsReadAddressReadiness.CanRead(true, false, false, 0, "7E8", "7E8"));
        Assert.False(UdsReadAddressReadiness.CanRead(true, false, false, 0, "xyz", "7E8"));
    }

    [Fact]
    public void BusyScanAndOfflineAcceptanceKeepReadsDisabled()
    {
        Assert.False(UdsReadAddressReadiness.CanRead(true, true, false, 0, "7E0", "7E8"));
        Assert.False(UdsReadAddressReadiness.CanRead(true, false, true, 0, "7E0", "7E8"));
        Assert.True(UdsReadAddressReadiness.CanRead(true, false, false, 0, "7E0", "7E8"));
    }

    [Fact]
    public void DidValidationIsIndependentOfVinAndDtcReadiness()
    {
        Assert.True(UdsReadAddressReadiness.ValidDid("F190"));
        Assert.True(UdsReadAddressReadiness.ValidDid("0xF187"));
        Assert.True(UdsReadAddressReadiness.ValidDid("0000"));
        Assert.False(UdsReadAddressReadiness.ValidDid(""));
        Assert.False(UdsReadAddressReadiness.ValidDid(null));
        Assert.False(UdsReadAddressReadiness.ValidDid("10000"));
        Assert.False(UdsReadAddressReadiness.ValidDid("0xZZZZ"));
        Assert.False(UdsReadAddressReadiness.ValidDid("F19G"));
    }

    [Fact]
    public void RealXamlStartsWithNoArmedAddressDefaultsAndExplicitGuide()
    {
        var doc = MainWindowXaml();
        var tx = Named(doc, "UdsRequestId");
        var rx = Named(doc, "UdsResponseId");
        var hint = Named(doc, "UdsReadinessHint");
        Assert.Equal("", (string?)tx.Attribute("Text"));
        Assert.Equal("", (string?)rx.Attribute("Text"));
        Assert.Contains("No request ID is selected by default", (string?)tx.Attribute("ToolTipService.ToolTip"));
        Assert.Contains("observation alone", (string?)rx.Attribute("ToolTipService.ToolTip"));
        Assert.Contains("Read-only UDS disabled", (string?)hint.Attribute("Text"));
        Assert.Equal("8", (string?)hint.Attribute("Grid.ColumnSpan"));
        Assert.Equal("Wrap", (string?)hint.Attribute("TextWrapping"));
        foreach (var name in new[] { "ReadDidButton", "ReadVinButton", "ReadDtcButton" })
            Assert.Equal("False", (string?)Named(doc, name).Attribute("IsEnabled"));
    }

    [Fact]
    public void UiArmsOnlyValidatedAddressesAndRechecksEveryInputEdit()
    {
        var source = MainWindowSource();
        var gate = Between(source, "private void SetUdsEnabled(", "private long ClearDiagnosticReadResult()");
        Assert.Contains("UdsReadAddressReadiness.CanRead(", gate);
        Assert.Contains("UdsReadAddressReadiness.ValidDid(", gate);
        Assert.Contains("_udsGate.CurrentCount == 0", gate);
        Assert.Contains("_discoveryCts is not null", gate);
        Assert.Contains("DvinholmUserDataDirectory.IsAcceptanceLaunch", gate);
        Assert.DoesNotContain("ReadVinButton.IsEnabled = enabled;", gate);
        Assert.Contains("ReadVinButton.IsEnabled = ready;", gate);
        Assert.Contains("ReadDtcButton.IsEnabled = ready;", gate);

        var edits = Between(source, "private void SnapshotCanIdTextChanged(", "private bool CanCaptureSelectedSnapshot(");
        Assert.Contains("private void SnapshotCanIdFormatChanged(", edits);
        Assert.Contains("private void UdsDid_TextChanged(", edits);
        Assert.Equal(3, edits.Split("SetUdsEnabled(_connected && !_closing);",
            StringSplitOptions.None).Length - 1);
        Assert.Contains("ClearDiagnosticReadResult();", edits);
    }

    private static readonly XNamespace XamlName =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument MainWindowXaml()
    {
        using var stream = typeof(UdsReadAddressReadinessTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml is not embedded.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e =>
            (string?)e.Attribute(XamlName + "Name") == name);

    private static string Between(string source, string first, string next)
    {
        var start = source.IndexOf(first, StringComparison.Ordinal);
        var end = source.IndexOf(next, start + first.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }

    private static string MainWindowSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(file))
                return File.ReadAllText(file);
        }
        throw new FileNotFoundException("src/Dvinholm.App/MainWindow.xaml.cs");
    }
}
