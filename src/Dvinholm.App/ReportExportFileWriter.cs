using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Write report bytes in the chosen destination's directory first, then
/// replace the target with a same-directory rename only after a nonempty
/// report has been produced. Failed writes never truncate an existing report.
/// </summary>
internal static class ReportExportFileWriter
{
    internal static Task WriteTextAsync(string destinationPath, string reportText)
    {
        if (string.IsNullOrEmpty(reportText))
            throw new ArgumentException("Refusing to export an empty report.", nameof(reportText));

        return WriteAtomicallyAsync(destinationPath, tempPath =>
            File.WriteAllTextAsync(tempPath, reportText, new UTF8Encoding(false)));
    }

    internal static Task WriteLinesAsync(
        string destinationPath, IReadOnlyList<string> reportLines)
    {
        ArgumentNullException.ThrowIfNull(reportLines);
        if (reportLines.Count == 0)
            throw new ArgumentException("Refusing to export an empty report.", nameof(reportLines));

        return WriteAtomicallyAsync(destinationPath, tempPath =>
            File.WriteAllLinesAsync(tempPath, reportLines, new UTF8Encoding(false)));
    }

    /// <summary>
    /// Kept internal so tests can simulate a failed/partial temporary write
    /// and prove that the original target is never touched.
    /// </summary>
    internal static async Task WriteAtomicallyAsync(
        string destinationPath, Func<string, Task> produceReport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(produceReport);

        var absolutePath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(absolutePath)
            ?? throw new IOException("Report destination has no parent directory.");
        var tempPath = Path.Combine(directory, $".dvinholm-report-{Guid.NewGuid():N}.tmp");

        try
        {
            await produceReport(tempPath);
            if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
                throw new IOException("Report generation produced an empty file.");

            // Same-directory replacement protects the existing destination
            // from being truncated if saving the temporary file fails.
            File.Move(tempPath, absolutePath, overwrite: true);
        }
        finally
        {
            try { File.Delete(tempPath); }
            catch (IOException) { /* Keep the original save result/error. */ }
            catch (UnauthorizedAccessException) { /* No target was truncated. */ }
        }
    }
}
