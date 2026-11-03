using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline Aurora wide-header layout and safety contracts.
/// Visual states are source assertions, not a native pixel screenshot pass.
/// </summary>
public sealed class AuroraWideHeaderActionLayoutTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraWideHeaderActionLayoutTests).Assembly
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
    public void TrueWideHeaderHasIndependent1280DipActionBreakpoint()
    {
        var doc = Layout();
        var group = Named(doc, "HeaderActionDensityStates");
        Assert.Equal(new[] { "HeaderActionsInline", "HeaderActionsStacked" },
            group.Elements().Where(e => e.Name.LocalName == "VisualState")
                .Select(e => (string?)e.Attribute(X + "Name")));
        Assert.Equal("1280", (string?)Named(doc, "HeaderActionsInline").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.Equal("0", (string?)Named(doc, "HeaderActionsStacked").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.Equal("980", (string?)Named(doc, "ShellWide").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
    }

    [Fact]
    public void WideActionRowIsHorizontalWithJustTwoPresentationSetters()
    {
        var doc = Layout();
        var setters = Setters(doc, "HeaderActionsInline");
        Assert.Equal(2, setters.Count);
        Assert.Equal("Horizontal", setters["WorkspaceHeaderActions.Orientation"]);
        Assert.Equal("8", setters["WorkspaceHeaderActions.Spacing"]);
        Assert.DoesNotContain(setters.Keys, s => s.Contains("Visibility", StringComparison.Ordinal)
            || s.Contains("IsEnabled", StringComparison.Ordinal)
            || s.Contains("Tag", StringComparison.Ordinal));
        Assert.Empty(Setters(doc, "HeaderActionsStacked"));
    }

    [Fact]
    public void At980To1279ActionsRemainNarrowVerticalForLongPageTitles()
    {
        var doc = Layout();
        var actions = Named(doc, "WorkspaceHeaderActions");
        Assert.Null(actions.Attribute("Orientation"));
        Assert.Equal("1", (string?)actions.Attribute("Grid.Column"));
        Assert.Null(actions.Attribute("Grid.Row"));
        Assert.Equal("10", (string?)actions.Attribute("Spacing"));
        Assert.Equal("0,12,16,10", (string?)actions.Attribute("Margin"));
        Assert.Empty(Setters(doc, "ShellWide"));
        Assert.Equal("2", (string?)Named(doc, "WorkspaceHeaderGrid")
            .Descendants().Single(e => e.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements().Count().ToString());
    }

    [Fact]
    public void Under980CompactActionsKeepTheirOwnSecondHorizontalRow()
    {
        var doc = Layout();
        var compact = Setters(doc, "ShellCompact");
        Assert.Equal("1", compact["WorkspaceHeaderActions.(Grid.Row)"]);
        Assert.Equal("0", compact["WorkspaceHeaderActions.(Grid.Column)"]);
        Assert.Equal("2", compact["WorkspaceHeaderActions.(Grid.ColumnSpan)"]);
        Assert.Equal("Horizontal", compact["WorkspaceHeaderActions.Orientation"]);
        Assert.Equal("8", compact["WorkspaceHeaderActions.Spacing"]);
        Assert.Equal("Left", compact["WorkspaceHeaderActions.HorizontalAlignment"]);
        Assert.Equal("64", compact["ShellSidebarColumn.Width"]);
    }

    [Fact]
    public void J2534ChipIsAlignedWithComboBoxAndNeverConnectsOnClick()
    {
        var doc = Layout();
        var status = Named(doc, "HeaderConnectionStatusButton");
        Assert.Equal("Button", status.Name.LocalName);
        Assert.Equal("Bottom", (string?)status.Attribute("VerticalAlignment"));
        Assert.Equal("WorkspaceNavigate_Click", (string?)status.Attribute("Click"));
        Assert.Equal("J2534Connection", (string?)status.Attribute("Tag"));
        Assert.Equal("DvinholmHeaderConnectionStatus",
            (string?)status.Attribute("AutomationProperties.AutomationId"));
        Assert.Contains(Named(doc, "ShellConnectionDot"), status.Descendants());
        Assert.Contains(Named(doc, "ShellConnectionText"), status.Descendants());
        Assert.DoesNotContain("Connect_Click", status.ToString());
    }

    [Fact]
    public void ExactRuEnChoiceAndSafeCancelRemainInActionRow()
    {
        var doc = Layout();
        var actions = Named(doc, "WorkspaceHeaderActions");
        var children = actions.Elements().ToArray();
        Assert.Equal(new[] { "HeaderConnectionStatusButton",
            "UnifiedLanguageSelector", "CancelUdsButton" },
            children.Select(c => (string?)c.Attribute(X + "Name")));
        var locale = Named(doc, "UnifiedLanguageSelector");
        Assert.Equal("DvinholmLanguageSelector",
            (string?)locale.Attribute("AutomationProperties.AutomationId"));
        Assert.Equal(new[] { "ru", "en" },
            locale.Elements().Select(e => (string?)e.Attribute("Tag")));
        var cancel = Named(doc, "CancelUdsButton");
        Assert.Equal("Collapsed", (string?)cancel.Attribute("Visibility"));
        Assert.Equal("False", (string?)cancel.Attribute("IsEnabled"));
        Assert.Equal("CancelUds_Click", (string?)cancel.Attribute("Click"));
    }

    [Fact]
    public void BannerImageAndRealPrimaryPageTitleRemainAboveContent()
    {
        var doc = Layout();
        var header = Named(doc, "WorkspaceHeaderGrid");
        Assert.Contains(Named(doc, "AuroraHeaderArtwork"), header.Descendants());
        Assert.Equal("2", (string?)Named(doc, "AuroraHeaderArtwork").Attribute("Grid.RowSpan"));
        Assert.Contains("Dvinholm-Banner.png", header.ToString());
        Assert.Equal("DvinholmWorkspaceTitle",
            (string?)Named(doc, "WorkspaceTitle")
                .Attribute("AutomationProperties.AutomationId"));
        Assert.Null(Named(doc, "WorkspaceTitle").Attribute("Visibility"));
        Assert.Equal("1", (string?)Named(doc, "WorkspaceScroll").Attribute("Grid.Row"));
        Assert.Equal("2", (string?)Named(doc, "ShellStatusBar").Attribute("Grid.Row"));
    }

    [Fact]
    public void HeaderActionLayoutCannotAlterVehicleOrUpdateOperations()
    {
        var doc = Layout();
        var layout = Named(doc, "HeaderActionDensityStates").ToString();
        Assert.DoesNotContain("PassThru", layout);
        Assert.DoesNotContain("Connect_Click", layout);
        Assert.DoesNotContain("ReadDtc", layout);
        Assert.DoesNotContain("ReadCemPin", layout);
        Assert.DoesNotContain("IsEnabled", layout);
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        foreach (var id in new[] { "CemConfigReadButton", "CemConfigWriteButton",
            "DtcClearButton" })
        {
            var button = Named(doc, id);
            Assert.Equal("False", (string?)button.Attribute("IsEnabled"));
            Assert.Null(button.Attribute("Click"));
        }
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("ApplyAuroraPrimaryScreenLocalization();", locale);
        Assert.DoesNotContain("Connect_Click(", locale);
    }
}
