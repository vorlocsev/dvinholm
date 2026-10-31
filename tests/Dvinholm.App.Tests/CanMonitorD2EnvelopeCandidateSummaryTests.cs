namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline, read-only D2-shaped RX research checks. These are not
/// hardware tests and never claim a validated Volvo CEM or D2 response.
/// </summary>
public sealed class CanMonitorD2EnvelopeCandidateSummaryTests
{
    [Fact]
    public void EmptyHistoryCannotInventAProprietaryProtocol()
    {
        var report = Report([]);
        Assert.Contains("Buffered RX rows: 0", report);
        Assert.Contains("RX 11-bit: total=0", report);
        Assert.Contains("RX 29-bit: total=0", report);
        Assert.Contains("NOT CEM acknowledgment", report);
        Assert.Contains("No J2534/CAN calls", report);
    }

    [Fact]
    public void TwoEnvelopeRoutesCountE3_02CandidatesWithoutPayloadDisclosure()
    {
        var report = Report([
            Row("120", "CB 40 E3 02 59 56 31 43"),
            Row("120", "CF 50 E3 02 4D 35 39 48")
        ]);
        Assert.Contains("RX 11-bit: total=2, candidate-E3-02=2", report);
        Assert.Contains("candidate-E3-other=0", report);
        Assert.DoesNotContain("59 56 31 43", report);
        Assert.DoesNotContain("4D 35 39 48", report);
        Assert.DoesNotContain("120", report);
    }

    [Fact]
    public void E3OtherAndF9SubtypesAreOnlyHeaderShapeCounts()
    {
        var report = Report([
            Row("120", "CB 40 E3 03"),
            Row("120", "CF 50 E3"),
            Row("120", "CB 40 F9 E0"),
            Row("120", "CF 50 F9 F0")
        ]);
        Assert.Contains("candidate-E3-02=0, candidate-E3-other=2", report);
        Assert.Contains("candidate-F9-E0=1, candidate-F9-F0=1", report);
        Assert.Contains("Counts are RX byte-shape hypotheses only", report);
    }

    [Fact]
    public void FragmentLikePrefixesAreNotPromotedToD2Transactions()
    {
        var report = Report([
            Row("123", "08 11"),
            Row("123", "4F 22"),
            Row("123", "80 33"),
            Row("123", "8F 44"),
            Row("123", "50 55"), // Not in the broad source candidate ranges.
            Row("123", "CB 40 F9 FF"), // Unknown subtype is never recognized.
            Row("123", "CF 60 E3 02"), // Wrong route byte.
            Row("123", "CB 40 F9") // Truncated subtype.
        ]);
        Assert.Contains("fragment-like=4, other=4", report);
        Assert.Contains("These patterns can occur in unrelated CAN traffic", report);
        Assert.DoesNotContain("CEM confirmed", report);
    }

    [Fact]
    public void ElevenAndTwentyNineBitFramesAreSeparatedWithoutCanIdMapping()
    {
        var report = Report([
            Row("7E8", "CF 40 E3 02"),
            Row("000007E8", "CF 50 F9 F0", extended: true)
        ]);
        Assert.Contains("RX 11-bit: total=1, candidate-E3-02=1", report);
        Assert.Contains("RX 29-bit: total=1, candidate-E3-02=0", report);
        Assert.Contains("candidate-F9-F0=1", report);
        Assert.DoesNotContain("7E8", report);
        Assert.DoesNotContain("000007E8", report);
        Assert.Contains("No transport mode, bus speed, service direction, CAN address", report);
    }

    [Fact]
    public void MalformedFrameAndInvalidCanMetadataAreRejected()
    {
        var bad = new[]
        {
            new CanMonitorRow(1, "7E8", "11-bit", 4, "CB 40 E3"),
            new CanMonitorRow(1, "7E8", "11-bit", 2, "CB-40"),
            new CanMonitorRow(1, "7E8", "11-bit", 2, "CB 4f"),
            new CanMonitorRow(1, "7E8", "11-bit", 0, "00"),
            new CanMonitorRow(1, "7e8", "11-bit", 2, "CB 40"),
            new CanMonitorRow(1, "800", "11-bit", 2, "CB 40"),
            new CanMonitorRow(1, "7E8", "11-bit", 9, "CB 40 E3 02")
        };
        foreach (var row in bad)
            Assert.Throws<InvalidDataException>(() => Report([row]));
        Assert.Throws<InvalidDataException>(() => Report([Row("120", "CB 40 E3 02")], 0));
    }

    [Fact]
    public void SourceAndDropReportingNeverClaimFullCapture()
    {
        var frames = new[] { Row("100", "CF 50 E3 02") };
        var evicted = CanMonitorD2EnvelopeCandidateSummary.Build(
            frames, 3, 0, syntheticDemo: false);
        Assert.Contains("History evicted: 2", evicted);
        Assert.Contains("Observation completeness: INCOMPLETE", evicted);

        var dropped = CanMonitorD2EnvelopeCandidateSummary.Build(
            frames, 1, 4, syntheticDemo: false);
        Assert.Contains("Subscriber RX drops: 4", dropped);
        Assert.Contains("Observation completeness: INCOMPLETE", dropped);

        var unknown = CanMonitorD2EnvelopeCandidateSummary.Build(
            frames, 1, null, syntheticDemo: true);
        Assert.Contains("SYNTHETIC DEMO ONLY", unknown);
        Assert.Contains("Observation completeness: UNKNOWN", unknown);
        Assert.True(unknown.Length < 2200);
    }

    [Fact]
    public void VirtualJ2534RxAndExistingUiIntegrationNeverTransmit()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var bus = new CanBus(adapter);
        using var sub = bus.Subscribe();
        bus.Start();
        adapter.InjectReceiveFrame(new CanFrame(
            0x120, false, false, [0xCF, 0x40, 0xE3, 0x02, 0x59, 0x56, 0x31, 0x43],
            11223344, 0));
        Assert.True(sub.TryRead(TimeSpan.FromSeconds(2),
            CancellationToken.None, out var frame));
        var report = Report([CanMonitorRow.FromFrame(frame)]);
        Assert.Contains("candidate-E3-02=1", report);
        Assert.DoesNotContain("59 56 31 43", report);
        Assert.DoesNotContain("11223344", report);
        Assert.Equal(0, adapter.TransmittedFrames);

        var ui = ReadUi();
        Assert.Contains("CanMonitorD2EnvelopeCandidateSummary.Build(", ui);
        Assert.Contains("Environment.NewLine + d2Shapes", ui);
        Assert.Contains("package.SetText(copyableReport)", ui);
        Assert.Contains("CanMonitorRxMetadataJson.Build(", ui);
    }

    private static CanMonitorRow Row(string id, string data, bool extended = false) =>
        new(42, id, extended ? "29-bit" : "11-bit",
            (uint)((data.Length + 1) / 3), data);

    private static string Report(
        IReadOnlyList<CanMonitorRow> rows, long? received = null, long? drops = 0) =>
        CanMonitorD2EnvelopeCandidateSummary.Build(
            rows, received ?? rows.Count, drops, syntheticDemo: false);

    private static string ReadUi()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException("MainWindow.xaml.cs");
    }
}
