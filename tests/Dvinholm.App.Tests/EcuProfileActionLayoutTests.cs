using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight structural offline checks for narrow-window ECU Profiles actions.
/// Tests the actual MainWindow.xaml resource without starting WinUI or J2534.
/// Visual appearance still requires separate Windows manual acceptance.
/// </summary>
public sealed class EcuProfileActionLayoutTests
{
    private static readonly XNamespace XamlName =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void ProfileActionsUseTwoRowsAndFourFlexibleColumns()
    {
        var panel = Actions();
        Assert.Equal("Grid", panel.Name.LocalName);
        Assert.Equal("1", Attr(panel, "Grid.Row"));
        Assert.Equal(2, RowDefinitions(panel).Count());
        Assert.Equal(new[] { "*", "*", "*", "*" },
            ColumnDefinitions(panel).Select(x => Attr(x, "Width")));
        Assert.Equal("6", Attr(panel, "RowSpacing"));
        Assert.Equal("8", Attr(panel, "ColumnSpacing"));
    }

    [Fact]
    public void SevenProfileAndSnapshotActionsRemainInsideCompactGrid()
    {
        var panel = Actions();
        Assert.Equal(7, Buttons(panel).Count);
        Assert.DoesNotContain(panel.Descendants(),
            e => e.Name.LocalName == "StackPanel" &&
                 Attr(e, "Orientation") == "Horizontal");
        Assert.Equal("ProfileActionsPanel", panel.Attribute(XamlName + "Name")?.Value);
    }

    [Fact]
    public void FourProfileButtonsOccupyFirstRowAndRetainOriginalHandlers()
    {
        var controls = Buttons(Actions()).Where(x => Attr(x, "Grid.Row") == "0")
            .OrderBy(x => int.Parse(Attr(x, "Grid.Column")!)).ToArray();
        Assert.Equal(new[] { "0", "1", "2", "3" },
            controls.Select(x => Attr(x, "Grid.Column")));
        Assert.Equal(new[]
        {
            "CreateProfile_Click", "EditProfile_Click",
            "ImportProfiles_Click", "ExportProfiles_Click"
        }, controls.Select(x => Attr(x, "Click")));
        Assert.Equal("False", Attr(controls[1], "IsEnabled"));
    }

    [Fact]
    public void CaptureActionHasDoubleWidthAndStaysDisabledInitially()
    {
        var button = Named("CaptureSnapshotButton");
        Assert.Equal("1", Attr(button, "Grid.Row"));
        Assert.Equal("0", Attr(button, "Grid.Column"));
        Assert.Equal("2", Attr(button, "Grid.ColumnSpan"));
        Assert.Equal("CaptureSnapshot_Click", Attr(button, "Click"));
        Assert.Equal("False", Attr(button, "IsEnabled"));
        Assert.Contains("Not a restorable ECU backup",
            Attr(button, "ToolTipService.ToolTip")!);
    }

    [Fact]
    public void HistoricalInspectionAndComparisonRemainOfflineActions()
    {
        var inspection = Named("InspectSnapshotButton");
        var comparison = Named("CompareSnapshotsButton");
        Assert.Equal("1", Attr(inspection, "Grid.Row"));
        Assert.Equal("2", Attr(inspection, "Grid.Column"));
        Assert.Equal("InspectSnapshot_Click", Attr(inspection, "Click"));
        Assert.Equal("1", Attr(comparison, "Grid.Row"));
        Assert.Equal("3", Attr(comparison, "Grid.Column"));
        Assert.Equal("CompareSnapshots_Click", Attr(comparison, "Click"));
        Assert.Contains("without connecting", Attr(inspection, "ToolTipService.ToolTip")!);
        Assert.Contains("offline", Attr(comparison, "ToolTipService.ToolTip")!);
    }

    [Fact]
    public void SearchAndSelectorResizeRatherThanUsingFixed250PixelColumn()
    {
        var filter = Named("ProfileSearch").Parent!;
        Assert.Equal("Grid", filter.Name.LocalName);
        Assert.Equal(new[] { "2*", "2*", "Auto" },
            ColumnDefinitions(filter).Select(x => Attr(x, "Width")));
        Assert.Equal("ProfileSelector",
            Named("ProfileSelector").Attribute(XamlName + "Name")?.Value);
        Assert.Equal("MarkedVerifiedOnly",
            Named("MarkedVerifiedOnly").Attribute(XamlName + "Name")?.Value);
    }

    [Fact]
    public void ProfileMetadataRemainsInSmallVerticalScrollRegion()
    {
        var text = Named("EcuInfoText");
        Assert.Equal("ScrollViewer", text.Parent?.Name.LocalName);
        Assert.Equal("76", Attr(text.Parent!, "MaxHeight"));
        Assert.Equal("2", Attr(text.Parent!, "Grid.Row"));
        Assert.Equal("Auto", Attr(text.Parent!, "VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", Attr(text.Parent!, "HorizontalScrollBarVisibility"));
    }

    [Fact]
    public void AllSevenActionsRetainUniqueClickHandlersAndStretchHorizontally()
    {
        var buttons = Buttons(Actions());
        Assert.All(buttons, b => Assert.Equal("Stretch", Attr(b, "HorizontalAlignment")));
        var names = buttons.Select(x => Attr(x, "Click")).ToArray();
        Assert.DoesNotContain(names, string.IsNullOrWhiteSpace);
        Assert.Equal(7, names.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(Actions().Descendants(), x =>
            x.Name.LocalName is "ScrollViewer" or "ScrollBar");
    }

    private static XDocument Load()
    {
        using var stream = typeof(EcuProfileActionLayoutTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml test resource missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(string name) =>
        Load().Descendants().Single(e =>
            e.Attribute(XamlName + "Name")?.Value == name);

    private static XElement Actions() => Named("ProfileActionsPanel");

    private static List<XElement> Buttons(XElement element) =>
        element.Descendants().Where(e => e.Name.LocalName == "Button").ToList();

    private static IEnumerable<XElement> RowDefinitions(XElement element) =>
        element.Elements().Where(x => x.Name.LocalName == "Grid.RowDefinitions")
            .SelectMany(x => x.Elements()).Where(x => x.Name.LocalName == "RowDefinition");

    private static IEnumerable<XElement> ColumnDefinitions(XElement element) =>
        element.Elements().Where(x => x.Name.LocalName == "Grid.ColumnDefinitions")
            .SelectMany(x => x.Elements()).Where(x => x.Name.LocalName == "ColumnDefinition");

    private static string? Attr(XElement element, string name) =>
        element.Attribute(name)?.Value;
}
