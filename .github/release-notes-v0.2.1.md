# Dvinholm v0.2.1 — Windows x86 portable release

This maintenance release brings the Dvinholm branding into the actual Windows app and consolidates recent software-only improvements. It is an independent project, **not affiliated with or endorsed by Volvo Cars**.

## What's new
- Silver-and-blue Dvinholm icon for the EXE, WinUI window and the portable archive; refreshed GitHub README and banner.
- Saved CAN speed (125 / 250 / 500 kbit/s), restored after restart.
- Two **synthetic, unverified** ECU profiles available for UI testing, with improved profile-selection refresh and persistence.
- Hardened in-app updater and expanded regressions for cancellation immediately before extraction, sanitized HTTP download errors, and automatic rollback when launching an updated app fails.
- Further Windows x86 CI and virtual-adapter test coverage.

## Install
1. Download **Dvinholm-Windows-x86.zip** and **SHA256SUMS.txt** from this release.
2. Check the archive's SHA-256 hash, extract the complete ZIP to a writable folder, and launch **Dvinholm.App.exe**.
3. Install a suitable vendor-provided **32-bit J2534 driver** separately if you have an adapter.

The ZIP is a **portable, self-contained Windows x86 build**, not an installer. No driver is bundled.

## Limitations
- J2534 hardware, vendor-driver and vehicle/ECU compatibility have **not** been validated using a physical adapter. Virtual tests do not establish hardware support.
- Windows titlebar, taskbar, and Explorer icon appearance should be confirmed in a manual smoke test. Automated CI verifies the icon files and EXE packaging.
- UDS functions are **read-only**; do not assume any particular ECU supports candidate IDs or DIDs.
- No security bypass, actuation, key extraction, or ECU writing is included.

See the [README](https://github.com/vorlocsev/dvinholm#readme) and [changelog](https://github.com/vorlocsev/dvinholm/blob/main/CHANGELOG.md) for details.
