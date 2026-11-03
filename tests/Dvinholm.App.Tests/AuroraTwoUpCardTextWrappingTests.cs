using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for RU/EN captions in 980..1279 DIP two-up
/// Connection and Updates cards. Native UI pixels require separate acceptance.
/// </summary>
public sealed class AuroraTwoUpCardTextWrappingTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraTwoUpCardTextWrappingTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded MainWindow.xaml unavailable.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static IReadOnlyDictionary<string, string> Setters(XDocument document, string state) =>
        Named(document, state).Descendants().Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    private static string FileText(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void InstalledVersionTitleNumberAndExplanationWrapWithoutTruncating()
    {
        var doc = Layout();
        var pane = Named(doc, "UpdatesVersionPane");
        foreach (var id in new[] {
            "UnifiedCurrentVersionTitle", "UnifiedCurrentVersion", "UnifiedVersionNote" })
        {
            var label = Named(doc, id);
            Assert.Contains(pane, label.Ancestors());
            Assert.Equal("Wrap", (string?)label.Attribute("TextWrapping"));
            Assert.Null(label.Attribute("MaxLines"));
        }
        Assert.Equal("27", (string?)Named(doc, "UnifiedCurrentVersion").Attribute("FontSize"));
    }

    [Fact]
    public void ChannelHeadingAndPersistedChannelExplanationWrap()
    {
        var doc = Layout();
        var pane = Named(doc, "UpdatesChannelPane");
        foreach (var id in new[] { "UnifiedChannelTitle", "UnifiedChannelNote" })
        {
            var label = Named(doc, id);
            Assert.Contains(pane, label.Ancestors());
            Assert.Equal("Wrap", (string?)label.Attribute("TextWrapping"));
            Assert.Null(label.Attribute("MaxLines"));
        }
        Assert.Contains(Named(doc, "UnifiedUpdateChannel"), pane.Descendants());
        Assert.Equal("UnifiedUpdateChannel_SelectionChanged",
            (string?)Named(doc, "UnifiedUpdateChannel").Attribute("SelectionChanged"));
    }

    [Fact]
    public void UpdateActionHeadingWrapsWhileSafetyDetailsRemainComplete()
    {
        var doc = Layout();
        var pane = Named(doc, "UpdatesActionPane");
        Assert.Contains(pane, Named(doc, "UnifiedUpdateSafetyTitle").Ancestors());
        foreach (var id in new[] {
            "UnifiedUpdateSafetyTitle", "UnifiedUpdateStatus", "UnifiedUpdateSafety" })
        {
            var label = Named(doc, id);
            Assert.Equal("Wrap", (string?)label.Attribute("TextWrapping"));
            Assert.Null(label.Attribute("MaxLines"));
        }
        Assert.Equal("4", Named(doc, "UnifiedUpdateActionList").Elements()
            .Count(e => e.Name.LocalName == "Button").ToString());
    }

    [Fact]
    public void ConnectionAdapterAndCanCardHeadingsWrapWithoutLosingRealControls()
    {
        var doc = Layout();
        Assert.Equal("Wrap",
            (string?)Named(doc, "ConnectionPageHeading").Attribute("TextWrapping"));
        Assert.Equal("Wrap",
            (string?)Named(doc, "ConnectionBusHeading").Attribute("TextWrapping"));
        Assert.Contains(Named(doc, "ConnectionAdapterPane"),
            Named(doc, "ConnectionPageHeading").Ancestors());
        Assert.Contains(Named(doc, "ConnectionBusPane"),
            Named(doc, "ConnectionBusHeading").Ancestors());
        Assert.Contains(Named(doc, "J2534Devices"),
            Named(doc, "ConnectionAdapterPane").Descendants());
        Assert.Contains(Named(doc, "BaudRate"),
            Named(doc, "ConnectionBusPane").Descendants());
    }

    [Fact]
    public void ConnectionStatusHeadingAndEvidenceWrapButNeverFakeAnOnlineSession()
    {
        var doc = Layout();
        var status = Named(doc, "ConnectionStatusPane");
        foreach (var id in new[] {
            "ConnectionDriverHeading", "ConnectionDriverIntro", "AdapterMetadataText" })
        {
            var label = Named(doc, id);
            Assert.Contains(status, label.Ancestors());
            Assert.Equal("Wrap", (string?)label.Attribute("TextWrapping"));
        }
        Assert.Equal("{Binding Text, ElementName=ShellConnectionText}",
            (string?)Named(doc, "ConnectionSessionState").Attribute("Text"));
        Assert.Equal("Connect_Click", (string?)Named(doc, "ConnectButton").Attribute("Click"));
        Assert.Equal("#728FA8", (string?)Named(doc, "ConnectionSessionState").Attribute("Foreground"));
    }

    [Fact]
    public void WrappingPreservesThreeResponsiveBreakpointsAndTwoUpSpans()
    {
        var doc = Layout();
        foreach (var (prefix, pane) in new[] {
            ("Connection", "ConnectionStatusPane"), ("Updates", "UpdatesActionPane") })
        {
            Assert.Equal("1280", (string?)Named(doc, prefix + "ThreeColumns")
                .Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
                .Attribute("MinWindowWidth"));
            Assert.Equal("980", (string?)Named(doc, prefix + "TwoColumns")
                .Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
                .Attribute("MinWindowWidth"));
            Assert.Equal("0", (string?)Named(doc, prefix + "VerticalPanels")
                .Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
                .Attribute("MinWindowWidth"));
            var two = Setters(doc, prefix + "TwoColumns");
            Assert.Equal("2", two[pane + ".(Grid.ColumnSpan)"]);
            Assert.Equal("1", two[pane + ".(Grid.Row)"]);
        }
        Assert.Equal("Disabled", (string?)Named(doc, "WorkspaceScroll")
            .Attribute("HorizontalScrollBarVisibility"));
    }

    [Fact]
    public void ActualRuEnProjectionStillSourcesStatusAndVersionFromRealState()
    {
        var source = FileText("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("UnifiedCurrentVersion.Text = LatestBuildUpdater.CurrentDisplayVersion;", source);
        Assert.Contains("UnifiedCurrentVersionTitle.Text = L(", source);
        Assert.Contains("UnifiedVersionNote.Text = L(", source);
        Assert.Contains("UnifiedChannelTitle.Text = L(", source);
        Assert.Contains("UnifiedChannelNote.Text = L(", source);
        Assert.Contains("UnifiedUpdateSafetyTitle.Text = L(", source);
        var locale = FileText("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.Contains("ConnectionPageHeading.Text = L(", locale);
        Assert.Contains("ConnectionBusHeading.Text = L(", locale);
        Assert.Contains("ConnectionDriverHeading.Text = L(", locale);
    }

    [Fact]
    public void LegibilityChangesCannotEnableUnsafeUpdatesOrWriteActions()
    {
        var doc = Layout();
        foreach (var id in new[] {
            "UnifiedStageUpdateButton", "UnifiedCancelStageButton",
            "UnifiedPreflightButton", "CemConfigWriteButton", "DtcClearButton" })
        {
            var button = Named(doc, id);
            Assert.Equal("False", (string?)button.Attribute("IsEnabled"));
        }
        Assert.Equal("UnifiedCheckUpdate_Click",
            (string?)Named(doc, "UnifiedCheckUpdateButton").Attribute("Click"));
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.DoesNotContain(doc.Descendants(), e =>
            (string?)e.Attribute(X + "Name") is "SettingsPage" or "LauncherPage");
        var names = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(s => s is not null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }
}
