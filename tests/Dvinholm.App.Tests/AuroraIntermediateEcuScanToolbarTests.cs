using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Six offline checks for the true ECU first-fold scan toolbar at
/// 980–1279 DIP. These are XAML contracts, not native pixel screenshots.
/// </summary>
public sealed class AuroraIntermediateEcuScanToolbarTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraIntermediateEcuScanToolbarTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded MainWindow.xaml missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static IReadOnlyDictionary<string, string> Setters(XDocument doc, string state) =>
        Named(doc, state).Descendants().Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    private static string Width(XDocument doc, string state) =>
        (string?)Named(doc, state).Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth") ?? "";

    [Fact]
    public void EcuToolbarRetainsTheThreeResponsiveBreakpoints()
    {
        var doc = Layout();
        Assert.Equal("1280", Width(doc, "EcuThreeColumns"));
        Assert.Equal("980", Width(doc, "EcuTwoColumns"));
        Assert.Equal("0", Width(doc, "EcuVerticalWorkspace"));
        Assert.Contains(Named(doc, "EcuScanToolbarGrid"), Named(doc, "EcuPage").Descendants());
        Assert.Equal("8", (string?)Named(doc, "EcuScanToolbarGrid").Attribute("RowSpacing"));
    }

    [Fact]
    public void IntermediateSelectorOccupiesTheEntireFirstRow()
    {
        var doc = Layout();
        var mid = Setters(doc, "EcuTwoColumns");
        Assert.Equal("*", mid["EcuScanToolbarActionColumn.Width"]);
        Assert.Equal("0", mid["EcuScanToolbarCancelColumn.Width"]);
        Assert.Equal("2", mid["EcuSelector.(Grid.ColumnSpan)"]);
        Assert.Equal("0", (string?)Named(doc, "EcuSelector").Attribute("Grid.Column"));
        Assert.Null(Named(doc, "EcuSelector").Attribute("Grid.Row"));
        Assert.Equal("EcuSelector_SelectionChanged",
            (string?)Named(doc, "EcuSelector").Attribute("SelectionChanged"));
        Assert.Equal("False", (string?)Named(doc, "EcuSelector").Attribute("IsEnabled"));
    }

    [Fact]
    public void IntermediateScanAndCancelUseEqualWidthCellsOnSecondRow()
    {
        var doc = Layout();
        var mid = Setters(doc, "EcuTwoColumns");
        Assert.Equal("0", mid["DiscoverButton.(Grid.Column)"]);
        Assert.Equal("1", mid["CancelDiscoveryButton.(Grid.Column)"]);
        Assert.Equal("1", mid["DiscoverButton.(Grid.Row)"]);
        Assert.Equal("1", mid["CancelDiscoveryButton.(Grid.Row)"]);
        Assert.Equal("Stretch", mid["DiscoverButton.HorizontalAlignment"]);
        Assert.Equal("Stretch", mid["CancelDiscoveryButton.HorizontalAlignment"]);
        Assert.Equal("Discover_Click", (string?)Named(doc, "DiscoverButton").Attribute("Click"));
        Assert.Equal("CancelDiscovery_Click",
            (string?)Named(doc, "CancelDiscoveryButton").Attribute("Click"));
        Assert.Equal("False", (string?)Named(doc, "DiscoverButton").Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Named(doc, "CancelDiscoveryButton").Attribute("IsEnabled"));
    }

    [Fact]
    public void Under980TheOriginalThreeFullWidthRowsRemain()
    {
        var doc = Layout();
        var narrow = Setters(doc, "EcuVerticalWorkspace");
        Assert.Equal("0", narrow["EcuScanToolbarActionColumn.Width"]);
        Assert.Equal("0", narrow["EcuScanToolbarCancelColumn.Width"]);
        Assert.Equal("0", narrow["DiscoverButton.(Grid.Column)"]);
        Assert.Equal("0", narrow["CancelDiscoveryButton.(Grid.Column)"]);
        Assert.Equal("1", narrow["DiscoverButton.(Grid.Row)"]);
        Assert.Equal("2", narrow["CancelDiscoveryButton.(Grid.Row)"]);
        Assert.Equal("Stretch", narrow["DiscoverButton.HorizontalAlignment"]);
        Assert.Equal("Stretch", narrow["CancelDiscoveryButton.HorizontalAlignment"]);
        Assert.DoesNotContain("EcuSelector.(Grid.ColumnSpan)", narrow.Keys);
        Assert.Equal("Disabled", (string?)Named(doc, "WorkspaceScroll")
            .Attribute("HorizontalScrollBarVisibility"));
    }

    [Fact]
    public void Wide1280RetainsOneLineOriginalScanToolbar()
    {
        var doc = Layout();
        Assert.Empty(Setters(doc, "EcuThreeColumns"));
        var grid = Named(doc, "EcuScanToolbarGrid");
        var widths = grid.Elements()
            .Single(e => e.Name.LocalName == "Grid.ColumnDefinitions").Elements()
            .Select(e => (string?)e.Attribute("Width")).ToArray();
        Assert.Equal(new[] { "*", "Auto", "Auto" }, widths);
        Assert.Equal("1", (string?)Named(doc, "DiscoverButton").Attribute("Grid.Column"));
        Assert.Equal("2", (string?)Named(doc, "CancelDiscoveryButton").Attribute("Grid.Column"));
        Assert.Null(Named(doc, "EcuSelector").Attribute("Grid.ColumnSpan"));
        Assert.Null(Named(doc, "DiscoverButton").Attribute("Grid.Row"));
        Assert.Null(Named(doc, "CancelDiscoveryButton").Attribute("Grid.Row"));
    }

    [Fact]
    public void CompactVisualOnlySettersCannotChangeVehicleOrUpdateBehavior()
    {
        var doc = Layout();
        var mid = Setters(doc, "EcuTwoColumns");
        Assert.Equal(17, mid.Count);
        Assert.All(mid.Keys, key => Assert.DoesNotContain("Click", key));
        Assert.All(mid.Keys, key => Assert.DoesNotContain("IsEnabled", key));
        Assert.All(mid.Keys, key => Assert.DoesNotContain("Visibility", key));
        Assert.Equal("False", (string?)Named(doc, "EcusView").Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Named(doc, "CemConfigWriteButton").Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Named(doc, "DtcClearButton").Attribute("IsEnabled"));
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.Contains(Named(doc, "EcuVehicleModel"),
            Named(doc, "EcuWorkbenchVehiclePane").Descendants());
        Assert.Contains(Named(doc, "EcuSelectionInfo"),
            Named(doc, "EcuWorkbenchDetailPane").Descendants());
    }
}
