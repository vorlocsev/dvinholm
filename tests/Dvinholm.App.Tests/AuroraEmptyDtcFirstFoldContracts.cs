using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Offline source contracts for reducing the genuinely observed large empty
/// DTC table on the 2026-11-04 real 1280 x 800 DIP screenshots.
/// Does not assert a freshly rendered Windows screenshot.
/// </summary>
public sealed class AuroraEmptyDtcFirstFoldContracts
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string Source(string relative)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var file = Path.Combine(directory.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(relative);
    }

    private static string Presenter() => Source("src/Dvinholm.App/MainWindow.AuroraDtc.cs");

    private static XElement RecordsList()
    {
        using var stream = typeof(AuroraEmptyDtcFirstFoldContracts).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded MainWindow.xaml.");
        var doc = XDocument.Load(stream);
        return doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == "DtcRecordsView");
    }

    [Fact]
    public void EmptyListStartsCollapsedButRetainsRealReadHeightAndSelectionHandler()
    {
        var list = RecordsList();
        Assert.Equal("Collapsed", (string?)list.Attribute("Visibility"));
        Assert.Equal("260", (string?)list.Attribute("Height"));
        Assert.Equal("DtcRecordsView_SelectionChanged",
            (string?)list.Attribute("SelectionChanged"));
        Assert.Equal("Single", (string?)list.Attribute("SelectionMode"));
    }

    [Fact]
    public void ClearingStaleDiagnosticContextAlsoCollapsesEmptyList()
    {
        var source = Presenter();
        var start = source.IndexOf("private void ClearAuroraDtcTable()", StringComparison.Ordinal);
        var end = source.IndexOf("private void PublishAuroraDtcTable(", start,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var clear = source[start..end];
        var invalidate = clear.IndexOf("_lastAuroraDtcRead = null;", StringComparison.Ordinal);
        var clearRows = clear.IndexOf("_auroraDtcRows.Clear();", invalidate, StringComparison.Ordinal);
        var collapse = clear.IndexOf("DtcRecordsView.Visibility = Visibility.Collapsed;",
            clearRows, StringComparison.Ordinal);
        Assert.True(invalidate >= 0 && clearRows > invalidate && collapse > clearRows);
    }

    [Fact]
    public void RowsAppearOnlyWhenACompletedDtcReadProjectsActualRecords()
    {
        var source = Presenter();
        var start = source.IndexOf("private void RefreshAuroraDtcTableLocalization()",
            StringComparison.Ordinal);
        var end = source.IndexOf("private void DtcRecordsView_SelectionChanged(",
            start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var refresh = source[start..end];
        Assert.Contains("if (_lastAuroraDtcRead is not { } result)", refresh);
        Assert.Contains("DtcRecordsView.Visibility = Visibility.Collapsed;", refresh);
        Assert.Contains("AuroraDtcTableProjection.Project(result, _unifiedEnglish)", refresh);
        Assert.Contains("foreach (var row in rows) _auroraDtcRows.Add(row);", refresh);
        Assert.Contains("DtcRecordsView.Visibility = rows.Count == 0", refresh);
        Assert.Contains("? Visibility.Collapsed : Visibility.Visible;", refresh);
        Assert.Contains("DtcRecordsView.SelectedItem = selected;", refresh);
    }

    [Fact]
    public void ZeroReturnedDtcsNeverBecomeAFalseFaultFreeClaim()
    {
        var source = Presenter();
        Assert.Contains("ECU вернул 0 записей для выбранного фильтра. Это не доказывает отсутствие неисправностей.", source);
        Assert.Contains("This does not prove the vehicle is fault-free.", source);
        Assert.Contains("No DTCs read in the current diagnostic context.", source);
        Assert.Contains("The selected ECU returned 0 DTCs for this filter.", source);
    }

    [Fact]
    public void DtcFirstFoldProjectionHasNoVehicleMutationOrAutomaticScan()
    {
        var presenter = Presenter();
        foreach (var forbidden in new[]
        {
            "PassThruOpen(", "PassThruConnect(", "ReadDtc_Click(",
            "ClearDtc(", "ReadCemPin_Click(", "Discover_Click(",
            "UnifiedStageUpdate_Click("
        })
            Assert.DoesNotContain(forbidden, presenter);
        Assert.Contains("private void PublishAuroraDtcTable(UdsDtcReadResult result)", presenter);
    }
}
