using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight source-level Aurora 64-DIP navigation rail contracts. They do not
/// assert that a real Windows desktop or screenshot has been visually approved.
/// </summary>
public sealed class AuroraCompactNavigationAlignmentTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string[] NavIds =
    {
        "SidebarConnectionButton", "SidebarUpdatesButton", "SidebarCemPinButton",
        "SidebarCemConfigButton", "SidebarDtcButton", "SidebarEcuButton"
    };

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraCompactNavigationAlignmentTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded MainWindow.xaml.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string id) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);

    private static IReadOnlyDictionary<string, string> CompactSetters(XDocument doc) =>
        Named(doc, "ShellCompact").Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    [Fact]
    public void NavigationRailIs64DipUnder980AndStill216DipWhenWide()
    {
        var doc = Layout();
        var narrow = CompactSetters(doc);
        Assert.Equal("64", narrow["ShellSidebarColumn.Width"]);
        Assert.Equal("216", (string?)Named(doc, "ShellSidebarColumn").Attribute("Width"));
        Assert.Equal("980", (string?)Named(doc, "ShellWide").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.Equal("0", (string?)Named(doc, "ShellCompact").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
    }

    [Fact]
    public void ExactlySixNamedButtonsRetainApprovedNavigationOrder()
    {
        var doc = Layout();
        var buttons = Named(doc, "SidebarNavPanel").Elements()
            .Where(e => e.Name.LocalName == "Button").ToArray();
        Assert.Equal(NavIds, buttons.Select(e => (string?)e.Attribute(X + "Name")));
        Assert.Equal("J2534Connection", (string?)buttons[0].Attribute("Tag"));
        Assert.Equal("CemPinReadiness", (string?)buttons[2].Attribute("Tag"));
        Assert.Equal("CemConfigurationCatalog", (string?)buttons[3].Attribute("Tag"));
        Assert.Equal("DtcFaults", (string?)buttons[4].Attribute("Tag"));
        Assert.Equal("EcuWorkspace", (string?)buttons[5].Attribute("Tag"));
        Assert.Null(buttons[1].Attribute("Tag"));
    }

    [Fact]
    public void AllSixCompactButtonsCenterTheirSingleRemainingIcon()
    {
        var doc = Layout();
        var setters = CompactSetters(doc);
        foreach (var id in NavIds)
        {
            Assert.Equal("Center", setters[$"{id}.HorizontalContentAlignment"]);
            Assert.Equal("0,9", setters[$"{id}.Padding"]);
            Assert.Equal("{StaticResource AuroraNav}",
                (string?)Named(doc, id).Attribute("Style"));
        }
    }

    [Fact]
    public void ExpandedRailKeepsLeftAlignedOriginalSpacing()
    {
        var doc = Layout();
        var nav = doc.Descendants().Single(e => e.Name.LocalName == "Style" &&
            (string?)e.Attribute(X + "Key") == "AuroraNav");
        var setters = nav.Elements().Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Property")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);
        Assert.Equal("Left", setters["HorizontalContentAlignment"]);
        Assert.Equal("12,9", setters["Padding"]);
        Assert.Equal("Stretch", setters["HorizontalAlignment"]);
        Assert.Empty(Named(doc, "ShellWide").Descendants()
            .Where(e => e.Name.LocalName == "Setter"));
    }

    [Fact]
    public void IconsStayVisibleWhenAllSixLabelsCollapse()
    {
        var doc = Layout();
        var compact = CompactSetters(doc);
        var textIds = new[] {
            "ShellNavConnectionLabel", "ShellNavUpdatesLabel", "ShellNavCemPinLabel",
            "ShellNavConfigLabel", "ShellNavDtcLabel", "ShellNavEcuLabel"
        };
        for (var i = 0; i < NavIds.Length; i++)
        {
            Assert.Equal("Collapsed", compact[$"{textIds[i]}.Visibility"]);
            var button = Named(doc, NavIds[i]);
            Assert.Single(button.Descendants(), e => e.Name.LocalName == "FontIcon");
            Assert.Contains(Named(doc, textIds[i]), button.Descendants());
            Assert.Equal("18", (string?)button.Descendants()
                .Single(e => e.Name.LocalName == "FontIcon").Attribute("FontSize"));
        }
    }

    [Fact]
    public void CompactBrandRemainsCenteredAndAtmosphereDoesNotStealInput()
    {
        var doc = Layout();
        var compact = CompactSetters(doc);
        var crest = Named(doc, "SidebarCompactBrand");
        Assert.Equal("44", (string?)crest.Attribute("Width"));
        Assert.Equal("Center", (string?)crest.Attribute("HorizontalAlignment"));
        Assert.Equal("Visible", compact["SidebarCompactBrand.Visibility"]);
        Assert.Equal("Collapsed", compact["SidebarBrand.Visibility"]);
        Assert.Equal("0.12", compact["SidebarAuroraRibbon.Opacity"]);
        Assert.Equal("False", (string?)Named(doc, "SidebarAuroraRibbon")
            .Attribute("IsHitTestVisible"));
    }

    [Fact]
    public void NavigationCallbacksAndLocalizedAutomationNamesArePreserved()
    {
        var doc = Layout();
        for (var i = 0; i < NavIds.Length; i++)
        {
            var button = Named(doc, NavIds[i]);
            Assert.Equal(i == 1 ? "UnifiedUpdatesNavigate_Click" :
                "WorkspaceNavigate_Click", (string?)button.Attribute("Click"));
            Assert.NotNull(button.Attribute("AutomationProperties.Name"));
            Assert.NotNull(button.Attribute("ToolTipService.ToolTip"));
        }
        Assert.Equal("DvinholmHeaderConnectionStatus",
            (string?)Named(doc, "HeaderConnectionStatusButton")
                .Attribute("AutomationProperties.AutomationId"));
        Assert.Equal("WorkspaceNavigate_Click",
            (string?)Named(doc, "HeaderConnectionStatusButton").Attribute("Click"));
    }

    [Fact]
    public void CompactNavigationAddsNoSettingsOrVehicleActionsAndNamesRemainUnique()
    {
        var doc = Layout();
        var subtree = Named(doc, "SidebarNavPanel");
        Assert.Empty(subtree.Descendants().Where(e =>
            (string?)e.Attribute("Click") is "Connect_Click" or
                "ReadDtc_Click" or "ReadCemPin_Click"));
        Assert.DoesNotContain(doc.Descendants(), e =>
            (string?)e.Attribute(X + "Name") is "SettingsPage" or "LauncherPage");
        var all = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(n => n is not null).ToArray();
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
    }
}
