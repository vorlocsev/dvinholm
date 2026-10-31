using System.Text;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline-only gates for source-scoped schema-15 J2534 events.
/// No authenticated ECU reply, CAN transmit or Virtual Car publication.
/// </summary>
public sealed class J2534OfflineTraceInspectionTests
{
    [Fact]
    public void SuccessfulWriteThenDataReadCountsOnlyTemporalAssociation()
    {
        var report = Inspect(
            Event("connect", 100, 9011, status: 0),
            Event("write", 101, 9011, status: 0, count: 1, category: "a3_tx"),
            Event("read", 102, 9011, status: 0, count: 1, category: "e3_02_candidate"),
            Event("disconnect", 103, 9011, status: 0));
        Assert.Contains("Connected sessions reported: 1", report);
        Assert.Contains("Writes observed / accepted / unsuccessful: 1 / 1 / 0", report);
        Assert.Contains("Data reads temporally after accepted TX: 1", report);
        Assert.Contains("Reported TX category A3 / B9FB / B9E0 / B9F0 / B9FC: 1 / 0 / 0 / 0 / 0", report);
        Assert.Contains("Reported RX candidates E3-02 / F9E0 / F9F0 / D2-fragment: 1 / 0 / 0 / 0", report);
        Assert.Contains("NOT verified ECU response", report);
        Assert.DoesNotContain("9011", report);
        Assert.DoesNotContain("0x1A2B3C4D", report);
    }

    [Fact]
    public void ReadBeforeAnyWriteAndFailedWriteCannotClaimResponse()
    {
        var report = Inspect(
            Event("connect", 100, 19),
            Event("read", 101, 19, count: 1, category: "f9f0_candidate"),
            Event("write", 102, 19, status: 7, count: 0, category: "b9fb_tx"),
            Event("read", 103, 19, count: 1, category: "f9e0_candidate"));
        Assert.Contains("Writes observed / accepted / unsuccessful: 1 / 0 / 1", report);
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 2", report);
        Assert.Contains("B9FB / B9E0 / B9F0 / B9FC: 0 / 0 / 0 / 0", report);
        Assert.Contains("RX candidates E3-02 / F9E0 / F9F0 / D2-fragment: 0 / 1 / 1 / 0", report);
    }

    [Fact]
    public void IndependentChannelsDoNotBorrowWriteWindows()
    {
        var report = Inspect(
            Event("connect", 1, 1001),
            Event("connect", 2, 1002),
            Event("write", 3, 1001, count: 1),
            Event("read", 4, 1002, count: 1),
            Event("read", 5, 1001, count: 1));
        Assert.Contains("Connected sessions reported: 2", report);
        Assert.Contains("Data reads temporally after accepted TX: 1", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 1", report);
        Assert.DoesNotContain("1001", report);
        Assert.DoesNotContain("1002", report);
    }

    [Fact]
    public void ReusedChannelAndDisconnectDoNotReuseOldSuccessfulWrite()
    {
        var report = Inspect(
            Event("connect", 10, 36),
            Event("write", 11, 36, count: 1),
            Event("connect", 12, 36),
            Event("read", 13, 36, count: 1),
            Event("write", 14, 36, count: 1),
            Event("disconnect", 15, 36),
            Event("read", 16, 36, count: 1));
        Assert.Contains("Connected sessions reported: 2", report);
        Assert.Contains("Failed connects / repeated channel connects: 0 / 1", report);
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 1", report);
        Assert.Contains("Orphan channel calls / other events: 1 / 0", report);
    }

    [Fact]
    public void TimestampRollbackClearsAllChannelsAndWriteWindows()
    {
        var report = Inspect(
            Event("connect", uint.MaxValue - 3, 17),
            Event("write", uint.MaxValue - 2, 17, count: 1),
            Event("read", 0, 17, count: 1),
            Event("connect", 1, 17),
            Event("read", 2, 17, count: 1));
        Assert.Contains("Timestamp regressions/wraps: 1; invalidated TX windows: 1", report);
        Assert.Contains("Data reads temporally after accepted TX: 0", report);
        Assert.Contains("Data reads without preceding accepted TX in channel window: 1", report);
        Assert.Contains("Orphan channel calls / other events: 1 / 0", report);
        Assert.DoesNotContain(uint.MaxValue.ToString(), report);
    }

    [Fact]
    public void MalformedOrSensitiveInjectedJsonIsRejectedNotEchoed()
    {
        var valid = Event("connect", 100, 19);
        foreach (var json in new[]
        {
            "not json",
            valid.Replace("\"schema\":15", "\"schema\":16"),
            valid.Replace("\"status\":0", "\"status\":\"OK\""),
            valid.Replace("\"can_id\":\"0x1A2B3C4D\"", "\"can_id\":\"VIN123456789\""),
            valid.Replace("\"core_version\":26", "\"payload\":\"VINSECRET\",\"core_version\":26"),
            valid.Replace("\"core_version\":26", "\"status\":0,\"core_version\":26"),
            valid.Replace("\"event\":\"connect\"", "\"event\":\"program_ecu\""),
            valid.Replace("\"channel\":19", "\"channel\":-1"),
            valid.Replace("\"class\":\"other\"", "\"class\":\"other\\nunsafe\""),
            valid[..^1],
            "\uFEFF" + valid
        })
            Assert.Throws<InvalidDataException>(() =>
                J2534OfflineTraceInspection.BuildRedactedReport(json));
    }

    [Fact]
    public void EventWindowAndByteLimitsAreBounded()
    {
        Assert.Throws<InvalidDataException>(() =>
            J2534OfflineTraceInspection.BuildRedactedReport(""));
        Assert.Throws<InvalidDataException>(() =>
            J2534OfflineTraceInspection.BuildRedactedReport(
                new string('A', J2534OfflineTraceInspection.MaximumFileBytes + 1)));
        Assert.Throws<InvalidDataException>(() =>
            J2534OfflineTraceInspection.BuildRedactedReport(
                Event("connect", 1, 10) + "\n" + new string(' ', 1025)));
        Assert.Throws<InvalidDataException>(() =>
            J2534OfflineTraceInspection.BuildRedactedReport(
                string.Join("\n",
                    Enumerable.Repeat(Event("open", 1, 0),
                        J2534OfflineTraceInspection.MaximumEvents + 1))));
    }

    [Fact]
    public async Task UserSelectedLocalFileAndUiAreReadOnly()
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "dvinholm-j2534-inspect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, "trace.jsonl");
            await File.WriteAllTextAsync(file, InspectSource());
            var report = await J2534OfflineTraceInspection.InspectFileAsync(file);
            Assert.Contains("Connected sessions reported: 1", report);
            Assert.Contains("Data reads temporally after accepted TX: 1", report);
            Assert.DoesNotContain(folder, report);
            Assert.Single(Directory.EnumerateFiles(folder));

            await File.WriteAllBytesAsync(file, [0xEF, 0xBB, 0xBF, 0x41]);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                J2534OfflineTraceInspection.InspectFileAsync(file));
            await File.WriteAllBytesAsync(file, [0xFF, 0xFE, 0x41]);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                J2534OfflineTraceInspection.InspectFileAsync(file));
            Assert.Single(Directory.EnumerateFiles(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }

        var ui = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var xaml = Source("src/Dvinholm.App/MainWindow.xaml");
        Assert.Contains("J2534OfflineTraceInspection.InspectFileAsync(", ui);
        Assert.Contains("CanMonitorOfflineCsvInspection.InspectFileAsync(", ui);
        Assert.Contains("Clipboard.SetContent(package)", ui);
        Assert.Contains("picker.FileTypeFilter.Add(\".jsonl\")", ui);
        Assert.Contains("Content=\"Inspect trace\"", xaml);
    }

    private static string InspectSource() => string.Join("\n",
        Event("connect", 100, 19),
        Event("write", 101, 19, count: 1, category: "a3_tx"),
        Event("read", 102, 19, count: 1, category: "e3_02_candidate"));

    private static string Inspect(params string[] lines) =>
        J2534OfflineTraceInspection.BuildRedactedReport(string.Join("\n", lines));

    private static string Event(string kind, uint ms, uint channel,
        uint status = 0, uint count = 0, string category = "other",
        uint requested = 1) =>
        JsonSerializer.Serialize(new {
            schema = 15, ms, @event = kind, channel, status,
            requested, count, @class = category,
            can_id = "0x1A2B3C4D", size = 0, value = 0,
            core_version = 26
        });

    private static string Source(string path)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var full = Path.Combine(dir.FullName,
                path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full))
                return File.ReadAllText(full, Encoding.UTF8);
        }
        throw new FileNotFoundException(path);
    }
}
