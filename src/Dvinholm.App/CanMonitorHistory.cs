using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// UI-safe representation of a received Classic CAN frame. Values are copied
/// before entering the WinUI dispatcher queue, independent of adapter buffers.
/// </summary>
internal sealed record CanMonitorRow(
    uint Timestamp,
    string Id,
    string Type,
    uint Dlc,
    string Data)
{
    internal static CanMonitorRow FromFrame(in CanFrame frame) =>
        new(frame.Timestamp, frame.IdHex, frame.IsExtended ? "29-bit" : "11-bit",
            frame.Dlc, frame.DataHex);
}

/// <summary>
/// Keeps a bounded history of ALL received frames so a changing software ID
/// filter can rebuild the view without losing previously hidden messages.
/// Only the UI thread accesses this type.
/// </summary>
internal sealed class CanMonitorHistory
{
    internal const int DefaultCapacity = 5000;
    private readonly int _capacity;
    private readonly Queue<CanMonitorRow> _recent = new();

    internal CanMonitorHistory(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    internal long TotalReceived { get; private set; }
    internal int BufferedCount => _recent.Count;

    /// <returns>The evicted oldest row, if the buffer was full.</returns>
    internal CanMonitorRow? Append(CanMonitorRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _recent.Enqueue(row);
        TotalReceived++;
        return _recent.Count > _capacity ? _recent.Dequeue() : null;
    }

    internal IReadOnlyList<CanMonitorRow> Filter(string? query) =>
        _recent.Where(row => MatchesCanIdFilter(query, row.Id)).ToArray();

    internal void Clear()
    {
        _recent.Clear();
        TotalReceived = 0;
    }

    internal static bool MatchesCanIdFilter(string? query, string frameId)
    {
        query = (query ?? string.Empty).Trim();
        if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            query = query[2..];
        if (query.Length == 0)
            return true;

        // A numerical zero is a real CAN ID, not a wildcard. Keep the
        // existing software-filter semantics for 11-bit and 29-bit frames.
        return uint.TryParse(query, NumberStyles.HexNumber,
                   CultureInfo.InvariantCulture, out var wanted) &&
               uint.TryParse(frameId, NumberStyles.HexNumber,
                   CultureInfo.InvariantCulture, out var actual) &&
               wanted == actual;
    }
}
