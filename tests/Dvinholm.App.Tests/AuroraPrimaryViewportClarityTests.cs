using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline layout checks for a cleaner six-page Aurora first fold.
/// These test actual XAML, not installed Windows screenshots or live ECU data.
/// </summary>
public sealed class AuroraPrimaryViewportClarityTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraPrimaryViewportClarityTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml resource missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string id) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);

    private static IReadOnlyDictionary<string, string> CompactSetters(XDocument doc) =>
        Named(doc, "ShellCompact").Descendants()
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
            if (File.Exists(path)) return File.ReadAllText(path);
        }

        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void PrimaryPageTitleRemainsInVisibleGlobalHeader()
    {
        var doc = Layout();
        var global = Named(doc, "WorkspaceTitle");
        Assert.Contains(Named(doc, "WorkspaceHeaderGrid"), global.Ancestors());
        Assert.Null(global.Attribute("Visibility"));
        Assert.Equal("DvinholmWorkspaceTitle",
            (string?)global.Attribute("AutomationProperties.AutomationId"));
        Assert.Contains("ms-appx:///Assets/Dvinholm-Banner.png",
            Named(doc, "AuroraHeaderArtwork").ToString());
    }

    [Fact]
    public void CemConfigurationSuppressesDuplicatedLargeTitleNotTheContent()
    {
        var doc = Layout();
        var page = Named(doc, "CemConfigPage");
        var repeated = Named(doc, "CemConfigPageTitle");
        Assert.Equal("Collapsed", (string?)repeated.Attribute("Visibility"));
        Assert.Contains(page, repeated.Ancestors());
        foreach (var id in new[] { "CemConfigPageIntro", "CemIdentityEvidenceConfigText",
            "CemReferenceGrid", "CemConfigActionRail", "CemConfigReadOnlyNotice" })
        {
            var panel = Named(doc, id);
            Assert.Contains(page, panel.Ancestors());
            Assert.NotEqual("Collapsed", (string?)panel.Attribute("Visibility"));
        }
    }

    [Fact]
    public void DtcSuppressesDuplicatedLargeTitleButKeepsTruthfulInstruction()
    {
        var doc = Layout();
        var page = Named(doc, "DtcPage");
        Assert.Equal("Collapsed", (string?)Named(doc, "DtcPageHeading").Attribute("Visibility"));
        var intro = Named(doc, "DtcPageIntro");
        Assert.Contains(page, intro.Ancestors());
        Assert.Null(intro.Attribute("Visibility"));
        Assert.Contains("TX/RX", (string?)intro.Attribute("Text"));
        Assert.Contains(Named(doc, "DtcActionRail"), page.Descendants());
        Assert.Contains(Named(doc, "DtcWorkbenchGrid"), page.Descendants());
    }

    [Fact]
    public void DeveloperFrameworkFooterDoesNotCompeteWithUserNavigation()
    {
        var doc = Layout();
        var footer = Named(doc, "SidebarFooter");
        Assert.Equal("Collapsed", (string?)footer.Attribute("Visibility"));
        Assert.Contains("WINUI 3", footer.ToString());
        Assert.Equal("1", (string?)footer.Attribute("Grid.Row"));
        Assert.Contains(Named(doc, "SidebarNavPanel"), footer.Parent!.Descendants());
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.Null(Named(doc, "SidebarNavPanel").Attribute("Visibility"));
    }

    [Fact]
    public void NarrowShellHidesRedundantVersionButKeepsStatusText()
    {
        var doc = Layout();
        var compact = CompactSetters(doc);
        Assert.Equal("Collapsed", compact["StatusFooterVersion.Visibility"]);
        Assert.Equal("12,10", compact["ShellStatusBar.Padding"]);
        var version = Named(doc, "StatusFooterVersion");
        Assert.Contains(Named(doc, "ShellStatusBar"), version.Ancestors());
        Assert.Null(version.Attribute("Visibility"));
        Assert.Null(Named(doc, "ShellStatusText").Attribute("Visibility"));
        Assert.Equal("1", (string?)Named(doc, "ShellStatusText").Attribute("Grid.Column"));
    }

    [Fact]
    public void WideShellShowsVersionAndRealStatusOnSeparateColumns()
    {
        var doc = Layout();
        Assert.Equal("980", (string?)Named(doc, "ShellWide").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        var version = Named(doc, "StatusFooterVersion");
        Assert.Equal("2", (string?)version.Attribute("Grid.Column"));
        Assert.Null(version.Attribute("Visibility"));
        Assert.Equal("2", (string?)Named(doc, "ShellStatusBar").Attribute("Grid.Row"));
        Assert.Equal("#728FA8", (string?)Named(doc, "StatusFooterDot")
            .Attribute("Foreground"));
    }

    [Fact]
    public void LocalizationStillUpdatesHiddenNamedLabelsWithoutChangingSession()
    {
        var source = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.Contains("DtcPageHeading.Text = L(", source);
        var reference = Source("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        Assert.Contains("CemConfigPageTitle.Text = L(", reference);
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("ApplyAuroraPrimaryScreenLocalization();", locale);
        Assert.Contains("ApplyAuroraCemConfigLocalization();", locale);
        Assert.Contains("_connected && _session.IsConnected", locale);
        Assert.DoesNotContain("Connect_Click(", locale);
    }

    [Fact]
    public void VisualClarityChangesDoNotEnablePinConfigOrFaultErasure()
    {
        var doc = Layout();
        foreach (var id in new[] { "CemConfigReadButton", "CemConfigBackupButton",
            "CemConfigCompareButton", "CemConfigPrepareButton",
            "CemConfigWriteButton", "DtcClearButton" })
        {
            var control = Named(doc, id);
            Assert.Equal("False", (string?)control.Attribute("IsEnabled"));
            Assert.Null(control.Attribute("Click"));
        }

        Assert.Equal("Collapsed", (string?)Named(doc, "CemPinSearchCard")
            .Attribute("Visibility"));
        Assert.DoesNotContain(doc.Descendants(), e =>
            (string?)e.Attribute(X + "Name") is "SettingsPage" or "LauncherPage");
        var names = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(x => x is not null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    // Stage 66: four page-rhythm cases and four duplicate-route/safety cases.
    // These are XAML contracts, not native screenshot or hardware acceptance.
    [Theory]
    [InlineData("CemPinPage")]
    [InlineData("CemConfigPage")]
    [InlineData("DtcPage")]
    [InlineData("EcuPage")]
    public void PrimaryAuroraPagesAvoidCompoundedCardGutters(string pageName)
    {
        var doc = Layout();
        var page = Named(doc, pageName);
        Assert.Equal("0", (string?)page.Attribute("Spacing"));
        Assert.Equal("Collapsed", (string?)page.Attribute("Visibility"));
        Assert.Contains(page.Descendants(), element =>
            element.Name.LocalName == "Border" &&
            (string?)element.Attribute("Style") == "{StaticResource AuroraCard}");
        var style = doc.Descendants().Single(element =>
            element.Name.LocalName == "Style" &&
            (string?)element.Attribute(X + "Key") == "AuroraCard");
        Assert.Equal("0,0,0,12", (string?)style.Descendants()
            .Single(element => (string?)element.Attribute("Property") == "Margin")
            .Attribute("Value"));
    }

    [Fact]
    public void EcuDoesNotRenderSecondProfilesButtonBelowItsActionCard()
    {
        var doc = Layout();
        var redundant = Named(doc, "EcuOpenProfilesButton");
        Assert.Equal("Collapsed", (string?)redundant.Attribute("Visibility"));
        Assert.Contains(Named(doc, "EcuPage"), redundant.Ancestors());
        Assert.DoesNotContain(Named(doc, "EcuToolsFooter"), redundant.Ancestors());
        Assert.Equal("EcuProfiles", (string?)redundant.Attribute("Tag"));
        Assert.Equal("WorkspaceNavigate_Click", (string?)redundant.Attribute("Click"));
    }

    [Fact]
    public void ProfilesRemainAccessibleThroughVisibleEcuActionRail()
    {
        var doc = Layout();
        var footer = Named(doc, "EcuToolsFooter");
        var profiles = Named(doc, "EcuProfilesNavigateButton");
        Assert.Contains(footer, profiles.Ancestors());
        Assert.Null(profiles.Attribute("Visibility"));
        Assert.Equal("EcuProfiles", (string?)profiles.Attribute("Tag"));
        Assert.Equal("WorkspaceNavigate_Click", (string?)profiles.Attribute("Click"));
        Assert.Equal(3, Named(doc, "EcuToolsGrid").Elements()
            .Count(element => element.Name.LocalName == "Button"));
    }

    [Fact]
    public void HiddenDuplicateCannotDisruptRuEnPresentationProjection()
    {
        var source = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.Contains("EcuOpenProfilesButton.Content = L(", source);
        Assert.Contains("EcuProfilesNavigateButton.Content = L(", source);
        Assert.DoesNotContain("Connect_Click(", source);
        Assert.DoesNotContain("ReadDtc_Click(", source);
        Assert.DoesNotContain("ReadCemPin_Click(", source);
    }

    [Fact]
    public void VisualRhythmDoesNotActivatePinWriteOrDtcEraseOperations()
    {
        var doc = Layout();
        foreach (var id in new[] { "CemConfigReadButton", "CemConfigWriteButton",
            "DtcClearButton" })
        {
            var button = Named(doc, id);
            Assert.Equal("False", (string?)button.Attribute("IsEnabled"));
            Assert.Null(button.Attribute("Click"));
        }
        Assert.Equal("Collapsed",
            (string?)Named(doc, "CemPinSearchCard").Attribute("Visibility"));
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(element => element.Name.LocalName == "Button"));
        Assert.DoesNotContain(doc.Descendants(), element =>
            (string?)element.Attribute(X + "Name") is "SettingsPage" or "LauncherPage");
    }

    // Stage 74: eight source-based monitor toolbar/readability checks.
    // Not a native screenshot or a real CAN/J2534 integration test.
    [Theory]
    [InlineData("CanMonitorClearButton", "Clear_Click", "1", "0")]
    [InlineData("HardwareFilterButton", "HardwareFilter_Click", "2", "1")]
    [InlineData("CanMonitorCsvButton", "Csv_Click", "3", "2")]
    public void CanMonitorToolbarCommandsRetainHandlersAndWrap(
        string id, string handler, string wideColumn, string compactColumn)
    {
        var doc = Layout();
        var button = Named(doc, id);
        Assert.Contains(Named(doc, "CanMonitorFilterToolbar"), button.Ancestors());
        Assert.Equal(handler, (string?)button.Attribute("Click"));
        Assert.Equal(wideColumn, (string?)button.Attribute("Grid.Column"));
        Assert.Equal("Stretch", (string?)button.Attribute("HorizontalAlignment"));
        Assert.Equal("{StaticResource AuroraWrapCaption}",
            (string?)button.Attribute("ContentTemplate"));
        var setters = Named(doc, "CanMonitorToolbarCompact").Descendants()
            .Where(element => element.Name.LocalName == "Setter")
            .ToDictionary(element => (string)element.Attribute("Target")!,
                element => (string)element.Attribute("Value")!);
        Assert.Equal(compactColumn, setters[id + ".(Grid.Column)"]);
        Assert.Equal("1", setters[id + ".(Grid.Row)"]);
    }

    [Fact]
    public void MonitorToolbarHas980DipBreakpointAndReturnsToWideDefaults()
    {
        var doc = Layout();
        Assert.Equal("980", (string?)Named(doc, "CanMonitorToolbarWide").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.Equal("0", (string?)Named(doc, "CanMonitorToolbarCompact").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.DoesNotContain(Named(doc, "CanMonitorToolbarWide").Descendants(),
            element => element.Name.LocalName == "Setter");
        var toolbar = Named(doc, "CanMonitorFilterToolbar");
        Assert.Contains(Named(doc, "CanMonitorPage"), toolbar.Ancestors());
        Assert.Equal("10", (string?)toolbar.Attribute("ColumnSpacing"));
        Assert.Equal(2, toolbar.Elements()
            .Single(e => e.Name.LocalName == "Grid.RowDefinitions")
            .Elements().Count());
    }

    [Fact]
    public void CompactMonitorFilterSpansThreeEqualLiveColumns()
    {
        var doc = Layout();
        var grid = Named(doc, "CanMonitorFilterToolbar");
        var widths = grid.Elements().Single(e =>
            e.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements().Select(e => (string?)e.Attribute("Width")).ToArray();
        Assert.Equal(new[] { "2*", "*", "*", "*" }, widths);
        var setters = Named(doc, "CanMonitorToolbarCompact").Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!);
        Assert.Equal("0", setters["CanMonitorCsvColumn.Width"]);
        Assert.Equal("*", setters["CanMonitorFilterColumn.Width"]);
        Assert.Equal("*", setters["CanMonitorClearColumn.Width"]);
        Assert.Equal("*", setters["CanMonitorHardwareColumn.Width"]);
        Assert.Equal("3", setters["IdFilter.(Grid.ColumnSpan)"]);
        Assert.Equal("Filter_Changed", (string?)Named(doc, "IdFilter")
            .Attribute("TextChanged"));
    }

    [Fact]
    public void OfflineMonitorActionsStayInTwoBoundedWrappingCells()
    {
        var doc = Layout();
        var grid = Named(doc, "CanMonitorReviewActions");
        Assert.Equal(new[] { "*", "*" }, grid.Elements()
            .Single(e => e.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements().Select(e => (string?)e.Attribute("Width")));
        foreach (var (id, column, handler) in new[] {
            ("CanRxSummaryButton", "0", "CanRxSummary_Click"),
            ("CanTraceInspectButton", "1", "InspectCanCsv_Click") })
        {
            var button = Named(doc, id);
            Assert.Contains(grid, button.Ancestors());
            Assert.Equal(column, (string?)button.Attribute("Grid.Column"));
            Assert.Equal(handler, (string?)button.Attribute("Click"));
            Assert.Equal("Stretch", (string?)button.Attribute("HorizontalAlignment"));
            Assert.Equal("{StaticResource AuroraWrapCaption}",
                (string?)button.Attribute("ContentTemplate"));
        }
    }

    [Fact]
    public void MonitorCountersAreThreeEqualWrappingReadOnlyColumns()
    {
        var doc = Layout();
        var grid = Named(doc, "CanMonitorCounters");
        Assert.Equal(new[] { "*", "*", "*" }, grid.Elements()
            .Single(e => e.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements().Select(e => (string?)e.Attribute("Width")));
        foreach (var (id, column) in new[] {
            ("FrameRateText", "0"), ("FrameCountText", "1"),
            ("DroppedFrameText", "2") })
        {
            var counter = Named(doc, id);
            Assert.Contains(grid, counter.Ancestors());
            Assert.Equal(column, (string?)counter.Attribute("Grid.Column"));
            Assert.Equal("Wrap", (string?)counter.Attribute("TextWrapping"));
        }
    }

    [Fact]
    public void MonitorReflowKeepsRealLocalizedActionsAndOfflineGate()
    {
        var doc = Layout();
        var locale = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        foreach (var key in new[] { "CanMonitorClearButton", "HardwareFilterButton",
            "CanMonitorCsvButton", "CanRxSummaryButton", "CanTraceInspectButton" })
            Assert.Contains(key + ".Content =", locale);
        Assert.Equal("False", (string?)Named(doc, "HardwareFilterButton")
            .Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Named(doc, "StartButton")
            .Attribute("IsEnabled"));
        Assert.Equal("Disabled", (string?)Named(doc, "WorkspaceScroll")
            .Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.Null(Named(doc, "CanMonitorCsvButton").Attribute("IsEnabled"));
    }
}
