using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline structure/behavior contracts for the approved ECU and DTC
/// Aurora visual hierarchy. They do not render a Windows GUI or contact a car.
/// </summary>
public sealed class AuroraEcuDtcVisualParityTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraEcuDtcVisualParityTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded MainWindow.xaml is missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static Dictionary<string, string> Setters(XDocument doc, string state)
    {
        return Named(doc, state).Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);
    }

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
    public void EcuWorkbenchRemainsPrimaryAndToolsAreClearlyBelowIt()
    {
        var doc = Layout();
        var page = Named(doc, "EcuPage");
        var workbench = Named(doc, "EcuWorkbenchGrid");
        var footer = Named(doc, "EcuToolsFooter");
        Assert.Contains(page, workbench.Ancestors());
        Assert.Contains(page, footer.Ancestors());
        Assert.True(page.ToString().IndexOf("x:Name=\"EcuWorkbenchGrid\"", StringComparison.Ordinal)
            < page.ToString().IndexOf("x:Name=\"EcuToolsFooter\"", StringComparison.Ordinal));
        Assert.Contains(Named(doc, "EcuToolsGrid"), footer.Descendants());
    }

    [Fact]
    public void EcuTriptychHasRealObservationListArtboardAndDetails()
    {
        var doc = Layout();
        var grid = Named(doc, "EcuWorkbenchGrid");
        var panes = grid.Elements().Where(e => e.Name.LocalName == "Border").ToArray();
        Assert.Equal(new[] { "EcuWorkbenchListPane", "EcuWorkbenchVehiclePane",
            "EcuWorkbenchDetailPane" }, panes.Select(e => (string?)e.Attribute(X + "Name")));
        Assert.Equal(new[] { "0", "1", "2" },
            panes.Select(e => (string?)e.Attribute("Grid.Column")));
        Assert.Contains(Named(doc, "EcusView"), Named(doc, "EcuWorkbenchListPane").Descendants());
        Assert.Contains(Named(doc, "EcuVehicleArtboard"), Named(doc, "EcuWorkbenchVehiclePane").Descendants());
        Assert.Contains(Named(doc, "EcuSelectionInfo"), Named(doc, "EcuWorkbenchDetailPane").Descendants());
        Assert.NotNull(Named(doc, "EcuP2SideProfile"));
        Assert.NotNull(Named(doc, "EcuP3SideProfile"));
    }

    [Fact]
    public void DecorativeDiagramAndSchematicStampNeverImplyPhysicalEcUs()
    {
        var doc = Layout();
        var artboard = Named(doc, "EcuVehicleArtboard");
        var stamp = Named(doc, "EcuVehicleSchematicStamp");
        Assert.Contains(stamp, artboard.Descendants());
        Assert.Equal("False", (string?)stamp.Attribute("IsHitTestVisible"));
        Assert.Equal("УСЛОВНАЯ СХЕМА",
            (string?)Named(doc, "EcuVehicleSchematicLabel").Attribute("Text"));
        Assert.True(artboard.Elements().Count(e => e.Name.LocalName == "Path") >= 3);
        Assert.Contains("не указывает физические позиции",
            Named(doc, "EcuDiagramDisclaimer").ToString());
        Assert.Null(stamp.Attribute("Click"));
    }

    [Fact]
    public void EcuFooterActionsPreserveNavigationAndCompactReflow()
    {
        var doc = Layout();
        var grid = Named(doc, "EcuToolsGrid");
        var buttons = grid.Elements().Where(e => e.Name.LocalName == "Button").ToArray();
        Assert.Equal(new[] { "EcuDiagnosticsNavigateButton",
            "EcuMonitorNavigateButton", "EcuProfilesNavigateButton" },
            buttons.Select(e => (string?)e.Attribute(X + "Name")));
        Assert.All(buttons, button =>
            Assert.Equal("WorkspaceNavigate_Click", (string?)button.Attribute("Click")));
        Assert.Equal(new[] { "DiagnosticsWorkspace", "CanMonitor", "EcuProfiles" },
            buttons.Select(e => (string?)e.Attribute("Tag")));
        var compact = Setters(doc, "EcuVerticalWorkspace");
        Assert.Equal("0", compact["EcuToolsThirdColumn.Width"]);
        Assert.Equal("1", compact["EcuProfilesNavigateButton.(Grid.Row)"]);
        Assert.Equal("2", compact["EcuProfilesNavigateButton.(Grid.ColumnSpan)"]);
    }

    [Fact]
    public void DtcActionRailContainsOnlyExplicitFilterReadAndDisabledClear()
    {
        var doc = Layout();
        var rail = Named(doc, "DtcActionRail");
        var grid = Named(doc, "DtcActionsGrid");
        Assert.Contains(Named(doc, "DtcPage"), rail.Ancestors());
        Assert.Contains(grid, rail.Descendants());
        foreach (var name in new[] { "DtcStatusMaskSelector", "ReadDtcButton", "DtcClearButton" })
            Assert.Contains(Named(doc, name), grid.Descendants());
        var mask = Named(doc, "DtcStatusMaskSelector");
        Assert.Equal(new[] { "FF", "01", "04", "08", "80" },
            mask.Elements().Select(e => (string?)e.Attribute("Tag")));
        Assert.Equal("ReadDtc_Click", (string?)Named(doc, "ReadDtcButton").Attribute("Click"));
        var clear = Named(doc, "DtcClearButton");
        Assert.Equal("False", (string?)clear.Attribute("IsEnabled"));
        Assert.Null(clear.Attribute("Click"));
    }

    [Fact]
    public void CompactDtcCommandsStackButKeepTwoPaneWideLayout()
    {
        var doc = Layout();
        var compact = Setters(doc, "DtcStacked");
        Assert.Equal("0", compact["DtcDetailColumn.Width"]);
        Assert.Equal("1", compact["DtcDetailsPane.(Grid.Row)"]);
        Assert.Equal("0", compact["DtcActionReadColumn.Width"]);
        Assert.Equal("0", compact["DtcActionClearColumn.Width"]);
        Assert.Equal("1", compact["ReadDtcButton.(Grid.Row)"]);
        Assert.Equal("2", compact["DtcClearButton.(Grid.Row)"]);
        Assert.Equal("1280",
            (string?)Named(doc, "DtcTwoColumns").Descendants()
                .Single(e => e.Name.LocalName == "AdaptiveTrigger")
                .Attribute("MinWindowWidth"));
        Assert.Equal("0", (string?)Named(doc, "DtcResultsPane").Attribute("Grid.Column"));
        Assert.Equal("1", (string?)Named(doc, "DtcDetailsPane").Attribute("Grid.Column"));
    }

    [Fact]
    public void DtcResultAccentsNeverHideActualDataOrInventDescription()
    {
        var doc = Layout();
        var accent = Named(doc, "DtcResultsAccent");
        Assert.Equal("False", (string?)accent.Attribute("IsHitTestVisible"));
        Assert.Contains(accent, Named(doc, "DtcResultsPane").Descendants());
        Assert.Contains(Named(doc, "DtcDetailsAccent"),
            Named(doc, "DtcDetailsPane").Descendants());
        var table = Named(doc, "DtcRecordsView");
        Assert.Contains("{Binding Code}", table.ToString());
        Assert.Contains("{Binding Description}", table.ToString());
        Assert.Contains("{Binding Status}", table.ToString());
        Assert.Contains("Подтверждённого каталога описаний Volvo пока нет",
            Named(doc, "DtcEvidenceDisclaimer").ToString());
        Assert.Contains(Named(doc, "DtcSelectedRecordDetails"),
            Named(doc, "DtcDetailsPane").Descendants());
    }

    [Fact]
    public void AddedVisualsTranslateWithoutCanOrUpdateSideEffects()
    {
        var doc = Layout();
        var source = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.Contains("EcuVehicleSchematicLabel.Text = L(", source);
        Assert.Contains("DtcActionsHeading.Text = L(", source);
        Assert.Contains("ApplyAuroraPrimaryScreenLocalization();",
            Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs"));
        Assert.DoesNotContain("_session.Bus", source);
        Assert.DoesNotContain("ReadDtc_Click(", source);
        Assert.DoesNotContain("PassThruWriteMsgs(", source);
        var names = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(name => name is not null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
    }
}
