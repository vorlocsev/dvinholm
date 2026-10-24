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

    // A bounded explicit list prevents accidental work from enormous pasted
    // queries. This is only a software view filter, never a J2534 filter.
    internal const int MaxSoftwareFilterIds = 16;

    internal IReadOnlyList<CanMonitorRow> Filter(string? query)
    {
        // Parse once per UI refresh, not for every buffered CAN frame.
        if (!TryParseFilter(query, out var ids))
            return Array.Empty<CanMonitorRow>();

        return ids is null
            ? _recent.ToArray()
            : _recent.Where(row =>
                uint.TryParse(row.Id, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var id) &&
                ids.Contains(id)).ToArray();
    }

    internal void Clear()
    {
        _recent.Clear();
        TotalReceived = 0;
    }

    internal static bool MatchesCanIdFilter(string? query, string frameId)
    {
        if (!TryParseFilter(query, out var ids))
            return false;
        if (ids is null)
            return true;

        // Compare numbers so 11-bit 7E8 and 29-bit 000007E8 retain
        // the historical numeric-filter semantics.
        return uint.TryParse(frameId, NumberStyles.HexNumber,
                   CultureInfo.InvariantCulture, out var actual) &&
               ids.Contains(actual);
    }

    private static bool TryParseFilter(string? query, out HashSet<uint>? ids)
    {
        ids = null;
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0)
            return true;

        var segments = text.Split(',');
        if (segments.Length > MaxSoftwareFilterIds)
            return false;

        var parsed = new HashSet<uint>();
        foreach (var segment in segments)
        {
            var value = segment.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                value = value[2..];
            if (value.Length == 0 ||
                !uint.TryParse(value, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var id))
                return false;

            parsed.Add(id);
        }

        ids = parsed;
        return true;
    }
}
