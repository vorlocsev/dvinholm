using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline layout contracts for the actual Aurora WinUI compact header.
/// They are not pixel tests or proof that a Windows desktop has been rendered.
/// </summary>
public sealed class AuroraCompactHeaderParityTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraCompactHeaderParityTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded MainWindow.xaml.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument xml, string id) =>
        xml.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);

    private static IReadOnlyDictionary<string, string> Setters(XDocument xml, string state) =>
        Named(xml, state).Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void ShellBreakpointPreservesWideNavigationAndCompactIconRail()
    {
        var xml = Layout();
        var wide = Named(xml, "ShellWide");
        var trigger = wide.Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger");
        Assert.Equal("980", (string?)trigger.Attribute("MinWindowWidth"));
        var compact = Setters(xml, "ShellCompact");
        Assert.Equal("64", compact["ShellSidebarColumn.Width"]);
        Assert.Equal("Collapsed", compact["SidebarBrand.Visibility"]);
        Assert.Equal("Visible", compact["SidebarCompactBrand.Visibility"]);
        Assert.Equal("6,12", compact["SidebarNavPanel.Padding"]);
    }

    [Fact]
    public void CompactHeaderMovesActionControlsToOneHorizontalLowerRow()
    {
        var xml = Layout();
        var compact = Setters(xml, "ShellCompact");
        Assert.Equal("1", compact["WorkspaceHeaderActions.(Grid.Row)"]);
        Assert.Equal("0", compact["WorkspaceHeaderActions.(Grid.Column)"]);
        Assert.Equal("2", compact["WorkspaceHeaderActions.(Grid.ColumnSpan)"]);
        Assert.Equal("Horizontal", compact["WorkspaceHeaderActions.Orientation"]);
        Assert.Equal("Left", compact["WorkspaceHeaderActions.HorizontalAlignment"]);
        Assert.Equal("16,0,16,10", compact["WorkspaceHeaderActions.Margin"]);
        Assert.Equal("8", compact["WorkspaceHeaderActions.Spacing"]);
        Assert.Equal("16,10,16,0", compact["WorkspaceHeaderCopy.Margin"]);
    }

    [Fact]
    public void WideHeaderKeepsActualBannerAndOriginalActionsInRightColumn()
    {
        var xml = Layout();
        var header = Named(xml, "WorkspaceHeaderGrid");
        Assert.Equal("28,16,28,16", (string?)header.Attribute("Margin"));
        Assert.Equal("150", (string?)header.Attribute("MinHeight"));
        Assert.Equal("1", (string?)Named(xml, "WorkspaceHeaderActions").Attribute("Grid.Column"));
        Assert.Null(Named(xml, "WorkspaceHeaderActions").Attribute("Grid.Row"));
        Assert.Equal("2", (string?)Named(xml, "AuroraHeaderArtwork").Attribute("Grid.RowSpan"));
        Assert.Contains("ms-appx:///Assets/Dvinholm-Banner.png",
            Named(xml, "AuroraHeaderArtwork").ToString());
    }

    [Fact]
    public void CompactHeaderDoesNotHideTextBrandOrRealSessionState()
    {
        var xml = Layout();
        var copy = Named(xml, "WorkspaceHeaderCopy");
        foreach (var name in new[] { "WorkspaceScopeLabel", "WorkspaceTitle",
            "WorkspaceDescription" })
            Assert.Contains(Named(xml, name), copy.Descendants());
        Assert.Contains(Named(xml, "ShellConnectionText"),
            Named(xml, "WorkspaceHeaderActions").Descendants());
        var status = Named(xml, "ShellConnectionText");
        Assert.Equal("Не подключено", (string?)status.Attribute("Text"));
        Assert.Equal("23", Setters(xml, "ShellCompact")["WorkspaceTitle.FontSize"]);
    }

    [Fact]
    public void LanguageButtonAndStatusRemainExistingNonDestructiveRoutes()
    {
        var xml = Layout();
        var action = Named(xml, "WorkspaceHeaderActions");
        var controls = action.Elements().Where(e =>
            e.Name.LocalName is "Button" or "ComboBox").ToArray();
        Assert.Equal(3, controls.Length);
        var connection = controls.Single(e => (string?)e.Attribute("Tag") == "J2534Connection");
        Assert.Equal("WorkspaceNavigate_Click", (string?)connection.Attribute("Click"));
        var selector = Named(xml, "UnifiedLanguageSelector");
        Assert.Equal("UnifiedLanguageSelector_SelectionChanged",
            (string?)selector.Attribute("SelectionChanged"));
        Assert.Equal(new[] { "ru", "en" }, selector.Elements()
            .Select(e => (string?)e.Attribute("Tag")));
        var cancel = Named(xml, "CancelUdsButton");
        Assert.Equal("Collapsed", (string?)cancel.Attribute("Visibility"));
        Assert.Equal("False", (string?)cancel.Attribute("IsEnabled"));
        Assert.Equal("CancelUds_Click", (string?)cancel.Attribute("Click"));
    }

    [Fact]
    public void PageScrollingAndStatusStripRemainIndependentOfHeader()
    {
        var xml = Layout();
        var host = Named(xml, "AuroraWorkspaceHost");
        Assert.Contains(Named(xml, "WorkspaceScroll"), host.Descendants());
        Assert.Equal("1", (string?)Named(xml, "WorkspaceScroll").Attribute("Grid.Row"));
        Assert.Equal("2", (string?)Named(xml, "ShellStatusBar").Attribute("Grid.Row"));
        Assert.Equal("12,0,12,16",
            Setters(xml, "ShellCompact")["WorkspacePageContainer.Padding"]);
        Assert.Contains(Named(xml, "ShellStatusText"),
            Named(xml, "ShellStatusBar").Descendants());
    }

    [Fact]
    public void HeaderRemainsVisualOnlyAndLocaleSwitchKeepsActualStates()
    {
        var xml = Layout();
        var header = Named(xml, "WorkspaceHeaderGrid").ToString();
        Assert.DoesNotContain("ReadDtc_Click", header);
        Assert.DoesNotContain("Connect_Click", header);
        Assert.DoesNotContain("ReadCemPin", header);
        Assert.DoesNotContain("PassThru", header);
        var source = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("ApplyAuroraPrimaryScreenLocalization();", source);
        Assert.Contains("_connected && _session.IsConnected", source);
        Assert.Contains("SetPrimaryNavigationLabel(", source);
    }

    [Fact]
    public void ShellStillHasSixDestinationsWithoutNewLauncherOrSettings()
    {
        var xml = Layout();
        var sidebar = Named(xml, "SidebarNavPanel");
        Assert.Equal(6, sidebar.Elements().Count(e => e.Name.LocalName == "Button"));
        Assert.DoesNotContain(xml.Descendants(), e =>
            (string?)e.Attribute(X + "Name") is "SettingsPage" or "LauncherPage");
        var all = xml.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(x => x is not null).ToArray();
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
    }
}
