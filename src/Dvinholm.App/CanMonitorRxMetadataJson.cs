using System.Text.Json;

namespace Dvinholm.App;

/// <summary>
/// Versioned, privacy-reduced CAN RX metadata snapshot for manual offline
/// investigation. This is NOT a physical bus capture or ECU identity proof.
/// It never reads CanMonitorRow.Data, opens an adapter, writes a file or sends CAN.
/// </summary>
internal static class CanMonitorRxMetadataJson
{
    internal const string Format = "dvinholm-can-rx-metadata";
    internal const int SchemaVersion = 1;
    internal const int MaximumJsonCharacters = 65_536;

    internal static string Build(
        IReadOnlyList<CanMonitorRow> rows,
        long totalReceivedSinceClear,
        long? subscriberDroppedFrames,
        bool syntheticDemo)
    {
        // Reuse the fail-closed bounds, DLC/ID and count validation from the
        // human-readable summary. Never trust malformed injected monitor rows.
        _ = CanMonitorRxResearchSummary.Build(
            rows, totalReceivedSinceClear, subscriberDroppedFrames, syntheticDemo);

        // Work ONLY with ID, format and DLC. No timestamps or payload.
        var ordered = rows
            .GroupBy(row => (row.Type, row.Id))
            .OrderBy(group => group.Key.Type, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Id, StringComparer.Ordinal)
            .ToArray();

        var snapshot = new
        {
            format = Format,
            schemaVersion = SchemaVersion,
            source = syntheticDemo ? "synthetic-demo" : "buffered-rx-unverified",
            containsPayload = false,
            containsTimestamps = false,
            confirmsPhysicalCemIdentity = false,
            authorizesDiagnosticsOrConfiguration = false,
            totalMonitorRxRowsSinceClear = totalReceivedSinceClear,
            retainedRxRows = rows.Count,
            evictedRxRows = totalReceivedSinceClear - rows.Count,
            subscriberQueueDropsSinceClear = subscriberDroppedFrames,
            distinctCanIdFormatGroups = ordered.Length,
            omittedGroups = Math.Max(
                ordered.Length - CanMonitorRxResearchSummary.MaximumDisplayedIdGroups, 0),
            canIdGroups = ordered
                .Take(CanMonitorRxResearchSummary.MaximumDisplayedIdGroups)
                .Select(group => new
                {
                    format = group.Key.Type,
                    id = group.Key.Id,
                    frameCount = group.Count(),
                    dlcCounts = group.GroupBy(row => row.Dlc)
                        .OrderBy(histogram => histogram.Key)
                        .Select(histogram => new { dlc = histogram.Key, count = histogram.Count() })
                        .ToArray()
                }).ToArray(),
            warning = "CAN IDs and counts may fingerprint a vehicle. Incomplete if frames " +
                "were evicted, skipped or dropped. RX observation does not authenticate " +
                "a physical CEM, an ISO-TP direction, a Volvo service or bus passivity."
        };

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        if (json.Length > MaximumJsonCharacters)
            throw new InvalidDataException("CAN RX JSON metadata exceeds the bounded output size.");

        return json;
    }
}
