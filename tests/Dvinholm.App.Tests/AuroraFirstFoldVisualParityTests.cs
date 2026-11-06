using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline Aurora composition contracts for first-fold density and
/// the decorative Northern lights rail. Not a Windows pixel comparison.
/// </summary>
public sealed class AuroraFirstFoldVisualParityTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraFirstFoldVisualParityTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml resource missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static IReadOnlyDictionary<string, string> StateSetters(XDocument doc, string state) =>
        Named(doc, state).Descendants().Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    private static string Source(string relative)
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory);
             folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }

        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void HeaderKeepsApprovedImageWithMoreCompactFirstFold()
    {
        var doc = Layout();
        var header = Named(doc, "WorkspaceHeaderGrid");
        Assert.Equal("24,12,24,12", (string?)header.Attribute("Margin"));
        Assert.Equal("132", (string?)header.Attribute("MinHeight"));
        var banner = Named(doc, "AuroraHeaderArtwork");
        Assert.Contains(header, banner.Ancestors());
        Assert.Equal("False", (string?)banner.Attribute("IsHitTestVisible"));
        Assert.Contains("ms-appx:///Assets/Dvinholm-Banner.png", banner.ToString());
        Assert.Equal("2", (string?)banner.Attribute("Grid.RowSpan"));
    }

    [Fact]
    public void CompactFirstFoldHasReducedMinimumButPreservesTrueActions()
    {
        var doc = Layout();
        var compact = StateSetters(doc, "ShellCompact");
        Assert.Equal("12,10,12,10", compact["WorkspaceHeaderGrid.Margin"]);
        Assert.Equal("116", compact["WorkspaceHeaderGrid.MinHeight"]);
        Assert.Equal("Horizontal", compact["WorkspaceHeaderActions.Orientation"]);
        Assert.Equal("1", compact["WorkspaceHeaderActions.(Grid.Row)"]);
        Assert.Contains(Named(doc, "ShellConnectionText"),
            Named(doc, "WorkspaceHeaderActions").Descendants());
        Assert.Contains(Named(doc, "UnifiedLanguageSelector"),
            Named(doc, "WorkspaceHeaderActions").Descendants());
        Assert.Equal("Collapsed", (string?)Named(doc, "CancelUdsButton").Attribute("Visibility"));
    }

    [Fact]
    public void CardsHaveCompactConsistentGradientWithoutHidingContent()
    {
        var doc = Layout();
        var style = doc.Descendants().Single(e =>
            e.Name.LocalName == "Style" &&
            (string?)e.Attribute(X + "Key") == "AuroraCard");
        var setters = style.Elements().Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Property")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);
        Assert.Equal("16", setters["Padding"]);
        Assert.Equal("0,0,0,12", setters["Margin"]);
        Assert.Equal("12", setters["CornerRadius"]);
        Assert.Equal("{StaticResource AuroraPanelGradient}", setters["Background"]);
        Assert.Contains(Named(doc, "ConnectionAdapterPane"), Named(doc, "ConnectionPage").Descendants());
        Assert.Contains(Named(doc, "DtcResultsPane"), Named(doc, "DtcPage").Descendants());
    }

    [Fact]
    public void SidebarNorthernLightsAreSubtlePaintOnlyAndBehindNavigation()
    {
        var doc = Layout();
        var outer = Named(doc, "AuroraSidebarSurface");
        var background = Named(doc, "SidebarAuroraBackdrop");
        var ribbon = Named(doc, "SidebarAuroraRibbon");
        var rail = Named(doc, "SidebarNavPanel");
        Assert.Contains(outer, background.Ancestors());
        Assert.Contains(outer, ribbon.Ancestors());
        Assert.Contains(outer, rail.Ancestors());
        Assert.Equal("False", (string?)background.Attribute("IsHitTestVisible"));
        Assert.Equal("False", (string?)ribbon.Attribute("IsHitTestVisible"));
        Assert.Equal("0.24", (string?)ribbon.Attribute("Opacity"));
        Assert.Equal("2", (string?)ribbon.Attribute("Grid.RowSpan"));
        Assert.True(ribbon.Descendants().Count(e => e.Name.LocalName == "Path") >= 3);
        var children = background.Parent!.Elements().ToList();
        Assert.True(children.IndexOf(background) < children.IndexOf(ribbon));
        Assert.True(children.IndexOf(ribbon) < children.FindIndex(e =>
            e.Name.LocalName == "ScrollViewer"));
        Assert.Empty(ribbon.Descendants().Where(e => e.Attribute("Click") is not null));
    }

    [Fact]
    public void CompactIconRailDimsAuroraWithoutRemovingCrestOrLinks()
    {
        var doc = Layout();
        var compact = StateSetters(doc, "ShellCompact");
        Assert.Equal("0.12", compact["SidebarAuroraRibbon.Opacity"]);
        Assert.Equal("64", compact["ShellSidebarColumn.Width"]);
        Assert.Equal("Collapsed", compact["SidebarBrand.Visibility"]);
        Assert.Equal("Visible", compact["SidebarCompactBrand.Visibility"]);
        Assert.Equal("6,12", compact["SidebarNavPanel.Padding"]);
        Assert.Contains("Launcher-Northern-Emblem.svg", Named(doc, "SidebarCompactBrand").ToString());
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
    }

    [Fact]
    public void MainWorkspaceRetainsUsableWidthAndIndependentStatusStrip()
    {
        var doc = Layout();
        var container = Named(doc, "WorkspacePageContainer");
        Assert.Equal("24,0,24,20", (string?)container.Attribute("Padding"));
        Assert.Equal("1200", (string?)container.Attribute("MaxWidth"));
        Assert.Equal("12,0,12,16",
            StateSetters(doc, "ShellCompact")["WorkspacePageContainer.Padding"]);
        Assert.Equal("1", (string?)Named(doc, "WorkspaceScroll").Attribute("Grid.Row"));
        Assert.Equal("2", (string?)Named(doc, "ShellStatusBar").Attribute("Grid.Row"));
        Assert.Contains(Named(doc, "StatusFooterDot"), Named(doc, "ShellStatusBar").Descendants());
        Assert.Equal("#728FA8", (string?)Named(doc, "StatusFooterDot").Attribute("Foreground"));
    }

    [Fact]
    public void LongAuroraPagesScrollIndependentlyOfTheStatusBar()
    {
        var doc = Layout();
        var host = Named(doc, "AuroraWorkspaceHost");
        var rows = host.Elements().Single(e => e.Name.LocalName == "Grid.RowDefinitions")
            .Elements().Select(e => (string?)e.Attribute("Height")).ToArray();
        Assert.Equal(new[] { "Auto", "*", "Auto" }, rows);

        var scroll = Named(doc, "WorkspaceScroll");
        Assert.Same(host, scroll.Parent);
        Assert.Equal("1", (string?)scroll.Attribute("Grid.Row"));
        Assert.Equal("DvinholmWorkspaceScroll",
            (string?)scroll.Attribute("AutomationProperties.AutomationId"));
        Assert.Equal("Enabled", (string?)scroll.Attribute("VerticalScrollMode"));
        Assert.Equal("Auto", (string?)scroll.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)scroll.Attribute("HorizontalScrollMode"));
        Assert.Equal("Disabled", (string?)scroll.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("24,0,24,20",
            (string?)Named(doc, "WorkspacePageContainer").Attribute("Padding"));

        foreach (var page in new[] { "CemPinPage", "CemConfigPage", "DtcPage", "EcuPage" })
            Assert.Contains(scroll, Named(doc, page).Ancestors());

        var footer = Named(doc, "ShellStatusBar");
        Assert.Same(host, footer.Parent);
        Assert.Equal("2", (string?)footer.Attribute("Grid.Row"));
        Assert.DoesNotContain(scroll, footer.Ancestors());
        Assert.Equal("310", (string?)Named(doc, "CemConfigResults").Attribute("Height"));
        Assert.Equal("260", (string?)Named(doc, "DtcRecordsView").Attribute("Height"));
        Assert.Equal("260", (string?)Named(doc, "EcusView").Attribute("Height"));
    }

    [Fact]
    public void ConnectionAuditHeadingWrapsInBothLanguagesWithoutClipping()
    {
        var doc = Layout();
        var expander = Named(doc, "J2534ArchitectureAuditExpander");
        Assert.Null(expander.Attribute("Header"));
        Assert.Equal("False", (string?)expander.Attribute("IsExpanded"));
        Assert.Equal("Аудит разрядности драйверов (офлайн)",
            (string?)expander.Attribute("AutomationProperties.Name"));

        var label = Named(doc, "J2534ArchitectureAuditHeaderText");
        Assert.Contains(expander, label.Ancestors());
        Assert.Equal("TextBlock", label.Name.LocalName);
        Assert.Equal("Wrap", (string?)label.Attribute("TextWrapping"));
        Assert.Equal("240", (string?)label.Attribute("MaxWidth"));
        Assert.Equal("Аудит разрядности драйверов (офлайн)",
            (string?)label.Attribute("Text"));

        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("J2534ArchitectureAuditHeaderText.Text = L(", source);
        Assert.Contains("\"Driver architecture audit (offline)\"", source);
        Assert.Contains("J2534ArchitectureAuditExpander, J2534ArchitectureAuditHeaderText.Text", source);
        Assert.DoesNotContain("J2534ArchitectureAuditExpander.Header = L(", source);
    }

    [Fact]
    public void CoreNavigationAndRealDataViewsAreUnaffectedByAtmosphere()
    {
        var doc = Layout();
        var buttons = Named(doc, "SidebarNavPanel").Elements()
            .Where(e => e.Name.LocalName == "Button").ToArray();
        Assert.Equal(6, buttons.Length);
        Assert.Equal(5, buttons.Count(e =>
            (string?)e.Attribute("Click") == "WorkspaceNavigate_Click"));
        Assert.Single(buttons, e =>
            (string?)e.Attribute("Click") == "UnifiedUpdatesNavigate_Click");
        Assert.Contains(Named(doc, "CemConfigResults"), Named(doc, "CemConfigPage").Descendants());
        Assert.Contains(Named(doc, "DtcRecordsView"), Named(doc, "DtcPage").Descendants());
        Assert.Contains(Named(doc, "EcuWorkbenchGrid"), Named(doc, "EcuPage").Descendants());
        Assert.DoesNotContain(doc.Descendants(), e =>
            (string?)e.Attribute(X + "Name") is "SettingsPage" or "LauncherPage");
    }

    [Fact]
    public void DecorativeShellHasNoDiagnosticSideEffectsAndPreservesLocales()
    {
        var doc = Layout();
        var ribbon = Named(doc, "SidebarAuroraRibbon").ToString();
        Assert.DoesNotContain("J2534", ribbon);
        Assert.DoesNotContain("ReadDtc", ribbon);
        Assert.DoesNotContain("ReadCemPin", ribbon);
        Assert.DoesNotContain("IsEnabled", ribbon);
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("ApplyAuroraPrimaryScreenLocalization();", locale);
        Assert.Contains("AutomationProperties.SetName(", locale);
        Assert.DoesNotContain("Connect_Click(", locale);
        var names = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(name => name is not null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }
}
