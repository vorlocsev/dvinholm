using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Reports at most one update download message for each newly reached
/// ten-percent boundary. Uses the verified GitHub artifact byte count as
/// denominator, not the server's optional Content-Length response header.
/// </summary>
internal sealed class UpdateDownloadProgress
{
    private readonly long _expectedBytes;
    private int _lastMilestone;

    internal UpdateDownloadProgress(long expectedBytes)
    {
        if (expectedBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedBytes));
        _expectedBytes = expectedBytes;
    }

    internal string? ReportCopied(long receivedBytes, string displayVersion)
    {
        if (receivedBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(receivedBytes));
        // Progress is informational. Size mismatch is still enforced by the
        // existing downloaded-size and digest verification, not this class.
        var percentage = (int)Math.Min(100m, decimal.Floor(100m * receivedBytes / _expectedBytes));
        var milestone = (percentage / 10) * 10;
        if (milestone <= _lastMilestone)
            return null;

        _lastMilestone = milestone;
        return string.Create(CultureInfo.InvariantCulture,
            $"Downloading {displayVersion}: {milestone}% ({receivedBytes:N0}/{_expectedBytes:N0} bytes)");
    }

    // Only the downloader's structured milestone text can set a determinate
    // progress value. Checking, checksum, extraction and replacement phases
    // have no comparable percentage, so they stay indeterminate.
    internal static int? ReadMilestone(string? message)
    {
        if (string.IsNullOrEmpty(message) ||
            !message.StartsWith("Downloading ", StringComparison.Ordinal) ||
            !message.EndsWith(" bytes)", StringComparison.Ordinal))
            return null;

        var colon = message.LastIndexOf(": ", StringComparison.Ordinal);
        if (colon <= "Downloading ".Length)
            return null;

        var marker = message.IndexOf("% (", colon + 2, StringComparison.Ordinal);
        if (marker <= colon + 2)
            return null;

        if (!int.TryParse(message.AsSpan(colon + 2, marker - colon - 2),
                NumberStyles.None, CultureInfo.InvariantCulture, out var percent) ||
            percent < 10 || percent > 100 || percent % 10 != 0)
            return null;

        return percent;
    }
}
