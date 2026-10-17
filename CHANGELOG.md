# Changelog

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
