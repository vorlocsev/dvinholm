using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for 640-DIP Aurora first-fold header readability.
/// These are source/XAML checks, not installed Windows screenshot acceptance.
/// </summary>
public sealed class AuroraTightViewportHeaderTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraTightViewportHeaderTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded MainWindow.xaml is unavailable.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static IReadOnlyDictionary<string, string> Setters(XDocument doc, string state) =>
        Named(doc, state).Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    private static string Source(string relative)
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory);
             folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void VeryNarrowHeaderHasSeparate720DipBreakpoint()
    {
        var doc = Layout();
        var group = Named(doc, "HeaderReadabilityStates");
        var states = group.Elements().Where(e => e.Name.LocalName == "VisualState").ToArray();
        Assert.Equal(new[] { "HeaderFullContext", "HeaderTitleOnly" },
            states.Select(e => (string?)e.Attribute(X + "Name")));
        Assert.Equal("720", (string?)Named(doc, "HeaderFullContext").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.Equal("0", (string?)Named(doc, "HeaderTitleOnly").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
    }

    [Fact]
    public void At640DipOnlyNonessentialBreadcrumbAndDescriptionCollapse()
    {
        var doc = Layout();
        var setters = Setters(doc, "HeaderTitleOnly");
        Assert.Equal(3, setters.Count);
        Assert.Equal("Collapsed", setters["WorkspaceScopeLabel.Visibility"]);
        Assert.Equal("Collapsed", setters["WorkspaceDescription.Visibility"]);
        Assert.Equal("0", setters["WorkspaceHeaderCopy.Spacing"]);
        Assert.DoesNotContain(setters.Keys, k =>
            k.Contains("WorkspaceTitle", StringComparison.Ordinal) ||
            k.Contains("ShellConnection", StringComparison.Ordinal) ||
            k.Contains("UnifiedLanguageSelector", StringComparison.Ordinal));
    }

    [Fact]
    public void Above720DipOriginalDescriptionAndScopeReturnFromDefaults()
    {
        var doc = Layout();
        Assert.Empty(Named(doc, "HeaderFullContext").Descendants()
            .Where(e => e.Name.LocalName == "Setter"));
        Assert.Null(Named(doc, "WorkspaceScopeLabel").Attribute("Visibility"));
        Assert.Null(Named(doc, "WorkspaceDescription").Attribute("Visibility"));
        Assert.Equal("7", (string?)Named(doc, "WorkspaceHeaderCopy").Attribute("Spacing"));
        Assert.Equal("760", (string?)Named(doc, "WorkspaceDescription").Attribute("MaxWidth"));
    }

    [Fact]
    public void TheActualTitleStaysVisibleAndAccessibleAtEveryWidth()
    {
        var doc = Layout();
        var title = Named(doc, "WorkspaceTitle");
        Assert.Equal("DvinholmWorkspaceTitle",
            (string?)title.Attribute("AutomationProperties.AutomationId"));
        Assert.Null(title.Attribute("Visibility"));
        Assert.Contains(Named(doc, "WorkspaceHeaderGrid"), title.Ancestors());
        Assert.Equal("23", Setters(doc, "ShellCompact")["WorkspaceTitle.FontSize"]);
        Assert.Contains("Dvinholm-Banner.png", Named(doc, "AuroraHeaderArtwork").ToString());
    }

    [Fact]
    public void CompactStatusAndLanguageRemainOnTheSameSecondHeaderRow()
    {
        var doc = Layout();
        var compact = Setters(doc, "ShellCompact");
        Assert.Equal("1", compact["WorkspaceHeaderActions.(Grid.Row)"]);
        Assert.Equal("Horizontal", compact["WorkspaceHeaderActions.Orientation"]);
        Assert.Equal("2", compact["WorkspaceHeaderActions.(Grid.ColumnSpan)"]);
        var actions = Named(doc, "WorkspaceHeaderActions");
        Assert.Contains(Named(doc, "ShellConnectionText"), actions.Descendants());
        Assert.Contains(Named(doc, "ShellConnectionDot"), actions.Descendants());
        Assert.Contains(Named(doc, "UnifiedLanguageSelector"), actions.Descendants());
        Assert.Equal("WorkspaceNavigate_Click",
            (string?)Named(doc, "HeaderConnectionStatusButton").Attribute("Click"));
    }

    [Fact]
    public void TheSixPagesKeepActualSafetyAndContentExplanations()
    {
        var doc = Layout();
        foreach (var (page, description) in new[] {
            ("CemPinPage", "CemPinIdentitySectionIntro"),
            ("CemConfigPage", "CemConfigPageIntro"),
            ("DtcPage", "DtcPageIntro"),
            ("EcuPage", "EcuDiscoveryIntro"),
            ("ConnectionPage", "ConnectionPageIntro"),
            ("UnifiedUpdatesPage", "UnifiedVersionNote") })
        {
            Assert.Contains(Named(doc, page), Named(doc, description).Ancestors());
            Assert.NotEqual("Collapsed",
                (string?)Named(doc, description).Attribute("Visibility"));
        }
        Assert.Equal("Disabled",
            (string?)Named(doc, "WorkspaceScroll").Attribute("HorizontalScrollBarVisibility"));
    }

    [Fact]
    public void LanguageSwitchStillRefreshesPageHeaderWithoutVehicleOperations()
    {
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("WorkspaceDescription.Text = _unifiedEnglish ? destination switch", locale);
        Assert.Contains("WorkspaceTitle.Text = _unifiedEnglish ? destination switch", locale);
        Assert.Contains("UpdateUnifiedWorkspaceHeader()", locale);
        Assert.Contains("ApplyAuroraPrimaryScreenLocalization();", locale);
        Assert.DoesNotContain("Connect_Click(", locale);
        Assert.DoesNotContain("ReadDtc_Click(", locale);
        Assert.DoesNotContain("ReadCemPin_Click(", locale);
    }

    [Fact]
    public void TitleOnlyLayoutCannotUnlockCemOrFaultErasureOrSettings()
    {
        var doc = Layout();
        var state = Named(doc, "HeaderTitleOnly").ToString();
        Assert.DoesNotContain("Connect_Click", state);
        Assert.DoesNotContain("ReadCemPin", state);
        Assert.DoesNotContain("ReadDtc", state);
        foreach (var id in new[] { "CemConfigReadButton", "CemConfigBackupButton",
            "CemConfigCompareButton", "CemConfigPrepareButton", "CemConfigWriteButton",
            "DtcClearButton" })
        {
            var button = Named(doc, id);
            Assert.Equal("False", (string?)button.Attribute("IsEnabled"));
            Assert.Null(button.Attribute("Click"));
        }
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
    }
}
