using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// A privacy-limited view of a completed, in-memory UDS 0x22 capture.
/// It never returns raw DID bytes, VIN, CAN IDs, profile identity or
/// source notes, and never authorizes a CEM configuration operation.
/// </summary>
internal static class EcuReadOnlyDidInventoryDisplay
{
    internal static string Build(EcuConfigurationReadSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Treat this formatter as an independent boundary. A caller could
        // pass a synthetic or imported snapshot rather than Capture().
        var maxCanId = snapshot.IsExtended ? 0x1FFFFFFFu : 0x7FFu;
        if (snapshot.CapturedAtUtc == default ||
            snapshot.CapturedAtUtc.Offset != TimeSpan.Zero ||
            snapshot.RequestId > maxCanId ||
            snapshot.ResponseId > maxCanId ||
            snapshot.RequestId == snapshot.ResponseId ||
            snapshot.Values is null ||
            snapshot.Values.Count is < 1 or > EcuConfigurationReadback.MaximumDataIdentifiers)
            throw new InvalidDataException("Invalid read-only diagnostic capture metadata.");

        var lengths = new SortedDictionary<ushort, int>();
        foreach (var item in snapshot.Values)
        {
            if (item is null || item.ValueHex is not { } value ||
                value.Length is < 2 or > EcuConfigurationReadback.MaximumDidValueBytes * 2 ||
                (value.Length & 1) != 0 ||
                !value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') ||
                !lengths.TryAdd(item.DataIdentifier, value.Length / 2))
                throw new InvalidDataException("Invalid or duplicate diagnostic DID value.");
        }

        var summary = new StringBuilder();
        summary.AppendLine("Read-only ECU DID inventory | completed capture");
        summary.AppendLine("DID identifiers and response lengths only. Values, VIN and profile metadata are hidden.");
        summary.AppendLine("These observations do not verify physical CEM identity, firmware or DID meanings.");
        summary.AppendLine("Not a restorable configuration backup. No PIN or configuration write is supported.");
        summary.Append("Observed DIDs: ").AppendLine(lengths.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var pair in lengths)
            summary.Append("  DID 0x")
                .Append(pair.Key.ToString("X4", CultureInfo.InvariantCulture))
                .Append(": ")
                .Append(pair.Value.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" byte(s); value hidden");

        return summary.ToString();
    }
}
