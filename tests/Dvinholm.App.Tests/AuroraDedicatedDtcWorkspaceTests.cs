using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline acceptance gates for an Aurora DTC page separate from
/// VIN/DID diagnostics. They do not send commands or prove DTC erasure.
/// </summary>
public sealed class AuroraDedicatedDtcWorkspaceTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraDedicatedDtcWorkspaceTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing MainWindow.xaml embedded resource");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static string Source(string path)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }
        throw new FileNotFoundException(path);
    }

    [Fact]
    public void FaultPageIsItsOwnInitiallyHiddenRoute()
    {
        var doc = Layout();
        Assert.Equal("Collapsed", (string?)Named(doc, "DtcPage").Attribute("Visibility"));
        var route = AuroraWorkspaceNavigation.Describe(AuroraDestination.DtcFaults);
        Assert.Equal("DtcPage", route.Panel);
        Assert.Contains("DTC", route.Title);
        Assert.True(AuroraWorkspaceNavigation.TryParse("DtcFaults", out var parsed));
        Assert.Equal(AuroraDestination.DtcFaults, parsed);
        Assert.DoesNotContain(Named(doc, "DiagnosticsPage"), Named(doc, "DtcPage").Ancestors());
    }

    [Fact]
    public void SidebarHasDedicatedDtcRouteAndCompactIconLabel()
    {
        var doc = Layout();
        var sidebar = Named(doc, "SidebarNavPanel");
        var button = Assert.Single(sidebar.Elements(), e => (string?)e.Attribute("Tag") == "DtcFaults");
        Assert.Equal("WorkspaceNavigate_Click", (string?)button.Attribute("Click"));
        Assert.Equal("Ошибки / DTC", (string?)button.Attribute("AutomationProperties.Name"));
        Assert.Equal("Ошибки / DTC", (string?)button.Attribute("ToolTipService.ToolTip"));
        Assert.Contains(Named(doc, "ShellNavDtcLabel"), button.Descendants());
        Assert.Contains("ShellNavDtcLabel.Visibility", doc.ToString());
    }

    [Fact]
    public void SingleReadButtonBelongsToDtcNotVinDidScreen()
    {
        var doc = Layout();
        Assert.Contains(Named(doc, "DtcPage"), Named(doc, "ReadDtcButton").Ancestors());
        Assert.Contains(Named(doc, "DiagnosticsPage"), Named(doc, "ReadDidButton").Ancestors());
        Assert.Contains(Named(doc, "DiagnosticsPage"), Named(doc, "ReadVinButton").Ancestors());
        Assert.DoesNotContain(Named(doc, "DiagnosticsPage"), Named(doc, "ReadDtcButton").Ancestors());
        Assert.Single(doc.Descendants(), e => (string?)e.Attribute(X + "Name") == "ReadDtcButton");
        Assert.Equal("False", (string?)Named(doc, "ReadDtcButton").Attribute("IsEnabled"));
        Assert.Equal("ReadDtc_Click", (string?)Named(doc, "ReadDtcButton").Attribute("Click"));
    }

    [Fact]
    public void ExistingDtcStatusMaskStillRemainsExclusiveToFaultPage()
    {
        var doc = Layout();
        var filter = Named(doc, "DtcStatusMaskSelector");
        Assert.Contains(Named(doc, "DtcPage"), filter.Ancestors());
        Assert.Equal("0", (string?)filter.Attribute("SelectedIndex"));
        Assert.Equal(new[] {"FF", "01", "04", "08", "80"},
            filter.Elements().Select(e => (string?)e.Attribute("Tag")).ToArray());
    }

    [Fact]
    public void EraseActionIsExplicitlyVisibleButCannotIssueCommands()
    {
        var doc = Layout();
        var clear = Named(doc, "DtcClearButton");
        Assert.Contains(Named(doc, "DtcPage"), clear.Ancestors());
        Assert.Equal("False", (string?)clear.Attribute("IsEnabled"));
        Assert.Null(clear.Attribute("Click"));
        Assert.Contains("не реализовано", (string?)clear.Attribute("ToolTipService.ToolTip"));
    }

    [Fact]
    public void EmptyPresentationCannotPretendToHaveDtcDescriptions()
    {
        var doc = Layout();
        var page = Named(doc, "DtcPage").ToString();
        Assert.Contains("Коды появляются только после явного чтения", page);
        Assert.Contains("Подтверждённого каталога описаний Volvo пока нет", page);
        Assert.Contains("маски статуса", page);
        Assert.DoesNotContain("B1A62-14", page);
        Assert.DoesNotContain("U0151", page);
    }

    [Fact]
    public void SharedPrivateResultExpandsForDtcButNotInactivePages()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("AuroraDestination.DiagnosticsWorkspace or AuroraDestination.DtcFaults", source);
        Assert.Contains("DtcPage, EcuPage", source);
        Assert.Contains("ClearDiagnosticReadResult();", source);
        Assert.Contains("_udsCts?.Cancel();", source);
        var doc = Layout();
        Assert.Equal("Collapsed", (string?)Named(doc, "DiagnosticResultsCard").Attribute("Visibility"));
    }

    [Fact]
    public void NavigationAndDtcFilterDoNotStartVehicleRequests()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = source.IndexOf("private void DashboardNavigate(", StringComparison.Ordinal);
        var end = source.IndexOf("private void ShowDashboardWorkspace()", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var navigation = source[start..end];
        Assert.DoesNotContain("ReadDtc_Click(", navigation);
        Assert.DoesNotContain("RunUdsOperationAsync(", navigation);
        Assert.DoesNotContain("_session.Bus", navigation);
        Assert.Contains("ReadDtcButton.IsEnabled = ready && TryGetSelectedDtcStatusMask(out _);", source);
        Assert.Contains("client.ReadDtcResultByStatusMask(statusMask, token)", source);
    }
}
