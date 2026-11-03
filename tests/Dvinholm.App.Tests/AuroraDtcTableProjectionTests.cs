using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for projecting accepted single-ECU UDS DTC replies
/// into Aurora's bounded UI without fabricating OEM descriptions.
/// </summary>
public sealed class AuroraDtcTableProjectionTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string Source(string relative)
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(element => (string?)element.Attribute(X + "Name") == name);

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraDtcTableProjectionTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml resource missing");
        return XDocument.Load(stream);
    }

    [Fact]
    public void ReadRowsKeepExactSixHexDigitCodesAndNeverInferOemDescription()
    {
        var result = new UdsDtcReadResult(0xFF, new[]
        {
            new UdsDtcRecord(0x010203, 0x08),
            new UdsDtcRecord(0x000001, 0x01)
        });
        var rows = AuroraDtcTableProjection.Project(result);
        Assert.Equal(2, rows.Count);
        Assert.Equal("0x010203", rows[0].Code);
        Assert.Equal("0x000001", rows[1].Code);
        Assert.All(rows, row => Assert.Equal(AuroraDtcTableProjection.MissingDescription, row.Description));
        Assert.Contains("подтверждённый DTC", rows[0].Status);
        Assert.Contains("тест не пройден", rows[1].Status);
    }

    [Fact]
    public void AvailabilityMaskPreventsUnsupportedStatusBitFromBeingAsserted()
    {
        var read = new UdsDtcReadResult(0x08, new[] { new UdsDtcRecord(0x123456, 0x09) });
        var row = Assert.Single(AuroraDtcTableProjection.Project(read));
        Assert.Contains("0x09", row.Status);
        Assert.Contains("подтверждённый DTC", row.Status);
        Assert.Contains("неподдерживаемые биты 0x01", row.Status);
        Assert.DoesNotContain("тест не пройден", row.Status);
        Assert.Contains("Маска поддерживаемых ECU битов: 0x08", row.Details);
    }

    [Fact]
    public void ZeroStatusAndZeroAvailabilityDoNotClaimFaultFree()
    {
        var rows = AuroraDtcTableProjection.Project(
            new UdsDtcReadResult(0x00, new[] { new UdsDtcRecord(0x001234, 0x00) }));
        var row = Assert.Single(rows);
        Assert.Contains("Статус не подтверждён", row.Status);
        Assert.Contains("не подтверждены", row.Details);
        Assert.Empty(AuroraDtcTableProjection.Project(
            new UdsDtcReadResult(0x00, Array.Empty<UdsDtcRecord>())));
    }

    [Fact]
    public void InputIsValidatedAndOverflowCodesAreNeverTruncated()
    {
        Assert.Throws<ArgumentNullException>(() => AuroraDtcTableProjection.Project(null!));
        Assert.Throws<ArgumentNullException>(() => AuroraDtcTableProjection.Project(
            new UdsDtcReadResult(0xFF, null!)));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuroraDtcTableProjection.Project(
            new UdsDtcReadResult(0xFF, new[] { new UdsDtcRecord(0x1000000, 0x01) })));
    }

    [Fact]
    public void LargeResultsAreBoundedAndKeepOriginalRecordOrder()
    {
        var records = Enumerable.Range(0, AuroraDtcTableProjection.MaxVisibleRows + 10)
            .Select(i => new UdsDtcRecord((uint)i, 0x01)).ToArray();
        var rows = AuroraDtcTableProjection.Project(new UdsDtcReadResult(0xFF, records));
        Assert.Equal(AuroraDtcTableProjection.MaxVisibleRows, rows.Count);
        Assert.Equal("0x000000", rows[0].Code);
        Assert.Equal($"0x{AuroraDtcTableProjection.MaxVisibleRows - 1:X6}", rows[^1].Code);
    }

    [Fact]
    public void XamlOwnsOneRealBoundTableAndExplicitEmptyProvenanceWarning()
    {
        var doc = Layout();
        var table = Named(doc, "DtcRecordsView");
        Assert.Contains(Named(doc, "DtcPage"), table.Ancestors());
        Assert.Equal("DtcRecordsView_SelectionChanged", (string?)table.Attribute("SelectionChanged"));
        Assert.Contains("{Binding Code}", table.ToString());
        Assert.Contains("{Binding Status}", table.ToString());
        Assert.Contains("{Binding Description}", table.ToString());
        Assert.Contains("Подтверждённого каталога описаний Volvo пока нет", Named(doc, "DtcPage").ToString());
        Assert.NotNull(Named(doc, "DtcTableSummary"));
        Assert.NotNull(Named(doc, "DtcSelectedRecordDetails"));
    }

    [Fact]
    public void GenerationGuardOwnsPublishAndEveryClearDropsVisibleRows()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var presenter = Source("src/Dvinholm.App/MainWindow.AuroraDtc.cs");
        var start = source.IndexOf("if (!_udsResultLifetime.CanDisplay(resultGeneration))", StringComparison.Ordinal);
        var end = source.IndexOf("SetStatus(presentation.Activity)", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        Assert.Contains("onAcceptedResult?.Invoke();", source[start..end]);
        Assert.Contains("PublishAuroraDtcTable(completedRead)", source);
        Assert.Contains("ClearAuroraDtcTable();", source);
        Assert.Contains("_auroraDtcRows.Clear();", presenter);
        Assert.DoesNotContain("PassThru", presenter);
        Assert.DoesNotContain("ReadDtcResultByStatusMask(", presenter);
    }

    [Fact]
    public void DtcEraseStaysDisabledAndNoOemCauseMappingIsIntroduced()
    {
        var doc = Layout();
        var clear = Named(doc, "DtcClearButton");
        Assert.Equal("False", (string?)clear.Attribute("IsEnabled"));
        Assert.Null(clear.Attribute("Click"));
        var presenter = Source("src/Dvinholm.App/MainWindow.AuroraDtc.cs");
        Assert.Contains("Нет проверенного описания", presenter);
        Assert.DoesNotContain("0x14", presenter);
        Assert.DoesNotContain("ClearDiagnosticInformation", presenter);
        Assert.DoesNotContain("ServiceId", presenter);
    }

    // Stage 79: the eight ISO 14229 status flags must display in the chosen
    // language without changing raw ECU bytes, mask, code or evidence count.
    // Source-only/pure projection: no vehicle, J2534 or real screenshot.
    [Theory]
    [InlineData(0x01, "тест не пройден", "test failed")]
    [InlineData(0x02, "тест не пройден в текущем цикле", "failed this operation cycle")]
    [InlineData(0x04, "ожидающий DTC", "pending DTC")]
    [InlineData(0x08, "подтверждённый DTC", "confirmed DTC")]
    [InlineData(0x10, "тест не завершён с последней очистки", "test not completed since last clear")]
    [InlineData(0x20, "тест не пройден с последней очистки", "failed since last clear")]
    [InlineData(0x40, "тест не завершён в текущем цикле", "test not completed this operation cycle")]
    [InlineData(0x80, "запрошен индикатор неисправности", "warning indicator requested")]
    public void RussianDtcStatusBitsUseLocalizedObservedEvidence(
        byte status, string expectedRu, string expectedEn)
    {
        var read = new UdsDtcReadResult(0xFF, new[] {
            new UdsDtcRecord(0x012345, status)
        });
        var ru = Assert.Single(AuroraDtcTableProjection.Project(read, english: false));
        var en = Assert.Single(AuroraDtcTableProjection.Project(read, english: true));

        Assert.Equal("0x012345", ru.Code);
        Assert.Equal(ru.Code, en.Code);
        Assert.Equal(AuroraDtcTableProjection.MissingDescription, ru.Description);
        Assert.Equal(AuroraDtcTableProjection.MissingDescriptionEnglish, en.Description);
        Assert.Contains($"0x{status:X2}:", ru.Status);
        Assert.Contains($"0x{status:X2}:", en.Status);
        Assert.Contains(expectedRu, ru.Status);
        Assert.Contains(expectedEn, en.Status);
        Assert.Contains(expectedRu, ru.Details);
        Assert.Contains(expectedEn, en.Details);
        Assert.Contains("Маска поддерживаемых ECU битов: 0xFF", ru.Details);
        Assert.Contains("ECU-supported mask: 0xFF", en.Details);

        // Presentation only. Exact flags remain ECU-provided, while DTC
        // erasure and unknown OEM fault diagnoses are still unavailable.
        var doc = Layout();
        Assert.Equal("False", (string?)Named(doc, "DtcClearButton").Attribute("IsEnabled"));
        Assert.Null(Named(doc, "DtcClearButton").Attribute("Click"));
        var source = Source("src/Dvinholm.App/MainWindow.AuroraDtc.cs");
        Assert.Contains("UdsDtcStatusFormatter.Format(supported, english)", source);
        Assert.DoesNotContain("PassThruWriteMsgs(", source);
    }
}
