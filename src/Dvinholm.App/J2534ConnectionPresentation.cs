namespace Dvinholm.App;

/// <summary>
/// Separation between payload-free Aurora activity and locally visible adapter
/// metadata. Vendor version strings are untrusted, and must never flow into
/// MainWindow.SetStatus or the home page's last-activity feed.
/// </summary>
internal static class J2534ConnectionPresentation
{
    internal const string DisconnectedDetails = "J2534 adapter details: disconnected.";
    internal const string ConnectedActivity =
        "Connected to J2534 CAN. Driver-reported details are in the local adapter workspace.";
    internal const string ReadErrorActivity =
        "J2534 CAN read failed. Check the adapter connection and restart monitoring if needed.";

    internal static string LocalDetails(
        J2534Native.AdapterVersionInfo? version,
        J2534Native.AdapterCapabilities? capabilities)
    {
        var versionText = version is null
            ? "version not reported"
            : "API " + Clean(version.Api) +
              " · DLL " + Clean(version.Dll) +
              " · FW " + Clean(version.Firmware);

        var capabilityText = capabilities is null
            ? "capabilities not reported"
            : "HW filters " + (capabilities.HardwareCanFilters ? "available" : "unavailable") +
              " · version query " + (capabilities.VersionQuery ? "available" : "unavailable");

        return "Driver-reported (unverified; local workspace only): " +
               versionText + " · " + capabilityText;
    }

    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "not supplied";

        // Display only: redaction is not authorization to copy this metadata to
        // public activity or persist it in a report.
        var sanitized = J2534Native.SanitizeDriverDiagnostic(value);
        return string.IsNullOrWhiteSpace(sanitized)
            ? "not supplied"
            : sanitized.Length > 80 ? sanitized[..80] + "…" : sanitized;
    }
}
