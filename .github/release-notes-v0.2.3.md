# Dvinholm v0.2.3

Stable Windows x86 release. The Portable candidate was checked on Windows 11 LTSC, and the release assets were verified against the approved build.

## Changes since v0.2.2

- Removed synthetic and test-only controls from Stable, including Start Demo, Add Test Profiles, preview UDS examples and synthetic export functions. Development retains offline test functions.
- CAN Monitor supports a software display filter containing up to 16 exact comma-separated CAN IDs (for example `7E0,7E8`). J2534 hardware filtering remains single-ID.
- CAN Monitor CSV export safely escapes text and protects against formula injection while retaining the five-column Stable format.
- Preserves imported ECU profiles, CAN baud-rate preferences and existing data during normal use.
- Kept the Windows x86 self-contained Portable runtime. Trimming and AOT remain disabled.
- Improved archive checks, isolated Windows acceptance testing, and offline update/rollback regression coverage.

## Distribution

- **Dvinholm-Windows-x86.zip:** Portable Stable Windows x86 with bundled runtime.
- **SHA256SUMS.txt:** SHA-256 for the Portable ZIP.

The optional Lite package is a separate framework-dependent build. The experimental unsigned MSIX is not a signed Stable installer.

## Verification

- 1111/1111 application tests and 8/8 production-profile tests passed on the approved candidate.
- The staged ZIP passed eight archive/assembly/commit checks and eight acceptance-kit safety checks.
- Actual published v0.2.2 and staged v0.2.3 archive bytes were used in eight isolated forward-replacement and byte-for-byte rollback tests.
- Windows 11 LTSC app operation was confirmed by the tester. The acceptance copy's settings remain separate from installed 0.2.2 data.
- A real installed update from published v0.2.2 to published v0.2.3 can be checked only after the release is available.

## Boundaries

Real J2534 hardware, vendor drivers, live vehicle/ECU interoperability, and flash/programming operations are not validated in this release. Read-only UDS and CAN diagnostics remain subject to adapter and ECU support. Dvinholm is not affiliated with or endorsed by Volvo Cars.
