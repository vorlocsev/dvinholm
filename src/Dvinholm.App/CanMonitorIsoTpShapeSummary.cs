using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// A payload-redacted, heuristic summary of PCI *shapes* in already received
/// Classic CAN frames. A plausible normal-addressing ISO-TP byte pattern is
/// NOT a verified transport, request/response direction or CEM identity.
/// </summary>
internal static class CanMonitorIsoTpShapeSummary
{
    internal const int MaximumDisplayedIdGroups = 64;

    internal static string Build(
        IReadOnlyList<CanMonitorRow> rows,
        long totalReceivedSinceClear,
        long? subscriberDroppedFrames,
        bool syntheticDemo)
    {
        // Validate the same bounded capture metadata as the existing RX
        // report. Unlike that report, this deliberately inspects the CAN
        // payload in memory but outputs ONLY aggregate shape counts.
        _ = CanMonitorRxResearchSummary.Build(
            rows, totalReceivedSinceClear, subscriberDroppedFrames, syntheticDemo);

        var groups = new Dictionary<(bool Extended, uint Id), FrameCounts>();
        // Reuse one 8-byte scratch span across the bounded RX history. Each
        // iteration parses and classifies only the exact DLC-length slice.
        Span<byte> bytes = stackalloc byte[8];
        foreach (var row in rows)
        {
            if (row is null || row.Dlc > 8)
                throw new InvalidDataException("Invalid CAN RX shape input.");

            var isExtended = row.Type == "29-bit";
            if (!uint.TryParse(row.Id, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var id))
                throw new InvalidDataException("Invalid CAN RX arbitration identifier.");

            if (!TryDecodeData(row.Data, (int)row.Dlc, bytes))
                throw new InvalidDataException("Malformed CAN RX payload shape.");

            var shape = Classify(bytes[..(int)row.Dlc]);
            var key = (isExtended, id);
            if (!groups.TryGetValue(key, out var counts))
            {
                counts = new FrameCounts();
                groups.Add(key, counts);
            }
            counts.Total++;
            switch (shape)
            {
                case 0: counts.Single++; break;
                case 1: counts.First++; break;
                case 2: counts.Consecutive++; break;
                case 3: counts.Flow++; break;
                default: counts.Other++; break;
            }
        }

        var output = new StringBuilder()
            .AppendLine("POTENTIAL ISO-TP FRAME SHAPES (OFFLINE, HEURISTIC)")
            .AppendLine(syntheticDemo
                ? "Source: synthetic demo frames; NOT physical vehicle evidence."
                : "Source: buffered RX frames; physical origin NOT authenticated.")
            .AppendLine("Only the normal-addressing PCI pattern in the first payload byte is examined.")
            .AppendLine("An extended-addressing or proprietary Volvo D2 frame may NOT match these rules.")
            .AppendLine("Even a matching PCI shape may be unrelated traffic or a false positive.")
            .Append("Buffered frames examined: ").AppendLine(rows.Count.ToString(CultureInfo.InvariantCulture))
            .Append("Distinct CAN ID + format groups: ").AppendLine(groups.Count.ToString(CultureInfo.InvariantCulture));

        foreach (var pair in groups.OrderBy(x => x.Key.Extended)
                     .ThenBy(x => x.Key.Id).Take(MaximumDisplayedIdGroups))
        {
            var c = pair.Value;
            output.Append("  CAN ").Append(pair.Key.Extended ? "29-bit " : "11-bit ")
                .Append(pair.Key.Id.ToString(pair.Key.Extended ? "X8" : "X3",
                    CultureInfo.InvariantCulture))
                .Append(": possible SF=").Append(c.Single)
                .Append(", FF=").Append(c.First)
                .Append(", CF=").Append(c.Consecutive)
                .Append(", FC=").Append(c.Flow)
                .Append(", other=").Append(c.Other)
                .AppendLine();
        }

        if (groups.Count > MaximumDisplayedIdGroups)
            output.Append("Additional groups withheld: ")
                .AppendLine((groups.Count - MaximumDisplayedIdGroups)
                    .ToString(CultureInfo.InvariantCulture));

        return output
            .AppendLine("SF=single, FF=first, CF=consecutive, FC=flow control; counts are NOT validated exchanges.")
            .AppendLine("No request/response pairing, addressing direction, transport session or ECU is inferred.")
            .AppendLine("Missing/evicted frames or RX subscriber drops may hide complete message sequences.")
            .AppendLine("No raw payload bytes, DID values, VIN, PIN, timestamps or profile data are output.")
            .AppendLine("CAN identifiers and frame-count patterns may still fingerprint a vehicle.")
            .AppendLine("No diagnostic requests, J2534 writes, PIN access or CEM configuration changes.")
            .ToString();
    }

    // -1 means non-matching or ambiguous, never an ECU error.
    // Only 8-byte Classic CAN, normal-addressing candidate shapes are tested.
    private static int Classify(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            return -1;

        int pci = bytes[0] >> 4;
        int low = bytes[0] & 0x0F;
        return pci switch
        {
            0 when low >= 1 && low <= 7 && low <= bytes.Length - 1 => 0,
            1 when bytes.Length == 8 &&
                   (((low << 8) | bytes[1]) > 7) => 1,
            2 when bytes.Length >= 2 => 2,
            3 when bytes.Length >= 3 && low <= 2 &&
                   (bytes[2] <= 0x7F || bytes[2] is >= 0xF1 and <= 0xF9) => 3,
            _ => -1
        };
    }

    // CAN monitor DataHex is uppercase, space-separated byte pairs.
    // Do not echo or persist malformed user/device content.
    internal static bool TryDecodeData(string? data, int dlc, Span<byte> destination)
    {
        if (data is null || dlc is < 0 or > 8)
            return false;
        if (dlc == 0)
            return data.Length == 0;
        if (data.Length != dlc * 3 - 1)
            return false;

        for (int i = 0; i < dlc; i++)
        {
            int offset = i * 3;
            int hi = Hex(data[offset]), lo = Hex(data[offset + 1]);
            if (hi < 0 || lo < 0 || (i != dlc - 1 && data[offset + 2] != ' '))
                return false;
            destination[i] = (byte)((hi << 4) | lo);
        }
        return true;
    }

    private static int Hex(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };

    private sealed class FrameCounts
    {
        internal int Total;
        internal int Single;
        internal int First;
        internal int Consecutive;
        internal int Flow;
        internal int Other;
    }
}
