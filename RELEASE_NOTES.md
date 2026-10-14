# Dvinholm 0.1.0 — Software MVP release notes

> **Status: published.** GitHub Release [v0.1.0](https://github.com/vorlocsev/dvinholm/releases/tag/v0.1.0) was published on 2026-10-14 and points to commit `b3a9a82e8bb26527b34551c4b93b3e3381a6f94d`.

## Scope

Dvinholm is a Windows x86 J2534 CAN monitor and read-only UDS diagnostic client. This first software MVP is intended for evaluation with compatible 32-bit J2534 drivers.

### Included

- Discover registered 32-bit J2534 DLLs or select a DLL path manually.
- Connect/disconnect and monitor Classic CAN at 500, 250, or 125 kbit/s.
- CAN ID software filtering, optional driver-supported hardware filtering, statistics, and CSV export.
- Import, validate, select, and export JSON ECU profile catalogs. The built-in catalog is intentionally empty.
- ISO-TP single-/multi-frame transport with Flow Control, Block Size, STmin, deadlines, cancellation, and frame validation.
- Read-only UDS requests: configured Data Identifier (service 0x22), VIN (DID 0xF190), and DTC records (service 0x19/subfunction 0x02), where the target ECU supports them.
- Virtual-adapter automated regression tests and a self-contained Windows x86 CI artifact.

## Download and run

The release asset is a **portable build output, not an installer**. Download [Dvinholm-Windows-x86.zip from GitHub Release v0.1.0](https://github.com/vorlocsev/dvinholm/releases/download/v0.1.0/Dvinholm-Windows-x86.zip), extract the full archive to a normal folder, and launch `Dvinholm.App.exe`.

Install the adapter manufacturer's driver separately. The app is x86 and requires a compatible 32-bit J2534 DLL; it cannot load a 64-bit-only DLL. See the README for detailed build, driver, and troubleshooting instructions.

## Release validation evidence

- Published tag: `v0.1.0`; release: [Dvinholm 0.1.0 — Software MVP](https://github.com/vorlocsev/dvinholm/releases/tag/v0.1.0).
- Release target/main commit: `b3a9a82e8bb26527b34551c4b93b3e3381a6f94d`.
- Exact release-commit CI: [run #38644431705](https://github.com/vorlocsev/dvinholm/actions/runs/38644431705) — Restore, Build, Test, Publish, and Upload artifact succeeded on this SHA.
- Attached release asset: `Dvinholm-Windows-x86.zip`, 66,752,893 bytes; GitHub SHA-256 digest `990bb1e762b46bc0dff2883558c0b7398924f1e16b6b59434cd51d40047017bb`. This matches the CI artifact metadata for the release commit.
- User manually launched the preceding CI artifact from run [#38640011679](https://github.com/vorlocsev/dvinholm/actions/runs/38640011679) on Windows 11 IoT LTSC 26100.9550 and confirmed the window title `Dvinholm — J2534 CAN Diagnostics` and correct no-device status. The release commit after that smoke test contains documentation-only changes; do not describe the later release ZIP itself as separately manually launched.
- Earlier user-confirmed UI checks cover J2534 driver discovery/selection, missing-device status, failed-connect button recovery, and ECU profile import, validation, export/re-import, persistence, empty-catalog handling, and recovery. Some UI-only read-error/cancellation recovery paths remain unverified.

## Known limitations

- No physical J2534 adapter was available during this validation cycle. DiCE/Mongoose, vendor-driver behavior, electrical compatibility, and vehicle/ECU-specific operation have **not** been certified.
- Virtual-adapter CI proves software behavior within the test harness, not real-hardware compatibility.
- The profile catalog starts empty; request/response IDs and DIDs must come from reliable documentation or user-supplied profiles. Candidate DIDs do not guarantee ECU support.
- UDS functionality is read-only. ECU writes, actuation, immobilizer/PIN extraction, brute force, and security bypass are not included.
- This is not an installer and does not bundle third-party adapter drivers.

## Post-release follow-up

- [x] Freeze software-only MVP scope as version 0.1.0.
- [x] Run full CI on the exact release commit and verify the uploaded artifact metadata.
- [x] Publish tag `v0.1.0` and GitHub Release with the Windows x86 ZIP asset.
- [ ] Optionally download the published release asset independently and compare its local SHA-256 with the recorded digest.
- [ ] Continue only focused software/UI regression work for remaining unverified recovery paths; avoid duplicating existing virtual-adapter tests.
- [ ] Keep physical-adapter validation deferred to a future release cycle unless scope is explicitly changed.
