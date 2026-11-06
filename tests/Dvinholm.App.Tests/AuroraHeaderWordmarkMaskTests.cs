using System.Globalization;
using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Offline XAML contracts based on the real 2026-11-04 RU/EN desktop
/// screenshots: the large printed banner logo must sit beneath the active
/// localized page title. Rendering still needs a fresh Windows comparison.
/// </summary>
public sealed class AuroraHeaderWordmarkMaskTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraHeaderWordmarkMaskTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded MainWindow.xaml.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static XElement Artwork(XDocument doc) => Named(doc, "AuroraHeaderArtwork");

    private static XElement BannerMask(XDocument doc) =>
        Artwork(doc).Descendants().Single(e => e.Name.LocalName == "Rectangle");

    private static (double Offset, byte Alpha)[] Stops(XDocument doc) =>
        BannerMask(doc).Descendants().Where(e => e.Name.LocalName == "GradientStop")
            .Select(e =>
            {
                var color = (string?)e.Attribute("Color");
                Assert.NotNull(color);
                Assert.Matches("^#[0-9A-Fa-f]{8}$", color);
                return (
                    Offset: double.Parse((string?)e.Attribute("Offset") ?? "",
                        CultureInfo.InvariantCulture),
                    Alpha: byte.Parse(color[1..3], NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture));
            }).ToArray();

    [Fact]
    public void OriginalBundledSceneryIsStillPresent()
    {
        var art = Artwork(Layout());
        Assert.Equal("False", (string?)art.Attribute("IsHitTestVisible"));
        Assert.Contains(art.Descendants(), e => e.Name.LocalName == "ImageBrush" &&
            (string?)e.Attribute("ImageSource") ==
            "ms-appx:///Assets/Dvinholm-Banner.png");
        Assert.Equal("2", (string?)art.Attribute("Grid.RowSpan"));
        Assert.Equal("2", (string?)art.Attribute("Grid.ColumnSpan"));
    }

    [Fact]
    public void PrintedLogoIsMaskedEvenOnTheRightSideOfTheWideBanner()
    {
        var stops = Stops(Layout());
        Assert.Equal(4, stops.Length);
        Assert.Equal(new[] { 0.0, 0.42, 0.74, 1.0 },
            stops.Select(s => s.Offset).ToArray());
        Assert.True(stops[0].Alpha >= 0xF0, "Left heading needs an opaque backdrop.");
        Assert.True(stops[^1].Alpha >= 0xC0,
            "Right-aligned printed wordmark must not overpower live page labels.");
        Assert.All(stops, s => Assert.True(s.Alpha >= 0xC0));
    }

    [Fact]
    public void BannerTintFormsAContinuousProtectiveOpacityRamp()
    {
        var stops = Stops(Layout());
        for (var i = 1; i < stops.Length; i++)
            Assert.True(stops[i].Alpha <= stops[i - 1].Alpha,
                "Dark mask should ease toward scenery without transparent gaps.");
        Assert.Equal(0xFA, (int)stops[0].Alpha);
        Assert.Equal(0xC9, (int)stops[^1].Alpha);
    }

    [Fact]
    public void ExistingNorthernLightsStayVisibleOverDarkenedBackground()
    {
        var art = Artwork(Layout());
        var viewbox = art.Descendants()
            .Single(e => e.Name.LocalName == "Viewbox");
        Assert.Equal("0.48", (string?)viewbox.Attribute("Opacity"));
        Assert.True(viewbox.Descendants().Count(e => e.Name.LocalName == "Path") >= 3);
        Assert.Contains("Fill=\"Transparent\"", viewbox.ToString());
    }

    [Fact]
    public void RealPageHeadingAndLanguageControlsRemainAboveDecoration()
    {
        var doc = Layout();
        var art = Artwork(doc);
        var header = Named(doc, "WorkspaceHeaderGrid");
        var title = Named(doc, "WorkspaceTitle");
        var locale = Named(doc, "UnifiedLanguageSelector");
        var connection = Named(doc, "HeaderConnectionStatusButton");
        Assert.Contains(art, header.Elements());
        Assert.False(art.DescendantsAndSelf().Contains(title));
        Assert.False(art.DescendantsAndSelf().Contains(locale));
        Assert.False(art.DescendantsAndSelf().Contains(connection));
        Assert.Contains(title, header.Descendants());
        Assert.Contains(locale, header.Descendants());
        Assert.Equal("DvinholmWorkspaceTitle",
            (string?)title.Attribute("AutomationProperties.AutomationId"));
    }

    [Fact]
    public void MaskCannotInterceptNavigationOrChangeSelectionState()
    {
        var doc = Layout();
        var art = Artwork(doc);
        Assert.Equal("False", (string?)art.Attribute("IsHitTestVisible"));
        Assert.Null(art.Attribute("Click"));
        Assert.Null(art.Attribute("Tapped"));
        Assert.Null(art.Attribute("SelectionChanged"));
        Assert.Null(art.Attribute("PointerPressed"));
        Assert.Equal("WorkspaceNavigate_Click",
            (string?)Named(doc, "HeaderConnectionStatusButton").Attribute("Click"));
    }
}
