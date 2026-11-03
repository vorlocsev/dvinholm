using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts that prevent automatic Updates focus from scrolling
/// the compact first fold past the installed-version card. Native GUI acceptance
/// still requires actual Windows screenshots and keyboard testing.
/// </summary>
public sealed class AuroraUpdatesFirstFoldFocusTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

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

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraUpdatesFirstFoldFocusTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded MainWindow.xaml.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string id) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);

    private static string Navigation()
    {
        var source = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        var start = source.IndexOf("private void UnifiedUpdatesNavigate_Click(", StringComparison.Ordinal);
        Assert.True(start >= 0);
        return source[start..];
    }

    [Fact]
    public void CompactFocusThresholdMatchesTheReal1280DipCardReflow()
    {
        var doc = Layout();
        var trigger = Named(doc, "UpdatesThreeColumns").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger");
        Assert.Equal("1280", (string?)trigger.Attribute("MinWindowWidth"));
        Assert.Contains("if (RootLayout.ActualWidth >= 1280)", Navigation());
        var compact = Named(doc, "UpdatesVerticalPanels").Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger");
        Assert.Equal("0", (string?)compact.Attribute("MinWindowWidth"));
    }

    [Fact]
    public void NarrowUpdatesFocusesSelectedSidebarInsteadOfSecondCard()
    {
        var method = Navigation();
        Assert.Contains("else\n            SidebarUpdatesButton.Focus(FocusState.Programmatic);", method);
        Assert.Contains("SelectWorkspaceNavigation(null);", method);
        Assert.Equal("UnifiedUpdatesNavigate_Click",
            (string?)Named(Layout(), "SidebarUpdatesButton").Attribute("Click"));
    }

    [Fact]
    public void WideThreeCardWorkspacePreservesChannelKeyboardFocus()
    {
        var method = Navigation();
        Assert.Contains("if (RootLayout.ActualWidth >= 1280)\n            UnifiedUpdateChannel.Focus(FocusState.Programmatic);", method);
        Assert.Equal("UnifiedUpdateChannel_SelectionChanged",
            (string?)Named(Layout(), "UnifiedUpdateChannel").Attribute("SelectionChanged"));
        Assert.Contains(Named(Layout(), "UnifiedUpdateChannel"),
            Named(Layout(), "UpdatesChannelPane").Descendants());
    }

    [Fact]
    public void ScrollIsResetAfterFocusSoFirstCardRemainsInFirstFold()
    {
        var method = Navigation();
        var choose = method.IndexOf("SidebarUpdatesButton.Focus(FocusState.Programmatic);",
            StringComparison.Ordinal);
        var reset = method.IndexOf("WorkspaceScroll.ChangeView(null, 0, null);",
            StringComparison.Ordinal);
        Assert.True(choose >= 0 && reset > choose);
        var scroll = Named(Layout(), "WorkspaceScroll");
        Assert.Equal("Auto", (string?)scroll.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)scroll.Attribute("HorizontalScrollBarVisibility"));
    }

    [Fact]
    public void InstalledVersionIsFirstRealPanelAndCompactStillStacksAllThree()
    {
        var doc = Layout();
        var grid = Named(doc, "UpdatesThreeCardGrid");
        var panes = grid.Elements().Where(e => e.Name.LocalName == "Border").ToArray();
        Assert.Equal(new[] { "UpdatesVersionPane", "UpdatesChannelPane", "UpdatesActionPane" },
            panes.Select(e => (string?)e.Attribute(X + "Name")));
        Assert.Equal("0", (string?)Named(doc, "UpdatesVersionPane").Attribute("Grid.Column"));
        Assert.Contains(Named(doc, "UnifiedCurrentVersion"),
            Named(doc, "UpdatesVersionPane").Descendants());
        var compact = Named(doc, "UpdatesVerticalPanels").Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!, e => (string)e.Attribute("Value")!);
        Assert.Equal("1", compact["UpdatesChannelPane.(Grid.Row)"]);
        Assert.Equal("2", compact["UpdatesActionPane.(Grid.Row)"]);
    }

    [Fact]
    public void NavigatingToUpdatesKeepsExactlyOnePageVisibleBySelectionLogic()
    {
        var method = Navigation();
        Assert.Contains("_unifiedUpdatesActive = true;", method);
        Assert.Contains("_workspaceDestination = null;", method);
        Assert.Contains("ReferenceEquals(page, UnifiedUpdatesPage)", method);
        Assert.Contains("page.Visibility = ReferenceEquals(page, UnifiedUpdatesPage)", method);
        Assert.Contains("UpdateUnifiedWorkspaceHeader();", method);
        Assert.Contains("DiagnosticResultsCard.Visibility = Visibility.Collapsed;", method);
    }

    [Fact]
    public void UpdatesActionsAndCancellationRemainUnchangedAndSafe()
    {
        var doc = Layout();
        var actions = Named(doc, "UnifiedUpdateActionList").Elements()
            .Where(e => e.Name.LocalName == "Button").ToArray();
        Assert.Equal(new[] { "UnifiedCheckUpdateButton", "UnifiedStageUpdateButton",
            "UnifiedCancelStageButton", "UnifiedPreflightButton" },
            actions.Select(e => (string?)e.Attribute(X + "Name")));
        Assert.Equal("UnifiedCheckUpdate_Click",
            (string?)Named(doc, "UnifiedCheckUpdateButton").Attribute("Click"));
        foreach (var id in new[] { "UnifiedStageUpdateButton",
            "UnifiedCancelStageButton", "UnifiedPreflightButton" })
            Assert.Equal("False", (string?)Named(doc, id).Attribute("IsEnabled"));
        Assert.Contains("_udsCts?.Cancel();", Navigation());
        Assert.Contains("ClearDiagnosticReadResult();", Navigation());
    }

    [Fact]
    public void FocusAdjustmentIssuesNoConnectionOrDownloadOrVehicleOperation()
    {
        var method = Navigation();
        Assert.DoesNotContain("Connect_Click(", method);
        Assert.DoesNotContain("ReadDtc_Click(", method);
        Assert.DoesNotContain("ReadCemPin_Click(", method);
        Assert.DoesNotContain("UnifiedStageUpdate_Click(", method);
        Assert.DoesNotContain("PassThruConnect(", method);
        Assert.Equal("Collapsed",
            (string?)Named(Layout(), "CemPinSearchCard").Attribute("Visibility"));
        Assert.Equal(6, Named(Layout(), "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
    }
}
