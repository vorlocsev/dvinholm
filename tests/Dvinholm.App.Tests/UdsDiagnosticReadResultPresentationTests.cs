using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for the real read-only UDS result presentation.
/// No live ECU, VirtualCar, J2534 DLL, VIN file or physical hardware.
/// </summary>
public sealed class UdsDiagnosticReadResultPresentationTests
{
    [Fact]
    public void VinPayloadIsOnlyInSelectableDetailsAndNeverInGlobalActivity()
    {
        const string vin = "YV1ABCDEF12345678";
        var result = UdsDiagnosticReadResultPresentation.Completed(UdsReadResultKind.Vin, vin);

        Assert.Contains(vin, result.Details);
        Assert.Contains("VIN identifies a vehicle", result.Details);
        Assert.Contains("Read VIN completed", result.Activity);
        Assert.DoesNotContain(vin, result.Activity);
        Assert.DoesNotContain("YV1", result.Activity);
    }

    [Fact]
    public void DidResponsePreservesHexInDetailsWithoutRepublishingInStatus()
    {
        const string raw = "DID 0x1234: raw: 12 34 56 78 90";
        var result = UdsDiagnosticReadResultPresentation.Completed(UdsReadResultKind.Did, raw);

        Assert.Contains(raw, result.Details);
        Assert.Contains("may contain identifiers", result.Details);
        Assert.DoesNotContain("12 34 56 78", result.Activity);
        Assert.DoesNotContain("0x1234", result.Activity);
        Assert.Equal("Read DID completed. Result available in the Diagnostics panel.", result.Activity);
    }

    [Fact]
    public void DtcMultilineResponseIsPreservedWithoutPretendingFullVehicleScan()
    {
        var raw = "2 DTC records returned" + Environment.NewLine +
            "DTC 0x123456 | status 0x08: confirmed DTC" + Environment.NewLine +
            "DTC 0x123456 | status 0x04: pending DTC";
        var result = UdsDiagnosticReadResultPresentation.Completed(UdsReadResultKind.Dtc, raw);

        Assert.EndsWith(raw, result.Details);
        Assert.Contains("not confirmed causes or a vehicle-wide scan", result.Details);
        Assert.DoesNotContain("0x123456", result.Activity);
        Assert.DoesNotContain("confirmed DTC", result.Activity);
        Assert.Contains("Read DTC completed", result.Activity);
    }

    [Fact]
    public void KnownOperationNamesCannotBeSuppliedFromArbitraryDriverText()
    {
        Assert.Equal("Read DID", UdsDiagnosticReadResultPresentation.OperationName(UdsReadResultKind.Did));
        Assert.Equal("Read VIN", UdsDiagnosticReadResultPresentation.OperationName(UdsReadResultKind.Vin));
        Assert.Equal("Read DTC", UdsDiagnosticReadResultPresentation.OperationName(UdsReadResultKind.Dtc));

        const string sensitive = "SECRET-VALUE-XYZ";
        foreach (var kind in Enum.GetValues<UdsReadResultKind>())
        {
            var result = UdsDiagnosticReadResultPresentation.Completed(kind, sensitive);
            Assert.Contains(sensitive, result.Details);
            Assert.DoesNotContain(sensitive, result.Activity);
            Assert.EndsWith("Result available in the Diagnostics panel.", result.Activity);
        }
    }

    [Fact]
    public void EmptyOrWhitespaceResponsesCannotBeLabeledCompleted()
    {
        Assert.Throws<ArgumentException>(() =>
            UdsDiagnosticReadResultPresentation.Completed(UdsReadResultKind.Vin, ""));
        Assert.Throws<ArgumentException>(() =>
            UdsDiagnosticReadResultPresentation.Completed(UdsReadResultKind.Did, "  "));
        Assert.Throws<ArgumentNullException>(() =>
            UdsDiagnosticReadResultPresentation.Completed(UdsReadResultKind.Dtc, null!));
    }

    [Fact]
    public void UnsupportedReadKindIsRejectedWithoutAccidentalFallback()
    {
        var kind = (UdsReadResultKind)100;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UdsDiagnosticReadResultPresentation.OperationName(kind));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UdsDiagnosticReadResultPresentation.Completed(kind, "raw"));
    }

    [Fact]
    public void RealMainWindowHasPrivateBoundedScrollableSelectableResultPanel()
    {
        var main = MainWindowXaml();
        var result = Named(main, "DiagnosticResultText");
        var expander = Named(main, "DiagnosticResultExpander");
        var status = Named(main, "StatusText");
        Assert.Equal("False", (string?)expander.Attribute("IsExpanded"));
        Assert.Contains("VIN / DID / DTC", (string?)expander.Attribute("Header"));
        Assert.Equal("True", (string?)result.Attribute("IsTextSelectionEnabled"));
        Assert.Equal("Wrap", (string?)result.Attribute("TextWrapping"));
        var scroll = Assert.IsType<XElement>(result.Parent);
        Assert.Equal("ScrollViewer", scroll.Name.LocalName);
        Assert.Equal("170", (string?)scroll.Attribute("MaxHeight"));
        Assert.Equal("Disabled", (string?)scroll.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("StatusPanel", (string?)expander.Parent?.Attribute(XamlName+"Name"));
        Assert.Equal("StatusPanel", (string?)status.Parent?.Parent?.Attribute(XamlName+"Name"));
        var home = Source("src/Dvinholm.App/AuroraHome.xaml");
        Assert.DoesNotContain("DiagnosticResultText", home);
        Assert.DoesNotContain("DiagnosticResultExpander", home);
    }

    [Fact]
    public void RealUiInvalidatesStaleResultsAndNeverRoutesPayloadThroughSetStatus()
    {
        var code = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("RunUdsOperationAsync(UdsReadResultKind.Did", code);
        Assert.Contains("RunUdsOperationAsync(UdsReadResultKind.Vin", code);
        Assert.Contains("RunUdsOperationAsync(UdsReadResultKind.Dtc", code);
        var start = code.IndexOf("private async Task RunUdsOperationAsync(", StringComparison.Ordinal);
        var end = code.IndexOf("private void CancelUds_Click(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = code[start..end];
        Assert.Contains("resultGeneration = ClearDiagnosticReadResult()", region);
        Assert.Contains("if (!_udsResultLifetime.CanDisplay(resultGeneration))", region);
        Assert.Contains("DiagnosticResultText.Text = UdsDiagnosticReadResultPresentation.NoCompletedResult", code);
        Assert.Contains("DiagnosticResultExpander.IsExpanded = false", code);
        Assert.Contains("DiagnosticResultText.Text = presentation.Details", region);
        Assert.Contains("DiagnosticResultExpander.IsExpanded = true", region);
        Assert.Contains("SetStatus(presentation.Activity)", region);
        Assert.DoesNotContain("SetStatus(operation + \": \" + data)", region);
        Assert.DoesNotContain("SetStatus(presentation.Details)", region);
        Assert.DoesNotContain("AuroraLanding.UpdateActivity(data)", region);
        Assert.DoesNotContain("ReadDtcByStatusMask", region); // Existing read is in the caller.
    }

    private static readonly XNamespace XamlName = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument MainWindowXaml()
    {
        using var stream = typeof(UdsDiagnosticReadResultPresentationTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml not embedded.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(e =>
            (string?)e.Attribute(XamlName + "Name") == name);

    private static string Source(string path)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
            directory is not null; directory = directory.Parent)
        {
            var fullPath = Path.Combine(directory.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath)) return File.ReadAllText(fullPath);
        }
        throw new FileNotFoundException(path);
    }
}
