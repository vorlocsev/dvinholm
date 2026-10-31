using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic tests for user-selected offline CSV inspection.
/// No actual adapter, physical CEM or executable Volvo D2 service involved.
/// </summary>
public sealed class CanMonitorOfflineCsvInspectionTests
{
    private const string Header = "timestamp,id,type,dlc,data";

    [Fact]
    public void RegularExportRoundTripsWithoutRestoringOrAuthenticatingVehicle()
    {
        var csv = string.Join("\n", CanMonitorCsv.BuildLines(
            [Frame("7E8", "03 62 F1 90"),
             Frame("120", "CF 40 E3 02 59 56 31 43")], false)) + "\n";
        var result = CanMonitorOfflineCsvInspection.InspectCsv(csv);
        Assert.Equal(2, result.Rows.Count);
        Assert.False(result.IsSyntheticDemo);
        Assert.False(result.AuthenticatesVehicleOrCem);
        Assert.False(result.AllowsAdapterConnection);
        Assert.False(result.AllowsConfigurationOrPin);
        Assert.Equal("CF 40 E3 02 59 56 31 43", result.Rows[1].Data);

        var summary = CanMonitorOfflineCsvInspection.BuildRedactedReport(result);
        Assert.Contains("All-group shapes (CAN IDs withheld): SF=1", summary);
        Assert.Contains("candidate-E3-02=1", summary);
        Assert.Contains("UNKNOWN", summary);
        Assert.DoesNotContain("03 62 F1 90", summary);
        Assert.DoesNotContain("59 56 31 43", summary);
        Assert.DoesNotContain("7E8", summary);
        Assert.DoesNotContain("CAN 11-bit 7E8", summary);
    }

    [Fact]
    public void SyntheticSourceColumnCanNeverBeLaunderedAsRealCapture()
    {
        var result = CanMonitorOfflineCsvInspection.InspectCsv(
            "timestamp,id,type,dlc,data,source\n" +
            "3,000007E8,29-bit,4,\"CF 50 F9 F0\",SYNTHETIC_DEMO\n");
        Assert.True(result.IsSyntheticDemo);
        var summary = CanMonitorOfflineCsvInspection.BuildRedactedReport(result);
        Assert.Contains("SYNTHETIC DEMO", summary);
        Assert.Contains("candidate-F9-F0=1", summary);
        Assert.DoesNotContain("000007E8", summary);
        Assert.Throws<InvalidDataException>(() =>
            CanMonitorOfflineCsvInspection.InspectCsv(
                "timestamp,id,type,dlc,data,source\n" +
                "3,000007E8,29-bit,4,\"CF 50 F9 F0\",REAL_VEHICLE"));
    }

    [Fact]
    public void EmptyCsvAndUnknownColumnSchemasFailClosed()
    {
        foreach (var invalid in new[]
        {
            "", " ", "time,id,type,dlc,data\n1,7E8,11-bit,0,\"\"",
            Header + ",pin\n1,7E8,11-bit,0,\"\",1234",
            Header + "\n1,7E8,11-bit,0,\"\",EXTRA",
            Header + "\n1,7E8,11-bit,0,\"\"\n\n",
            "\uFEFF" + Header + "\n1,7E8,11-bit,0,\"\""
        })
            Assert.Throws<InvalidDataException>(() =>
                CanMonitorOfflineCsvInspection.InspectCsv(invalid));
    }

    [Fact]
    public void CsvInjectionMalformedTimestampOrCanAddressIsRejected()
    {
        foreach (var line in new[]
        {
            "-1,7E8,11-bit,2,\"CF 40\"",
            "4294967296,7E8,11-bit,2,\"CF 40\"",
            "1,=1+1,11-bit,2,\"CF 40\"",
            "1,7e8,11-bit,2,\"CF 40\"",
            "1,800,11-bit,2,\"CF 40\"",
            "1,7E8,29-bit,2,\"CF 40\"",
            "1,7E8,invalid,2,\"CF 40\"",
            "1,7E8,11-bit,-1,\"CF 40\""
        })
            Assert.Throws<InvalidDataException>(() =>
                CanMonitorOfflineCsvInspection.InspectCsv(Header + "\n" + line));
    }

    [Fact]
    public void QuotingDlcAndPayloadCannotSmuggleAdditionalRowsOrHeaders()
    {
        foreach (var line in new[]
        {
            "1,7E8,11-bit,2,CF 40",
            "1,7E8,11-bit,2,\"CF 40\"\",evil",
            "1,7E8,11-bit,2,\"CF,40\"",
            "1,7E8,11-bit,3,\"CF 40\"",
            "1,7E8,11-bit,2,\"CF 4f\"",
            "1,7E8,11-bit,2,\"CF ZZ\"",
            "1,7E8,11-bit,0,\"00\"",
            "1,7E8,11-bit,9,\"CF 40\""
        })
            Assert.Throws<InvalidDataException>(() =>
                CanMonitorOfflineCsvInspection.InspectCsv(Header + "\n" + line));
    }

    [Fact]
    public void MaximumHistoryAndMaximumFileSizeAreBounded()
    {
        var lines = new List<string> { Header };
        for (var i = 0; i < CanMonitorOfflineCsvInspection.MaximumRows; i++)
            lines.Add(i + ",120,11-bit,4,\"CF 40 E3 02\"");
        var accepted = CanMonitorOfflineCsvInspection.InspectCsv(
            string.Join("\n", lines));
        Assert.Equal(CanMonitorOfflineCsvInspection.MaximumRows, accepted.Rows.Count);
        lines.Add("5000,120,11-bit,4,\"CF 40 E3 02\"");
        Assert.Throws<InvalidDataException>(() =>
            CanMonitorOfflineCsvInspection.InspectCsv(string.Join("\n", lines)));
        Assert.Throws<InvalidDataException>(() =>
            CanMonitorOfflineCsvInspection.InspectCsv(
                Header + "\n" + new string('A',
                    CanMonitorOfflineCsvInspection.MaximumFileBytes)));
    }

    [Fact]
    public async Task LocalFileReadChecksStrictUtf8AndNeverWritesNearbyFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "dvinholm-offline-can-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "test.csv");
            await File.WriteAllTextAsync(path,
                Header + "\r\n42,120,11-bit,4,\"CF 40 E3 02\"\r\n");
            var result = await CanMonitorOfflineCsvInspection.InspectFileAsync(path);
            Assert.Single(result.Rows);
            Assert.Equal(42u, result.Rows[0].Timestamp);
            Assert.Single(Directory.EnumerateFiles(dir));

            await File.WriteAllBytesAsync(path, [0xEF, 0xBB, 0xBF, 0x41]);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CanMonitorOfflineCsvInspection.InspectFileAsync(path));

            await File.WriteAllBytesAsync(path, [0xFF, 0xFE, 0x41]);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CanMonitorOfflineCsvInspection.InspectFileAsync(path));
            Assert.Single(Directory.EnumerateFiles(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void VirtualJ2534AndWinUiImportAreReadOnlyAndDoNotPublishBytes()
    {
        using var adapter = new VirtualJ2534Adapter(_ => Array.Empty<CanFrame>());
        using var bus = new CanBus(adapter);
        using var sub = bus.Subscribe();
        bus.Start();
        adapter.InjectReceiveFrame(new CanFrame(
            0x120, false, false, [0xCF, 0x40, 0xE3, 0x02, 0x59, 0x56, 0x31, 0x43],
            199999, 0));
        Assert.True(sub.TryRead(TimeSpan.FromSeconds(2),
            CancellationToken.None, out var frame));

        var data = CanMonitorCsv.BuildLines([CanMonitorRow.FromFrame(frame)], false);
        var imported = CanMonitorOfflineCsvInspection.InspectCsv(
            string.Join("\n", data));
        var report = CanMonitorOfflineCsvInspection.BuildRedactedReport(imported);
        Assert.Contains("candidate-E3-02=1", report);
        Assert.DoesNotContain("199999", report);
        Assert.DoesNotContain("59 56 31 43", report);
        Assert.DoesNotContain("120", report);
        Assert.Equal(0, adapter.TransmittedFrames);

        var ui = ReadSource("src/Dvinholm.App/MainWindow.xaml.cs");
        var xaml = ReadSource("src/Dvinholm.App/MainWindow.xaml");
        Assert.Contains("CanMonitorOfflineCsvInspection.InspectFileAsync(", ui);
        Assert.Contains("CanMonitorOfflineCsvInspection.BuildRedactedReport(", ui);
        Assert.Contains("package.SetText(report)", ui);
        Assert.Contains("Click=\"InspectCanCsv_Click\"", xaml);
    }

    private static CanMonitorRow Frame(string id, string data) =>
        new(123, id, "11-bit",
            (uint)((data.Length + 1) / 3), data);

    private static string ReadSource(string relative)
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory);
             d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(p))
                return File.ReadAllText(p, Encoding.UTF8);
        }
        throw new FileNotFoundException(relative);
    }
}
