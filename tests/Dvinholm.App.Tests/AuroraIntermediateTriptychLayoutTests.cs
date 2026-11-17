using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for the 1 / 2 / 3-column Aurora Connection and
/// Updates workspaces. Visual screenshot review remains a separate gate.
/// </summary>
public sealed class AuroraIntermediateTriptychLayoutTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraIntermediateTriptychLayoutTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded MainWindow.xaml.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string id) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);

    private static IReadOnlyDictionary<string, string> Setters(XDocument doc, string state) =>
        Named(doc, state).Descendants().Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    private static string Trigger(XDocument doc, string state) =>
        (string?)Named(doc, state).Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth") ?? "";

    [Fact]
    public void IntermediateCardLayoutsActivateAt980DipForBothPages()
    {
        var doc = Layout();
        Assert.Equal("980", Trigger(doc, "ConnectionTwoColumns"));
        Assert.Equal("980", Trigger(doc, "UpdatesTwoColumns"));
        Assert.Equal("980", Trigger(doc, "ShellWide"));
        Assert.Equal("1280", Trigger(doc, "HeaderActionsInline"));
    }

    [Fact]
    public void ThreeCardWideLayoutAndFullWidthSidebarRemainTheDefault()
    {
        var doc = Layout();
        Assert.Equal("1280", Trigger(doc, "ConnectionThreeColumns"));
        Assert.Equal("1280", Trigger(doc, "UpdatesThreeColumns"));
        Assert.Equal("216", (string?)Named(doc, "ShellSidebarColumn").Attribute("Width"));
        Assert.Empty(Setters(doc, "ConnectionThreeColumns"));
        Assert.Empty(Setters(doc, "UpdatesThreeColumns"));
        Assert.Equal(new[] { "0", "1", "2" }, Named(doc, "ConnectionThreeCardGrid")
            .Elements().Where(e => e.Name.LocalName == "Border")
            .Select(e => (string?)e.Attribute("Grid.Column")));
    }

    [Fact]
    public void IntermediateConnectionRetainsAdapterAndBusBesideEachOther()
    {
        var doc = Layout();
        var s = Setters(doc, "ConnectionTwoColumns");
        Assert.Equal(6, s.Count);
        Assert.Equal("*", s["ConnectionAdapterColumn.Width"]);
        Assert.Equal("*", s["ConnectionBusColumn.Width"]);
        Assert.Equal("0", s["ConnectionStatusColumn.Width"]);
        Assert.DoesNotContain("ConnectionBusPane.(Grid.Row)", s.Keys);
        Assert.Equal("0", (string?)Named(doc, "ConnectionAdapterPane").Attribute("Grid.Column"));
        Assert.Equal("1", (string?)Named(doc, "ConnectionBusPane").Attribute("Grid.Column"));
    }

    [Fact]
    public void IntermediateConnectionStatusSpansSecondRowWithoutDisappearing()
    {
        var doc = Layout();
        var s = Setters(doc, "ConnectionTwoColumns");
        Assert.Equal("0", s["ConnectionStatusPane.(Grid.Column)"]);
        Assert.Equal("1", s["ConnectionStatusPane.(Grid.Row)"]);
        Assert.Equal("2", s["ConnectionStatusPane.(Grid.ColumnSpan)"]);
        Assert.Contains(Named(doc, "ConnectButton"),
            Named(doc, "ConnectionStatusPane").Descendants());
        Assert.Equal("Connect_Click",
            (string?)Named(doc, "ConnectButton").Attribute("Click"));
        Assert.Equal("{Binding Text, ElementName=ShellConnectionText}",
            (string?)Named(doc, "ConnectionSessionState").Attribute("Text"));
    }

    [Fact]
    public void IntermediateUpdatesShowsInstalledVersionAndChannelFirst()
    {
        var doc = Layout();
        var s = Setters(doc, "UpdatesTwoColumns");
        Assert.Equal(6, s.Count);
        Assert.Equal("*", s["UpdatesVersionColumn.Width"]);
        Assert.Equal("*", s["UpdatesChannelColumn.Width"]);
        Assert.Equal("0", s["UpdatesActionColumn.Width"]);
        Assert.DoesNotContain("UpdatesChannelPane.(Grid.Row)", s.Keys);
        Assert.Equal("0", (string?)Named(doc, "UpdatesVersionPane").Attribute("Grid.Column"));
        Assert.Equal("1", (string?)Named(doc, "UpdatesChannelPane").Attribute("Grid.Column"));
        Assert.Contains(Named(doc, "UnifiedCurrentVersion"),
            Named(doc, "UpdatesVersionPane").Descendants());
        Assert.Contains(Named(doc, "UnifiedUpdateChannel"),
            Named(doc, "UpdatesChannelPane").Descendants());
    }

    [Fact]
    public void IntermediateUpdatesActionPaneSpansSecondRowWithSafeButtons()
    {
        var doc = Layout();
        var s = Setters(doc, "UpdatesTwoColumns");
        Assert.Equal("0", s["UpdatesActionPane.(Grid.Column)"]);
        Assert.Equal("1", s["UpdatesActionPane.(Grid.Row)"]);
        Assert.Equal("2", s["UpdatesActionPane.(Grid.ColumnSpan)"]);
        var actions = Named(doc, "UnifiedUpdateActionList").Elements()
            .Where(e => e.Name.LocalName == "Button").ToArray();
        Assert.Equal(4, actions.Length);
        Assert.Equal("UnifiedCheckUpdate_Click", (string?)actions[0].Attribute("Click"));
        Assert.All(actions.Skip(1),
            a => Assert.Equal("False", (string?)a.Attribute("IsEnabled")));
    }

    [Fact]
    public void Under980BothCardsStillReflowIntoThreeFullWidthRows()
    {
        var doc = Layout();
        Assert.Equal("0", Trigger(doc, "ConnectionVerticalPanels"));
        Assert.Equal("0", Trigger(doc, "UpdatesVerticalPanels"));
        var c = Setters(doc, "ConnectionVerticalPanels");
        Assert.Equal("0", c["ConnectionBusColumn.Width"]);
        Assert.Equal("0", c["ConnectionStatusColumn.Width"]);
        Assert.Equal("1", c["ConnectionBusPane.(Grid.Row)"]);
        Assert.Equal("2", c["ConnectionStatusPane.(Grid.Row)"]);
        var u = Setters(doc, "UpdatesVerticalPanels");
        Assert.Equal("0", u["UpdatesChannelColumn.Width"]);
        Assert.Equal("0", u["UpdatesActionColumn.Width"]);
        Assert.Equal("1", u["UpdatesChannelPane.(Grid.Row)"]);
        Assert.Equal("2", u["UpdatesActionPane.(Grid.Row)"]);
        Assert.Equal("Disabled",
            (string?)Named(doc, "WorkspaceScroll").Attribute("HorizontalScrollBarVisibility"));
    }

    [Fact]
    public void ThreeBreakpointCompositionAddsNoFakeStateOrExtraRoutes()
    {
        var doc = Layout();
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.Equal("J2534Device_SelectionChanged",
            (string?)Named(doc, "J2534Devices").Attribute("SelectionChanged"));
        Assert.Equal("UnifiedUpdateChannel_SelectionChanged",
            (string?)Named(doc, "UnifiedUpdateChannel").Attribute("SelectionChanged"));
        Assert.Equal("False",
            (string?)Named(doc, "UnifiedStageUpdateButton").Attribute("IsEnabled"));
        var states = Named(doc, "ConnectionTwoColumns").ToString() +
            Named(doc, "UpdatesTwoColumns").ToString();
        Assert.DoesNotContain("Click=", states);
        Assert.DoesNotContain("IsEnabled", states);
        Assert.DoesNotContain("J2534", states);
        Assert.DoesNotContain(doc.Descendants(), e =>
            (string?)e.Attribute(X + "Name") is "SettingsPage" or "LauncherPage");
        var names = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(name => name is not null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }
}
