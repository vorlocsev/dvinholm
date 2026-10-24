# Dvinholm v0.2.3 — DRAFT NOTES, NOT PUBLISHED

This file prepares the next Stable release. It is not release approval.

## Since v0.2.2
- **Stable cleanup:** Start demo, Add test profiles, Preview UDS example (offline), and Export synthetic UDS TXT are omitted from production UI and compiled event handlers; synthetic fixture generators are also removed. Development retains those controls for offline testing. Existing stored profiles are preserved.
- CAN Monitor software display filter supports up to 16 comma-separated
  exact CAN IDs (e.g. 7E0,7E8,18DAF110), retaining filtered history;
  J2534 hardware filtering remains single-ID only.
- CAN Monitor CSV safely quotes comma/quote/newline text, neutralizes
  formula-like unexpected cells and retains ordinary five-column CSV in Stable; synthetic CSV exists only in Development.
- Portable Windows x86 keeps bundled .NET/Windows App SDK and updater
  compatibility; optional Lite ZIP remains framework-dependent.
- Both ZIPs exclude debugger files and retain trimming/AOT disabled.
- Experimental unsigned Lite MSIX gained stricter independent MakeAppx
  unpack, SHA-256 and Actions upload/download verification; it remains
  a test-only artifact, NOT an install-ready signed package.

## Verification before publishing
- Candidate exact commit Windows x86 CI tests and clean Portable/Lite ZIPs.
- Dedicated actual 0.2.3 self-contained RC build checks eight binary/archive
  gates, downloads own Actions artifact and rechecks checksum/embedded version.
- Manual Windows 11 LTSC Stable Portable startup, close/restart, absence of
  demo/test commands, comma-list filtering, real CSV export, profile
  persistence and update-from-0.2.2 behavior remain pending.
- Separate Development-only offline CAN demo acceptance remains pending.
- A separate v0.2.3 Stable workflow **automatically stages but never publishes** a clean binary for relevant main changes, with eight ZIP/DLL checks and SHA-256 re-download. Publishing is a different manually authorized action bound to an exact main SHA; the release remains draft until downloaded assets pass validation.

## Boundaries
No physical J2534 adapter, vendor driver, ECU or vehicle compatibility has
been demonstrated; hardware validation remains for later versions. Read-only
UDS commands only. No code signing or Stable 0.2.3 distribution yet.
Dvinholm is not affiliated with or endorsed by Volvo Cars.
