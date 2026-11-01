using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight structural contracts for Aurora's compact main-window shell.
/// Actual small-window layout still needs an installed Windows GUI resize check.
/// These tests make no adapter, PIN, network or vehicle requests.
/// </summary>
public sealed class AuroraCompactShellTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Shell()
    {
        using var stream = typeof(AuroraCompactShellTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded MainWindow XAML.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static XElement State(XDocument doc, string name) =>
        doc.Descendants().Single(e => e.Name.LocalName == "VisualState" &&
            (string?)e.Attribute(X + "Name") == name);

    private static Dictionary<string, string> Setters(XDocument doc, string state) =>
        State(doc, state).Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    [Fact]
    public void CompactAndWideStatesHaveExplicitNonOverlappingMinima()
    {
        var doc = Shell();
        Assert.Equal("980", (string?)State(doc, "ShellWide").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger").Attribute("MinWindowWidth"));
        Assert.Equal("0", (string?)State(doc, "ShellCompact").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger").Attribute("MinWindowWidth"));
        Assert.Empty(State(doc, "ShellWide").Descendants()
            .Where(e => e.Name.LocalName == "Setter"));
    }

    [Fact]
    public void CompactSidebarShrinksAndRemovesDecorationsWithoutRemovingNavigation()
    {
        var doc = Shell();
        var setters = Setters(doc, "ShellCompact");
        Assert.Equal("216", (string?)Named(doc, "ShellSidebarColumn").Attribute("Width"));
        Assert.Equal("64", setters["ShellSidebarColumn.Width"]);
        Assert.Equal("Collapsed", setters["SidebarBrand.Visibility"]);
        Assert.Equal("Collapsed", setters["SidebarFooter.Visibility"]);
        Assert.Equal("6,12", setters["SidebarNavPanel.Padding"]);
        Assert.Equal("⌂", setters["SidebarHomeButton.Content"]);
        Assert.Equal("Center", setters["SidebarHomeButton.HorizontalContentAlignment"]);
        Assert.Equal("DashboardReturn_Click", (string?)Named(doc, "SidebarHomeButton").Attribute("Click"));
    }

    [Fact]
    public void AllEightTextLabelsCollapseOnlyInCompactState()
    {
        var doc = Shell();
        var setters = Setters(doc, "ShellCompact");
        foreach (var label in new[]
        {
            "ShellNavCemPinLabel", "ShellNavConfigLabel", "ShellNavDtcLabel", "ShellNavEcuLabel",
            "ShellNavDiagnosticsLabel", "ShellNavMonitorLabel",
            "ShellNavProfilesLabel", "ShellNavConnectionLabel"
        })
        {
            Assert.Equal("TextBlock", Named(doc, label).Name.LocalName);
            Assert.Equal("Collapsed", setters[label + ".Visibility"]);
            Assert.Null(Named(doc, label).Attribute("Visibility"));
        }
    }

    [Fact]
    public void EveryCompactNavButtonKeepsItsTooltipAutomationNameAndClick()
    {
        var doc = Shell();
        var sidebar = Named(doc, "SidebarNavPanel");
        var links = sidebar.Elements().Where(e => e.Attribute("Tag") != null).ToArray();
        Assert.Equal(8, links.Length);
        Assert.All(links, button =>
        {
            var expectedName = (string?)button.Attribute("AutomationProperties.Name");
            Assert.False(string.IsNullOrWhiteSpace(expectedName));
            Assert.Equal(expectedName, (string?)button.Attribute("ToolTipService.ToolTip"));
            Assert.Equal("WorkspaceNavigate_Click", (string?)button.Attribute("Click"));
        });
        Assert.Equal("Главная", (string?)Named(doc, "SidebarHomeButton")
            .Attribute("ToolTipService.ToolTip"));
    }

    [Fact]
    public void HeaderMovesConnectionAndCancelActionsBelowTitleAtCompactWidth()
    {
        var doc = Shell();
        var setters = Setters(doc, "ShellCompact");
        var header = Named(doc, "WorkspaceHeaderGrid");
        Assert.Equal(2, header.Elements().Single(e => e.Name.LocalName == "Grid.RowDefinitions")
            .Elements().Count());
        Assert.Equal("1", setters["WorkspaceHeaderActions.(Grid.Row)"]);
        Assert.Equal("0", setters["WorkspaceHeaderActions.(Grid.Column)"]);
        Assert.Equal("Left", setters["WorkspaceHeaderActions.HorizontalAlignment"]);
        Assert.Equal("23", setters["WorkspaceTitle.FontSize"]);
        Assert.Equal("WorkspaceNavigate_Click", (string?)Named(doc, "WorkspaceHeaderActions")
            .Descendants().Single(e => (string?)e.Attribute("Tag") == "J2534Connection")
            .Attribute("Click"));
        Assert.Contains(Named(doc, "WorkspaceHeaderActions"), Named(doc, "CancelUdsButton").Ancestors());
    }

    [Fact]
    public void ContentAndStatusGuttersShrinkWhileVerticalScrollingRemains()
    {
        var doc = Shell();
        var setters = Setters(doc, "ShellCompact");
        Assert.Equal("12,12,12,12", setters["WorkspaceHeaderGrid.Margin"]);
        Assert.Equal("12,0,12,16", setters["WorkspacePageContainer.Padding"]);
        Assert.Equal("12,10", setters["ShellStatusBar.Padding"]);
        var scroll = Named(doc, "WorkspaceScroll");
        Assert.Equal("Auto", (string?)scroll.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)scroll.Attribute("HorizontalScrollBarVisibility"));
        Assert.Contains(Named(doc, "WorkspacePageContainer"), scroll.Descendants());
        var sidebarScroll = Named(doc, "SidebarNavPanel").Ancestors()
            .Single(e => e.Name.LocalName == "ScrollViewer");
        Assert.Equal("Auto", (string?)sidebarScroll.Attribute("VerticalScrollBarVisibility"));
    }

    [Fact]
    public void EverySetterReferencesAnExistingNamedXamlElement()
    {
        var doc = Shell();
        var names = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(s => s != null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        var setters = Setters(doc, "ShellCompact");
        foreach (var target in setters.Keys)
        {
            var element = target[..target.IndexOf('.')];
            Assert.Contains(element, names);
        }
    }

    [Fact]
    public void ResizingCannotAddAlternativeVehicleOrPinOperationHandlers()
    {
        var doc = Shell();
        var setters = Setters(doc, "ShellCompact");
        Assert.All(setters.Keys, target =>
            Assert.DoesNotContain("Click", target, StringComparison.Ordinal));
        foreach (var name in new[]
        {
            "CemPartNumberReadButton", "CemPinReadButton", "ConnectButton",
            "ReadVinButton", "ReadDidButton", "StartButton"
        })
            Assert.NotNull(Named(doc, name).Attribute("Click"));
        Assert.Equal("Collapsed", (string?)Named(doc, "AuroraWorkspaceHost")
            .Attribute("Visibility"));
    }
}
