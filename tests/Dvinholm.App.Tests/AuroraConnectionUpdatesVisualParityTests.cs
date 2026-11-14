using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline structural contracts for the approved Aurora three-card
/// Connection / Updates dashboards. No vendor DLL, download, CAN or GUI pixels.
/// </summary>
public sealed class AuroraConnectionUpdatesVisualParityTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var input = typeof(AuroraConnectionUpdatesVisualParityTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing MainWindow XAML.");
        return XDocument.Load(input);
    }

    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(element =>
            (string?)element.Attribute(X + "Name") == name);

    private static XElement State(XDocument document, string name) =>
        document.Descendants().Single(element =>
            element.Name.LocalName == "VisualState" &&
            (string?)element.Attribute(X + "Name") == name);

    private static Dictionary<string, string> Setters(XDocument document, string name) =>
        State(document, name).Descendants()
            .Where(element => element.Name.LocalName == "Setter")
            .ToDictionary(element => (string)element.Attribute("Target")!,
                element => (string)element.Attribute("Value")!, StringComparer.Ordinal);

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void ConnectionMatchesThreeDistinctApprovedPanels()
    {
        var doc = Layout();
        var page = Named(doc, "ConnectionPage");
        var grid = Named(doc, "ConnectionThreeCardGrid");
        Assert.Contains(page, grid.Ancestors());
        var panes = grid.Elements().Where(e => e.Name.LocalName == "Border").ToArray();
        Assert.Equal(new[] { "ConnectionAdapterPane", "ConnectionBusPane",
            "ConnectionStatusPane" },
            panes.Select(e => (string?)e.Attribute(X + "Name")!).ToArray());
        Assert.Equal(new[] { "0", "1", "2" },
            panes.Select(e => (string?)e.Attribute("Grid.Column")!).ToArray());
        Assert.Contains(Named(doc, "J2534Devices"),
            Named(doc, "ConnectionAdapterPane").Descendants());
        Assert.Contains(Named(doc, "BaudRate"),
            Named(doc, "ConnectionBusPane").Descendants());
        Assert.Contains(Named(doc, "ConnectButton"),
            Named(doc, "ConnectionStatusPane").Descendants());
    }

    [Theory]
    [InlineData("ConnectionAdapterPane")]
    [InlineData("ConnectionBusPane")]
    [InlineData("ConnectionStatusPane")]
    public void ConnectionCardsFitTheirContentWithoutEmptyStretchedLowerHalves(string paneName)
    {
        // Actual 2026-11-14 EN 1280x800 DIP Connection screenshot:
        // the CAN and status cards stretch to the taller J2534 adapter
        // card even though their contents end much higher. Keep live
        // labels/controls, and let each Border measure its own height.
        var document = Layout();
        var pane = Named(document, paneName);
        Assert.Equal("Top", (string?)pane.Attribute("VerticalAlignment"));
        Assert.Null(pane.Attribute("Height"));
        Assert.Null(pane.Attribute("MinHeight"));
        Assert.Null(pane.Attribute("MaxHeight"));
        Assert.Equal("0", (string?)pane.Attribute("Margin"));
        Assert.Equal("{StaticResource AuroraCard}", (string?)pane.Attribute("Style"));
        Assert.Contains(Named(document, "ConnectionThreeCardGrid"), pane.Ancestors());
    }

    [Fact]
    public void ConnectionNaturalHeightsKeepDynamicAdapterAuditAndAllBreakpoints()
    {
        var document = Layout();
        var grid = Named(document, "ConnectionThreeCardGrid");
        var rows = grid.Elements().Single(e => e.Name.LocalName == "Grid.RowDefinitions")
            .Elements().ToArray();
        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => Assert.Equal("Auto", (string?)row.Attribute("Height")));
        Assert.Equal("False",
            (string?)Named(document, "J2534ArchitectureAuditExpander").Attribute("IsExpanded"));
        Assert.Contains(Named(document, "J2534ArchitectureAuditExpander"),
            Named(document, "ConnectionAdapterPane").Descendants());

        var wide = Setters(document, "ConnectionThreeColumns");
        var middle = Setters(document, "ConnectionTwoColumns");
        var compact = Setters(document, "ConnectionVerticalPanels");
        Assert.Empty(wide);
        Assert.Equal("1", middle["ConnectionStatusPane.(Grid.Row)"]);
        Assert.Equal("2", middle["ConnectionStatusPane.(Grid.ColumnSpan)"]);
        Assert.Equal("1", compact["ConnectionBusPane.(Grid.Row)"]);
        Assert.Equal("2", compact["ConnectionStatusPane.(Grid.Row)"]);
        Assert.Equal("Disabled",
            (string?)Named(document, "WorkspaceScroll").Attribute("HorizontalScrollMode"));
    }

    [Fact]
    public void ConnectionStacksAllThreeAtCompactWidths()
    {
        var doc = Layout();
        Assert.Equal("1280", (string?)State(doc, "ConnectionThreeColumns")
            .Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.Equal("0", (string?)State(doc, "ConnectionVerticalPanels")
            .Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        var s = Setters(doc, "ConnectionVerticalPanels");
        Assert.Equal("*", s["ConnectionAdapterColumn.Width"]);
        Assert.Equal("0", s["ConnectionBusColumn.Width"]);
        Assert.Equal("0", s["ConnectionStatusColumn.Width"]);
        Assert.Equal("0", s["ConnectionBusPane.(Grid.Column)"]);
        Assert.Equal("1", s["ConnectionBusPane.(Grid.Row)"]);
        Assert.Equal("0", s["ConnectionStatusPane.(Grid.Column)"]);
        Assert.Equal("2", s["ConnectionStatusPane.(Grid.Row)"]);
        Assert.Null(Named(doc, "ConnectionThreeCardGrid").Attribute("MinWidth"));
    }

    [Fact]
    public void ConnectionKeepsRealJ2534HandlersAndNeverClaimsConnectedByDefault()
    {
        var doc = Layout();
        Assert.Equal("J2534Device_SelectionChanged",
            (string?)Named(doc, "J2534Devices").Attribute("SelectionChanged"));
        Assert.Equal("RefreshJ2534Devices_Click",
            (string?)Named(doc, "ConnectionRefreshButton").Attribute("Click"));
        Assert.Equal("Connect_Click",
            (string?)Named(doc, "ConnectButton").Attribute("Click"));
        Assert.Equal("Collapsed",
            (string?)Named(doc, "CanConnectionMode").Attribute("Visibility"));
        Assert.Equal("{Binding Text, ElementName=ShellConnectionText}",
            (string?)Named(doc, "ConnectionSessionState").Attribute("Text"));
        Assert.Contains("disconnected",
            (string?)Named(doc, "AdapterMetadataText").Attribute("Text"),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Named(doc, "J2534ArchitectureAuditExpander"),
            Named(doc, "ConnectionAdapterPane").Descendants());
    }

    [Fact]
    public void UpdatesMatchesThreeCardsWithoutASecondLauncher()
    {
        var doc = Layout();
        var grid = Named(doc, "UpdatesThreeCardGrid");
        Assert.Contains(Named(doc, "UnifiedUpdatesPage"), grid.Ancestors());
        var panes = grid.Elements().Where(e => e.Name.LocalName == "Border").ToArray();
        Assert.Equal(new[] { "UpdatesVersionPane", "UpdatesChannelPane",
            "UpdatesActionPane" },
            panes.Select(e => (string?)e.Attribute(X + "Name")!).ToArray());
        Assert.Contains(Named(doc, "UnifiedCurrentVersion"),
            Named(doc, "UpdatesVersionPane").Descendants());
        Assert.Contains(Named(doc, "UnifiedUpdateChannel"),
            Named(doc, "UpdatesChannelPane").Descendants());
        Assert.Contains(Named(doc, "UnifiedUpdateStatus"),
            Named(doc, "UpdatesActionPane").Descendants());
        Assert.DoesNotContain(doc.Descendants(), e =>
            (string?)e.Attribute(X + "Name") is "LauncherPage" or "SettingsPage");
    }

    [Theory]
    [InlineData("UpdatesVersionPane")]
    [InlineData("UpdatesChannelPane")]
    [InlineData("UpdatesActionPane")]
    public void UpdatesCardsFitTheirActualContentRatherThanStretchingEmptyRowSpace(string paneName)
    {
        // The archived real RU/EN 1280x800 DIP captures show blank lower
        // halves inside Version and Channel: Grid default Stretch made all
        // three Auto-row Borders as tall as the four-button Action pane.
        // Align each Border to its own desired height, without relocating
        // the approved three cards or changing their content/handlers.
        var document = Layout();
        var pane = Named(document, paneName);
        Assert.Equal("Top", (string?)pane.Attribute("VerticalAlignment"));
        Assert.Null(pane.Attribute("Height"));
        Assert.Null(pane.Attribute("MinHeight"));
        Assert.Null(pane.Attribute("MaxHeight"));
        Assert.Equal("0", (string?)pane.Attribute("Margin"));
        Assert.Equal("{StaticResource AuroraCard}", (string?)pane.Attribute("Style"));
    }

    [Fact]
    public void UpdatesNaturalHeightsKeepThreeTwoAndSingleColumnFallbacks()
    {
        var document = Layout();
        var grid = Named(document, "UpdatesThreeCardGrid");
        Assert.All(grid.Elements().Where(e => e.Name.LocalName == "RowDefinitions")
            .SelectMany(e => e.Elements()), row =>
                Assert.Equal("Auto", (string?)row.Attribute("Height")));
        Assert.Empty(Setters(document, "UpdatesThreeColumns"));
        var middle = Setters(document, "UpdatesTwoColumns");
        var compact = Setters(document, "UpdatesVerticalPanels");
        Assert.Equal("1", middle["UpdatesActionPane.(Grid.Row)"]);
        Assert.Equal("2", middle["UpdatesActionPane.(Grid.ColumnSpan)"]);
        Assert.Equal("1", compact["UpdatesChannelPane.(Grid.Row)"]);
        Assert.Equal("2", compact["UpdatesActionPane.(Grid.Row)"]);
        Assert.Equal("Disabled", (string?)Named(document, "WorkspaceScroll")
            .Attribute("HorizontalScrollMode"));
    }

    [Fact]
    public void UpdatesReflowsVerticallyAtCompactWidthsWithoutHorizontalScroll()
    {
        var doc = Layout();
        Assert.Equal("1280", (string?)State(doc, "UpdatesThreeColumns")
            .Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        var s = Setters(doc, "UpdatesVerticalPanels");
        Assert.Equal("*", s["UpdatesVersionColumn.Width"]);
        Assert.Equal("0", s["UpdatesChannelColumn.Width"]);
        Assert.Equal("0", s["UpdatesActionColumn.Width"]);
        Assert.Equal("0", s["UpdatesChannelPane.(Grid.Column)"]);
        Assert.Equal("1", s["UpdatesChannelPane.(Grid.Row)"]);
        Assert.Equal("0", s["UpdatesActionPane.(Grid.Column)"]);
        Assert.Equal("2", s["UpdatesActionPane.(Grid.Row)"]);
        Assert.Equal("Disabled",
            (string?)Named(doc, "WorkspaceScroll")
                .Attribute("HorizontalScrollBarVisibility"));
    }

    [Fact]
    public void UpdatesPreservesFourRealHandlersAndInstallerIsStillNotExposed()
    {
        var doc = Layout();
        var buttons = Named(doc, "UnifiedUpdateActionList").Elements()
            .Where(e => e.Name.LocalName == "Button").ToArray();
        Assert.Equal(new[] { "UnifiedCheckUpdateButton", "UnifiedStageUpdateButton",
            "UnifiedCancelStageButton", "UnifiedPreflightButton" },
            buttons.Select(e => (string?)e.Attribute(X + "Name")!).ToArray());
        Assert.Equal(new[] { "UnifiedCheckUpdate_Click", "UnifiedStageUpdate_Click",
            "UnifiedCancelStage_Click", "UnifiedPreflight_Click" },
            buttons.Select(e => (string?)e.Attribute("Click")!).ToArray());
        Assert.Equal("False", (string?)buttons[1].Attribute("IsEnabled"));
        Assert.Equal("False", (string?)buttons[2].Attribute("IsEnabled"));
        Assert.Equal("False", (string?)buttons[3].Attribute("IsEnabled"));
        Assert.All(buttons, button =>
            Assert.Equal("Stretch", (string?)button.Attribute("HorizontalAlignment")));
        Assert.DoesNotContain(buttons, button =>
            (string?)button.Attribute("Content") is "Установить" or "Install");
    }

    [Fact]
    public void NewConnectionPanelsTranslateWithoutChangingSessionOrSelections()
    {
        var source = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.Contains("ConnectionBusHeading.Text = L(", source);
        Assert.Contains("ConnectionBusEvidenceNote.Text = L(", source);
        Assert.Contains("ConnectionPageHeading.Text = L(", source);
        Assert.Contains("ConnectionDriverHeading.Text = L(", source);
        Assert.Contains("ConnectionRefreshButton.Content = L(", source);
        Assert.DoesNotContain("Connect_Click(", source);
        Assert.DoesNotContain("RefreshJ2534Devices_Click(", source);
        Assert.DoesNotContain("UnifiedUpdateChannel.SelectedIndex =", source);
        Assert.DoesNotContain("PassThru", source);
    }

    [Fact]
    public void EveryPanelAndTriggerHasUniqueNameAndPresentationOnlySetters()
    {
        var doc = Layout();
        var names = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(name => name is not null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        foreach (var state in new[] { "ConnectionVerticalPanels", "UpdatesVerticalPanels" })
        {
            foreach (var target in Setters(doc, state).Keys)
            {
                Assert.Contains(target[..target.IndexOf('.')], names);
                Assert.DoesNotContain("Click", target, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("IsEnabled", target, StringComparison.OrdinalIgnoreCase);
            }
        }
        Assert.Contains("Aurora-Northern-Coast.svg",
            Named(doc, "AuroraHeaderArtwork").ToString());
    }

    // Stage 65: six actual cards plus two grid-breakpoint cases = eight
    // independent offline visual-rhythm checks. Native pixels are still NOT RUN.
    [Theory]
    [InlineData("ConnectionAdapterPane", "ConnectionThreeCardGrid")]
    [InlineData("ConnectionBusPane", "ConnectionThreeCardGrid")]
    [InlineData("ConnectionStatusPane", "ConnectionThreeCardGrid")]
    [InlineData("UpdatesVersionPane", "UpdatesThreeCardGrid")]
    [InlineData("UpdatesChannelPane", "UpdatesThreeCardGrid")]
    [InlineData("UpdatesActionPane", "UpdatesThreeCardGrid")]
    public void DashboardCardsUseGridOwnedSpacingWithoutSecondBottomMargin(
        string paneName, string gridName)
    {
        var document = Layout();
        var pane = Named(document, paneName);
        var grid = Named(document, gridName);
        Assert.Contains(grid, pane.Ancestors());
        Assert.Equal("{StaticResource AuroraCard}", (string?)pane.Attribute("Style"));
        Assert.Equal("0", (string?)pane.Attribute("Margin"));
        Assert.Equal("12", (string?)grid.Attribute("RowSpacing"));
    }

    [Theory]
    [InlineData("ConnectionThreeCardGrid", "ConnectionThreeColumns",
        "ConnectionTwoColumns", "ConnectionVerticalPanels")]
    [InlineData("UpdatesThreeCardGrid", "UpdatesThreeColumns",
        "UpdatesTwoColumns", "UpdatesVerticalPanels")]
    public void ThreeTwoAndOneColumnLayoutsPreserveSingleTwelveDipGutter(
        string gridName, string wide, string middle, string narrow)
    {
        var document = Layout();
        var grid = Named(document, gridName);
        Assert.Equal("12", (string?)grid.Attribute("RowSpacing"));
        Assert.Equal("12", (string?)grid.Attribute("ColumnSpacing"));
        Assert.Null(grid.Attribute("MinWidth"));
        Assert.Equal(new[] { "1280", "980", "0" },
            new[] { wide, middle, narrow }.Select(name =>
                (string?)State(document, name).Descendants()
                    .Single(element => element.Name.LocalName == "AdaptiveTrigger")
                    .Attribute("MinWindowWidth")));
        foreach (var state in new[] { wide, middle, narrow })
            Assert.DoesNotContain(Setters(document, state).Keys,
                target => target.Contains("Margin", StringComparison.Ordinal));
    }
}
