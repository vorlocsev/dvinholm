using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Stage 82: eight offline responsive-layout checks for the secondary
/// ECU profile workspace. No WinUI screenshot or J2534 device is used.
/// </summary>
public sealed class AuroraCompactEcuProfileLayoutTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraCompactEcuProfileLayoutTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml not embedded.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static Dictionary<string, string> Setters(XDocument doc, string state) =>
        Named(doc, state).Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    [Theory]
    [InlineData("ProfileImportButton", "2", "0", "1", "ImportProfiles_Click")]
    [InlineData("ProfileExportButton", "3", "1", "1", "ExportProfiles_Click")]
    [InlineData("CaptureSnapshotButton", "0", "0", "2", "CaptureSnapshot_Click")]
    [InlineData("InspectSnapshotButton", "2", "0", "3", "InspectSnapshot_Click")]
    [InlineData("CompareSnapshotsButton", "3", "1", "3", "CompareSnapshots_Click")]
    public void OriginalProfileCommandsReflowToTwoCompactCellsWithoutLosingHandlers(
        string id, string wideColumn, string compactColumn, string compactRow,
        string handler)
    {
        var doc = Layout();
        var button = Named(doc, id);
        var compact = Setters(doc, "ProfileCompact");
        Assert.Contains(Named(doc, "ProfileActionsPanel"), button.Ancestors());
        Assert.Equal(wideColumn, (string?)button.Attribute("Grid.Column"));
        Assert.Equal(handler, (string?)button.Attribute("Click"));
        Assert.Equal("Stretch", (string?)button.Attribute("HorizontalAlignment"));
        Assert.Equal("{StaticResource AuroraWrapCaption}",
            (string?)button.Attribute("ContentTemplate"));
        Assert.Equal(compactRow, compact[id + ".(Grid.Row)"]);

        if (id == "CaptureSnapshotButton")
        {
            Assert.Equal("2", (string?)button.Attribute("Grid.ColumnSpan"));
            Assert.False(compact.ContainsKey(id + ".(Grid.Column)"));
        }
        else
            Assert.Equal(compactColumn, compact[id + ".(Grid.Column)"]);
    }

    [Fact]
    public void ProfilesKeepWideFourColumnLayoutAndCompactTwoColumnLayout()
    {
        var doc = Layout();
        var wide = Named(doc, "ProfileWide");
        var narrow = Named(doc, "ProfileCompact");
        Assert.Equal("980", (string?)wide.Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.Equal("0", (string?)narrow.Descendants()
            .Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.DoesNotContain(wide.Descendants(),
            e => e.Name.LocalName == "Setter");
        var grid = Named(doc, "ProfileActionsPanel");
        Assert.Contains(Named(doc, "ProfilesPage"), grid.Ancestors());
        Assert.Equal(new[] { "*", "*", "*", "*" }, grid.Elements()
            .Single(e => e.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements().Select(e => (string?)e.Attribute("Width")));
        Assert.Equal(4, grid.Elements()
            .Single(e => e.Name.LocalName == "Grid.RowDefinitions")
            .Elements().Count());
        var compact = Setters(doc, "ProfileCompact");
        Assert.Equal("0", compact["ProfileActionThirdColumn.Width"]);
        Assert.Equal("0", compact["ProfileActionFourthColumn.Width"]);
    }

    [Fact]
    public void CompactProfileSearchAndSelectorStackWithoutChangingSelectionEvents()
    {
        var doc = Layout();
        var grid = Named(doc, "ProfileFiltersGrid");
        var compact = Setters(doc, "ProfileCompact");
        Assert.Contains(Named(doc, "ProfilesPage"), grid.Ancestors());
        Assert.Equal(3, grid.Elements()
            .Single(e => e.Name.LocalName == "Grid.RowDefinitions")
            .Elements().Count());
        Assert.Equal("*", compact["ProfileSearchColumn.Width"]);
        Assert.Equal("0", compact["ProfileSelectorColumn.Width"]);
        Assert.Equal("0", compact["ProfileFiltersThirdColumn.Width"]);
        Assert.Equal("0", compact["ProfileSelector.(Grid.Column)"]);
        Assert.Equal("1", compact["ProfileSelector.(Grid.Row)"]);
        Assert.Equal("2", compact["MarkedVerifiedOnly.(Grid.Row)"]);
        Assert.Equal("1", compact["MarkedVerifiedOnly.(Grid.ColumnSpan)"]);
        Assert.Equal("ProfileSearch_TextChanged",
            (string?)Named(doc, "ProfileSearch").Attribute("TextChanged"));
        Assert.Equal("ProfileSelector_SelectionChanged",
            (string?)Named(doc, "ProfileSelector").Attribute("SelectionChanged"));
        Assert.Equal("MarkedVerifiedOnly_Changed",
            (string?)Named(doc, "MarkedVerifiedOnly").Attribute("Checked"));
        Assert.Equal("MarkedVerifiedOnly_Changed",
            (string?)Named(doc, "MarkedVerifiedOnly").Attribute("Unchecked"));
    }

    [Fact]
    public void CompactProfileLayoutPreservesExistingDisabledActionsAndSixPages()
    {
        var doc = Layout();
        foreach (var id in new[] { "EditProfileButton", "CaptureSnapshotButton" })
            Assert.Equal("False", (string?)Named(doc, id).Attribute("IsEnabled"));
        Assert.Equal("CreateProfile_Click",
            (string?)Named(doc, "CreateProfileButton").Attribute("Click"));
        Assert.Equal("2", (string?)Named(doc, "CaptureSnapshotButton")
            .Attribute("Grid.ColumnSpan"));
        Assert.Equal("Wrap", (string?)Named(doc, "EcuInfoText")
            .Attribute("TextWrapping"));
        Assert.Equal("Disabled", (string?)Named(doc, "WorkspaceScroll")
            .Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.Equal("False",
            (string?)Named(doc, "CemConfigWriteButton").Attribute("IsEnabled"));
        Assert.Equal("False",
            (string?)Named(doc, "DtcClearButton").Attribute("IsEnabled"));
    }
}
