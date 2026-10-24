using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Formats the CAN monitor's currently visible rows as RFC-style CSV.
/// Ordinary CAN values retain the historical column layout and quoting.
/// A demo-only source column explicitly labels synthetic output.
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
            ArgumentNullException.ThrowIfNull(frame);
            // Timestamp/DLC are numeric. All display strings are separately
            // escaped in case an upstream source supplies unexpected text.
            var line = string.Join(",",
                frame.Timestamp.ToString(CultureInfo.InvariantCulture),
                CsvCell(frame.Id),
                CsvCell(frame.Type),
                frame.Dlc.ToString(CultureInfo.InvariantCulture),
                CsvCell(frame.Data, alwaysQuote: true));
            lines.Add(syntheticDemo ? line + ",SYNTHETIC_DEMO" : line);
        }

        return lines;
    }

    private static string CsvCell(string? value, bool alwaysQuote = false)
    {
        var text = value ?? string.Empty;

        // Excel and other spreadsheet applications can evaluate cells that
        // start with formula operators, including after leading whitespace.
        // Neutralize only unexpected text; actual CAN hex values stay intact.
        var leading = text.TrimStart(' ', '\t', '\r', '\n');
        var formula = leading.Length != 0 &&
                      (leading[0] is '=' or '+' or '-' or '@');
        var controlPrefix = text.Length != 0 &&
                            (text[0] is '\t' or '\r' or '\n');
        if (formula || controlPrefix)
            text = "'" + text;

        var needsQuotes = alwaysQuote || formula || controlPrefix ||
                          text.IndexOfAny([',', '"', '\r', '\n']) >= 0;
        return needsQuotes ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}
