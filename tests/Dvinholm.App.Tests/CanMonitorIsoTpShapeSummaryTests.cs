namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks of *possible* normal-addressed ISO-TP PCI patterns.
/// No physical J2534/DiCE transport, authentic CEM identity or PIN operation.
/// </summary>
public sealed class CanMonitorIsoTpShapeSummaryTests
{
    [Fact]
    public void EmptyHistoryReportsNoEvidenceAndNoProtocolIdentity()
    {
        var report = Report([]);
        Assert.Contains("Buffered frames examined: 0", report);
        Assert.Contains("Distinct CAN ID + format groups: 0", report);
        Assert.Contains("false positive", report);
        Assert.Contains("No request/response pairing", report);
        Assert.Contains("No diagnostic requests", report);
    }

    [Fact]
    public void PlausibleSingleFirstConsecutiveAndFlowControlShapesAreCounted()
    {
        var report = Report([
            Row("7E8", false, "03 62 F1 90"),
            Row("7E8", false, "10 09 62 F1 90 11 22 33"),
            Row("7E8", false, "21 44 55 66"),
            Row("7E8", false, "30 00 00"),
            Row("7E8", false, "40 11")
        ]);
        Assert.Contains("CAN 11-bit 7E8: possible SF=1, FF=1, CF=1, FC=1, other=1", report);
        Assert.DoesNotContain("62 F1 90", report);
        Assert.DoesNotContain("44 55 66", report);
    }

    [Fact]
    public void Separate11And29BitIdsAndOrderingArePreserved()
    {
        var report = Report([
            Row("000007E8", true, "03 62 F1 90"),
            Row("7E8", false, "03 62 F1 90"),
            Row("123", false, "21 AB")
        ]);
        Assert.Contains("Distinct CAN ID + format groups: 3", report);
        Assert.Contains("CAN 11-bit 7E8: possible SF=1", report);
        Assert.Contains("CAN 29-bit 000007E8: possible SF=1", report);
        Assert.True(report.IndexOf("CAN 11-bit 123", StringComparison.Ordinal) <
                    report.IndexOf("CAN 11-bit 7E8", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidPciPatternsRemainOtherRatherThanVerifiedDiagnosis()
    {
        var report = Report([
            Row("123", false, "00 00"),
            Row("123", false, "07 11"),
            Row("123", false, "10 03 00 00 00 00 00 00"),
            Row("123", false, "33 00 00"),
            Row("123", false, "30 00 F0"),
            Row("123", false, ""),
            Row("123", false, "21")
        ]);
        Assert.Contains("other=7", report);
        Assert.Contains("possible SF=0, FF=0, CF=0, FC=0", report);
        Assert.Contains("may be unrelated traffic", report);
    }

    [Fact]
    public void MalformedDataAndInconsistentDlcFailClosedWithoutEchoingInput()
    {
        var bad = new[]
        {
            new CanMonitorRow(1, "7E8", "11-bit", 4, "03 62 F1"),
            new CanMonitorRow(1, "7E8", "11-bit", 2, "03-62"),
            new CanMonitorRow(1, "7E8", "11-bit", 2, "03 XX"),
            new CanMonitorRow(1, "7E8", "11-bit", 2, "03 6a"),
            new CanMonitorRow(1, "7E8", "11-bit", 1, "03 "),
            new CanMonitorRow(1, "7E8", "11-bit", 0, "00"),
            new CanMonitorRow(1, "800", "11-bit", 2, "03 62")
        };
        foreach (var frame in bad)
            Assert.Throws<InvalidDataException>(() => Report([frame]));
    }

    [Fact]
    public void PayloadVinOrPinAndTimestampsNeverAppearInReport()
    {
        const string privatePayload = "59 56 31 43 4D 35 39 48";
        var report = Report([
            new CanMonitorRow(1122334455, "120", "11-bit", 8, privatePayload),
            new CanMonitorRow(99887766, "121", "11-bit", 3, "03 27 01")
        ]);
        Assert.DoesNotContain(privatePayload, report);
        Assert.DoesNotContain("27 01", report);
        Assert.DoesNotContain("1122334455", report);
        Assert.DoesNotContain("99887766", report);
        Assert.Contains("No raw payload bytes", report);
        Assert.Contains("may still fingerprint a vehicle", report);
    }

    [Fact]
    public void MoreThan64CanIdGroupsAreBoundedAndDemoIsLabeled()
    {
        var many = Enumerable.Range(0, 65)
            .Select(i => Row(i.ToString("X3"), false, "03 62 F1 90"))
            .ToArray();
        var report = CanMonitorIsoTpShapeSummary.Build(
            many, 65, 0, syntheticDemo: true);
        Assert.Contains("synthetic demo frames", report);
        Assert.Contains("Additional groups withheld: 1", report);
        Assert.Contains("CAN 11-bit 03F", report);
        Assert.DoesNotContain("CAN 11-bit 040:", report);
        Assert.Throws<InvalidDataException>(() =>
            CanMonitorIsoTpShapeSummary.Build(many, 64, 0, false));
        Assert.True(report.Length < 10000);
    }

    [Fact]
    public void VirtualAdapterRxOnlyAndUiPresentationNeverTransmit()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var bus = new CanBus(adapter);
        using var monitor = bus.Subscribe();
        bus.Start();
        adapter.InjectReceiveFrame(new CanFrame(
            0x7E8, false, false, [0x03, 0x62, 0xF1, 0x90], 100, 0));
        Assert.True(monitor.TryRead(TimeSpan.FromSeconds(2),
            CancellationToken.None, out var frame));
        var report = Report([CanMonitorRow.FromFrame(frame)]);
        Assert.Contains("possible SF=1", report);
        Assert.Equal(0, adapter.TransmittedFrames);

        var ui = ReadUi();
        Assert.Contains("CanMonitorIsoTpShapeSummary.Build(", ui);
        Assert.Contains("Text = copyableReport", ui);
        Assert.Contains("package.SetText(copyableReport)", ui);
        Assert.Contains("CanMonitorRxMetadataJson.Build(", ui);
    }

    private static CanMonitorRow Row(string id, bool extended, string data) =>
        new(42, id, extended ? "29-bit" : "11-bit",
            data.Length == 0 ? 0 : (uint)((data.Length + 1) / 3), data);

    private static string Report(IReadOnlyList<CanMonitorRow> rows) =>
        CanMonitorIsoTpShapeSummary.Build(rows, rows.Count, 0, false);

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
