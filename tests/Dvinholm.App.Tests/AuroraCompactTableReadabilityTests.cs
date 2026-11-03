using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline UI density contracts for CEM/DTC tables. No diagnostic
/// operations, synthetic ECU data, or desktop screenshot acceptance is implied.
/// </summary>
public sealed class AuroraCompactTableReadabilityTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraCompactTableReadabilityTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded MainWindow.xaml missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static XElement HeaderGrid(XElement list) =>
        list.Descendants().Single(e => e.Name.LocalName == "ListView.Header")
            .Elements().Single(e => e.Name.LocalName == "Grid");

    private static XElement ItemGrid(XElement list) =>
        list.Descendants().Single(e => e.Name.LocalName == "DataTemplate")
            .Elements().Single(e => e.Name.LocalName == "Grid");

    private static string[] Widths(XElement grid) =>
        grid.Elements().Single(e => e.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements().Select(e => (string)e.Attribute("Width")!).ToArray();

    private static IReadOnlyDictionary<string, string> Setters(XDocument doc, string state) =>
        Named(doc, state).Descendants().Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    [Fact]
    public void CemHeaderRetainsFourResearchFieldsButReservesMoreNameWidth()
    {
        var doc = Layout();
        var cem = Named(doc, "CemConfigResults");
        Assert.Contains(Named(doc, "CemConfigPage"), cem.Ancestors());
        Assert.Equal(new[] { "*", "76", "76", "60" }, Widths(HeaderGrid(cem)));
        Assert.Equal("8", (string?)HeaderGrid(cem).Attribute("ColumnSpacing"));
        Assert.Contains(Named(doc, "CemConfigListParameterHeader"), HeaderGrid(cem).Descendants());
    }

    [Fact]
    public void CemRowsMatchHeaderWidthsAndKeepAllRealProvenanceBindings()
    {
        var doc = Layout();
        var item = ItemGrid(Named(doc, "CemConfigResults"));
        Assert.Equal(new[] { "*", "76", "76", "60" }, Widths(item));
        Assert.Equal("8", (string?)item.Attribute("ColumnSpacing"));
        Assert.Contains("{Binding Code}", item.ToString());
        Assert.Contains("{Binding Name}", item.ToString());
        Assert.Contains("{Binding Initial}", item.ToString());
        Assert.Contains("{Binding Current}", item.ToString());
        Assert.Contains("{Binding Proposed}", item.ToString());
    }

    [Fact]
    public void CemResearchHeadingsRemainAlignedWithFourCells()
    {
        var doc = Layout();
        var header = HeaderGrid(Named(doc, "CemConfigResults"));
        foreach (var (name, col) in new[] {
            ("CemConfigListParameterHeader", "0"),
            ("CemConfigListInitialHeader", "1"),
            ("CemConfigListCurrentHeader", "2"),
            ("CemConfigListProposedHeader", "3") })
        {
            var label = Named(doc, name);
            Assert.Contains(label, header.Descendants());
            Assert.Equal(col, (string?)label.Attribute("Grid.Column") ?? "0");
        }
        Assert.Equal("310", (string?)Named(doc, "CemConfigResults").Attribute("Height"));
    }

    [Fact]
    public void DtcHeaderPreservesCodeAndFlexibleDescriptionAndStatus()
    {
        var doc = Layout();
        var results = Named(doc, "DtcResultsPane");
        Assert.Contains(Named(doc, "DtcPage"), results.Ancestors());
        var header = Named(doc, "DtcCodeHeading").Parent!;
        Assert.Equal("Grid", header.Name.LocalName);
        Assert.Equal(new[] { "96", "*", "124" }, Widths(header));
        Assert.Equal("8", (string?)header.Attribute("ColumnSpacing"));
        Assert.Contains(Named(doc, "DtcDescriptionHeading"), header.Elements());
        Assert.Contains(Named(doc, "DtcStatusHeading"), header.Elements());
    }

    [Fact]
    public void DtcRowsMatchHeaderAndNeverHideRawStatusOrCode()
    {
        var doc = Layout();
        var list = Named(doc, "DtcRecordsView");
        var grid = ItemGrid(list);
        Assert.Equal(new[] { "96", "*", "124" }, Widths(grid));
        Assert.Equal("8", (string?)grid.Attribute("ColumnSpacing"));
        Assert.Contains("{Binding Code}", grid.ToString());
        Assert.Contains("{Binding Description}", grid.ToString());
        Assert.Contains("{Binding Status}", grid.ToString());
        Assert.Contains("TextWrapping=\"Wrap\"", grid.ToString());
        Assert.Equal("260", (string?)list.Attribute("Height"));
    }

    [Fact]
    public void BothCompactPagesRetainVerticalStackInsteadOfFixedWideClipping()
    {
        var doc = Layout();
        var cem = Setters(doc, "CemReferenceStacked");
        Assert.Equal("0", cem["CemReferenceValuesColumn.Width"]);
        Assert.Equal("0", cem["CemReferenceDetailColumn.Width"]);
        Assert.Equal("1", cem["CemReferenceValuesPane.(Grid.Row)"]);
        Assert.Equal("2", cem["CemReferenceDetailPane.(Grid.Row)"]);
        var dtc = Setters(doc, "DtcStacked");
        Assert.Equal("0", dtc["DtcDetailColumn.Width"]);
        Assert.Equal("1", dtc["DtcDetailsPane.(Grid.Row)"]);
    }

    [Fact]
    public void DenseTablesNeverEnableUnverifiedCemWritesOrDtcErase()
    {
        var doc = Layout();
        foreach (var key in new[] { "CemConfigReadButton", "CemConfigBackupButton",
            "CemConfigCompareButton", "CemConfigPrepareButton", "CemConfigWriteButton",
            "DtcClearButton" })
        {
            var btn = Named(doc, key);
            Assert.Equal("False", (string?)btn.Attribute("IsEnabled"));
            Assert.Null(btn.Attribute("Click"));
        }
        Assert.Equal("ReadDtc_Click", (string?)Named(doc, "ReadDtcButton").Attribute("Click"));
        Assert.Equal("P2ConfigCatalog_Click",
            (string?)Named(doc, "P2ConfigCatalogButton").Attribute("Click"));
    }

    [Fact]
    public void TableDensityDoesNotChangeSixScreenNavigationOrSessionEvidence()
    {
        var doc = Layout();
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.Contains(Named(doc, "CemIdentityEvidenceConfigText"),
            Named(doc, "CemConfigPage").Descendants());
        Assert.Contains(Named(doc, "DtcEvidenceDisclaimer"),
            Named(doc, "DtcPage").Descendants());
        Assert.Contains(Named(doc, "ShellConnectionDot"),
            Named(doc, "WorkspaceHeaderActions").Descendants());
        Assert.DoesNotContain(doc.Descendants(), e =>
            (string?)e.Attribute(X + "Name") is "SettingsPage" or "LauncherPage");
        var names = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(name => name is not null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }
}
