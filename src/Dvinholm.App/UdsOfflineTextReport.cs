namespace Dvinholm.App;

/// <summary>
/// Generates an explicitly synthetic TXT report from the deterministic offline
/// fixture. This class cannot read StatusText, live UDS results or a vehicle.
/// </summary>
internal static class UdsOfflineTextReport
{
    internal const string SuggestedFileName = "uds-SYNTHETIC-example";

    internal static string Build()
    {
        return string.Join(Environment.NewLine,
        [
            "Dvinholm — OFFLINE SYNTHETIC UDS TXT REPORT",
            "No J2534 adapter, ECU or vehicle was accessed. This is not a diagnostic report.",
            "",
            UdsOfflineSample.Format(),
            ""
        ]);
    }
}
