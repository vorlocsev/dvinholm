namespace Dvinholm.App;

/// <summary>
/// Locale-only captions for existing CAN monitor counters. This never reads
/// frames, changes filters or resets dropped-frame accounting.
/// </summary>
internal static class CanMonitorCounterCaptions
{
    internal static string Rate(int framesPerSecond, bool english)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(framesPerSecond);
        return english
            ? $"{framesPerSecond:N0} frames/s"
            : $"{framesPerSecond:N0} кадров/с";
    }

    internal static string Frames(int shown, long received, bool english)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shown);
        ArgumentOutOfRangeException.ThrowIfNegative(received);
        return english
            ? $"{shown:N0} shown / {received:N0} rx"
            : $"{shown:N0} показано / {received:N0} RX";
    }

    internal static string Drops(long? dropped, bool english)
    {
        if (dropped is < 0)
            throw new ArgumentOutOfRangeException(nameof(dropped));
        if (dropped is not long count)
            return english ? "drops unknown" : "потери неизвестны";
        return english
            ? $"{count:N0} dropped since clear"
            : $"{count:N0} потеряно с очистки";
    }
}
