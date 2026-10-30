using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline lifetime and UI checks. No VirtualCar, native driver,
/// physical ECU, CAN frame, credentials or vehicle snapshot is used.
/// </summary>
public sealed class UdsReadResultLifetimeTests
{
    [Fact]
    public void NoResultIsDisplayableBeforeTheFirstAttempt()
    {
        var lifetime = new UdsReadResultLifetime();
        Assert.Equal(0, lifetime.Generation);
        Assert.False(lifetime.CanDisplay(0));
        Assert.False(lifetime.CanDisplay(-1));
    }

    [Fact]
    public void NewAttemptMakesOnlyItsOwnGenerationDisplayable()
    {
        var lifetime = new UdsReadResultLifetime();
        var first = lifetime.Invalidate();
        Assert.Equal(1, first);
        Assert.True(lifetime.CanDisplay(first));
        var second = lifetime.Invalidate();
        Assert.Equal(2, second);
        Assert.False(lifetime.CanDisplay(first));
        Assert.True(lifetime.CanDisplay(second));
    }

    [Fact]
    public void MultipleFieldEditsInvalidateAllEarlierCompletions()
    {
        var lifetime = new UdsReadResultLifetime();
        var fromPreviousEcu = lifetime.Invalidate();
        for (var i = 0; i < 100; i++)
            lifetime.Invalidate();
        Assert.Equal(101, lifetime.Generation);
        Assert.False(lifetime.CanDisplay(fromPreviousEcu));
        Assert.True(lifetime.CanDisplay(101));
    }

    [Fact]
    public void ExplicitClearSuppressesLateResultWithoutChangingItsPayload()
    {
        var lifetime = new UdsReadResultLifetime();
        var requestToken = lifetime.Invalidate();
        const string secretResult = "DID 0xF190: YV1ABCDEF12345678";
        lifetime.Invalidate();
        Assert.False(lifetime.CanDisplay(requestToken));
        Assert.Equal("DID 0xF190: YV1ABCDEF12345678", secretResult);
        Assert.DoesNotContain("YV1", UdsDiagnosticReadResultPresentation.NoCompletedResult);
    }

    [Fact]
    public void ClearButtonIsVisibleOutsideResultExpanderAndDoesNotSendCommands()
    {
        var xml = ReadXaml();
        var expander = Named(xml, "DiagnosticResultExpander");
        var button = Named(xml, "ClearDiagnosticResultButton");
        Assert.Equal("StatusPanel", (string?)button.Parent?.Attribute(XamlNs + "Name"));
        Assert.Same(expander.Parent, button.Parent);
        Assert.Equal("ClearDiagnosticResult_Click", (string?)button.Attribute("Click"));
        Assert.Equal("Right", (string?)button.Attribute("HorizontalAlignment"));
        Assert.Contains("Does not contact the ECU", (string?)button.Attribute("ToolTipService.ToolTip"));
        Assert.NotNull(Named(xml, "DiagnosticResultText"));
    }

    [Fact]
    public void IdFormatAndDidEditsAllInvalidateTheResult()
    {
        var xml = ReadXaml();
        Assert.Equal("SnapshotCanIdTextChanged",
            (string?)Named(xml, "UdsRequestId").Attribute("TextChanged"));
        Assert.Equal("SnapshotCanIdTextChanged",
            (string?)Named(xml, "UdsResponseId").Attribute("TextChanged"));
        Assert.Equal("SnapshotCanIdFormatChanged",
            (string?)Named(xml, "CanIdFormat").Attribute("SelectionChanged"));
        Assert.Equal("UdsDid_TextChanged",
            (string?)Named(xml, "UdsDid").Attribute("TextChanged"));

        var code = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = code.IndexOf("private long ClearDiagnosticReadResult()", StringComparison.Ordinal);
        var end = code.IndexOf("private bool CanCaptureSelectedSnapshot(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = code[start..end];
        Assert.Contains("_udsResultLifetime.Invalidate()", region);
        Assert.Contains("DiagnosticResultText.Text = UdsDiagnosticReadResultPresentation.NoCompletedResult", region);
        Assert.Contains("DiagnosticResultExpander.IsExpanded = false", region);
        Assert.Contains("private void ClearDiagnosticResult_Click(", region);
        Assert.Contains("private void UdsDid_TextChanged(", region);
        Assert.Contains("ClearDiagnosticReadResult();", region);
    }

    [Fact]
    public void AsyncReadChecksGenerationBeforeRevealingPrivatePayload()
    {
        var code = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = code.IndexOf("private async Task RunUdsOperationAsync(", StringComparison.Ordinal);
        var end = code.IndexOf("private void CancelUds_Click(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = code[start..end];
        var acquire = region.IndexOf("var resultGeneration = ClearDiagnosticReadResult()", StringComparison.Ordinal);
        var awaited = region.IndexOf("var data = await Task.Run(", StringComparison.Ordinal);
        var guard = region.IndexOf("if (!_udsResultLifetime.CanDisplay(resultGeneration))", StringComparison.Ordinal);
        var reveal = region.IndexOf("DiagnosticResultText.Text = presentation.Details", StringComparison.Ordinal);
        Assert.True(0 <= acquire && acquire < awaited && awaited < guard && guard < reveal);
        Assert.Contains("Output discarded.", region);
        Assert.Contains("return;", region[guard..reveal]);
        Assert.Contains("SetStatus(presentation.Activity)", region);
        Assert.DoesNotContain("SetStatus(presentation.Details)", region);
        Assert.DoesNotContain("SetStatus(operation + \": \" + data)", region);
    }

    [Fact]
    public void NavigationScanEcuSelectionAndAdapterChangesInvalidatePriorResults()
    {
        var code = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var home = Between(code, "private void DashboardReturn_Click(", "private void RefreshDashboardSummary(");
        var scan = Between(code, "private async void Discover_Click(", "private void ShowDiscoveredEcus(");
        // Helper is located between discovery and selection; locate exact handler independently.
        var select = Between(code, "private void ApplyEcuSelection(", "private void SetUdsEnabled(");
        var connect = Between(code, "private async void Connect_Click(", "private void Start_Click(");
        Assert.Contains("ClearDiagnosticReadResult();", home);
        Assert.Contains("ClearDiagnosticReadResult();", scan);
        Assert.Contains("ClearDiagnosticReadResult();", select);
        Assert.True(connect.Split("ClearDiagnosticReadResult();", StringSplitOptions.None).Length >= 3);
        Assert.Contains("ClearPassiveDiscoverySelection()", connect);
        Assert.DoesNotContain("DiagnosticResultText.Text = presentation.Details", connect);
    }

    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument ReadXaml()
    {
        using var stream = typeof(UdsReadResultLifetimeTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml must be embedded.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(x => (string?)x.Attribute(XamlNs + "Name") == name);

    private static string Between(string source, string first, string next)
    {
        var start = source.IndexOf(first, StringComparison.Ordinal);
        var end = source.IndexOf(next, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Expected source region: {first}");
        return source[start..end];
    }

    private static string Source(string relativePath)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
            dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relativePath);
    }
}
