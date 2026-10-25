using System.Text;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline regression tests of report staging integrity.
/// Only private temp directories are used; no J2534 hardware or UI is needed.
/// </summary>
public sealed class ReportExportStagingIntegrityTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "Dvinholm-report-integrity", Guid.NewGuid().ToString("N"));

    public ReportExportStagingIntegrityTests() => Directory.CreateDirectory(_root);

    private string Destination => Path.Combine(_root, "report.csv");

    private void AssertNoTemporaryFiles() =>
        Assert.Empty(Directory.GetFiles(_root, ".dvinholm-report-*.tmp"));

    private static byte[] Utf8(string value) => new UTF8Encoding(false).GetBytes(value);

    private async Task AssertStageRejectedAsync(Func<string, Task> writer, string expected)
    {
        await File.WriteAllTextAsync(Destination, "previous complete export");
        var oldBytes = await File.ReadAllBytesAsync(Destination);

        await Assert.ThrowsAsync<IOException>(() =>
            ReportExportFileWriter.WriteAtomicallyAsync(
                Destination, writer, Utf8(expected)));

        Assert.Equal(oldBytes, await File.ReadAllBytesAsync(Destination));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task UnicodeTextExportCommitsExactUtf8WithoutBom()
    {
        const string content = "Dvinholm\nСеверная Двина\nDID F190";

        await ReportExportFileWriter.WriteTextAsync(Destination, content);

        Assert.Equal(Utf8(content), await File.ReadAllBytesAsync(Destination));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task CsvLinesExportIncludesExactPlatformNewlineAndEscapedFields()
    {
        var rows = new[]
        {
            "timestamp,id,type,dlc,data",
            "123,7E8,11-bit,2,\"01 02\"",
            "124,7E9,11-bit,2,\"AB,CD\""
        };

        await ReportExportFileWriter.WriteLinesAsync(Destination, rows);

        var expected = string.Join(Environment.NewLine, rows) + Environment.NewLine;
        Assert.Equal(Utf8(expected), await File.ReadAllBytesAsync(Destination));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task NonemptyTruncatedStageCannotOverwriteExistingReport()
    {
        const string expected = "complete CAN and UDS report";
        await AssertStageRejectedAsync(
            temp => File.WriteAllTextAsync(temp, expected[..9]), expected);
    }

    [Fact]
    public async Task SameLengthButAlteredStageCannotOverwriteExistingReport()
    {
        const string expected = "123,7E8,11-bit";
        await AssertStageRejectedAsync(
            temp => File.WriteAllTextAsync(temp, "123,7E9,11-bit"), expected);
    }

    [Fact]
    public async Task MissingStageCannotOverwriteExistingReport()
    {
        await AssertStageRejectedAsync(_ => Task.CompletedTask, "expected report");
    }

    [Fact]
    public async Task EmptyStageCannotOverwriteExistingReport()
    {
        await AssertStageRejectedAsync(
            temp => File.WriteAllTextAsync(temp, ""), "expected report");
    }

    [Fact]
    public async Task ExceptionDuringPartialStagePreservesEarlierReportAndCleansTemp()
    {
        const string expected = "new complete report";
        await File.WriteAllTextAsync(Destination, "last known-good report");
        var original = await File.ReadAllBytesAsync(Destination);

        await Assert.ThrowsAsync<IOException>(() =>
            ReportExportFileWriter.WriteAtomicallyAsync(
                Destination,
                async temp =>
                {
                    await File.WriteAllTextAsync(temp, expected[..5]);
                    throw new IOException("Injected interrupted export");
                },
                Utf8(expected)));

        Assert.Equal(original, await File.ReadAllBytesAsync(Destination));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task SuccessfulRetryAfterCorruptStageReplacesReportOnce()
    {
        await File.WriteAllTextAsync(Destination, "old report");
        await Assert.ThrowsAsync<IOException>(() =>
            ReportExportFileWriter.WriteAtomicallyAsync(
                Destination,
                temp => File.WriteAllTextAsync(temp, "wrong"),
                Utf8("new report")));

        await ReportExportFileWriter.WriteTextAsync(Destination, "new report");
        Assert.Equal("new report", await File.ReadAllTextAsync(Destination));
        AssertNoTemporaryFiles();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
