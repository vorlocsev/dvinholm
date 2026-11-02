using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline structural gates for the approved six-page unified WinUI shell.
/// No network, ECU, J2534 DLL loading, update execution or GUI pixel simulation.
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
    public void WideAndCompactBreakpointsStayExplicit()
    {
        var doc = Shell();
        Assert.Equal("980", (string?)State(doc, "ShellWide").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger").Attribute("MinWindowWidth"));
        Assert.Equal("0", (string?)State(doc, "ShellCompact").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger").Attribute("MinWindowWidth"));
        Assert.Equal("64", Setters(doc, "ShellCompact")["ShellSidebarColumn.Width"]);
    }

    [Fact]
    public void ExactlySixApprovedTopLevelSidebarDestinationsAreVisible()
    {
        var doc = Shell();
        var sidebar = Named(doc, "SidebarNavPanel");
        var buttons = sidebar.Elements().Where(e => e.Name.LocalName == "Button").ToArray();
        Assert.Equal(6, buttons.Length);
        Assert.Equal(new[] { "J2534Connection", null, "CemPinReadiness",
                             "CemConfigurationCatalog", "DtcFaults", "EcuWorkspace" },
            buttons.Select(b => (string?)b.Attribute("Tag")));
        Assert.Equal("SidebarUpdatesButton", (string?)buttons[1].Attribute(X + "Name"));
        Assert.Equal("UnifiedUpdatesNavigate_Click", (string?)buttons[1].Attribute("Click"));
        Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName == "Button" &&
            (string?)e.Attribute(X + "Name") == "SidebarHomeButton");
    }

    [Fact]
    public void AllSixLabelsCollapseOnlyAtCompactWidth()
    {
        var doc = Shell();
        var setters = Setters(doc, "ShellCompact");
        foreach (var label in new[] { "ShellNavConnectionLabel", "ShellNavUpdatesLabel",
            "ShellNavCemPinLabel", "ShellNavConfigLabel", "ShellNavDtcLabel", "ShellNavEcuLabel" })
        {
            Assert.Equal("TextBlock", Named(doc, label).Name.LocalName);
            Assert.Equal("Collapsed", setters[label + ".Visibility"]);
            Assert.Null(Named(doc, label).Attribute("Visibility"));
        }
        Assert.DoesNotContain(setters.Keys, key => key.Contains("HomeButton", StringComparison.Ordinal));
    }

    [Fact]
    public void TopLevelButtonsRetainTooltipsAutomationAndRealNavigation()
    {
        var doc = Shell();
        var buttons = Named(doc, "SidebarNavPanel").Elements()
            .Where(e => e.Name.LocalName == "Button").ToArray();
        foreach (var button in buttons)
        {
            Assert.Equal((string?)button.Attribute("AutomationProperties.Name"),
                         (string?)button.Attribute("ToolTipService.ToolTip"));
            Assert.False(string.IsNullOrEmpty((string?)button.Attribute("Click")));
            if (button != buttons[1])
                Assert.Equal("WorkspaceNavigate_Click", (string?)button.Attribute("Click"));
        }
    }

    [Fact]
    public void UnifiedShellStartsWithWorkspaceAndHomeIsHidden()
    {
        var doc = Shell();
        Assert.Equal("Visible", (string?)Named(doc, "AuroraWorkspaceHost").Attribute("Visibility"));
        Assert.Equal("Collapsed", (string?)Named(doc, "AuroraLanding").Attribute("Visibility"));
        Assert.Equal("Collapsed", (string?)Named(doc, "UnifiedUpdatesPage").Attribute("Visibility"));
        Assert.Equal("Disabled", (string?)Named(doc, "WorkspaceScroll").Attribute("HorizontalScrollBarVisibility"));
    }

    [Fact]
    public void UpdatesHasLocalChannelButNoUnverifiedInstallerControls()
    {
        var doc = Shell();
        var page = Named(doc, "UnifiedUpdatesPage");
        var picker = Named(doc, "UnifiedUpdateChannel");
        Assert.Contains(page, picker.Ancestors());
        Assert.Equal("UnifiedUpdateChannel_SelectionChanged", (string?)picker.Attribute("SelectionChanged"));
        Assert.Equal(new[] { "stable", "development" },
            picker.Elements().Select(e => (string?)e.Attribute("Tag")));
        Assert.Contains("SHA-256", page.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(page.Descendants(), e => e.Name.LocalName == "Button" &&
            (string?)e.Attribute("IsEnabled") != "False");
    }

    [Fact]
    public void EveryCompactSetterReferencesAnActualUniqueXamlName()
    {
        var doc = Shell();
        var names = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(s => s != null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        foreach (var target in Setters(doc, "ShellCompact").Keys)
            Assert.Contains(target[..target.IndexOf('.')], names);
        Assert.Equal("12,0,12,16", Setters(doc, "ShellCompact")["WorkspacePageContainer.Padding"]);
    }

    [Fact]
    public void DiagnosticHandlersStayAvailableOnlyInTheirVerifiedWorkspaces()
    {
        var doc = Shell();
        foreach (var (page, name) in new[] { ("ConnectionPage", "ConnectButton"),
                ("CemPinPage", "CemPinReadButton"), ("DtcPage", "ReadDtcButton"),
                ("CanMonitorPage", "StartButton"), ("EcuPage", "DiscoverButton") })
        {
            var button = Named(doc, name);
            Assert.Contains(Named(doc, page), button.Ancestors());
            Assert.NotNull(button.Attribute("Click"));
        }
        foreach (var tag in new[] { "DiagnosticsWorkspace", "CanMonitor", "EcuProfiles" })
            Assert.Contains(Named(doc, "EcuPage").Descendants(),
                e => (string?)e.Attribute("Tag") == tag &&
                     (string?)e.Attribute("Click") == "WorkspaceNavigate_Click");
    }
}
