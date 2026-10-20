namespace Dvinholm.App;

/// <summary>
/// Synthetic data for offline UI demonstrations ONLY. No bus, J2534 device,
/// ECU profile, UDS client or vehicle is accessed by this generator.
/// </summary>
internal static class CanDemoFrameGenerator
{
    internal const uint IntervalMilliseconds = 200;

    internal static IReadOnlyList<CanMonitorRow> CreateBatch(uint tick)
    {
        var timestamp = unchecked(tick * IntervalMilliseconds);
        var marker = (byte)tick;
        return
        [
            new CanMonitorRow(timestamp, "7E0", "11-bit", 4,
                $"A5 5A {marker:X2} 01"),
            new CanMonitorRow(timestamp, "7E8", "11-bit", 3,
                $"D0 {marker:X2} 02"),
            new CanMonitorRow(timestamp, "18DAF110", "29-bit", 5,
                $"CA FE 00 {marker:X2} 55"),
            new CanMonitorRow(timestamp, "000", "11-bit", 1, "00")
        ];
    }
}
