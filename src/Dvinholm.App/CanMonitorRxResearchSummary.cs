using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Privacy-reduced view of a bounded CAN monitor RX history. This is
/// observational metadata, not proof of a passive-only capture, actual CEM
/// addressing, a UDS session, a source ECU or permission to transmit.
/// </summary>
internal static class CanMonitorRxResearchSummary
{
    internal const int MaximumDisplayedIdGroups = 64;

    /// <summary>
    /// Build a deterministic, bounded report from already-buffered RX rows.
    /// The CAN payload Data property is deliberately NEVER accessed.
    /// No J2534 access, file I/O, transmission or clipboard interaction.
    /// </summary>
    internal static string Build(
        IReadOnlyList<CanMonitorRow> rows,
        long totalReceivedSinceClear,
        long? subscriberDroppedFrames,
        bool syntheticDemo)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > CanMonitorHistory.DefaultCapacity ||
            totalReceivedSinceClear < rows.Count ||
            subscriberDroppedFrames is < 0)
            throw new InvalidDataException("Invalid CAN RX monitor history metadata.");

        var groups = new Dictionary<(bool Extended, uint Id), DlcCounter>();

        foreach (var row in rows)
        {
            if (row is null ||
                row.Type is not ("11-bit" or "29-bit") ||
                row.Dlc > 8)
                throw new InvalidDataException("Malformed CAN RX monitor row metadata.");

            var extended = row.Type == "29-bit";
            var expectedWidth = extended ? 8 : 3;
            if (row.Id is null || row.Id.Length != expectedWidth ||
                !row.Id.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') ||
                !uint.TryParse(row.Id, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var id) ||
                id > (extended ? 0x1FFFFFFFu : 0x7FFu))
                throw new InvalidDataException("Malformed CAN arbitration ID.");

            var key = (extended, id);
            if (!groups.TryGetValue(key, out var counter))
            {
                counter = new DlcCounter();
                groups.Add(key, counter);
            }

            counter.FrameCount++;
            counter.DlcCounts[(int)row.Dlc]++;

            // Even when a frame contains VIN, PIN or credentials in bytes,
            // the Data field is NEVER used or reflected in this report.
        }

        var report = new StringBuilder()
            .AppendLine("Dvinholm | CAN RX RESEARCH METADATA (OFFLINE REPORT)")
            .AppendLine(syntheticDemo
                ? "Source: SYNTHETIC DEMO. No physical vehicle evidence."
                : "Source: buffered monitor RX rows. Bus/ECU origin NOT authenticated.")
            .Append("Total monitor RX rows since clear: ")
            .AppendLine(totalReceivedSinceClear.ToString(CultureInfo.InvariantCulture))
            .Append("Rows retained in bounded history: ")
            .AppendLine(rows.Count.ToString(CultureInfo.InvariantCulture))
            .Append("Older rows no longer buffered: ")
            .AppendLine((totalReceivedSinceClear - rows.Count).ToString(CultureInfo.InvariantCulture))
            .Append("RX subscriber reported drops: ")
            .AppendLine(subscriberDroppedFrames is long dropped
                ? dropped.ToString(CultureInfo.InvariantCulture)
                : "UNKNOWN (no active subscription)")
            .Append("Distinct CAN ID + format groups: ")
            .AppendLine(groups.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine("Only CAN identifier, format, DLC distribution and frame counts follow.")
            .AppendLine("No frame payload, raw DID values, VIN or timestamps are included.");

        var ordered = groups.OrderBy(pair => pair.Key.Extended)
            .ThenBy(pair => pair.Key.Id)
            .Take(MaximumDisplayedIdGroups);

        foreach (var (key, counter) in ordered)
        {
            report.Append("  CAN ")
                .Append(key.Extended ? "29-bit " : "11-bit ")
                .Append(key.Id.ToString(key.Extended ? "X8" : "X3",
                    CultureInfo.InvariantCulture))
                .Append(": ").Append(counter.FrameCount)
                .Append(" row(s), DLC ");

            var first = true;
            for (var dlc = 0; dlc < counter.DlcCounts.Length; dlc++)
            {
                if (counter.DlcCounts[dlc] == 0)
                    continue;
                if (!first)
                    report.Append(", ");
                report.Append(dlc).Append('=').Append(counter.DlcCounts[dlc]);
                first = false;
            }
            report.AppendLine();
        }

        if (groups.Count > MaximumDisplayedIdGroups)
            report.Append("Additional groups withheld by bounded display: ")
                .AppendLine((groups.Count - MaximumDisplayedIdGroups)
                    .ToString(CultureInfo.InvariantCulture));

        return report
            .AppendLine("Metadata is incomplete if monitor rows were evicted or RX frames dropped.")
            .AppendLine("CAN IDs and their timing/count distributions can fingerprint a vehicle.")
            .AppendLine("RX-only reporting does not prove another tool did not transmit CAN.")
            .AppendLine("This report cannot identify a physical Volvo P2 CEM, revision,")
            .AppendLine("diagnostic request/response address or compatible PIN/configuration service.")
            .AppendLine("No CAN transmission, diagnostic requests, PIN or writes performed by this report.")
            .AppendLine("Review before sharing; clipboard copy requires explicit user action.")
            .ToString();
    }

    private sealed class DlcCounter
    {
        internal int FrameCount { get; set; }
        internal int[] DlcCounts { get; } = new int[9];
    }
}
