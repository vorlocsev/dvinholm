# Changelog

## [0.1.1] — 2026-10-16

### Fixed
- Prevent the updater from offering to replace the application when the installed and available commit SHAs are identical.
- Keep the CAN Monitor visible when an ECU profile is selected by compacting the ECU profile controls.
- Keep the ECU profile selector and JSON import/export actions visible at narrower window widths.

### Validation
- GitHub Actions build and PR validation passed for the compact ECU layout and same-SHA updater fix before merge.
- Windows manual UI checks confirmed CAN Monitor visibility and no clipping of ECU profile controls at normal and narrower widths.
- Physical J2534 adapter and vehicle testing remains deferred to a future release.
