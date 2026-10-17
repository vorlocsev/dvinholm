# Dvinholm 0.2.0

Stable portable release for Windows x86.

## Highlights
- More robust in-app update discovery across paginated GitHub Actions runs.
- Stronger validation of candidate update assets before replacement.
- Improved J2534 session, CAN monitor, and UDS cancellation/shutdown lifecycle handling.
- Expanded automated regression coverage for updater, ISO-TP/UDS, and virtual J2534 paths.

## Download
- `Dvinholm-Windows-x86.zip` — self-contained portable Windows x86 build; no installer is included.
- `SHA256SUMS.txt` — SHA-256 checksum for the ZIP.

## Requirements and limitations
- Install your J2534 adapter's driver and 32-bit vendor DLL separately.
- UDS functionality is read-only.
- Physical adapter and vehicle validation has not been performed for this release and remains deferred.
