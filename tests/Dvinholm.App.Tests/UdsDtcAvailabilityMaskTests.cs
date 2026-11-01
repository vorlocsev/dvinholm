namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline UDS 0x19/0x02 status-availability regressions. These use
/// synthetic ECU response bytes only: no adapter, PIN or ECU programming.
/// </summary>
public sealed class UdsDtcAvailabilityMaskTests
{
    [Fact]
    public void ParseRetainsReportedAvailabilityAndDtcRecord()
    {
        var parsed = UdsClient.ParseDtcReadResult(
            [0x02, 0x08, 0x12, 0x34, 0x56, 0x08]);
        Assert.Equal((byte)0x08, parsed.StatusAvailabilityMask);
        var record = Assert.Single(parsed.Records);
        Assert.Equal(0x123456u, record.Code);
        Assert.Equal((byte)0x08, record.Status);
    }

    [Fact]
    public void EmptyDtcReplyStillRetainsZeroAvailability()
    {
        var parsed = UdsClient.ParseDtcReadResult([0x02, 0x00]);
        Assert.Equal((byte)0, parsed.StatusAvailabilityMask);
        Assert.Empty(parsed.Records);
        var text = UdsDtcDisplayFormatter.FormatWithAvailability(parsed);
        Assert.Contains("ECU-supported status bits 0x00", text);
        Assert.Contains("does not confirm a fault-free vehicle", text);
    }

    [Fact]
    public void FullAvailabilityAllowsAllStandardStatusDescriptions()
    {
        var parsed = UdsClient.ParseDtcReadResult(
            [0x02, 0xFF, 0x01, 0x23, 0x45, 0xFF]);
        var text = UdsDtcDisplayFormatter.FormatWithAvailability(parsed, 0xFF);
        Assert.Contains("confirmed DTC", text);
        Assert.Contains("warning indicator requested", text);
        Assert.DoesNotContain("unsupported status bits", text);
        Assert.Contains("raw status 0xFF", text);
    }

    [Fact]
    public void UnsupportedStatusBitsAreReportedWithoutUnsupportedFlagDescriptions()
    {
        var parsed = UdsClient.ParseDtcReadResult(
            [0x02, 0x04, 0x12, 0x34, 0x56, 0x0C]);
        var text = UdsDtcDisplayFormatter.FormatWithAvailability(parsed);
        Assert.Contains("pending DTC", text);
        Assert.DoesNotContain("confirmed DTC", text);
        Assert.Contains("unsupported status bits 0x08", text);
        Assert.Contains("raw status 0x0C", text);
    }

    [Fact]
    public void ZeroAvailabilityNeverInventsDiagnosticStatusFlag()
    {
        var parsed = UdsClient.ParseDtcReadResult(
            [0x02, 0x00, 0xAB, 0xCD, 0xEF, 0x80]);
        var text = UdsDtcDisplayFormatter.FormatWithAvailability(parsed, 0x80);
        Assert.Contains("ECU-supported status bits 0x00", text);
        Assert.Contains("no status bits set", text);
        Assert.Contains("unsupported status bits 0x80", text);
        Assert.DoesNotContain("warning indicator requested", text);
    }

    [Fact]
    public void MultipleRecordsPreserveWireOrderAndAvailabilityIsEcuScoped()
    {
        var parsed = UdsClient.ParseDtcReadResult(
            [0x02, 0x09,
             0xAA, 0xBB, 0xCC, 0x08,
             0x01, 0x02, 0x03, 0x02]);
        var lines = UdsDtcDisplayFormatter.FormatWithAvailability(parsed, 0x09)
            .Split(Environment.NewLine);
        Assert.Equal(3, lines.Length);
        Assert.Contains("requested status mask 0x09", lines[0]);
        Assert.Contains("ECU-supported status bits 0x09", lines[0]);
        Assert.Contains("DTC 0xAABBCC", lines[1]);
        Assert.Contains("confirmed DTC", lines[1]);
        Assert.Contains("DTC 0x010203", lines[2]);
        Assert.Contains("unsupported status bits 0x02", lines[2]);
        Assert.DoesNotContain("failed this operation cycle", lines[2]);
    }

    [Fact]
    public void LegacyParserStillReturnsImmutableRecordsAndRejectsMalformedFrames()
    {
        var legacy = UdsClient.ParseDtcResponse(
            [0x02, 0x08, 0x12, 0x34, 0x56, 0x08]);
        Assert.Equal(0x123456u, Assert.Single(legacy).Code);
        var list = Assert.IsAssignableFrom<IList<UdsDtcRecord>>(legacy);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() =>
            list.Add(new UdsDtcRecord(0x123456u, 0x08)));

        foreach (var malformed in new byte[][] {
            [], [0x02], [0x01, 0x08],
            [0x02, 0x08, 0x12, 0x34]
        })
            Assert.Throws<InvalidOperationException>(() =>
                UdsClient.ParseDtcReadResult(malformed));
    }

    [Fact]
    public void ExistingReadDtcButtonUsesAvailabilityAwarePathWithOneUdsRequest()
    {
        var window = MainWindowSource();
        var handlerStart = window.IndexOf(
            "private async void ReadDtc_Click(", StringComparison.Ordinal);
        var handlerEnd = window.IndexOf(
            "private bool TryParseDid(", handlerStart, StringComparison.Ordinal);
        Assert.True(handlerStart >= 0 && handlerEnd > handlerStart);
        var handler = window[handlerStart..handlerEnd];
        Assert.Contains("client.ReadDtcResultByStatusMask(statusMask, token)", handler);
        Assert.Contains("UdsDtcDisplayFormatter.FormatWithAvailability(result, statusMask)", handler);
        Assert.DoesNotContain("ReadDtcByStatusMask(", handler);

        var clientSource = UdsClientSource();
        Assert.Contains("return ParseDtcReadResult(response);", clientSource);
        Assert.Contains("=> ReadDtcResultByStatusMask(statusMask, cancellationToken).Records;",
            clientSource);
    }

    private static string MainWindowSource() =>
        ReadRepoFile("src", "Dvinholm.App", "MainWindow.xaml.cs");

    private static string UdsClientSource() =>
        ReadRepoFile("src", "Dvinholm.App", "UdsClient.cs");

    private static string ReadRepoFile(params string[] parts)
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory);
             folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(new[] { folder.FullName }.Concat(parts).ToArray());
            if (File.Exists(path)) return File.ReadAllText(path);
        }

        throw new FileNotFoundException(string.Join("/", parts));
    }
}
