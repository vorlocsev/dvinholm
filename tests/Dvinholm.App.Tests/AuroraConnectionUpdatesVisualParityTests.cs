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
        Assert.Contains("Dvinholm-Banner.png",
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
