using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline geometry and read-only contracts for the ECU 1/2/3-column
/// layouts. They do not substitute for Windows screenshots or hardware checks.
/// </summary>
public sealed class AuroraIntermediateEcuTwoPaneLayoutTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var resource = typeof(AuroraIntermediateEcuTwoPaneLayoutTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded MainWindow.xaml unavailable.");
        return XDocument.Load(resource);
    }

    private static XElement Named(XDocument doc, string id) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);

    private static string Trigger(XDocument doc, string state) =>
        (string?)Named(doc, state).Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth") ?? "";

    private static IReadOnlyDictionary<string, string> Setters(XDocument doc, string state) =>
        Named(doc, state).Descendants().Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    [Fact]
    public void ThreeEcuBreakpointLevelsAre1280Then980ThenZero()
    {
        var doc = Layout();
        var group = Named(doc, "EcuResponsiveStates");
        Assert.Equal(new[] { "EcuThreeColumns", "EcuTwoColumns", "EcuVerticalWorkspace" },
            group.Elements().Where(e => e.Name.LocalName == "VisualState")
                .Select(e => (string?)e.Attribute(X + "Name")));
        Assert.Equal("1280", Trigger(doc, "EcuThreeColumns"));
        Assert.Equal("980", Trigger(doc, "EcuTwoColumns"));
        Assert.Equal("0", Trigger(doc, "EcuVerticalWorkspace"));
        Assert.Equal("980", Trigger(doc, "ShellWide"));
    }

    [Fact]
    public void AtIntermediateWidthsObservedRxAndVehicleUseTheOriginalFirstRow()
    {
        var doc = Layout();
        var mid = Setters(doc, "EcuTwoColumns");
        Assert.Equal("218", (string?)Named(doc, "EcuWorkbenchListColumn").Attribute("Width"));
        Assert.Equal("*", (string?)Named(doc, "EcuWorkbenchVehicleColumn").Attribute("Width"));
        Assert.DoesNotContain("EcuWorkbenchListColumn.Width", mid.Keys);
        Assert.DoesNotContain("EcuWorkbenchVehicleColumn.Width", mid.Keys);
        Assert.Equal("0", (string?)Named(doc, "EcuWorkbenchListPane").Attribute("Grid.Column"));
        Assert.Equal("1", (string?)Named(doc, "EcuWorkbenchVehiclePane").Attribute("Grid.Column"));
        Assert.Null(Named(doc, "EcuWorkbenchVehiclePane").Attribute("Grid.Row"));
        Assert.Contains(Named(doc, "EcuP2SideProfile"),
            Named(doc, "EcuWorkbenchVehiclePane").Descendants());
        Assert.Contains(Named(doc, "EcusView"),
            Named(doc, "EcuWorkbenchListPane").Descendants());
    }

    [Fact]
    public void AtIntermediateWidthsSelectedEvidenceSpansBothColumnsOnSecondRow()
    {
        var doc = Layout();
        var mid = Setters(doc, "EcuTwoColumns");
        Assert.Equal("0", mid["EcuWorkbenchDetailColumn.Width"]);
        Assert.Equal("0", mid["EcuWorkbenchDetailPane.(Grid.Column)"]);
        Assert.Equal("1", mid["EcuWorkbenchDetailPane.(Grid.Row)"]);
        Assert.Equal("2", mid["EcuWorkbenchDetailPane.(Grid.ColumnSpan)"]);
        Assert.Equal("Disabled",
            (string?)Named(doc, "WorkspaceScroll").Attribute("HorizontalScrollBarVisibility"));
        var pane = Named(doc, "EcuWorkbenchDetailPane");
        Assert.Contains(Named(doc, "EcuSelectionInfo"), pane.Descendants());
        Assert.Contains(Named(doc, "EcuSelectionDisclaimer"), pane.Descendants());
    }

    [Fact]
    public void IntermediateToolbarPlacesScanButtonsBesideEachOtherUnderFullWidthSelector()
    {
        var doc = Layout();
        var mid = Setters(doc, "EcuTwoColumns");
        Assert.Equal("*", mid["EcuScanToolbarActionColumn.Width"]);
        Assert.Equal("2", mid["EcuSelector.(Grid.ColumnSpan)"]);
        Assert.Equal("0", mid["EcuScanToolbarCancelColumn.Width"]);
        Assert.Equal("0", mid["DiscoverButton.(Grid.Column)"]);
        Assert.Equal("1", mid["DiscoverButton.(Grid.Row)"]);
        Assert.Equal("Stretch", mid["DiscoverButton.HorizontalAlignment"]);
        Assert.Equal("1", mid["CancelDiscoveryButton.(Grid.Column)"]);
        Assert.Equal("1", mid["CancelDiscoveryButton.(Grid.Row)"]);
        Assert.Equal("Stretch", mid["CancelDiscoveryButton.HorizontalAlignment"]);
        Assert.Equal("Discover_Click", (string?)Named(doc, "DiscoverButton").Attribute("Click"));
        Assert.Equal("CancelDiscovery_Click", (string?)Named(doc, "CancelDiscoveryButton").Attribute("Click"));
        Assert.Equal("False", (string?)Named(doc, "DiscoverButton").Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Named(doc, "CancelDiscoveryButton").Attribute("IsEnabled"));
    }

    [Fact]
    public void IntermediateSecondaryToolActionsStayTwoPlusFullWidthOne()
    {
        var doc = Layout();
        var mid = Setters(doc, "EcuTwoColumns");
        Assert.Equal("0", mid["EcuToolsThirdColumn.Width"]);
        Assert.Equal("0", mid["EcuProfilesNavigateButton.(Grid.Column)"]);
        Assert.Equal("1", mid["EcuProfilesNavigateButton.(Grid.Row)"]);
        Assert.Equal("2", mid["EcuProfilesNavigateButton.(Grid.ColumnSpan)"]);
        foreach (var id in new[] { "EcuDiagnosticsNavigateButton",
            "EcuMonitorNavigateButton", "EcuProfilesNavigateButton" })
        {
            var button = Named(doc, id);
            Assert.Equal("WorkspaceNavigate_Click", (string?)button.Attribute("Click"));
            Assert.NotNull(button.Attribute("Tag"));
        }
        Assert.Equal(3, Named(doc, "EcuToolsGrid").Elements()
            .Count(e => e.Name.LocalName == "Button"));
    }

    [Fact]
    public void Under980TheExistingFullyVerticalEcuLayoutIsUnchanged()
    {
        var doc = Layout();
        var stacked = Setters(doc, "EcuVerticalWorkspace");
        Assert.Equal("*", stacked["EcuWorkbenchListColumn.Width"]);
        Assert.Equal("0", stacked["EcuWorkbenchVehicleColumn.Width"]);
        Assert.Equal("0", stacked["EcuWorkbenchDetailColumn.Width"]);
        Assert.Equal("0", stacked["EcuWorkbenchVehiclePane.(Grid.Column)"]);
        Assert.Equal("1", stacked["EcuWorkbenchVehiclePane.(Grid.Row)"]);
        Assert.Equal("0", stacked["EcuWorkbenchDetailPane.(Grid.Column)"]);
        Assert.Equal("2", stacked["EcuWorkbenchDetailPane.(Grid.Row)"]);
        Assert.Equal("0", stacked["EcuToolsThirdColumn.Width"]);
        Assert.Equal("1", stacked["DiscoverButton.(Grid.Row)"]);
        Assert.Equal("2", stacked["CancelDiscoveryButton.(Grid.Row)"]);
    }

    [Fact]
    public void Wide1280LayoutRestoresAllThreeOriginalColumnsWithoutOverrides()
    {
        var doc = Layout();
        Assert.Empty(Setters(doc, "EcuThreeColumns"));
        Assert.Equal("218", (string?)Named(doc, "EcuWorkbenchListColumn").Attribute("Width"));
        Assert.Equal("*", (string?)Named(doc, "EcuWorkbenchVehicleColumn").Attribute("Width"));
        Assert.Equal("240", (string?)Named(doc, "EcuWorkbenchDetailColumn").Attribute("Width"));
        Assert.Equal(new[] { "0", "1", "2" },
            Named(doc, "EcuWorkbenchGrid").Elements()
                .Where(e => e.Name.LocalName == "Border")
                .Select(e => (string?)e.Attribute("Grid.Column")));
        Assert.Null(Named(doc, "EcuWorkbenchDetailPane").Attribute("Grid.ColumnSpan"));
        Assert.Equal("False", (string?)Named(doc, "EcusView").Attribute("IsEnabled"));
    }

    [Fact]
    public void NewSettersCannotChangeVehicleRequestsOrMakeUnverifiedValuesReal()
    {
        var doc = Layout();
        var mid = Setters(doc, "EcuTwoColumns");
        Assert.Equal(17, mid.Count);
        Assert.All(mid.Keys, target => Assert.DoesNotContain("IsEnabled", target));
        Assert.All(mid.Keys, target => Assert.DoesNotContain("Click", target));
        Assert.All(mid.Keys, target => Assert.DoesNotContain("Visibility", target));
        Assert.Equal("Ecu_SelectionChanged",
            (string?)Named(doc, "EcusView").Attribute("SelectionChanged"));
        Assert.Equal("EcuVehicleModel_SelectionChanged",
            (string?)Named(doc, "EcuVehicleModel").Attribute("SelectionChanged"));
        Assert.Equal("False", (string?)Named(doc, "CemConfigWriteButton").Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Named(doc, "DtcClearButton").Attribute("IsEnabled"));
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        var named = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(n => n is not null).ToArray();
        Assert.Equal(named.Length, named.Distinct(StringComparer.Ordinal).Count());
    }
}
