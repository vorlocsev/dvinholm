using System.Globalization;
using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Stage 562: the original printed wordmark banner is replaced by an offline
/// cinematic JPEG. Structural checks are not actual Windows GUI pixel proof.
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

    private static string FindFile(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null;
             dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
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
    public void HeaderUsesOfflineCoastPhotoInsteadOfPrintedWordmark()
    {
        var art = Artwork(Layout());
        Assert.Equal("False", (string?)art.Attribute("IsHitTestVisible"));
        Assert.Equal("2", (string?)art.Attribute("Grid.RowSpan"));
        Assert.Equal("2", (string?)art.Attribute("Grid.ColumnSpan"));
        var image = art.Descendants().Single(e => e.Name.LocalName == "Image");
        Assert.Equal("ms-appx:///Assets/Aurora-Northern-Coast.jpg",
            (string?)image.Attribute("Source"));
        Assert.Equal("UniformToFill", (string?)image.Attribute("Stretch"));
        Assert.Equal("False", (string?)image.Attribute("IsHitTestVisible"));
        Assert.DoesNotContain("Dvinholm-Banner.png", art.ToString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SvgImageSource", art.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NewPhotoHasRealJpegBytesAndIsPackagedForOfflineX64()
    {
        var project = File.ReadAllText(FindFile("src/Dvinholm.App/Dvinholm.App.csproj"));
        Assert.Contains("assets\\aurora-northern-coast.jpg", project,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Link=\"Assets\\Aurora-Northern-Coast.jpg\"", project);
        Assert.Contains("CopyToPublishDirectory=\"PreserveNewest\"", project);
        var bytes = File.ReadAllBytes(FindFile("assets/aurora-northern-coast.jpg"));
        Assert.True(bytes.Length > 10_000, "The real JPEG source must be bundled.");
        Assert.Equal((byte)0xFF, bytes[0]);
        Assert.Equal((byte)0xD8, bytes[1]);
        Assert.Equal((byte)0xFF, bytes[^2]);
        Assert.Equal((byte)0xD9, bytes[^1]);

        // The previous 1024-pixel banner had to be strongly enlarged on
        // high-DPI monitors. Verify this asset has enough raster samples for
        // the header's ~1040 DIP width near 200% Windows scaling. This does
        // not claim new native photographic detail or a real GUI pixel PASS.
        var (width, height) = ReadJpegFrameDimensions(bytes);
        Assert.InRange(width, 2000, 4096);
        Assert.InRange(height, 300, 700);
        Assert.InRange((double)width / height, 5.2, 6.2);
    }

    private static (int Width, int Height) ReadJpegFrameDimensions(ReadOnlySpan<byte> jpeg)
    {
        // Read the JPEG SOF0/SOF2 frame header; no native image codec, UI
        // window, network request or external image library is required.
        if (jpeg.Length < 12 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            throw new InvalidDataException("Missing JPEG SOI.");
        var offset = 2;
        while (offset + 4 <= jpeg.Length)
        {
            if (jpeg[offset++] != 0xFF)
                throw new InvalidDataException("Missing JPEG marker boundary.");
            while (offset < jpeg.Length && jpeg[offset] == 0xFF)
                offset++;
            if (offset >= jpeg.Length)
                break;
            var marker = jpeg[offset++];
            if (marker is 0xD8 or 0x01 or >= 0xD0 and <= 0xD7)
                continue;
            if (marker is 0xD9 or 0xDA)
                break;
            if (offset + 2 > jpeg.Length)
                break;
            var length = (jpeg[offset] << 8) | jpeg[offset + 1];
            if (length < 2 || offset + length > jpeg.Length)
                throw new InvalidDataException("Invalid JPEG segment length.");
            if (marker is >= 0xC0 and <= 0xCF &&
                marker is not (0xC4 or 0xC8 or 0xCC))
            {
                if (length < 7)
                    throw new InvalidDataException("Short JPEG SOF segment.");
                return ((jpeg[offset + 5] << 8) | jpeg[offset + 6],
                        (jpeg[offset + 3] << 8) | jpeg[offset + 4]);
            }
            offset += length;
        }
        throw new InvalidDataException("JPEG frame header not found.");
    }

    [Fact]
    public void ArtworkHasNoProgrammaticFakeStatusOrRemoteImageDependencies()
    {
        var art = Artwork(Layout());
        Assert.DoesNotContain(art.Descendants(),
            e => e.Name.LocalName is "TextBlock" or "Button" or "ComboBox");
        var image = art.Descendants().Single(e => e.Name.LocalName == "Image");
        var source = (string?)image.Attribute("Source");
        Assert.NotNull(source);
        Assert.StartsWith("ms-appx:///Assets/", source);
        Assert.DoesNotContain("http://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", source, StringComparison.OrdinalIgnoreCase);
        Assert.Null(image.Attribute("Tapped"));
        Assert.Null(image.Attribute("Click"));
    }

    [Fact]
    public void TintProtectsLiveLocalizedHeadingWhileRevealingAuroraSky()
    {
        var stops = Stops(Layout());
        Assert.Equal(new[] { 0.0, 0.42, 0.74, 1.0 },
            stops.Select(s => s.Offset).ToArray());
        Assert.Equal(4, stops.Length);
        Assert.True(stops[0].Alpha >= 0xF0, "White localized title needs dark left.");
        Assert.True(stops[1].Alpha >= 0xD0, "Description needs a dark central mask.");
        Assert.True(stops[2].Alpha < 0xB0, "Aurora must remain visible to the right.");
        Assert.True(stops[3].Alpha >= stops[2].Alpha,
            "Status and language controls need a darker right edge.");
    }

    [Fact]
    public void PhotoCannotInterceptNavigationOrReplaceTrueConnectionState()
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
        Assert.Null(art.Attribute("PointerPressed"));
        Assert.Equal("WorkspaceNavigate_Click", (string?)status.Attribute("Click"));
        Assert.Equal("DvinholmWorkspaceTitle",
            (string?)title.Attribute("AutomationProperties.AutomationId"));
    }
}
