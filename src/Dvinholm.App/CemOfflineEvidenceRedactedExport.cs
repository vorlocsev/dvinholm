using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Explicitly export ONLY an already-redacted, bounded research summary.
/// The caller must supply a new absolute .txt path. No overwrites, no
/// automatic export, no binary configuration or provenance identifiers.
/// </summary>
internal static class CemOfflineEvidenceRedactedExport
{
    internal const int MaximumReportBytes = 32 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static byte[] PrepareUtf8(
        CemOfflineEvidenceFreshnessAssessment assessment, bool english)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        var report = assessment.CreateRedactedReport(english);
        var bytes = StrictUtf8.GetBytes(report);
        if (bytes.Length == 0 || bytes.Length > MaximumReportBytes)
            throw new InvalidDataException("CEM redacted report exceeds 32 KiB.");
        return bytes;
    }

    internal static async Task SaveNewTextFileAsync(
        string selectedOutputPath,
        CemOfflineEvidenceFreshnessAssessment assessment,
        bool english,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedOutputPath);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(selectedOutputPath) ||
            !string.Equals(Path.GetExtension(selectedOutputPath),
                ".txt", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "Explicitly select a new absolute .txt file for a redacted report.");

        var payload = PrepareUtf8(assessment, english);
        cancellationToken.ThrowIfCancellationRequested();
        var completed = false;
        var created = false;
        try
        {
            await using (var output = new FileStream(selectedOutputPath,
                FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                created = true;
                await output.WriteAsync(payload, cancellationToken);
                await output.FlushAsync(cancellationToken);
                completed = true;
            }
        }
        finally
        {
            // Only a file created by THIS call is eligible for cleanup.
            // Existing files are never opened or overwritten.
            if (created && !completed && File.Exists(selectedOutputPath))
                File.Delete(selectedOutputPath);
        }
    }
}
