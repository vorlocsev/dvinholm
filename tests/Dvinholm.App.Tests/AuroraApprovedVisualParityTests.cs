using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight visual-structure contracts against the approved Aurora six-screen
/// concept. They check real packaged assets and layout, never assume a PIN,
/// an ECU identity, a vehicle response or physical Windows screenshot pass.
/// </summary>
public sealed class AuroraApprovedVisualParityTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraApprovedVisualParityTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded MainWindow.xaml is missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(element =>
            (string?)element.Attribute(X + "Name") == name);

    private static XElement State(XDocument doc, string name) =>
        doc.Descendants().Single(element =>
            element.Name.LocalName == "VisualState" &&
            (string?)element.Attribute(X + "Name") == name);

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
    public void AuroraHeaderUsesActualBundledSceneryNotAnEmptyColorBlock()
    {
        var doc = Layout();
        var art = Named(doc, "AuroraHeaderArtwork");
        Assert.Contains(Named(doc, "WorkspaceHeaderGrid"), art.Ancestors());
        Assert.Equal("False", (string?)art.Attribute("IsHitTestVisible"));
        Assert.Equal("2", (string?)art.Attribute("Grid.RowSpan"));
        Assert.Equal("2", (string?)art.Attribute("Grid.ColumnSpan"));
        Assert.Contains("ms-appx:///Assets/Dvinholm-Banner.png", art.ToString());
        Assert.True(art.Descendants().Count(e => e.Name.LocalName == "GradientStop") >= 4);
        Assert.Contains(Named(doc, "WorkspaceTitle"),
            Named(doc, "WorkspaceHeaderGrid").Descendants());
    }

    [Fact]
    public void ApprovedGullGearBrandIsBundledAndRetainedInCompactSidebar()
    {
        var doc = Layout();
        var brand = Named(doc, "SidebarBrand");
        var image = brand.Descendants().Single(e => e.Name.LocalName == "Image");
        Assert.Equal("ms-appx:///Assets/Launcher-Northern-Emblem.svg",
            (string?)image.Attribute("Source"));
        Assert.Equal("Collapsed",
            (string?)Named(doc, "SidebarCompactBrand").Attribute("Visibility"));
        Assert.Equal("Visible", (string?)State(doc, "ShellCompact")
            .Descendants().Single(e => e.Name.LocalName == "Setter" &&
                (string?)e.Attribute("Target") == "SidebarCompactBrand.Visibility")
            .Attribute("Value"));
        var project = Source("src/Dvinholm.App/Dvinholm.App.csproj");
        Assert.Contains("assets\\launcher-northern-emblem.svg", project,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dvinholm-Banner.png", project);
    }

    [Fact]
    public void SelectedNavigationHasDedicatedGradientAndNoFakeSessionClaims()
    {
        var source = Source("src/Dvinholm.App/MainWindow.AuroraChrome.cs");
        var navigation = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("LinearGradientBrush", source);
        Assert.Contains("GradientStops.Add(", source);
        Assert.Contains("ApplyAuroraNavigationSurface(button, selected)", navigation);
        Assert.Contains("ApplyAuroraNavigationSurface(SidebarUpdatesButton", navigation);
        Assert.Contains("button.BorderThickness = new Thickness(1);", source);
        Assert.DoesNotContain("PassThru", source);
        Assert.DoesNotContain("Connect_Click(", source);
    }

    [Fact]
    public void RealAdapterStateAndBuildVersionHaveCompactFooter()
    {
        var doc = Layout();
        var footer = Named(doc, "ShellStatusBar");
        Assert.Contains(Named(doc, "StatusFooterDot"), footer.Descendants());
        Assert.Contains(Named(doc, "ShellStatusText"), footer.Descendants());
        Assert.Contains(Named(doc, "StatusFooterVersion"), footer.Descendants());
        Assert.Equal("#728FA8",
            (string?)Named(doc, "StatusFooterDot").Attribute("Foreground"));
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var update = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("_connected && _session.IsConnected", source);
        Assert.Contains("StatusFooterDot.Foreground =", source);
        Assert.Contains("StatusFooterVersion.Text =", update);
    }

    [Fact]
    public void ThreePinStagesAreVisualOnlyAndNoSecretOrProgressIsInvented()
    {
        var doc = Layout();
        var page = Named(doc, "CemPinPage");
        var stepper = Named(doc, "CemPinStepper");
        Assert.Contains(page, stepper.Ancestors());
        var columns = stepper.Descendants().Single(e =>
            e.Name.LocalName == "Grid.ColumnDefinitions");
        Assert.Equal(3, columns.Elements().Count());
        foreach (var name in new[] { "CemPinStepIdentity", "CemPinStepResearch",
            "CemPinStepResult" })
            Assert.Contains(Named(doc, name), stepper.Descendants());
        Assert.Empty(stepper.Descendants().Where(e =>
            e.Name.LocalName == "Button" ||
            e.Attribute("Click") is not null));
        Assert.Equal("Collapsed", (string?)Named(doc, "CemPinSearchCard")
            .Attribute("Visibility"));
        Assert.Equal("False", (string?)Named(doc, "CemPinReadButton")
            .Attribute("IsEnabled"));
        Assert.DoesNotContain("68%", stepper.ToString());
        Assert.DoesNotContain("4387", stepper.ToString());
    }

    [Fact]
    public void DtcTableAndDetailsMatchApprovedWideSideBySideArrangement()
    {
        var doc = Layout();
        var grid = Named(doc, "DtcWorkbenchGrid");
        Assert.Contains(Named(doc, "DtcPage"), grid.Ancestors());
        Assert.Contains(Named(doc, "DtcRecordsView"),
            Named(doc, "DtcResultsPane").Descendants());
        Assert.Contains(Named(doc, "DtcSelectedRecordDetails"),
            Named(doc, "DtcDetailsPane").Descendants());
        Assert.Equal("0", (string?)Named(doc, "DtcResultsPane")
            .Attribute("Grid.Column"));
        Assert.Equal("1", (string?)Named(doc, "DtcDetailsPane")
            .Attribute("Grid.Column"));
        Assert.Equal("1280", (string?)State(doc, "DtcTwoColumns")
            .Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
    }

    [Fact]
    public void CompactDtcDetailsStackAndErasureStaysDisabled()
    {
        var doc = Layout();
        var state = State(doc, "DtcStacked");
        var setters = state.Descendants().Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!);
        Assert.Equal("0", setters["DtcDetailColumn.Width"]);
        Assert.Equal("0", setters["DtcDetailsPane.(Grid.Column)"]);
        Assert.Equal("1", setters["DtcDetailsPane.(Grid.Row)"]);
        var erase = Named(doc, "DtcClearButton");
        Assert.Equal("False", (string?)erase.Attribute("IsEnabled"));
        Assert.Null(erase.Attribute("Click"));
        Assert.Equal("ReadDtc_Click",
            (string?)Named(doc, "ReadDtcButton").Attribute("Click"));
    }

    [Fact]
    public void StillExactlySixWorkspacesAndEnglishBrandAndStepperRefresh()
    {
        var doc = Layout();
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.Empty(doc.Descendants().Where(e =>
            (string?)e.Attribute(X + "Name") is "SettingsPage" or "LauncherPage"));
        var source = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        var entry = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("SidebarBrandSubtitle.Text = L(", source);
        Assert.Contains("CemPinStepIdentity.Text = L(", source);
        Assert.Contains("CemPinStepResearch.Text = L(", source);
        Assert.Contains("CemPinStepResult.Text = L(", source);
        Assert.Contains("ApplyAuroraPrimaryScreenLocalization();", entry);
        Assert.DoesNotContain("ReadCemPin_Click(", source);
        Assert.DoesNotContain("ReadDtc_Click(", source);
    }
}
