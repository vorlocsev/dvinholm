namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic, offline-only checks for candidate ISO-TP sequences.
/// These are NOT verified CEM transport identifiers or UDS exchanges.
/// </summary>
public sealed class CanMonitorIsoTpCandidateSequencesTests
{
    [Fact]
    public void EmptyHistoryDoesNotInventTransportOrSession()
    {
        var text = Report([]);
        Assert.Contains("Buffered RX rows: 0", text);
        Assert.Contains("Distinct groups: 0", text);
        Assert.Contains("NOT a validated diagnostic exchange", text);
        Assert.Contains("No source ECU", text);
        Assert.Contains("No CAN/J2534 operation", text);
    }

    [Fact]
    public void FirstAndConsecutiveFramesCanFormPlausibleSameIdSequence()
    {
        var text = Report([
            Frame("7E8", "10 09 11 11 11 11 11 11"),
            Frame("123", "03 62 F1 90"), // Another ID may interleave.
            Frame("7E8", "21 22 33 44")
        ]);
        Assert.Contains("CAN 11-bit 7E8: candidate FF=1, plausible-completion=1", text);
        Assert.Contains("trailing-incomplete=0, sequence-mismatch=0, interrupted=0", text);
        Assert.Contains("CAN 11-bit 123: candidate FF=0", text);
        Assert.DoesNotContain("62 F1 90", text);
        Assert.DoesNotContain("22 33 44", text);
    }

    [Fact]
    public void CounterWrapsFromFToZeroAndTracksRemainingLength()
    {
        var rows = new List<CanMonitorRow>
        {
            Frame("7E8", "10 77 11 11 11 11 11 11") // 119 bytes total.
        };
        // Six FF bytes + 16 * 7 CF bytes = 118; a final byte is needed.
        for (var n = 1; n <= 16; n++)
        {
            var pci = (0x20 | (n & 0x0F)).ToString("X2");
            rows.Add(Frame("7E8", pci + " 11 11 11 11 11 11 11"));
        }
        var incomplete = Report(rows);
        Assert.Contains("candidate FF=1, plausible-completion=0, trailing-incomplete=1",
            incomplete);

        rows.Add(Frame("7E8", "21 22"));
        var complete = Report(rows);
        Assert.Contains("candidate FF=1, plausible-completion=1, trailing-incomplete=0",
            complete);
        Assert.Contains("sequence-mismatch=0", complete);
    }

    [Fact]
    public void OutOfOrderCounterFailsClosedAndDoesNotCountCompletion()
    {
        var text = Report([
            Frame("7E8", "10 0C 11 11 11 11 11 11"),
            Frame("7E8", "22 11 11 11 11 11 11 11"),
            Frame("7E8", "21 11 11 11 11 11 11 11")
        ]);
        Assert.Contains("candidate FF=1, plausible-completion=0", text);
        Assert.Contains("sequence-mismatch=1", text);
        Assert.Contains("orphan-CF=1", text);
    }

    [Fact]
    public void RepeatedFirstFrameAndSingleFrameInterruptPreviousCandidate()
    {
        var text = Report([
            Frame("7E8", "10 14 11 11 11 11 11 11"),
            Frame("7E8", "10 20 11 11 11 11 11 11"),
            Frame("7E8", "03 62 F1 90")
        ]);
        Assert.Contains("candidate FF=2, plausible-completion=0", text);
        Assert.Contains("interrupted=2", text);
        Assert.Contains("trailing-incomplete=0", text);
    }

    [Fact]
    public void TrailingIncompleteAndOrphanCfAreReportedSeparately()
    {
        var text = Report([
            Frame("7E8", "21 11 11"),
            Frame("123", "10 20 11 11 11 11 11 11")
        ], received: 5, drops: null);
        Assert.Contains("CAN 11-bit 7E8: candidate FF=0", text);
        Assert.Contains("orphan-CF=1", text);
        Assert.Contains("CAN 11-bit 123: candidate FF=1, plausible-completion=0, trailing-incomplete=1",
            text);
        Assert.Contains("Frame continuity UNKNOWN/INCOMPLETE", text);
    }

    [Fact]
    public void MalformedInputsRejectAndMoreThan64GroupsAreBounded()
    {
        var bad = new[]
        {
            new CanMonitorRow(1, "7E8", "11-bit", 4, "10 09 11"),
            new CanMonitorRow(1, "7E8", "11-bit", 2, "21 ZZ"),
            new CanMonitorRow(1, "7e8", "11-bit", 2, "21 11"),
            new CanMonitorRow(1, "800", "11-bit", 2, "21 11")
        };
        foreach (var row in bad)
            Assert.Throws<InvalidDataException>(() => Report([row]));

        var frames = Enumerable.Range(0, 65)
            .Select(i => Frame(i.ToString("X3"), "21 11")).ToArray();
        var bounded = CanMonitorIsoTpCandidateSequences.Build(
            frames, frames.Length, 0, syntheticDemo: true);
        Assert.Contains("SYNTHETIC DEMO", bounded);
        Assert.Contains("Additional groups withheld: 1", bounded);
        Assert.Contains("CAN 11-bit 03F", bounded);
        Assert.DoesNotContain("CAN 11-bit 040:", bounded);
        Assert.True(bounded.Length < 20000);
    }

    [Fact]
    public void VirtualJ2534RxOnlyNoTxNoRawBytesAndExistingUiUsesReport()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var bus = new CanBus(adapter);
        using var monitor = bus.Subscribe();
        bus.Start();

        adapter.InjectReceiveFrame(new CanFrame(
            0x7E8, false, false, [0x10, 0x09, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11],
            12345678, 0));
        adapter.InjectReceiveFrame(new CanFrame(
            0x7E8, false, false, [0x21, 0x59, 0x56, 0x31],
            87654321, 0));

        var rows = new List<CanMonitorRow>();
        for (int i = 0; i < 2; i++)
        {
            Assert.True(monitor.TryRead(TimeSpan.FromSeconds(2),
                CancellationToken.None, out var frame));
            rows.Add(CanMonitorRow.FromFrame(frame));
        }
        var report = Report(rows);
        Assert.Contains("plausible-completion=1", report);
        Assert.DoesNotContain("59 56 31", report);
        Assert.DoesNotContain("12345678", report);
        Assert.DoesNotContain("87654321", report);
        Assert.Equal(0, adapter.TransmittedFrames);

        var ui = ReadMainWindowSource();
        Assert.Contains("CanMonitorIsoTpCandidateSequences.Build(", ui);
        Assert.Contains("Text = copyableReport", ui);
        Assert.Contains("package.SetText(copyableReport)", ui);
        Assert.Contains("CanMonitorRxMetadataJson.Build(", ui);
    }

    private static CanMonitorRow Frame(string id, string hex) =>
        new(42, id, id.Length == 8 ? "29-bit" : "11-bit",
            (uint)((hex.Length + 1) / 3), hex);

    private static string Report(
        IReadOnlyList<CanMonitorRow> rows, long? received = null, long? drops = 0) =>
        CanMonitorIsoTpCandidateSequences.Build(
            rows, received ?? rows.Count, drops, syntheticDemo: false);

    private static string ReadMainWindowSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "src", "Dvinholm.App",
                "MainWindow.xaml.cs");
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("MainWindow.xaml.cs");
    }
}
