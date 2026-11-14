using System.Globalization;
using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Stage 562: a text-free, offline Aurora coast banner replaces the printed
/// DVINHOLM wordmark. Structural guards are NOT real Windows visual proof.
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

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null;
             dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }
        throw new FileNotFoundException(relative);
    }

    private static (double Offset, byte Alpha)[] Stops(XDocument doc)
    {
        var art = Artwork(doc);
        var mask = art.Descendants().Single(e => e.Name.LocalName == "Rectangle");
        return mask.Descendants().Where(e => e.Name.LocalName == "GradientStop")
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
    }

    [Fact]
    public void HeaderUsesNewTextFreeSvgInsteadOfOldPrintedBranding()
    {
        var art = Artwork(Layout());
        Assert.Equal("False", (string?)art.Attribute("IsHitTestVisible"));
        Assert.Equal("2", (string?)art.Attribute("Grid.RowSpan"));
        Assert.Equal("2", (string?)art.Attribute("Grid.ColumnSpan"));
        var svg = art.Descendants().Single(e => e.Name.LocalName == "SvgImageSource");
        Assert.Equal("ms-appx:///Assets/Aurora-Northern-Coast.svg",
            (string?)svg.Attribute("UriSource"));
        Assert.DoesNotContain("Dvinholm-Banner.png", art.ToString(),
            StringComparison.OrdinalIgnoreCase);
        var photo = svg.Parent?.Parent;
        Assert.NotNull(photo);
        Assert.Equal("Image", photo.Name.LocalName);
        Assert.Equal("UniformToFill", (string?)photo.Attribute("Stretch"));
        Assert.Equal("False", (string?)photo.Attribute("IsHitTestVisible"));
    }

    [Fact]
    public void NewIllustrationIsPackagedInOfflineSelfContainedBuild()
    {
        var project = Source("src/Dvinholm.App/Dvinholm.App.csproj");
        Assert.Contains("assets\\aurora-northern-coast.svg", project,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Link=\"Assets\\Aurora-Northern-Coast.svg\"", project);
        Assert.Contains("CopyToPublishDirectory=\"PreserveNewest\"", project);
        var svg = Source("assets/aurora-northern-coast.svg");
        var doc = XDocument.Parse(svg);
        Assert.Equal("svg", doc.Root?.Name.LocalName);
        Assert.Equal("0 0 1600 260", (string?)doc.Root?.Attribute("viewBox"));
    }

    [Fact]
    public void BannerAssetContainsOnlyStaticSceneryNoTextOrFakeVehicleData()
    {
        var source = Source("assets/aurora-northern-coast.svg");
        var asset = XDocument.Parse(source);
        Assert.Empty(asset.Descendants().Where(e => new[]
        {
            "text", "tspan", "script", "foreignObject", "image", "animate"
        }.Contains(e.Name.LocalName, StringComparer.OrdinalIgnoreCase)));
        foreach (var forbidden in new[] { "VIN:", "CAN-ID", "ECU:", "Connected",
            "Dvinholm", "Search PIN", "100%" })
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        Assert.True(asset.Descendants().Count(e => e.Name.LocalName == "path") >= 10);
        Assert.True(asset.Descendants().Count(e => e.Name.LocalName == "linearGradient") >= 4);
        Assert.DoesNotContain("http://", source.Replace(
            "http://www.w3.org/2000/svg", ""), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TintKeepsTrueLocalizedHeadingLegibleWithBrightAuroraOnRight()
    {
        var stops = Stops(Layout());
        Assert.Equal(new[] { 0.0, 0.42, 0.74, 1.0 },
            stops.Select(s => s.Offset).ToArray());
        Assert.Equal(4, stops.Length);
        Assert.True(stops[0].Alpha >= 0xF0, "White localized title needs dark left.");
        Assert.True(stops[1].Alpha >= 0xD0, "Description needs a dark central mask.");
        Assert.True(stops[2].Alpha < 0xB0, "Aurora must remain visible toward the right.");
        Assert.True(stops[3].Alpha >= stops[2].Alpha,
            "Connection status and language controls need a dark right edge.");
    }

    [Fact]
    public void ArtworkCannotInterceptNavigationOrDisplayLiveStatus()
    {
        var doc = Layout();
        var art = Artwork(doc);
        var header = Named(doc, "WorkspaceHeaderGrid");
        var title = Named(doc, "WorkspaceTitle");
        var locale = Named(doc, "UnifiedLanguageSelector");
        var status = Named(doc, "HeaderConnectionStatusButton");
        Assert.Contains(art, header.Elements());
        Assert.Contains(title, header.Descendants());
        Assert.Contains(locale, header.Descendants());
        Assert.DoesNotContain(title, art.Descendants());
        Assert.DoesNotContain(locale, art.Descendants());
        Assert.DoesNotContain(status, art.Descendants());
        Assert.Equal("False", (string?)art.Attribute("IsHitTestVisible"));
        Assert.Null(art.Attribute("Click"));
        Assert.Null(art.Attribute("Tapped"));
        Assert.Null(art.Attribute("PointerPressed"));
        Assert.Equal("WorkspaceNavigate_Click", (string?)status.Attribute("Click"));
        Assert.Equal("DvinholmWorkspaceTitle",
            (string?)title.Attribute("AutomationProperties.AutomationId"));
    }
}
