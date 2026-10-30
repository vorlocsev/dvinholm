using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight structural layout and navigation checks for the real XAML resource.
/// Physical window-size/scroll acceptance still requires a Windows GUI run.
/// No CAN, J2534, VirtualCar, PIN or ECU configuration operation is used.
/// </summary>
public sealed class AuroraResponsiveHomeTests
{
    private static readonly XNamespace XamlName =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Home()
    {
        using var stream = typeof(AuroraResponsiveHomeTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.AuroraHome.xaml")
            ?? throw new InvalidOperationException("Missing embedded Aurora XAML.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(e =>
            (string?)e.Attribute(XamlName + "Name") == name);

    private static XElement State(XDocument document, string name) =>
        document.Descendants().Single(e =>
            e.Name.LocalName == "VisualState" &&
            (string?)e.Attribute(XamlName + "Name") == name);

    private static Dictionary<string, string> Setters(XDocument document, string state) =>
        State(document, state).Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    private static string Source(string relative)
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var path = Path.Combine(d.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void HomeDoesNotForceAnOversizedHorizontalScrollSurface()
    {
        var home = Home();
        var scroll = Named(home, "DashboardScroll");
        Assert.Equal("Disabled", (string?)scroll.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("Auto", (string?)scroll.Attribute("VerticalScrollBarVisibility"));
        var content = Assert.Single(scroll.Elements());
        Assert.Equal("StackPanel", content.Name.LocalName);
        Assert.Null(content.Attribute("MinWidth"));
    }

    [Fact]
    public void ThreeWindowBreakpointsCoverWideMediumAndCompact()
    {
        var home = Home();
        foreach (var (name, width) in new[]
        {
            ("Wide", "1280"), ("Medium", "900"), ("Narrow", "0")
        })
        {
            var state = State(home, name);
            var trigger = Assert.Single(state.Descendants(),
                e => e.Name.LocalName == "AdaptiveTrigger");
            Assert.Equal(width, (string?)trigger.Attribute("MinWindowWidth"));
        }
    }

    [Fact]
    public void PriorityCardsAndOriginalNavigationRemainIntact()
    {
        var home = Home();
        var expected = new[]
        {
            ("CemPinCard", "CardPin_Click"),
            ("CemConfigCard", "CardConfig_Click"),
            ("EcuCard", "CardEcu_Click"),
            ("DiagnosticsCard", "CardDiagnostics_Click")
        };
        var grid = Named(home, "PrimaryCardsGrid");
        var cards = grid.Elements().Where(e => e.Name.LocalName == "Button").ToArray();
        Assert.Equal(expected.Select(x => x.Item1),
            cards.Select(c => (string?)c.Attribute(XamlName + "Name")));
        foreach (var (name, click) in expected)
            Assert.Equal(click, (string?)Named(home, name).Attribute("Click"));
    }

    [Fact]
    public void MediumWidthReflowsFourPriorityCardsIntoTwoRows()
    {
        var setters = Setters(Home(), "Medium");
        Assert.Equal("2", setters["CemPinCard.(Grid.ColumnSpan)"]);
        Assert.Equal("2", setters["CemConfigCard.(Grid.Column)"]);
        Assert.Equal("2", setters["CemConfigCard.(Grid.ColumnSpan)"]);
        Assert.Equal("1", setters["EcuCard.(Grid.Row)"]);
        Assert.Equal("0", setters["EcuCard.(Grid.Column)"]);
        Assert.Equal("2", setters["EcuCard.(Grid.ColumnSpan)"]);
        Assert.Equal("1", setters["DiagnosticsCard.(Grid.Row)"]);
        Assert.Equal("2", setters["DiagnosticsCard.(Grid.Column)"]);
        Assert.Equal("2", setters["DiagnosticsCard.(Grid.ColumnSpan)"]);
    }

    [Fact]
    public void NarrowWidthStacksAllCardsWithoutOverlappingColumns()
    {
        var setters = Setters(Home(), "Narrow");
        var cards = new[] { "CemPinCard", "CemConfigCard", "EcuCard", "DiagnosticsCard" };
        for (var i = 0; i < cards.Length; i++)
        {
            Assert.Equal("4", setters[$"{cards[i]}.(Grid.ColumnSpan)"]);
            if (i != 0)
            {
                Assert.Equal("0", setters[$"{cards[i]}.(Grid.Column)"]);
                Assert.Equal(i.ToString(), setters[$"{cards[i]}.(Grid.Row)"]);
            }
        }
        Assert.Equal("12", setters["PrimaryCardsGrid.RowSpacing"]);
        Assert.Equal(4, Named(Home(), "PrimaryCardsGrid").Descendants()
            .Count(e => e.Name.LocalName == "RowDefinition"));
    }

    [Fact]
    public void NarrowWidthStacksAllStatusAndQuickActionPanels()
    {
        var home = Home();
        var setters = Setters(home, "Narrow");
        Assert.Equal("12", setters["SummaryGrid.RowSpacing"]);
        foreach (var name in new[] { "SummaryConnectionPanel", "SummaryProfilePanel",
                                     "SummaryActionsPanel" })
            Assert.Equal("3", setters[$"{name}.(Grid.ColumnSpan)"]);
        Assert.Equal("1", setters["SummaryProfilePanel.(Grid.Row)"]);
        Assert.Equal("2", setters["SummaryActionsPanel.(Grid.Row)"]);
        Assert.Equal("0", setters["SummaryProfilePanel.(Grid.Column)"]);
        Assert.Equal("0", setters["SummaryActionsPanel.(Grid.Column)"]);
    }

    [Fact]
    public void HomeSidebarButtonIsFunctionalWithoutStartingAnEcuOperation()
    {
        var home = Home();
        var button = Named(home, "HomeButton");
        Assert.Equal("Home_Click", (string?)button.Attribute("Click"));
        var source = Source("src/Dvinholm.App/AuroraHome.xaml.cs");
        var start = source.IndexOf("private void Home_Click(", StringComparison.Ordinal);
        var end = source.IndexOf("private void Navigate(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var body = source[start..end];
        Assert.Contains("DashboardScroll.ChangeView(null, 0.0, null)", body);
        Assert.DoesNotContain("NavigationRequested", body);
        Assert.DoesNotContain("J2534", body);
        Assert.DoesNotContain("PinSearch", body);
    }

    [Fact]
    public void BannerAndAllHomeButtonsRemainVisibleAndActionable()
    {
        var home = Home();
        Assert.Contains("Dvinholm-Banner.webp", home.ToString());
        var buttons = home.Descendants()
            .Where(e => e.Name.LocalName == "Button").ToArray();
        Assert.NotEmpty(buttons);
        Assert.All(buttons, button =>
            Assert.False(string.IsNullOrWhiteSpace((string?)button.Attribute("Click"))));
        var scroll = Named(home, "DashboardScroll");
        Assert.NotNull(scroll);
        Assert.NotNull(Named(home, "ConnectionSummary"));
        Assert.NotNull(Named(home, "ProfileSummary"));
        Assert.NotNull(Named(home, "ActivitySummary"));
    }
}
