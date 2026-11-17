using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Stage 80: eight offline bilingual evidence checks on the actual Aurora
/// DTC details presenter. No J2534 device, live ECU or screenshot is used.
/// </summary>
public sealed class AuroraDtcEvidenceDetailLocalizationTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData(0xFF, 0x08)]
    [InlineData(0xFF, 0x0D)]
    [InlineData(0x08, 0x09)]
    [InlineData(0x00, 0x80)]
    public void RawCodeStatusAndEcuMaskStayIdenticalInBothLanguages(
        int available, int rawStatus)
    {
        var read = new UdsDtcReadResult((byte)available,
            new[] { new UdsDtcRecord(0x123456, (byte)rawStatus) });
        var ru = Assert.Single(AuroraDtcTableProjection.Project(read, english: false));
        var en = Assert.Single(AuroraDtcTableProjection.Project(read, english: true));

        Assert.Equal("0x123456", ru.Code);
        Assert.Equal(ru.Code, en.Code);
        Assert.Equal(ru.Status[..5], en.Status[..5]);
        Assert.Contains($"Исходный статус: 0x{rawStatus:X2}", ru.Details);
        Assert.Contains($"Маска поддерживаемых ECU битов: 0x{available:X2}", ru.Details);
        Assert.Contains("Поддерживаемые биты статуса:", ru.Details);
        Assert.Contains($"Raw status: 0x{rawStatus:X2}", en.Details);
        Assert.Contains($"ECU-supported mask: 0x{available:X2}", en.Details);
        Assert.Contains("Supported status:", en.Details);
        Assert.DoesNotContain("Raw status:", ru.Details);
        Assert.DoesNotContain("ECU-supported mask:", ru.Details);
        Assert.DoesNotContain("Supported status:", ru.Details);
        Assert.Equal(AuroraDtcTableProjection.MissingDescription, ru.Description);
        Assert.Equal(AuroraDtcTableProjection.MissingDescriptionEnglish, en.Description);
    }

    [Fact]
    public void EmptyStatusDoesNotBecomeVerifiedOrFaultFreeThroughTranslation()
    {
        var read = new UdsDtcReadResult(0x00,
            new[] { new UdsDtcRecord(0x000001, 0x00) });
        var ru = Assert.Single(AuroraDtcTableProjection.Project(read, english: false));
        var en = Assert.Single(AuroraDtcTableProjection.Project(read, english: true));
        Assert.Contains("Статус не подтверждён", ru.Status);
        Assert.Contains("Status not verified", en.Status);
        Assert.Contains("Статус не подтверждён", ru.Details);
        Assert.Contains("Status not verified", en.Details);
        Assert.DoesNotContain("неисправностей нет", ru.Details);
        Assert.DoesNotContain("fault-free", en.Details);
    }

    [Fact]
    public void UnsupportedBitsAreExcludedFromBothLocalizedSupportedFlagLists()
    {
        var read = new UdsDtcReadResult(0x08,
            new[] { new UdsDtcRecord(0x123456, 0x09) });
        var ru = Assert.Single(AuroraDtcTableProjection.Project(read, english: false));
        var en = Assert.Single(AuroraDtcTableProjection.Project(read, english: true));
        Assert.Contains("подтверждённый DTC", ru.Status);
        Assert.Contains("confirmed DTC", en.Status);
        Assert.Contains("неподдерживаемые биты 0x01", ru.Status);
        Assert.Contains("unsupported bits 0x01", en.Status);
        Assert.DoesNotContain("тест не пройден", ru.Status);
        Assert.DoesNotContain("test failed", en.Status);
        Assert.Equal("0x09", ru.Status[..4]);
        Assert.Equal("0x09", en.Status[..4]);
    }

    [Fact]
    public void ExistingSelectedRowRerendersLocallyWithoutDtcBusRequests()
    {
        var presenter = Source("src/Dvinholm.App/MainWindow.AuroraDtc.cs");
        var locale = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.Contains("Project(result, _unifiedEnglish)", presenter);
        Assert.Contains("var selectedCode =", presenter);
        Assert.Contains("DtcRecordsView.SelectedItem = selected;", presenter);
        Assert.Contains("RefreshAuroraDtcTableLocalization();", locale);
        Assert.DoesNotContain("ReadDtcResultByStatusMask(", presenter);
        Assert.DoesNotContain("PassThruWriteMsgs(", presenter);
        Assert.DoesNotContain("ClearDiagnosticInformation(", presenter);
    }

    [Fact]
    public void DetailsPaneWrapsWithoutUnlockingDtcEraseOrExtraRoutes()
    {
        var doc = Layout();
        var details = Named(doc, "DtcSelectedRecordDetails");
        Assert.Contains(Named(doc, "DtcDetailsPane"), details.Ancestors());
        Assert.Equal("Wrap", (string?)details.Attribute("TextWrapping"));
        Assert.Equal("True", (string?)details.Attribute("IsTextSelectionEnabled"));
        var erase = Named(doc, "DtcClearButton");
        Assert.Equal("False", (string?)erase.Attribute("IsEnabled"));
        Assert.Null(erase.Attribute("Click"));
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
    }

    private static string Source(string path)
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var file = Path.Combine(d.FullName,
                path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(path);
    }

    private static XDocument Layout()
    {
        using var resource = typeof(AuroraDtcEvidenceDetailLocalizationTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded Aurora XAML missing.");
        return XDocument.Load(resource);
    }

    private static XElement Named(XDocument doc, string id) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);
}
