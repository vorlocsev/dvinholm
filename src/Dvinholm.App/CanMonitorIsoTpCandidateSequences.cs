using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Offline, bounded *candidate* sequence tracking over already retained
/// CAN Monitor RX rows. The tracker never reassembles or emits payload bytes.
/// Neither a matching PCI pattern nor a plausible byte count establishes
/// ISO-TP, direction, an ECU, a CEM revision or physical bus completeness.
/// </summary>
internal static class CanMonitorIsoTpCandidateSequences
{
    internal const int MaximumDisplayedGroups = 64;

    internal static string Build(
        IReadOnlyList<CanMonitorRow> rows,
        long totalReceivedSinceClear,
        long? subscriberDroppedFrames,
        bool syntheticDemo) =>
        BuildCore(rows, totalReceivedSinceClear, subscriberDroppedFrames,
            syntheticDemo, showCanIds: true);

    /// <summary>
    /// Use for a selected offline CSV. All candidate sequences are still
    /// counted by CAN ID internally, but no arbitration IDs appear in the
    /// output. This does NOT authenticate the CSV or its frame continuity.
    /// </summary>
    internal static string BuildAggregate(
        IReadOnlyList<CanMonitorRow> rows,
        long totalReceivedSinceClear,
        long? subscriberDroppedFrames,
        bool syntheticDemo) =>
        BuildCore(rows, totalReceivedSinceClear, subscriberDroppedFrames,
            syntheticDemo, showCanIds: false);

    private static string BuildCore(
        IReadOnlyList<CanMonitorRow> rows,
        long totalReceivedSinceClear,
        long? subscriberDroppedFrames,
        bool syntheticDemo,
        bool showCanIds)
    {
        // Reuse strict capture-history bounds and canonical arbitration-ID
        // validation. No adapters, files, network, telemetry or clipboard.
        _ = CanMonitorRxResearchSummary.Build(
            rows, totalReceivedSinceClear, subscriberDroppedFrames, syntheticDemo);

        var groups = new Dictionary<(bool Extended, uint Id), GroupStats>();
        Span<byte> bytes = stackalloc byte[8];
        uint? previousTimestamp = null;
        var timestampRegressions = 0;
        var boundaryInvalidatedCandidates = 0;

        foreach (var row in rows)
        {
            // A selected CSV may contain a restarted logger, concatenated
            // captures or a wrapping 32-bit J2534 clock. The origin and units
            // of timestamps are unverified; never join candidate messages
            // across a backwards jump. Reset ALL CAN-ID groups because the
            // discontinuity is in the global CSV capture, not one ECU.
            // The live per-ID monitor report retains its existing behavior.
            if (!showCanIds &&
                previousTimestamp is uint previous &&
                row.Timestamp < previous)
            {
                timestampRegressions++;
                foreach (var group in groups.Values)
                {
                    if (group.Active is null)
                        continue;
                    group.Active = null;
                    boundaryInvalidatedCandidates++;
                }
            }
            previousTimestamp = row.Timestamp;
            if (!uint.TryParse(row.Id, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var id) ||
                !CanMonitorIsoTpShapeSummary.TryDecodeData(
                    row.Data, (int)row.Dlc, bytes))
                throw new InvalidDataException("Invalid historical CAN RX frame shape.");

            var key = (row.Type == "29-bit", id);
            if (!groups.TryGetValue(key, out var stats))
            {
                stats = new GroupStats();
                groups.Add(key, stats);
            }

            var payload = bytes[..(int)row.Dlc];
            int kind = payload.Length == 0 ? -1 : payload[0] >> 4;

            // Classic CAN, normal-addressed FF: 12-bit length and six initial
            // data bytes. The shape can also occur in unrelated traffic.
            if (kind == 1 && payload.Length == 8 &&
                (((payload[0] & 0x0F) << 8) | payload[1]) > 7)
            {
                if (stats.Active is not null)
                    stats.Interrupted++;

                int length = ((payload[0] & 0x0F) << 8) | payload[1];
                stats.Starts++;
                stats.Active = new Candidate(length - 6, nextSequenceNumber: 1);
                continue;
            }

            // A CF has one PCI byte and 1-7 payload bytes. We compare only
            // the four-bit sequence counter within the SAME CAN-ID/format.
            // We never invent the complementary CAN ID or flow-control peer.
            if (kind == 2 && payload.Length >= 2)
            {
                if (stats.Active is null)
                {
                    stats.OrphanConsecutive++;
                    continue;
                }

                int sequenceNumber = payload[0] & 0x0F;
                if (sequenceNumber != stats.Active.NextSequenceNumber)
                {
                    stats.SequenceMismatch++;
                    stats.Active = null;
                    continue;
                }

                stats.Active.RemainingBytes -= payload.Length - 1;
                stats.Active.NextSequenceNumber =
                    (stats.Active.NextSequenceNumber + 1) & 0x0F;

                if (stats.Active.RemainingBytes <= 0)
                {
                    stats.PlausibleCompletions++;
                    stats.Active = null;
                }
                continue;
            }

            // Any other shape on this exact CAN ID interrupts a candidate.
            // Other CAN IDs may interleave freely and never imply direction.
            if (stats.Active is not null)
            {
                stats.Interrupted++;
                stats.Active = null;
            }
        }

        // Aggregate across ALL groups, including those withheld by the
        // 64-group preview limit. These counts describe only byte patterns.
        var totals = new
        {
            First = groups.Values.Sum(g => g.Starts),
            Plausible = groups.Values.Sum(g => g.PlausibleCompletions),
            Trailing = groups.Values.Count(g => g.Active is not null),
            Mismatch = groups.Values.Sum(g => g.SequenceMismatch),
            Interrupted = groups.Values.Sum(g => g.Interrupted),
            Orphan = groups.Values.Sum(g => g.OrphanConsecutive)
        };
        long evicted = totalReceivedSinceClear - rows.Count;
        bool dropsUnknown = subscriberDroppedFrames is null;
        bool hasReportedLoss = evicted > 0 || subscriberDroppedFrames > 0;

        var output = new StringBuilder()
            .AppendLine("POTENTIAL ISO-TP SEQUENCES (OFFLINE, UNVERIFIED)")
            .AppendLine(syntheticDemo
                ? "Source: SYNTHETIC DEMO, not physical vehicle evidence."
                : "Source: buffered RX rows, physical origin unverified.")
            .AppendLine("Assumes Classic CAN normal addressing only; proprietary D2/extended addressing not decoded.")
            .AppendLine("Tracks first-frame lengths and consecutive-frame counters ONLY within identical CAN ID/format.")
            .Append("Buffered RX rows: ")
            .AppendLine(rows.Count.ToString(CultureInfo.InvariantCulture))
            .Append("Distinct groups: ")
            .AppendLine(groups.Count.ToString(CultureInfo.InvariantCulture))
            .Append("History rows evicted: ")
            .AppendLine(evicted.ToString(CultureInfo.InvariantCulture))
            .Append("Subscriber RX drops: ")
            .AppendLine(dropsUnknown ? "UNKNOWN" :
                subscriberDroppedFrames!.Value.ToString(CultureInfo.InvariantCulture))
            .Append("Observed window quality: ")
            .AppendLine(hasReportedLoss
                ? "INCOMPLETE: retained history eviction or subscriber drops were reported."
                : dropsUnknown
                    ? "UNKNOWN: subscriber drop counter is unavailable."
                    : "NO REPORTED LOSS within the retained monitor window (NOT a full bus capture).")
            .Append("All-group candidate totals (including hidden groups): FF=")
            .Append(totals.First)
            .Append(", plausible-completion=").Append(totals.Plausible)
            .Append(", trailing-incomplete=").Append(totals.Trailing)
            .Append(", sequence-mismatch=").Append(totals.Mismatch)
            .Append(", interrupted=").Append(totals.Interrupted)
            .Append(", orphan-CF=").AppendLine(totals.Orphan.ToString(CultureInfo.InvariantCulture))
            .AppendLine("All counts are heuristics, not verified ISO-TP sessions:");

        if (!showCanIds)
        {
            output.AppendLine("Offline CSV aggregate mode: CAN identifiers withheld.")
                .Append("Timestamp regressions/wraps in CSV: ")
                .AppendLine(timestampRegressions.ToString(CultureInfo.InvariantCulture))
                .Append("Active candidates invalidated at timestamp boundaries: ")
                .AppendLine(boundaryInvalidatedCandidates.ToString(CultureInfo.InvariantCulture))
                .AppendLine("A timestamp decrease is an unverified capture boundary or uint32 wrap; no frames are joined across it.")
                .AppendLine("Even monotonic timestamps do not validate timestamp units, inter-frame delays or capture completeness.");
        }
        else
        {
        foreach (var group in groups.OrderBy(g => g.Key.Extended)
                     .ThenBy(g => g.Key.Id).Take(MaximumDisplayedGroups))
        {
            var c = group.Value;
            int incomplete = c.Active is null ? 0 : 1;
            output.Append("  CAN ")
                .Append(group.Key.Extended ? "29-bit " : "11-bit ")
                .Append(group.Key.Id.ToString(
                    group.Key.Extended ? "X8" : "X3", CultureInfo.InvariantCulture))
                .Append(": candidate FF=").Append(c.Starts)
                .Append(", plausible-completion=").Append(c.PlausibleCompletions)
                .Append(", trailing-incomplete=").Append(incomplete)
                .Append(", sequence-mismatch=").Append(c.SequenceMismatch)
                .Append(", interrupted=").Append(c.Interrupted)
                .Append(", orphan-CF=").Append(c.OrphanConsecutive)
                .AppendLine();
        }

        if (groups.Count > MaximumDisplayedGroups)
            output.Append("Additional groups withheld: ")
                .AppendLine((groups.Count - MaximumDisplayedGroups)
                    .ToString(CultureInfo.InvariantCulture));
        }

        return output
            .AppendLine(!hasReportedLoss && !dropsUnknown
                ? "No history eviction or subscriber drops reported; other missed frames still possible."
                : "Frame continuity UNKNOWN/INCOMPLETE: eviction, drops or missing counters possible.")
            .AppendLine("History and subscriber counters cannot prove that all physical bus frames were received.")
            .AppendLine("A plausible completion is a length/counter pattern, NOT a validated diagnostic exchange.")
            .AppendLine("No frame payload, VIN, PIN, DID values, timestamps, profile IDs or secret strings are output.")
            .AppendLine("No source ECU, request/response pairing, flow-control peer or Volvo CEM address is identified.")
            .AppendLine(showCanIds
                ? "CAN IDs and traffic patterns may fingerprint a vehicle. Review before sharing."
                : "CAN IDs are withheld in this view, but aggregate patterns may still fingerprint traffic. Review before sharing.")
            .AppendLine("No CAN/J2534 operation, diagnostic request, configuration write or PIN access.")
            .ToString();
    }

    private sealed class Candidate
    {
        internal Candidate(int remainingBytes, int nextSequenceNumber)
        {
            RemainingBytes = remainingBytes;
            NextSequenceNumber = nextSequenceNumber;
        }

        internal int RemainingBytes;
        internal int NextSequenceNumber;
    }

    private sealed class GroupStats
    {
        internal int Starts;
        internal int PlausibleCompletions;
        internal int Interrupted;
        internal int SequenceMismatch;
        internal int OrphanConsecutive;
        internal Candidate? Active;
    }
}
