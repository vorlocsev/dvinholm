using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Metadata-only, offline candidate header counts for *already buffered* RX
/// CAN Monitor frames. These proprietary-looking byte patterns are inspired
/// by the read-only classifier in the separate Virtual Car research project.
/// They are not proof of Volvo D2 traffic, physical CEM identity, addressing,
/// accepted responses, source ECU, protocol transport or bus completeness.
/// </summary>
internal static class CanMonitorD2EnvelopeCandidateSummary
{
    private enum CandidateKind
    {
        E3_02,
        E3Other,
        F9E0,
        F9F0,
        FragmentLike,
        Other
    }

    internal static string Build(
        IReadOnlyList<CanMonitorRow> rows,
        long totalReceivedSinceClear,
        long? subscriberDroppedFrames,
        bool syntheticDemo)
    {
        // Validate canonical CAN IDs, frame lengths, history bounds and drop
        // counters using the existing RX-only summary. Do not read its text or
        // treat any CAN arbitration ID as a verified CEM address.
        _ = CanMonitorRxResearchSummary.Build(
            rows, totalReceivedSinceClear, subscriberDroppedFrames, syntheticDemo);

        var standard = new Counts();
        var extended = new Counts();
        Span<byte> scratch = stackalloc byte[8];

        foreach (var row in rows)
        {
            if (!CanMonitorIsoTpShapeSummary.TryDecodeData(
                    row.Data, (int)row.Dlc, scratch))
                throw new InvalidDataException("Malformed buffered CAN RX D2 candidate input.");

            var counters = row.Type == "29-bit" ? extended : standard;
            counters.Total++;
            switch (Classify(scratch[..(int)row.Dlc]))
            {
                case CandidateKind.E3_02: counters.E3_02++; break;
                case CandidateKind.E3Other: counters.E3Other++; break;
                case CandidateKind.F9E0: counters.F9E0++; break;
                case CandidateKind.F9F0: counters.F9F0++; break;
                case CandidateKind.FragmentLike: counters.FragmentLike++; break;
                default: counters.Other++; break;
            }
        }

        var evicted = totalReceivedSinceClear - rows.Count;
        bool loss = evicted > 0 || subscriberDroppedFrames is > 0;
        bool dropsUnknown = subscriberDroppedFrames is null;
        var text = new StringBuilder()
            .AppendLine("POSSIBLE VOLVO D2 RX ENVELOPE SHAPES (OFFLINE, UNVERIFIED)")
            .AppendLine(syntheticDemo
                ? "Source: SYNTHETIC DEMO ONLY; no physical vehicle evidence."
                : "Source: retained CAN Monitor RX; ECU and physical origin NOT authenticated.")
            .AppendLine("Heuristic byte positions only: candidate [CB/CF] [40/50] [E3/F9] [subtype].")
            .AppendLine("Broad 08..0F / 40..4F / 80..8F first-byte markers are FRAGMENT-LIKE only.")
            .AppendLine("These patterns can occur in unrelated CAN traffic; no D2 validation is performed.")
            .Append("Buffered RX rows: ")
            .AppendLine(rows.Count.ToString(CultureInfo.InvariantCulture))
            .Append("History evicted: ")
            .AppendLine(evicted.ToString(CultureInfo.InvariantCulture))
            .Append("Subscriber RX drops: ")
            .AppendLine(dropsUnknown ? "UNKNOWN" :
                subscriberDroppedFrames!.Value.ToString(CultureInfo.InvariantCulture))
            .Append("Observation completeness: ")
            .AppendLine(loss ? "INCOMPLETE (reported history loss or subscriber drops)." :
                dropsUnknown ? "UNKNOWN (subscriber drop counter unavailable)." :
                    "NO REPORTED LOSS (not proof of complete physical bus capture).");

        AppendCounts(text, "11-bit", standard);
        AppendCounts(text, "29-bit", extended);

        return text
            .AppendLine("Counts are RX byte-shape hypotheses only, NOT CEM acknowledgment or D2 transaction evidence.")
            .AppendLine("No transport mode, bus speed, service direction, CAN address, ECU, VIN or PIN is inferred.")
            .AppendLine("No payload bytes, DID values, VIN, PIN, timestamps, CAN IDs or profile labels are displayed.")
            .AppendLine("Traffic shape and counts can still fingerprint a vehicle; review before sharing.")
            .AppendLine("No J2534/CAN calls, diagnostic requests, configuration changes or security access.")
            .ToString();
    }

    private static CandidateKind Classify(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 &&
            bytes[0] is 0xCB or 0xCF &&
            bytes[1] is 0x40 or 0x50)
        {
            if (bytes[2] == 0xE3)
                return bytes.Length >= 4 && bytes[3] == 0x02
                    ? CandidateKind.E3_02 : CandidateKind.E3Other;

            if (bytes.Length >= 4 && bytes[2] == 0xF9)
                return bytes[3] switch
                {
                    0xE0 => CandidateKind.F9E0,
                    0xF0 => CandidateKind.F9F0,
                    _ => CandidateKind.Other
                };
        }

        if (bytes.Length > 0 &&
            (bytes[0] is >= 0x08 and <= 0x0F or
                >= 0x40 and <= 0x4F or
                >= 0x80 and <= 0x8F))
            return CandidateKind.FragmentLike;

        return CandidateKind.Other;
    }

    private static void AppendCounts(StringBuilder text, string format, Counts c)
    {
        text.Append("  RX ").Append(format)
            .Append(": total=").Append(c.Total)
            .Append(", candidate-E3-02=").Append(c.E3_02)
            .Append(", candidate-E3-other=").Append(c.E3Other)
            .Append(", candidate-F9-E0=").Append(c.F9E0)
            .Append(", candidate-F9-F0=").Append(c.F9F0)
            .Append(", fragment-like=").Append(c.FragmentLike)
            .Append(", other=").Append(c.Other)
            .AppendLine();
    }

    private sealed class Counts
    {
        internal int Total;
        internal int E3_02;
        internal int E3Other;
        internal int F9E0;
        internal int F9F0;
        internal int FragmentLike;
        internal int Other;
    }
}
