using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Export ONLY explicitly composed redacted research reports. Write to a
/// private, new temporary file in the selected directory, then publish
/// via a no-overwrite rename. Never leave a partial FINAL report at the
/// user-selected path; never overwrite an existing report or ECU dump.
/// </summary>
internal static class CemOfflineEvidenceRedactedExport
{
    internal const int MaximumReportBytes = 32 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static byte[] PrepareUtf8(
        CemOfflineEvidenceFreshnessAssessment assessment, bool english)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        return EncodeRedactedText(assessment.CreateRedactedReport(english));
    }

    internal static byte[] PrepareUtf8(
        CemOfflineEvidenceFinalSummary summary, bool english)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return EncodeRedactedText(summary.CreateRedactedReport(english));
    }

    private static byte[] EncodeRedactedText(string report)
    {
        var bytes = StrictUtf8.GetBytes(report);
        if (bytes.Length == 0 || bytes.Length > MaximumReportBytes)
            throw new InvalidDataException("CEM redacted report exceeds 32 KiB.");
        return bytes;
    }

    internal static Task SaveNewTextFileAsync(
        string selectedOutputPath,
        CemOfflineEvidenceFreshnessAssessment assessment,
        bool english,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        return SavePayloadAsync(selectedOutputPath,
            PrepareUtf8(assessment, english), cancellationToken);
    }

    internal static Task SaveNewTextFileAsync(
        string selectedOutputPath,
        CemOfflineEvidenceFinalSummary summary,
        bool english,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return SavePayloadAsync(selectedOutputPath,
            PrepareUtf8(summary, english), cancellationToken);
    }

    private static async Task SavePayloadAsync(
        string selectedOutputPath, byte[] payload,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedOutputPath);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(selectedOutputPath) ||
            !string.Equals(Path.GetExtension(selectedOutputPath),
                ".txt", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "Explicitly select a new absolute .txt path for a redacted report.");

        var directory = Path.GetDirectoryName(selectedOutputPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidDataException("Missing report output directory.");

        // A separate random name in the same directory supports a
        // same-volume rename and prevents any partial final .txt file.
        var temporary = Path.Combine(directory, ".dvinholm-" +
            Guid.NewGuid().ToString("N") + ".tmp");
        var temporaryCreated = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using (var output = new FileStream(temporary,
                FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                temporaryCreated = true;
                await output.WriteAsync(payload, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            // Atomic publish on one volume. false prohibits overwriting
            // an existing destination even if it appears during the write.
            File.Move(temporary, selectedOutputPath, overwrite: false);
            temporaryCreated = false;
        }
        finally
        {
            if (temporaryCreated && File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
