using System.Xml.Linq;

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
