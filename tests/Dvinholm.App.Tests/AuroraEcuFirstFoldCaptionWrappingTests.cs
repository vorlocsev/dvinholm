using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for RU/EN ECU first-fold caption containment.
/// Tests inspect actual XAML and localization sources, not native GUI pixels.
/// </summary>
public sealed class AuroraEcuFirstFoldCaptionWrappingTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraEcuFirstFoldCaptionWrappingTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded MainWindow.xaml missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string id) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);

    private static void Wraps(XDocument doc, params string[] ids)
    {
        foreach (var id in ids)
        {
            var element = Named(doc, id);
            Assert.Equal("Wrap", (string?)element.Attribute("TextWrapping"));
            Assert.Null(element.Attribute("MaxLines"));
            Assert.Null(element.Attribute("TextTrimming"));
        }
    }

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
    public void DiscoveryTitleInstructionAndLiveScanStatusWrap()
    {
        var doc = Layout();
        Wraps(doc, "EcuDiscoveryTitle", "EcuDiscoveryIntro", "DiscoveryStatus");
        Assert.Contains(Named(doc, "EcuPage"), Named(doc, "EcuDiscoveryIntro").Ancestors());
        Assert.Equal("Discover_Click", (string?)Named(doc, "DiscoverButton").Attribute("Click"));
        Assert.Equal("False", (string?)Named(doc, "DiscoverButton").Attribute("IsEnabled"));
        Assert.Equal("CancelDiscovery_Click",
            (string?)Named(doc, "CancelDiscoveryButton").Attribute("Click"));
    }

    [Fact]
    public void NarrowObservedResponseHeaderAndInferredTxWarningWrap()
    {
        var doc = Layout();
        Wraps(doc, "EcuListTitle", "EcuListObservedHeader", "EcuListInferredHeader");
        var list = Named(doc, "EcusView");
        Assert.Contains(Named(doc, "EcuWorkbenchListPane"), list.Ancestors());
        Assert.Contains(Named(doc, "EcuListInferredHeader"), list.Descendants());
        Assert.Contains("inferred only",
            (string?)Named(doc, "EcuListInferredHeader").Attribute("Text"));
        Assert.Equal("False", (string?)list.Attribute("IsEnabled"));
    }

    [Fact]
    public void VehicleTitleWrapsBesideActualFamilySelector()
    {
        var doc = Layout();
        Wraps(doc, "EcuVehicleHeading");
        var title = Named(doc, "EcuVehicleHeading");
        var selector = Named(doc, "EcuVehicleModel");
        Assert.Contains(Named(doc, "EcuWorkbenchVehiclePane"), title.Ancestors());
        Assert.Equal("1", (string?)selector.Attribute("Grid.Column"));
        Assert.Equal("150", (string?)selector.Attribute("Width"));
        Assert.Equal("EcuVehicleModel_SelectionChanged",
            (string?)selector.Attribute("SelectionChanged"));
        Assert.Equal(2, selector.Elements().Count(e => e.Name.LocalName == "ComboBoxItem"));
    }

    [Fact]
    public void ObservationalRailCaptionWrapsWithoutMakingPhysicalPositionClaims()
    {
        var doc = Layout();
        Wraps(doc, "EcuObservationHeading");
        var title = Named(doc, "EcuObservationHeading");
        Assert.Contains(Named(doc, "EcuWorkbenchVehiclePane"), title.Ancestors());
        Assert.Contains("не позиции ECU", (string?)title.Attribute("Text"));
        Assert.Equal("False",
            (string?)Named(doc, "EcuVehicleSchematicStamp").Attribute("IsHitTestVisible"));
        Assert.Contains(Named(doc, "EcuObservationRail"),
            Named(doc, "EcuWorkbenchVehiclePane").Descendants());
    }

    [Fact]
    public void SelectedResponseHeadingWrapsAndKeepsUnverifiedContext()
    {
        var doc = Layout();
        Wraps(doc, "EcuSelectedHeading");
        Assert.Contains(Named(doc, "EcuWorkbenchDetailPane"),
            Named(doc, "EcuSelectedHeading").Ancestors());
        var evidence = Named(doc, "EcuSelectionInfo");
        Assert.Equal("Wrap", (string?)evidence.Attribute("TextWrapping"));
        Assert.Contains("TX-адрес не установлены", evidence.ToString());
        Assert.Contains(Named(doc, "EcuWorkbenchDetailPane"), evidence.Ancestors());
    }

    [Fact]
    public void SecondaryEcuToolHeadingsWrapButRemainBelowPrimaryTriptych()
    {
        var doc = Layout();
        Wraps(doc, "EcuToolsTitle", "EcuToolsIntro");
        var page = Named(doc, "EcuPage").ToString();
        Assert.True(page.IndexOf("x:Name=\"EcuWorkbenchGrid\"", StringComparison.Ordinal) <
                    page.IndexOf("x:Name=\"EcuToolsFooter\"", StringComparison.Ordinal));
        Assert.Contains(Named(doc, "EcuToolsFooter"), Named(doc, "EcuToolsTitle").Ancestors());
        var actions = Named(doc, "EcuToolsGrid").Elements()
            .Where(e => e.Name.LocalName == "Button").ToArray();
        Assert.Equal(3, actions.Length);
        Assert.All(actions, e =>
            Assert.Equal("WorkspaceNavigate_Click", (string?)e.Attribute("Click")));
    }

    [Fact]
    public void ExistingRuEnProjectionStillOwnsAllElevenCaptions()
    {
        var locale = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        foreach (var id in new[] { "EcuDiscoveryTitle", "EcuDiscoveryIntro",
            "EcuListTitle", "EcuListObservedHeader", "EcuListInferredHeader",
            "EcuVehicleHeading", "EcuObservationHeading", "EcuSelectedHeading",
            "EcuToolsTitle", "EcuToolsIntro" })
            Assert.Contains(id + ".Text = L(", locale);

        // DiscoveryStatus is live scan state, not static translated placeholder.
        Assert.DoesNotContain("DiscoveryStatus.Text = L(", locale);
        Assert.DoesNotContain("ReadDtc_Click(", locale);
        Assert.DoesNotContain("PassThru", locale);
    }

    [Fact]
    public void SixPassiveBindingsAndOriginalNoWriteGatesRemainIntact()
    {
        var doc = Layout();
        var template = Named(doc, "EcusView").Descendants()
            .Single(e => e.Name.LocalName == "DataTemplate").ToString();
        foreach (var id in new[] { "Response", "Type", "Request", "RequestBasis",
            "Frames", "MaxPayload" })
            Assert.Equal(1, template.Split("{Binding " + id + "}").Length - 1);
        Assert.Equal("Ecu_SelectionChanged",
            (string?)Named(doc, "EcusView").Attribute("SelectionChanged"));
        Assert.Equal("Disabled", (string?)Named(doc, "WorkspaceScroll")
            .Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("False", (string?)Named(doc, "CemConfigWriteButton").Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Named(doc, "DtcClearButton").Attribute("IsEnabled"));
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
    }
}
