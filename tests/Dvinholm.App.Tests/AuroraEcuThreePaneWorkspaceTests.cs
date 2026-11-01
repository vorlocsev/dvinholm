using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline structural checks for the approved Aurora ECU layout.
/// Nothing in these tests sends J2534/CAN traffic or verifies real ECU positions.
/// </summary>
public sealed class AuroraEcuThreePaneWorkspaceTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var resource = typeof(AuroraEcuThreePaneWorkspaceTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded MainWindow XAML missing");
        return XDocument.Load(resource);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static XElement State(XDocument doc, string name) =>
        doc.Descendants().Single(e => e.Name.LocalName == "VisualState" &&
            (string?)e.Attribute(X + "Name") == name);

    private static Dictionary<string, string> Setters(XDocument doc, string name) =>
        State(doc, name).Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    private static string Source(string path)
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var file = Path.Combine(folder.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(path);
    }

    [Fact]
    public void ApprovedWorkspacePresentsObservationListCarAndDetailsInThatOrder()
    {
        var doc = Layout();
        var grid = Named(doc, "EcuWorkbenchGrid");
        Assert.Contains(Named(doc, "EcuPage"), grid.Ancestors());
        var panes = grid.Elements().Where(e => e.Name.LocalName == "Border").ToArray();
        Assert.Equal(new[] { "EcuWorkbenchListPane", "EcuWorkbenchVehiclePane", "EcuWorkbenchDetailPane" },
            panes.Select(e => (string)e.Attribute(X + "Name")!).ToArray());
        Assert.Equal(new[] { "0", "1", "2" },
            panes.Select(e => (string)e.Attribute("Grid.Column")!).ToArray());
    }

    [Fact]
    public void WideLayoutLeavesTheVehicleFluidAndCapsBothSidePanels()
    {
        var doc = Layout();
        Assert.Equal("218", (string?)Named(doc, "EcuWorkbenchListColumn").Attribute("Width"));
        Assert.Equal("*", (string?)Named(doc, "EcuWorkbenchVehicleColumn").Attribute("Width"));
        Assert.Equal("240", (string?)Named(doc, "EcuWorkbenchDetailColumn").Attribute("Width"));
        Assert.Equal("1280", (string?)State(doc, "EcuThreeColumns").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger").Attribute("MinWindowWidth"));
        Assert.Empty(State(doc, "EcuThreeColumns").Descendants()
            .Where(e => e.Name.LocalName == "Setter"));
    }

    [Fact]
    public void CompactLayoutStacksThreePanesWithoutDroppingNavigation()
    {
        var doc = Layout();
        Assert.Equal("0", (string?)State(doc, "EcuVerticalWorkspace").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger").Attribute("MinWindowWidth"));
        var setters = Setters(doc, "EcuVerticalWorkspace");
        Assert.Equal("*", setters["EcuWorkbenchListColumn.Width"]);
        Assert.Equal("0", setters["EcuWorkbenchVehicleColumn.Width"]);
        Assert.Equal("0", setters["EcuWorkbenchDetailColumn.Width"]);
        Assert.Equal("0", setters["EcuWorkbenchVehiclePane.(Grid.Column)"]);
        Assert.Equal("1", setters["EcuWorkbenchVehiclePane.(Grid.Row)"]);
        Assert.Equal("0", setters["EcuWorkbenchDetailPane.(Grid.Column)"]);
        Assert.Equal("2", setters["EcuWorkbenchDetailPane.(Grid.Row)"]);
        Assert.Equal("Auto", (string?)Named(doc, "WorkspaceScroll").Attribute("VerticalScrollBarVisibility"));
    }

    [Fact]
    public void CompactScanToolbarMovesActionsBelowTheSelector()
    {
        var doc = Layout();
        var grid = Named(doc, "EcuScanToolbarGrid");
        Assert.Contains(Named(doc, "EcuSelector"), grid.Elements());
        Assert.Contains(Named(doc, "DiscoverButton"), grid.Elements());
        Assert.Contains(Named(doc, "CancelDiscoveryButton"), grid.Elements());
        Assert.Equal(3, grid.Elements().Single(e => e.Name.LocalName == "Grid.RowDefinitions")
            .Elements().Count());
        var setters = Setters(doc, "EcuVerticalWorkspace");
        Assert.Equal("0", setters["EcuScanToolbarActionColumn.Width"]);
        Assert.Equal("0", setters["EcuScanToolbarCancelColumn.Width"]);
        Assert.Equal("1", setters["DiscoverButton.(Grid.Row)"]);
        Assert.Equal("2", setters["CancelDiscoveryButton.(Grid.Row)"]);
        Assert.Equal("0", setters["DiscoverButton.(Grid.Column)"]);
        Assert.Equal("0", setters["CancelDiscoveryButton.(Grid.Column)"]);
        Assert.Equal("Stretch", setters["DiscoverButton.HorizontalAlignment"]);
        Assert.Equal("Stretch", setters["CancelDiscoveryButton.HorizontalAlignment"]);
    }

    [Fact]
    public void ObservationListKeepsRealBindingAndOriginalSelectionHandler()
    {
        var doc = Layout();
        var list = Named(doc, "EcusView");
        Assert.Contains(Named(doc, "EcuWorkbenchListPane"), list.Ancestors());
        Assert.Equal("Ecu_SelectionChanged", (string?)list.Attribute("SelectionChanged"));
        Assert.Equal("False", (string?)list.Attribute("IsEnabled"));
        Assert.Equal("260", (string?)list.Attribute("Height"));
        var xaml = list.ToString();
        foreach (var binding in new[] { "{Binding Response}", "{Binding Request}",
            "{Binding RequestBasis}", "{Binding Frames}", "{Binding Type}",
            "{Binding MaxPayload}" })
            Assert.Contains(binding, xaml);
        Assert.Contains("Observed RX", xaml);
        Assert.Contains("Guessed TX", xaml);
    }

    [Fact]
    public void BothSideProfilesAndPassiveObservationRailRemainInCentralPane()
    {
        var doc = Layout();
        var car = Named(doc, "EcuWorkbenchVehiclePane");
        foreach (var name in new[] { "EcuP2SideProfile", "EcuP3SideProfile",
            "EcuVehicleModel", "EcuObservationRail", "EcuObservationRailSummary" })
            Assert.Contains(car, Named(doc, name).Ancestors());
        Assert.Equal("Collapsed", (string?)Named(doc, "EcuP3SideProfile").Attribute("Visibility"));
        Assert.Contains("не позиции ECU", car.ToString());
        Assert.Contains("не указывает физические позиции ECU", car.ToString());
    }

    [Fact]
    public void RightPaneOwnsDetailsAndNoRequestSendingAction()
    {
        var doc = Layout();
        var pane = Named(doc, "EcuWorkbenchDetailPane");
        var info = Named(doc, "EcuSelectionInfo");
        Assert.Contains(pane, info.Ancestors());
        Assert.Equal("86", (string?)info.Parent?.Attribute("MaxHeight"));
        Assert.Equal("Disabled", (string?)info.Parent?.Attribute("HorizontalScrollBarVisibility"));
        var action = pane.Descendants().Single(e => e.Name.LocalName == "Button");
        Assert.Equal("DiagnosticsWorkspace", (string?)action.Attribute("Tag"));
        Assert.Equal("WorkspaceNavigate_Click", (string?)action.Attribute("Click"));
        Assert.Contains("Нет подтверждённого сопоставления", pane.ToString());
    }

    [Fact]
    public void ResponsiveSettersArePresentationOnlyAndSelectionSafetyIsPreserved()
    {
        var doc = Layout();
        var names = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(n => n is not null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        foreach (var target in Setters(doc, "EcuVerticalWorkspace").Keys)
        {
            Assert.DoesNotContain("Click", target, StringComparison.Ordinal);
            Assert.Contains(target[..target.IndexOf('.')], names);
        }
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("!ReferenceEquals(_ecus[index], row)", source);
        Assert.Contains("UdsRequestId.Text = string.Empty;", source);
        Assert.Contains("UdsPassiveEcuListPresentation.SelectionDetails(row.Candidate)", source);
        Assert.Contains("ShowAuroraEcuObservationRail(found, complete)", source);
    }
}
