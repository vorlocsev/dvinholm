namespace Dvinholm.App.Tests;

public sealed class ReportExportFileWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "Dvinholm-report-atomic-tests", Guid.NewGuid().ToString("N"));

    public ReportExportFileWriterTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task TxtSave_WritesNonemptyUtf8WithoutBom()
    {
        var path = Path.Combine(_directory, "uds-SYNTHETIC-example.txt");

        await ReportExportFileWriter.WriteTextAsync(path, UdsOfflineTextReport.Build());

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.True(bytes.Length > 100);
        Assert.False(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Contains("OFFLINE SYNTHETIC UDS TXT REPORT", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task TxtSave_OverwritesExistingOnlyAfterFullReportWritten()
    {
        var path = Path.Combine(_directory, "existing.txt");
        await File.WriteAllTextAsync(path, "old report");

        await ReportExportFileWriter.WriteTextAsync(path, "new complete report");

        Assert.Equal("new complete report", await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.GetFiles(_directory, ".dvinholm-report-*.tmp"));
    }

    [Fact]
    public async Task CsvSave_PreservesSyntheticSourceColumnsAndCompleteRows()
    {
        var path = Path.Combine(_directory, "can-demo-SYNTHETIC.csv");
        var lines = new[]
        {
            "timestamp,id,type,dlc,data,source",
            "100,7E8,standard,2,\"01 02\",SYNTHETIC_DEMO"
        };

        await ReportExportFileWriter.WriteLinesAsync(path, lines);

        Assert.Equal(lines, await File.ReadAllLinesAsync(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public async Task PartialWriteException_PreservesExistingDestinationAndCleansTemporaryFile()
    {
        var path = Path.Combine(_directory, "original.txt");
        await File.WriteAllTextAsync(path, "do not change");

        await Assert.ThrowsAsync<IOException>(() =>
            ReportExportFileWriter.WriteAtomicallyAsync(path, async temp =>
            {
                await File.WriteAllTextAsync(temp, "incomplete");
                throw new IOException("Simulated interrupted save");
            }));

        Assert.Equal("do not change", await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.GetFiles(_directory, ".dvinholm-report-*.tmp"));
    }

    [Fact]
    public async Task EmptyGeneratedReport_LeavesNoFileAtSelectedDestination()
    {
        var path = Path.Combine(_directory, "empty.txt");

        await Assert.ThrowsAsync<IOException>(() =>
            ReportExportFileWriter.WriteAtomicallyAsync(path,
                temp => File.WriteAllTextAsync(temp, string.Empty)));

        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(_directory, ".dvinholm-report-*.tmp"));
    }

    [Fact]
    public async Task RefusesEmptyReportInputsBeforeAnyFileIsCreated()
    {
        var txt = Path.Combine(_directory, "empty.txt");
        var csv = Path.Combine(_directory, "empty.csv");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportExportFileWriter.WriteTextAsync(txt, string.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReportExportFileWriter.WriteLinesAsync(csv, Array.Empty<string>()));

        Assert.False(File.Exists(txt));
        Assert.False(File.Exists(csv));
    }

    [Fact]
    public void Win32Picker_FilenameIncludesExtensionWithoutDuplicatingIt()
    {
        Assert.Equal("uds-SYNTHETIC-example.txt",
            ReportSavePathPicker.SuggestedName("uds-SYNTHETIC-example", ".txt"));
        Assert.Equal("can-demo-SYNTHETIC.csv",
            ReportSavePathPicker.SuggestedName("can-demo-SYNTHETIC.csv", ".csv"));
    }

    [Fact]
    public void Win32Picker_FilenameRejectsDirectoryComponentsAndInvalidExtension()
    {
        Assert.Throws<ArgumentException>(() =>
            ReportSavePathPicker.SuggestedName("../Dvinholm/Report", ".txt"));
        Assert.Throws<ArgumentException>(() =>
            ReportSavePathPicker.SuggestedName("Report", "txt"));
        Assert.Throws<ArgumentException>(() =>
            ReportSavePathPicker.SuggestedName("Report", ".x/y"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
