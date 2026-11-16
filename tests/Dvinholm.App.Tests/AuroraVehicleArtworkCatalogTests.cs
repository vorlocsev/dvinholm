using System.Xml.Linq;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Offline acceptance contracts for manually selected vehicle illustrations.
/// No VIN, CAN, J2534 or ECU side effects; Windows visual inspection is separate.
/// </summary>
public sealed class AuroraVehicleArtworkCatalogTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

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

    private static string FindSourcePath(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return file;
        }
        throw new FileNotFoundException(relative);
    }

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraVehicleArtworkCatalogTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded MainWindow.xaml missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument document, string id) =>
        document.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);

    [Fact]
    public void CatalogContainsNineDistinctManualModelChoices()
    {
        var models = AuroraVehicleArtworkCatalog.Models;
        Assert.Equal(9, models.Count);
        Assert.Equal(9, models.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(new[] { "xc90-p2", "xc70-p2", "v70-p2", "s60-p2",
            "s80-p2", "xc90-spa", "xc70-p3", "v70-p3", "s80-p3" },
            models.Select(m => m.Id));
    }

    [Fact]
    public void Xc90SecondGenerationIsSpaAndNeverMisrepresentedAsP3()
    {
        var models = AuroraVehicleArtworkCatalog.Models;
        Assert.Equal("SPA", models.Single(m => m.Id == "xc90-spa").Platform);
        Assert.Equal("P2", models.Single(m => m.Id == "xc90-p2").Platform);
        Assert.All(models.Where(m => m.Id.EndsWith("-p3", StringComparison.Ordinal)),
            m => Assert.Equal("P3", m.Platform));
    }

    [Fact]
    public void ModelImageUrisAreDistinctAndOffline()
    {
        var uris = AuroraVehicleArtworkCatalog.Models.Select(AuroraVehicleArtworkCatalog.ImageUri).ToArray();
        Assert.Equal(9, uris.Distinct(StringComparer.Ordinal).Count());
        Assert.All(uris, uri => Assert.StartsWith("ms-appx:///Assets/Vehicles/", uri));
        Assert.Equal(AuroraVehicleArtworkCatalog.Asset, uris[0]);
    }

    [Fact]
    public void UnknownModelFallsBackToExistingXc90P2Artwork()
    {
        var expected = AuroraVehicleArtworkCatalog.Models[0];
        Assert.Same(expected, AuroraVehicleArtworkCatalog.Resolve(null));
        Assert.Same(expected, AuroraVehicleArtworkCatalog.Resolve("unknown-model"));
        Assert.Same(AuroraVehicleArtworkCatalog.Models[5],
            AuroraVehicleArtworkCatalog.Resolve("xc90-spa"));
    }

    [Fact]
    public void XamlSelectorMapsExactlyToCatalogWithoutImplicitVehicleDetection()
    {
        var doc = Layout();
        var selector = Named(doc, "EcuVehicleModel");
        Assert.Equal("0", (string?)selector.Attribute("SelectedIndex"));
        Assert.Equal("EcuVehicleModel_SelectionChanged",
            (string?)selector.Attribute("SelectionChanged"));
        Assert.Equal(AuroraVehicleArtworkCatalog.Models.Select(m => m.Id),
            selector.Elements().Select(e => (string?)e.Attribute("Tag")));
        Assert.DoesNotContain("VIN", selector.ToString());
        Assert.DoesNotContain("ECM", selector.ToString());
    }

    [Fact]
    public void NineGeneratedJpgsAreRealUniqueFilesAndPackaged()
    {
        var digests = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in AuroraVehicleArtworkCatalog.Models)
        {
            var bytes = File.ReadAllBytes(FindSourcePath("assets/vehicles/" + model.Id + ".jpg"));
            Assert.InRange(bytes.Length, 20_000, 2_000_000);
            Assert.Equal((byte)0xff, bytes[0]);
            Assert.Equal((byte)0xd8, bytes[1]);
            Assert.Equal((byte)0xff, bytes[2]);
            Assert.Equal((byte)0xff, bytes[^2]);
            Assert.Equal((byte)0xd9, bytes[^1]);
            Assert.True(digests.Add(Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(bytes))));
        }
        var project = Source("src/Dvinholm.App/Dvinholm.App.csproj");
        Assert.Contains(@"assets\vehicles\*.jpg", project);
        Assert.Contains(@"Assets\Vehicles\%(Filename)%(Extension)", project);
        Assert.DoesNotContain("volvo-model-atlas.svg", project);
    }


    private static (int Width, int Height) ReadJpegFrameDimensions(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12 || data[0] != 0xFF || data[1] != 0xD8)
            throw new InvalidDataException("Not a JPEG image.");
        var position = 2;
        while (position + 4 <= data.Length)
        {
            if (data[position++] != 0xFF)
                throw new InvalidDataException("Invalid JPEG marker boundary.");
            while (position < data.Length && data[position] == 0xFF) position++;
            if (position >= data.Length) break;
            var marker = data[position++];
            if (marker is 0xDA or 0xD9) break;
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
                continue;
            if (position + 2 > data.Length)
                throw new InvalidDataException("Truncated JPEG segment.");
            var length = (data[position] << 8) | data[position + 1];
            if (length < 2 || position + length > data.Length)
                throw new InvalidDataException("Invalid JPEG segment length.");
            // SOF markers carry actual pixel dimensions (not JFIF display density).
            if (marker is >= 0xC0 and <= 0xCF &&
                marker is not (0xC4 or 0xC8 or 0xCC))
            {
                if (length < 7)
                    throw new InvalidDataException("Truncated JPEG frame header.");
                var height = (data[position + 3] << 8) | data[position + 4];
                var width = (data[position + 5] << 8) | data[position + 6];
                if (width == 0 || height == 0)
                    throw new InvalidDataException("Empty JPEG frame.");
                return (width, height);
            }
            position += length;
        }
        throw new InvalidDataException("JPEG start-of-frame marker missing.");
    }

    [Fact]
    public void ReviewedJpgManifestPinsNineModelIdsPixelsAndContentDigests()
    {
        using var manifest = JsonDocument.Parse(Source("assets/vehicles/manifest.json"));
        var entries = manifest.RootElement.EnumerateArray().ToArray();
        var models = AuroraVehicleArtworkCatalog.Models;
        Assert.Equal(models.Count, entries.Length);
        Assert.Equal(models.Select(model => model.Id),
            entries.Select(entry => entry.GetProperty("modelId").GetString()));

        var folder = Path.GetDirectoryName(FindSourcePath("assets/vehicles/xc90-p2.jpg"))!;
        Assert.Equal(9, Directory.EnumerateFiles(folder, "*.jpg").Count());
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (model, entry) in models.Zip(entries))
        {
            Assert.Equal(model.Label, entry.GetProperty("label").GetString());
            Assert.Equal(model.Id + ".jpg", entry.GetProperty("file").GetString());
            Assert.Equal("AI-generated reference render; not verified OEM geometry",
                entry.GetProperty("source").GetString());

            var bytes = File.ReadAllBytes(FindSourcePath("assets/vehicles/" + model.Id + ".jpg"));
            Assert.Equal(entry.GetProperty("bytes").GetInt32(), bytes.Length);
            var (width, height) = ReadJpegFrameDimensions(bytes);
            Assert.Equal(1280, width);
            Assert.Equal(510, height);
            Assert.Equal(entry.GetProperty("width").GetInt32(), width);
            Assert.Equal(entry.GetProperty("height").GetInt32(), height);

            var digest = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            Assert.Equal(entry.GetProperty("sha256").GetString(), digest);
            Assert.True(hashes.Add(digest), "Two vehicle images are byte-for-byte identical.");
        }
    }

    [Fact]
    public void S80P2UsesTheReframedDarkBlueStudioImageBaseline()
    {
        var model = AuroraVehicleArtworkCatalog.Resolve("s80-p2");
        Assert.Equal("Volvo S80 I · P2", model.Label);
        Assert.Equal("ms-appx:///Assets/Vehicles/s80-p2.jpg",
            AuroraVehicleArtworkCatalog.ImageUri(model));

        // Pin the exact reviewed generated JPG bytes, not OEM accuracy.
        using var manifest = JsonDocument.Parse(Source("assets/vehicles/manifest.json"));
        var entry = manifest.RootElement.EnumerateArray()
            .Single(element => element.GetProperty("modelId").GetString() == model.Id);
        Assert.Equal(92122, entry.GetProperty("bytes").GetInt32());
        Assert.Equal("7f3e107e0003ca5148b69934d4b01a46c10e4c2f4126e17d98e934c16059bc54",
            entry.GetProperty("sha256").GetString());
        Assert.Equal("AI-generated reference render; not verified OEM geometry",
            entry.GetProperty("source").GetString());
    }

    [Fact]
    public void S80P3UsesTheReframedSilverStudioImageBaseline()
    {
        var model = AuroraVehicleArtworkCatalog.Resolve("s80-p3");
        Assert.Equal("Volvo S80 II · P3", model.Label);
        Assert.Equal("ms-appx:///Assets/Vehicles/s80-p3.jpg",
            AuroraVehicleArtworkCatalog.ImageUri(model));

        // Image-change gate: a different source image must be deliberately
        // reviewed and this baseline updated. This is NOT OEM visual approval.
        using var manifest = JsonDocument.Parse(Source("assets/vehicles/manifest.json"));
        var entry = manifest.RootElement.EnumerateArray()
            .Single(element => element.GetProperty("modelId").GetString() == model.Id);
        Assert.Equal(99677, entry.GetProperty("bytes").GetInt32());
        Assert.Equal("50edd340437d40dfc7f925db9c8542c713470bc1471374fe1a9a9b6d9fc96956",
            entry.GetProperty("sha256").GetString());
        Assert.Equal("AI-generated reference render; not verified OEM geometry",
            entry.GetProperty("source").GetString());
    }

    [Fact]
    public void JpgViewportPreservesEcuEvidenceOutside()
    {
        var doc = Layout();
        var artboard = Named(doc, "EcuVehicleArtboard");
        var image = Named(doc, "EcuVehicleArtworkAtlas");
        Assert.Contains(artboard, image.Ancestors());
        Assert.Equal("640", (string?)artboard.Attribute("Width"));
        Assert.Equal("255", (string?)artboard.Attribute("Height"));
        Assert.Equal("640", (string?)image.Attribute("Width"));
        Assert.Equal("255", (string?)image.Attribute("Height"));
        Assert.Equal(AuroraVehicleArtworkCatalog.Asset, (string?)image.Attribute("Source"));
        Assert.Equal("Uniform", (string?)image.Attribute("Stretch"));
        Assert.DoesNotContain("TranslateTransform", image.ToString());
        Assert.Contains("RectangleGeometry", artboard.ToString());
        Assert.DoesNotContain(artboard, Named(doc, "EcuObservationRail").Ancestors());
        Assert.Contains("не указывает физические позиции ECU",
            Named(doc, "EcuDiagramDisclaimer").ToString());
        var fallback = Named(doc, "EcuVehicleArtworkFallbackNotice");
        Assert.Contains(artboard, fallback.Ancestors());
        Assert.Contains("Изображение модели временно недоступно", fallback.ToString());
        Assert.Contains("EcuVehicleArtworkFallbackNotice.Text = L(",
            Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs"));
    }

    [Fact]
    public void ModelSwitchAndRuEnReprojectionDoNotChangeDiagnosticContext()
    {
        var select = Source("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        var localization = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        var begin = select.IndexOf("private void EcuVehicleModel_SelectionChanged(", StringComparison.Ordinal);
        Assert.True(begin >= 0);
        var handler = select[begin..];
        Assert.Contains("RefreshAuroraVehicleArtwork()", handler);
        Assert.Contains("AuroraVehicleArtworkCatalog.Resolve(id)", handler);
        Assert.Contains("AuroraVehicleArtworkCatalog.ImageUri(model)", handler);
        Assert.Contains("new BitmapImage(", handler);
        Assert.Contains("_auroraVehicleArtworkLoaded = false;", handler);
        foreach (var forbidden in new[] { "_session", "Connect_Click(", "ReadDtc_Click(",
            "UdsRequestId.Text =", "EcusView.SelectedItem =", "PassThru", "SendAsync(" })
            Assert.DoesNotContain(forbidden, handler);
        Assert.Contains("RefreshAuroraVehicleArtwork();", localization);
        Assert.DoesNotContain("EcuVehicleModel.SelectedIndex =", localization);
        Assert.Contains("reference render, selected manually", select);
    }


    [Fact]
    public void SwitchingRuEnWithTheSameManualModelDoesNotReloadJpg()
    {
        var reference = Source("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        var localization = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        var start = reference.IndexOf("private void RefreshAuroraVehicleArtwork()", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var method = reference[start..];
        Assert.Contains("private string? _auroraSelectedVehicleArtworkId;", reference);
        Assert.Contains("string.Equals(_auroraSelectedVehicleArtworkId, model.Id,", method);
        Assert.Contains("_auroraSelectedVehicleArtworkId = model.Id;", method);
        Assert.Contains("StringComparison.Ordinal", method);
        Assert.Contains("EcuVehicleArtworkAtlas.Source = new BitmapImage(", method);

        // The decode gate is reset only within the model-change guard,
        // never by merely replacing Russian/English caption text.
        var guard = method.IndexOf("if (!string.Equals(_auroraSelectedVehicleArtworkId", StringComparison.Ordinal);
        var reset = method.IndexOf("_auroraVehicleArtworkLoaded = false;", StringComparison.Ordinal);
        var setSource = method.IndexOf("EcuVehicleArtworkAtlas.Source = new BitmapImage(", StringComparison.Ordinal);
        var caption = method.IndexOf("EcuDiagramCaption.Text = L(", StringComparison.Ordinal);
        Assert.True(guard >= 0 && guard < reset && reset < setSource && setSource < caption);
        Assert.Contains("RefreshAuroraVehicleArtwork();", localization);
        Assert.DoesNotContain("EcuVehicleArtworkAtlas.Source =", localization);
        Assert.DoesNotContain("_auroraSelectedVehicleArtworkId =", localization);
    }
    [Fact]
    public void SourceLoadEventsRevealFallbackAndExposeActualDecodeStatusToUia()
    {
        var xaml = Layout();
        var art = Named(xaml, "EcuVehicleArtworkAtlas");
        var caption = Named(xaml, "EcuDiagramCaption");
        Assert.Equal("EcuVehicleArtworkAtlas_ImageOpened",
            (string?)art.Attribute("ImageOpened"));
        Assert.Equal("EcuVehicleArtworkAtlas_ImageFailed",
            (string?)art.Attribute("ImageFailed"));
        Assert.Equal("DvinholmVehicleAtlasPending",
            (string?)caption.Attribute("AutomationProperties.HelpText"));

        var source = Source("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        Assert.Contains("private void EcuVehicleArtworkAtlas_ImageOpened(", source);
        Assert.Contains("private void EcuVehicleArtworkAtlas_ImageFailed(", source);
        Assert.Contains("_auroraVehicleArtworkLoaded = true;", source);
        Assert.Contains("_auroraVehicleArtworkLoaded = false;", source);
        Assert.Contains("image.Opacity = 0;", source);
        Assert.Contains("image.Opacity = 1;", source);
        Assert.Contains("AutomationProperties.SetHelpText(EcuDiagramCaption,", source);
        Assert.Contains("DvinholmVehicleAtlasLoaded", source);
        Assert.Contains("DvinholmVehicleAtlasUnavailable", source);
        Assert.Contains("RefreshAuroraVehicleArtworkLoadState();", source);
        Assert.DoesNotContain("ReadDtc_Click(", source);
        Assert.DoesNotContain("PassThru", source);
    }
}
