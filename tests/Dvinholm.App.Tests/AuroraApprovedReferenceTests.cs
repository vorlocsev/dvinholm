using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for the user-approved four-screen Aurora design.
/// Visual/sizing acceptance remains a separate installed-Windows review.
/// Never connects J2534 or sends ECU traffic.
/// </summary>
public sealed class AuroraApprovedReferenceTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraApprovedReferenceTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow XAML resource missing");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static string ReadSource(string file)
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName, file.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(file);
    }

    [Fact]
    public void ApprovedPrimaryCemPageAndEcuPageRemainDistinct()
    {
        var doc = Layout();
        var config = Named(doc, "CemConfigPage");
        var ecu = Named(doc, "EcuPage");
        Assert.NotSame(config, ecu);
        Assert.DoesNotContain(config, ecu.Ancestors());
        Assert.DoesNotContain(ecu, config.Ancestors());
        Assert.Equal("Collapsed", (string?)config.Attribute("Visibility"));
        Assert.Equal("Collapsed", (string?)ecu.Attribute("Visibility"));
    }

    [Fact]
    public void CEMUsesThreePaneResearchBrowserWithExistingSourceAction()
    {
        var doc = Layout();
        foreach (var name in new[] { "CemConfigCategory", "CemConfigSearch",
            "CemConfigResults", "CemConfigDetailTitle", "CemConfigDetailEvidence" })
            Assert.Contains(Named(doc, name), Named(doc, "CemConfigPage").Descendants());
        Assert.Equal("P2ConfigCatalog_Click", (string?)Named(doc, "P2ConfigCatalogButton").Attribute("Click"));
        var results = Named(doc, "CemConfigResults");
        Assert.Equal("CemConfigResults_SelectionChanged", (string?)results.Attribute("SelectionChanged"));
    }

    [Fact]
    public void UnverifiedWriteBackupAndDiffRemainActuallyDisabled()
    {
        var doc = Layout();
        var page = Named(doc, "CemConfigPage");
        foreach (var content in new[] { "Резервная копия", "Сравнить", "Подготовить изменения" })
        {
            var button = page.Descendants().Single(e =>
                e.Name.LocalName == "Button" && (string?)e.Attribute("Content") == content);
            Assert.Equal("False", (string?)button.Attribute("IsEnabled"));
            Assert.Null(button.Attribute("Click"));
        }
        Assert.Contains("Чтение, изменение и запись CEM пока заблокированы", page.ToString());
    }

    [Fact]
    public void ConfigurationRowsNeverManufactureRawOrLiveValues()
    {
        var source = ReadSource("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        Assert.Contains("VolvoP2CemConfigOptionManifest.Entries", source);
        Assert.Contains("public string Current => \"Не прочитано\"", source);
        Assert.Contains("public string Proposed => \"—\"", source);
        Assert.Contains("Values", source);
        Assert.DoesNotContain("PassThru", source);
        Assert.DoesNotContain("SendAsync(", source);
        Assert.DoesNotContain("WriteMemory", source);
    }

    [Fact]
    public void VehicleIllustrationIsStrictlySideOnAndHasTwoModelVariants()
    {
        var doc = Layout();
        Assert.Contains(Named(doc, "EcuP2SideProfile"), Named(doc, "EcuPage").Descendants());
        Assert.Contains(Named(doc, "EcuP3SideProfile"), Named(doc, "EcuPage").Descendants());
        Assert.Equal("Collapsed", (string?)Named(doc, "EcuP3SideProfile").Attribute("Visibility"));
        Assert.Equal("EcuVehicleModel_SelectionChanged",
            (string?)Named(doc, "EcuVehicleModel").Attribute("SelectionChanged"));
        Assert.Equal(2, Named(doc, "EcuVehicleModel").Elements()
            .Count(e => e.Name.LocalName == "ComboBoxItem"));
        Assert.All(new[] { "EcuP2SideProfile", "EcuP3SideProfile" }, name =>
            Assert.True(Named(doc, name).Descendants().Any(e => e.Name.LocalName == "Ellipse")));
    }

    [Fact]
    public void SelectingVehicleDrawingDoesNotAssertPhysicalModuleIdentity()
    {
        var doc = Layout();
        var page = Named(doc, "EcuPage").ToString();
        Assert.Contains("не указывает физические позиции", page);
        Assert.Contains("не подтверждённого сопоставления", page);
        var source = ReadSource("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        var start = source.IndexOf("private void EcuVehicleModel_SelectionChanged(", StringComparison.Ordinal);
        Assert.True(start > 0);
        var handler = source[start..];
        Assert.DoesNotContain("_session", handler);
        Assert.DoesNotContain("Connect_Click", handler);
        Assert.DoesNotContain("ReadDtc_Click", handler);
        Assert.DoesNotContain("ReadCemPin", handler);
    }

    [Fact]
    public void ExistingPassiveDiscoveryAndResultsStillOwnTheirCallbacks()
    {
        var doc = Layout();
        foreach (var (name, action) in new[]
        {
            ("DiscoverButton", "Discover_Click"),
            ("CancelDiscoveryButton", "CancelDiscovery_Click"),
            ("EcuSelector", "EcuSelector_SelectionChanged"),
            ("EcusView", "Ecu_SelectionChanged")
        })
        {
            var element = Named(doc, name);
            var eventName = name is "EcuSelector" or "EcusView"
                ? "SelectionChanged" : "Click";
            Assert.Equal(action, (string?)element.Attribute(eventName));
        }
        Assert.Contains(Named(doc, "EcuSelectionInfo"), Named(doc, "EcuPage").Descendants());
    }

    [Fact]
    public void ReferenceIsWrittenDownAndAllNamesAreUnique()
    {
        var design = ReadSource("docs/design/aurora-approved/README.md");
        Assert.Contains("One primary function", design);
        Assert.Contains("side-on", design);
        Assert.Contains("not copied as a full-resolution raster", design);
        var doc = Layout();
        var names = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(n => n is not null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        var main = ReadSource("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("InitializeAuroraConfigReference();", main);
    }
}
