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
    public void AtlasCellsCoverTheThreeByThreeGridWithoutOverlap()
    {
        var models = AuroraVehicleArtworkCatalog.Models;
        Assert.All(models, m =>
        {
            Assert.InRange(m.AtlasColumn, 0, 2);
            Assert.InRange(m.AtlasRow, 0, 2);
        });
        Assert.Equal(9, models.Select(m => (m.AtlasColumn, m.AtlasRow)).Distinct().Count());
        Assert.Equal(640, AuroraVehicleArtworkCatalog.CellWidth);
        Assert.Equal(255, AuroraVehicleArtworkCatalog.CellHeight);
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
    public void PackagedSvgContainsNineIndividualStudioReferenceIllustrations()
    {
        var svg = XDocument.Parse(Source("assets/vehicles/volvo-model-atlas.svg"));
        Assert.Equal("svg", svg.Root?.Name.LocalName);
        Assert.Equal("0 0 2880 1140", (string?)svg.Root?.Attribute("viewBox"));
        var modelTiles = svg.Descendants().Where(e => e.Name.LocalName == "g" &&
            (string?)e.Attribute("transform") is { } pos &&
            pos.StartsWith("translate(", StringComparison.Ordinal)).ToArray();
        Assert.Equal(9, modelTiles.Length);
        foreach (var model in AuroraVehicleArtworkCatalog.Models)
            Assert.Contains(model.Id, svg.ToString());
    }

    [Fact]
    public void ImageViewportClipsOnlyOneAtlasTileAndPreservesEcuEvidenceOutside()
    {
        var doc = Layout();
        var artboard = Named(doc, "EcuVehicleArtboard");
        var image = Named(doc, "EcuVehicleArtworkAtlas");
        Assert.Contains(artboard, image.Ancestors());
        Assert.Equal("640", (string?)artboard.Attribute("Width"));
        Assert.Equal("255", (string?)artboard.Attribute("Height"));
        Assert.Equal("1920", (string?)image.Attribute("Width"));
        Assert.Equal("765", (string?)image.Attribute("Height"));
        Assert.Equal(AuroraVehicleArtworkCatalog.Asset, (string?)image.Attribute("Source"));
        Assert.Contains("RectangleGeometry", artboard.ToString());
        Assert.DoesNotContain(artboard, Named(doc, "EcuObservationRail").Ancestors());
        Assert.Contains("не указывает физические позиции ECU",
            Named(doc, "EcuDiagramDisclaimer").ToString());
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
        Assert.Contains("offset.X = -model.AtlasColumn", handler);
        Assert.Contains("offset.Y = -model.AtlasRow", handler);
        foreach (var forbidden in new[] { "_session", "Connect_Click(", "ReadDtc_Click(",
            "UdsRequestId.Text =", "EcusView.SelectedItem =", "PassThru", "SendAsync(" })
            Assert.DoesNotContain(forbidden, handler);
        Assert.Contains("RefreshAuroraVehicleArtwork();", localization);
        Assert.DoesNotContain("EcuVehicleModel.SelectedIndex =", localization);
        Assert.Contains("reference render, selected manually", select);
    }
}
