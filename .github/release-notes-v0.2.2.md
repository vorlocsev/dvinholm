# Dvinholm v0.2.2 — Windows x86 portable release

This release expands the safe, read-only software workflow for exploring CAN traffic and ECU profile metadata. Dvinholm is an independent project, **not affiliated with or endorsed by Volvo Cars**.

## What's new

- **Offline Demo CAN Monitor:** use **Start demo** without a J2534 adapter to generate clearly marked synthetic CAN frames. Practice filtering, observe counters, Clear/Stop, and export a demo CSV whose source column explicitly contains `SYNTHETIC_DEMO`. Demo does not connect to a vehicle or send a diagnostic request.
- **Better CAN Monitor history:** last 5,000 RX frames remain in a bounded buffer independent of the displayed CAN ID filter. Previously hidden recent frames reappear when a filter is removed.
- **ECU profiles:** create and edit local test profiles using a validated WinUI editor; search by metadata, CAN ID or candidate DID. New/edited entries are unverified, and missing source notes, invalid IDs and duplicate DIDs are rejected.
- **Read-only UDS results:** clearer DID, VIN and DTC output, with selectable raw data, DTC status flags and optional explicitly unverified ASCII previews.
- **Passive ECU discovery** and additional no-hardware regression test coverage, with careful reporting of incomplete scans and receive drops.

## Verified before release

- All eight main **offline Demo CAN Monitor Windows scenarios** were confirmed manually on CI builds: Start demo, filter `7E8`, zero/invalid filter cases, restoration of buffered rows, tagged CSV saved and inspected, Clear, Stop, and restart default-off.
- Editor validation, valid profile persistence and protection against duplicates were also exercised in Windows.
- The dedicated release workflow builds and tests x86, packages the portable archive, publishes a SHA-256 checksum, checks the branded icon, and re-downloads and verifies the published ZIP.

## Download and installation

1. Download **Dvinholm-Windows-x86.zip** and **SHA256SUMS.txt** from this release.
2. Verify SHA-256, extract the entire archive into a **dedicated writable folder**, and run `Dvinholm.App.exe`.
3. If you later use a real adapter, install the manufacturer's compatible **32-bit J2534 driver** separately. No driver is bundled.

The package is a **self-contained, portable Windows x86 application**, not an installer.

## Important limitations

- Compatibility with a **physical J2534 adapter, vendor driver, ECU or vehicle has not been demonstrated**. Hardware/vehicle testing is deferred. Virtual tests and offline demo mode are not evidence of real hardware support.
- Synthetic CAN IDs, ECU profiles and candidate DIDs are not verified for any Volvo or other vehicle.
- UDS operations are read-only; this release does not implement ECU programming, DTC clearing, Security Access or actuation.
- The updater's **Check update** finds successful public `main` CI builds; it does **not** automatically pin the app to the latest stable release.

See the [README](https://github.com/vorlocsev/dvinholm#readme), [Changelog](https://github.com/vorlocsev/dvinholm/blob/main/CHANGELOG.md), and [Russian user guide](https://github.com/vorlocsev/dvinholm/blob/main/docs/USER_GUIDE_RU.md).
