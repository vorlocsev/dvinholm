# Changelog

## [0.2.3] — Unreleased (candidate preparation)

- Stable Release now excludes synthetic demo buttons, handlers and fixture generators; Development explicitly opts in, existing saved user profiles remain untouched.

- CAN Monitor adds bounded multi-ID software filtering without changing J2534 hardware filter behavior.
- CSV export handles delimiters, quotes, multiline cells and unexpected spreadsheet formulas.
- Additional Portable/Lite SHA-256, updater regression and experimental unsigned MSIX verification; trimming/AOT disabled.
- Release-candidate Windows validation is pending; no physical J2534 support is claimed.


## [0.2.2] — 2026-10-20

### Added
- Offline **Demo CAN Monitor** in WinUI: clearly labeled, deterministic synthetic CAN frames without a J2534 adapter or vehicle, with Start/Stop, filter, counters, Clear and source-marked CSV export. Demo frames are isolated from real J2534 and read-only UDS workflows.
- Built-in **New profile / Edit selected** dialogs with validated 11/29-bit CAN IDs, candidate DIDs, required provenance and persistent JSON catalog updates; edited/new profiles are always marked unverified.
- Searchable ECU-profile catalog by ID/name/source/CAN ID/DID with provenance details and an optional author-marked verified filter.
- Readable, selectable UDS DID and DTC response formatting: validated VIN, raw bytes and strictly labeled optional ASCII previews; individual DTC records and ISO 14229 status bits.
- Additional coverage and visibility of passive ECU discovery, cancellation and CAN receive losses.

### Improved and fixed
- CAN Monitor now buffers the last **5,000 received frames** independently of the active software CAN-ID filter; changing or clearing the filter restores still-buffered rows. Live counters distinguish displayed vs received frames.
- Invalid ECU-editor fields display a clear validation message with appropriate field highlighting and focus; invalid or duplicate CAN IDs/DIDs and required metadata are rejected.
- Improved WinUI layout for bounded diagnostic output and ECU-profile descriptions; continued virtual-J2534, ISO-TP/UDS, updater and lifecycle regression coverage.

### Validation and limitations
- Offline Demo CAN Monitor completed eight **user-confirmed Windows smoke scenarios** on a CI build, including filter replay, zero/invalid ID handling, actual synthetic-tagged CSV export, Clear/Stop and default-off after restart. ECU profile editor validation and persistence were also manually exercised.
- The versioned release workflow rebuilds/tests Windows x86, packages a self-contained portable ZIP, checks the branded icon and published release metadata, and re-downloads and verifies the ZIP against SHA-256.
- No physical J2534 adapter, vendor-driver, ECU or vehicle compatibility has been verified; the application is **not affiliated with or endorsed by Volvo Cars**. UDS functionality remains read-only. No vehicle-specific DID or fault interpretation is implied by synthetic data.

## [0.2.1] — 2026-10-19

### Added
- Branded GitHub README banner and new Dvinholm application icon, embedded in the Windows executable and displayed in the WinUI title bar.
- Built-in clearly labeled synthetic ECU profiles for UI testing, without asserting vehicle-specific compatibility.
- Persistent CAN baud-rate selection for supported speeds: 125, 250 and 500 kbit/s.

### Improved and fixed
- Restore ECU profile fields reliably when switching between saved profiles.
- Expand updater safety coverage: cancel before archive extraction, redact signed download query parameters from error messages, and restore the previous build when a new executable fails to start.
- Polish connection, update-status, and Windows UI recovery behavior with additional hardware-independent regressions.

### Validation and limitations
- The stable-release workflow builds/tests a self-contained portable Windows x86 ZIP, checks that the icon is packaged, publishes a SHA-256 checksum, and verifies the uploaded ZIP.
- Real J2534 adapter, driver and vehicle compatibility is **not verified**; hardware testing remains deferred.
- UDS remains read-only. Not affiliated with Volvo Cars or adapter manufacturers.


## [0.2.0] — 2026-10-17

### Improved
- Make in-app update discovery robust when the newest GitHub Actions runs are not eligible to update: paginate workflow runs and exclude pull-request merge commits.
- Validate release asset metadata and content integrity before preparing an update.
- Harden J2534 connection shutdown, CAN monitor lifecycle, and UDS cancellation so active operations do not race native resource cleanup.
- Improve recovery behavior around update failures and diagnostic operation errors.

### Validation
- Windows x86 Release build and automated test suite must pass in the stable-release workflow before publication.
- Published ZIP and SHA-256 checksum are generated from the same self-contained portable Windows x86 package.
- Physical J2534 adapter and vehicle validation remains deferred; no hardware compatibility claim is made.

## [0.1.1] — 2026-10-16

### Fixed
- Prevent the updater from offering to replace the application when the installed and available commit SHAs are identical.
- Keep the CAN Monitor visible when an ECU profile is selected by compacting the ECU profile controls.
- Keep the ECU profile selector and JSON import/export actions visible at narrower window widths.

### Validation
- GitHub Actions build and PR validation passed for the compact ECU layout and same-SHA updater fix before merge.
- Windows manual UI checks confirmed CAN Monitor visibility and no clipping of ECU profile controls at normal and narrower widths.
- Physical J2534 adapter and vehicle testing remains deferred to a future release.
