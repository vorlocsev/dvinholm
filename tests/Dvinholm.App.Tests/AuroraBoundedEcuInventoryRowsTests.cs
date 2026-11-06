using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline structural contracts for passive ECU inventory readability in
/// the 218-DIP left pane. These do not assert a car connection or pixel parity.
/// </summary>
public sealed class AuroraBoundedEcuInventoryRowsTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraBoundedEcuInventoryRowsTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml resource is missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static XElement ItemTemplate(XDocument doc) =>
        Named(doc, "EcusView").Descendants().Single(e => e.Name.LocalName == "DataTemplate");

    private static XElement[] Rows(XDocument doc) =>
        ItemTemplate(doc).Descendants().Where(e => e.Name.LocalName == "Grid").ToArray();

    private static string[] Widths(XElement grid) => grid.Elements()
        .Single(e => e.Name.LocalName == "Grid.ColumnDefinitions").Elements()
        .Select(e => (string)e.Attribute("Width")!).ToArray();

    [Fact]
    public void InventoryBelongsToThePrimaryThreePaneEcuWorkspace()
    {
        var doc = Layout();
        Assert.Equal("218", (string?)Named(doc, "EcuWorkbenchListColumn").Attribute("Width"));
        Assert.Contains(Named(doc, "EcuWorkbenchListPane"), Named(doc, "EcusView").Ancestors());
        Assert.Contains(Named(doc, "EcuPage"), Named(doc, "EcuWorkbenchListPane").Ancestors());
        Assert.Contains(Named(doc, "EcuWorkbenchVehiclePane"),
            Named(doc, "EcuWorkbenchGrid").Descendants());
        Assert.Contains(Named(doc, "EcuWorkbenchDetailPane"),
            Named(doc, "EcuWorkbenchGrid").Descendants());
    }

    [Fact]
    public void ObservationItemUsesThreeFiniteGridRowsNotUnboundedHorizontalStacks()
    {
        var template = ItemTemplate(Layout());
        var grids = template.Descendants().Where(e => e.Name.LocalName == "Grid").ToArray();
        Assert.Equal(3, grids.Length);
        Assert.Empty(template.Descendants().Where(e =>
            e.Name.LocalName == "StackPanel" &&
            (string?)e.Attribute("Orientation") == "Horizontal"));
        Assert.All(grids, grid =>
        {
            Assert.Null(grid.Attribute("MinWidth"));
            Assert.Equal(2, Widths(grid).Length);
        });
    }

    [Fact]
    public void ResponseIdAndFrameFormatAreBothBoundAndConstrained()
    {
        var first = Rows(Layout())[0];
        Assert.Equal(new[] { "*", "Auto" }, Widths(first));
        var response = first.Elements().Single(e =>
            (string?)e.Attribute("Text") == "{Binding Response}");
        Assert.Equal("Wrap", (string?)response.Attribute("TextWrapping"));
        Assert.Equal("Consolas", (string?)response.Attribute("FontFamily"));
        var format = first.Elements().Single(e =>
            (string?)e.Attribute("Text") == "{Binding Type}");
        Assert.Equal("1", (string?)format.Attribute("Grid.Column"));
        Assert.Equal("Wrap", (string?)format.Attribute("TextWrapping"));
    }

    [Fact]
    public void InferredTxAddressAndUncertaintyAreKeptInBoundedSecondColumn()
    {
        var row = Rows(Layout())[1];
        Assert.Equal(new[] { "Auto", "*" }, Widths(row));
        Assert.Contains(row.Elements(), e => (string?)e.Attribute("Text") == "TX?");
        var data = row.Elements().Single(e => e.Name.LocalName == "StackPanel");
        Assert.Equal("1", (string?)data.Attribute("Grid.Column"));
        var texts = data.Elements().Where(e => e.Name.LocalName == "TextBlock").ToArray();
        Assert.Equal(new[] { "{Binding Request}", "{Binding RequestBasis}" },
            texts.Select(e => (string?)e.Attribute("Text")));
        Assert.All(texts, text => Assert.Equal("Wrap", (string?)text.Attribute("TextWrapping")));
        Assert.Contains("E6C68D", texts[1].ToString());
    }

    [Fact]
    public void FramesAndPayloadUseIndependentEqualWidthCells()
    {
        var row = Rows(Layout())[2];
        Assert.Equal(new[] { "*", "*" }, Widths(row));
        var cells = row.Elements().Where(e => e.Name.LocalName == "StackPanel").ToArray();
        Assert.Equal(2, cells.Length);
        Assert.Null(cells[0].Attribute("Grid.Column"));
        Assert.Equal("1", (string?)cells[1].Attribute("Grid.Column"));
        Assert.Equal(new[] { "{Binding FramesCaption}", "{Binding Frames}" },
            cells[0].Elements().Select(e => (string?)e.Attribute("Text")));
        Assert.Equal(new[] { "{Binding MaxPayloadCaption}", "{Binding MaxPayload}" },
            cells[1].Elements().Select(e => (string?)e.Attribute("Text")));
        foreach (var cell in cells)
            Assert.Equal("Wrap", (string?)cell.Elements().Last().Attribute("TextWrapping"));
    }

    [Fact]
    public void ItemTemplateRetainsExactlyTheOriginalSixDataBindings()
    {
        var template = ItemTemplate(Layout()).ToString();
        foreach (var field in new[] { "Response", "Type", "Request",
            "RequestBasis", "Frames", "MaxPayload", "FramesCaption", "MaxPayloadCaption" })
            Assert.Equal(1, template.Split("{Binding " + field + "}").Length - 1);
        Assert.DoesNotContain("{Binding VerifiedEcuName}", template);
        Assert.DoesNotContain("{Binding PhysicalLocation}", template);
        Assert.DoesNotContain("{Binding Pin}", template);
        Assert.DoesNotContain("IsEnabled=\"True\"", template);
    }

    [Fact]
    public void SelectingObservedRxStillOnlyUsesExistingPassiveSelection()
    {
        var doc = Layout();
        Assert.Equal("Ecu_SelectionChanged",
            (string?)Named(doc, "EcusView").Attribute("SelectionChanged"));
        Assert.Equal("False", (string?)Named(doc, "EcusView").Attribute("IsEnabled"));
        Assert.Equal("260", (string?)Named(doc, "EcusView").Attribute("Height"));
        Assert.Equal("EcuVehicleModel_SelectionChanged",
            (string?)Named(doc, "EcuVehicleModel").Attribute("SelectionChanged"));
        var template = ItemTemplate(doc).ToString();
        Assert.DoesNotContain("Click=", template);
        Assert.DoesNotContain("PassThru", template);
    }

    [Fact]
    public void ResponsiveInventoryKeepsOneColumnAtCompactWidthsAndSixSafeRoutes()
    {
        var doc = Layout();
        var stacked = Named(doc, "EcuVerticalWorkspace").Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!);
        Assert.Equal("*", stacked["EcuWorkbenchListColumn.Width"]);
        Assert.Equal("0", stacked["EcuWorkbenchVehicleColumn.Width"]);
        Assert.Equal("0", stacked["EcuWorkbenchDetailColumn.Width"]);
        Assert.Equal("Disabled", (string?)Named(doc, "WorkspaceScroll")
            .Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.Equal("False", (string?)Named(doc, "CemConfigWriteButton").Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Named(doc, "DtcClearButton").Attribute("IsEnabled"));
    }
}
