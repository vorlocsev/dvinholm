using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// A user-chosen CSV is an unverified local research file, not a live CAN
/// capture or evidence of an identified physical CEM/vehicle. No adapter or
/// profile objects are created and nothing is written back to disk.
/// </summary>
internal sealed record CanMonitorOfflineCsvSnapshot(
    IReadOnlyList<CanMonitorRow> Rows,
    bool IsSyntheticDemo)
{
    internal bool AuthenticatesVehicleOrCem => false;
    internal bool AllowsAdapterConnection => false;
    internal bool AllowsConfigurationOrPin => false;
}

internal static class CanMonitorOfflineCsvInspection
{
    internal const int MaximumFileBytes = 512 * 1024;
    internal const int MaximumRows = CanMonitorHistory.DefaultCapacity;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static CanMonitorOfflineCsvSnapshot InspectCsv(string? csv)
    {
        if (string.IsNullOrEmpty(csv) || csv.Length > MaximumFileBytes ||
            csv[0] == '\uFEFF' || csv.IndexOf('\0') >= 0)
            throw new InvalidDataException("Invalid or oversized CAN research CSV.");

        // Canonical Dvinholm CSV intentionally never puts a newline/comma
        // inside a valid CAN field. Reject other dialects and embedded
        // formula/quoted fields instead of guessing at potentially unsafe data.
        var lines = csv.Split('\n');
        var count = lines.Length;
        if (lines[^1].Length == 0)
            count--;
        if (count < 1 || count > MaximumRows + 1)
            throw new InvalidDataException("Invalid CAN research CSV row count.");

        static string Line(string line) =>
            line.EndsWith('\r') ? line[..^1] : line;

        var header = Line(lines[0]);
        bool synthetic = header switch
        {
            "timestamp,id,type,dlc,data" => false,
            "timestamp,id,type,dlc,data,source" => true,
            _ => throw new InvalidDataException("Unknown CAN research CSV columns.")
        };

        var rows = new List<CanMonitorRow>(count - 1);
        Span<byte> scratch = stackalloc byte[8];
        for (var i = 1; i < count; ++i)
        {
            var line = Line(lines[i]);
            var cols = line.Split(',');
            if (cols.Length != (synthetic ? 6 : 5) ||
                (synthetic && cols[5] != "SYNTHETIC_DEMO") ||
                !uint.TryParse(cols[0], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var timestamp) ||
                !uint.TryParse(cols[3], NumberStyles.None,
                    CultureInfo.InvariantCulture, out var dlc) ||
                dlc > 8 ||
                cols[4].Length < 2 || cols[4][0] != '"' ||
                cols[4][^1] != '"')
                throw new InvalidDataException("Invalid CAN research CSV frame.");

            var data = cols[4][1..^1];
            if (!CanMonitorIsoTpShapeSummary.TryDecodeData(data, (int)dlc, scratch))
                throw new InvalidDataException("Malformed CAN research CSV payload.");

            rows.Add(new CanMonitorRow(timestamp, cols[1], cols[2], dlc, data));
        }

        // Strict ID/type/DLC bounds apply even when an input is only used
        // for a redacted report. No single row can silently become a profile.
        _ = CanMonitorRxResearchSummary.Build(rows, rows.Count, null, synthetic);
        return new CanMonitorOfflineCsvSnapshot(
            Array.AsReadOnly(rows.ToArray()), synthetic);
    }

    internal static async Task<CanMonitorOfflineCsvSnapshot> InspectFileAsync(
        string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length is < 1 or > MaximumFileBytes)
            throw new InvalidDataException("CAN research CSV file exceeds its size limit.");

        var bytes = new byte[(int)file.Length];
        await file.ReadExactlyAsync(bytes, cancellationToken);
        var extra = new byte[1];
        if (await file.ReadAsync(extra, cancellationToken) != 0)
            throw new InvalidDataException("CAN research CSV changed during inspection.");
        if (bytes.Length >= 3 && bytes[0] == 0xEF &&
            bytes[1] == 0xBB && bytes[2] == 0xBF)
            throw new InvalidDataException("Use UTF-8 without a BOM.");
        try
        {
            return InspectCsv(StrictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("Invalid UTF-8 in CAN research CSV.", ex);
        }
    }

    internal static string BuildRedactedReport(CanMonitorOfflineCsvSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var rows = snapshot.Rows;
        var result = new StringBuilder()
            .AppendLine("USER-SELECTED CAN CSV: OFFLINE RESEARCH ONLY")
            .AppendLine(snapshot.IsSyntheticDemo
                ? "Source marked SYNTHETIC DEMO; NOT a physical CAN capture."
                : "Source is an unverified local CSV; NOT an authenticated physical CAN capture.")
            .AppendLine("Historical dropped/evicted frames are UNKNOWN; a CSV cannot prove bus completeness.")
            .AppendLine("No live monitor, profile, bus speed or J2534 settings are changed.")
            .AppendLine();

        // The aggregate ISO-TP view suppresses *all* arbitration IDs;
        // never include the existing per-ID research summary here.
        result.AppendLine(CanMonitorIsoTpShapeSummary.BuildAggregate(
            rows, rows.Count, null, snapshot.IsSyntheticDemo));
        // Sequence continuity uses the CSV's recorded order ONLY. Without
        // original subscriber drop counters or a verified capture boundary,
        // even plausible completion is not an authenticated ISO-TP exchange.
        // This variant never emits a CAN arbitration identifier or payload.
        result.AppendLine(CanMonitorIsoTpCandidateSequences.BuildAggregate(
            rows, rows.Count, null, snapshot.IsSyntheticDemo));
        result.AppendLine(CanMonitorD2EnvelopeCandidateSummary.Build(
            rows, rows.Count, null, snapshot.IsSyntheticDemo));
        return result
            .AppendLine("Frame contents and timestamps are not included in this report.")
            .AppendLine("CSV source may still contain sensitive data. Do not share it without review.")
            .AppendLine("These are possible byte shapes, NOT validated CEM or D2 diagnostics.")
            .ToString();
    }
}
