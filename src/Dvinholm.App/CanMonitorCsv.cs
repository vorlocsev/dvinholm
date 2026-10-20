namespace Dvinholm.App;

/// <summary>
/// Formats the CAN monitor's currently visible rows for CSV. Demo-only output
/// gets an explicit synthetic-source column; real capture keeps the old schema.
/// </summary>
internal static class CanMonitorCsv
{
    internal static IReadOnlyList<string> BuildLines(
        IEnumerable<CanMonitorRow> frames, bool syntheticDemo)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var lines = new List<string>
        {
            syntheticDemo
                ? "timestamp,id,type,dlc,data,source"
                : "timestamp,id,type,dlc,data"
        };

        foreach (var frame in frames)
        {
            var line = $"{frame.Timestamp},{frame.Id},{frame.Type},{frame.Dlc},\"{frame.Data}\"";
            lines.Add(syntheticDemo ? line + ",SYNTHETIC_DEMO" : line);
        }

        return lines;
    }
}
