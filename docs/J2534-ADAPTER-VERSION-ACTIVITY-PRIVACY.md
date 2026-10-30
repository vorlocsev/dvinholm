# J2534 version metadata privacy boundary (Development)

## Problem

The status bar uses `SetStatus`, which also updates the Aurora home screen's last-activity feed. Even after connection/filter errors were hardened in PR #641, two J2534 paths still used external, driver-controlled text there:

1. Successful connections appended the vendor-reported firmware, DLL and API strings, or the driver's error returned by `PassThruReadVersion`.
2. The background RX worker forwarded sanitized `ReadError.Exception.Message`. Redacting common filesystem paths is not sufficient to guarantee a vendor message is free of private vehicle identifiers or unknown driver-specific values.

## Change

- The global success activity is a **fixed** confirmation that J2534 CAN is connected; it never includes driver-returned strings.
- The RX read-error activity is a **fixed**, actionable warning with no exception payload, and ignores late callbacks after closing or disconnecting.
- Existing driver-reported version and capability information stays available for troubleshooting in a small, dedicated **local adapter-details line in the diagnostics workspace**. Its text is explicitly labeled *driver-reported and unverified*, is not copied to Aurora, and never proves an ECU identity or compatibility.
- Driver-provided version parts in that local line are bounded, control characters removed and common path-like fragments redacted for display. A failed or unsupported optional version query reports **version not reported** without forwarding a driver error string.
- Starting a new connection or disconnecting clears prior adapter details immediately. Failure to establish a replacement connection therefore cannot display stale version metadata.
- No changes to native J2534 version queries, CAN TX/RX transport, filter operations, UDS, CEM PIN/configuration or driver installation.

## Eight offline checks

1. Public successful-connection activity is constant and contains no vendor data.
2. Public RX error activity has no vendor exception details.
3. Local view retains separately labeled API, DLL and firmware versions.
4. Missing version and capability information remains unknown.
5. Boolean filter/version-query capabilities remain visible only locally.
6. Local version strings are redacted/bounded for display.
7. XAML metadata line lives inside the local diagnostic workspace, not Aurora home.
8. Actual WinUI source uses trusted activity messages and clears stale metadata on connection/disconnection.

This is not an installed GUI, physical DiCE or other J2534 acceptance test. No release is published. Stable v0.2.4 and VirtualCar remain unchanged.
