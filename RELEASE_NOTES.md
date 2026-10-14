# Dvinholm — first software MVP release notes (draft)

> **Status: draft only. Not a published release.** Version number, release tag, and publication date have not been set. Do not treat this document or a CI artifact as a signed/versioned release.

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

The CI artifact is a **portable build output, not an installer**. Obtain Dvinholm-Windows-x86 from the [successful main CI run](https://github.com/vorlocsev/dvinholm/actions/runs/38611124491), extract the full archive to a normal folder, and launch Dvinholm.App.exe.

Install the adapter manufacturer's driver separately. The app is x86 and requires a compatible 32-bit J2534 DLL; it cannot load a 64-bit-only DLL. See the README for detailed build, driver, and troubleshooting instructions.

## Validation recorded for this draft

- Main commit: 65ea0af8447bd746a16261199eba4952b1753c6c.
- Full CI run: [#38611124491](https://github.com/vorlocsev/dvinholm/actions/runs/38611124491) — Restore, Build, Test, Publish, and Upload artifact succeeded.
- Artifact name: Dvinholm-Windows-x86; size: 66,752,774 bytes.
- GitHub-reported artifact digest: sha256:d836c8f4c965d6fbbfe47b76b18c39a1f3c5874cb63f00466a44a9998392ed98. This is the service-reported digest; it has not been independently recalculated from a locally downloaded ZIP in this checkpoint.
- User-confirmed manual smoke tests on Windows 11 IoT LTSC 26100.9550 include app startup, driver discovery, absent-device reporting, profile import/validation/export/re-import and persistence checks. Some UI-only recovery paths remain unverified; the manual checks were not all performed on the exact artifact from this latest run.

## Known limitations

- No physical J2534 adapter was available during this validation cycle. DiCE/Mongoose, vendor-driver behavior, electrical compatibility, and vehicle/ECU-specific operation have **not** been certified.
- Virtual-adapter CI proves software behavior within the test harness, not real-hardware compatibility.
- The profile catalog starts empty; request/response IDs and DIDs must come from reliable documentation or user-supplied profiles. Candidate DIDs do not guarantee ECU support.
- UDS functionality is read-only. ECU writes, actuation, immobilizer/PIN extraction, brute force, and security bypass are not included.
- This is not an installer and does not bundle third-party adapter drivers.

## Before publishing

- [ ] Choose and record the version number and release scope.
- [ ] Run full CI on the exact candidate commit and verify its uploaded artifact.
- [ ] Independently download and inspect the final artifact; compare its hash with the recorded release evidence.
- [ ] Confirm release notes, README, tag, commit, and artifact all identify the same candidate.
- [ ] Only after explicit release approval, create the version tag and publish the GitHub Release.
- [ ] Keep physical-adapter validation deferred to a future release cycle unless scope is explicitly changed.
