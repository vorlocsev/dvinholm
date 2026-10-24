# Stable 0.2.3 Windows 11 LTSC acceptance

This kit prepares the real, staged **unpublished** Stable 0.2.3 ZIP for
human Windows testing. The double-click launcher starts **only the newly extracted test copy** after validation; the underlying preparation helper never launches WinUI or updates installed v0.2.2.

1. Open the successful **Stable v0.2.3 (automated staging, manual publish)**
   GitHub Actions run on main for the exact selected commit.
2. Download **Dvinholm-v0.2.3-STAGED-NOT-PUBLISHED** and extract the outer
   Actions ZIP into a new folder named candidate. It must contain exactly
   Dvinholm-Windows-x86.zip and SHA256SUMS.txt. Do not extract the inner ZIP.
3. From the SAME RUN download **Dvinholm-v0.2.3-WINDOWS-ACCEPTANCE-KIT**
   and extract to a separate folder named kit (scripts are under eng/).
4. Put the **kit** and **candidate** folders next to each other. Double-click
   **kit/START-ACCEPTANCE.cmd** on the Windows 11 LTSC machine. No SHA input
   or PowerShell command is required: BUILD_COMMIT.txt in this same kit
   identifies the exact staging commit.
5. The launcher checks the original ZIP/manifest and embedded commit using
   eight verification gates, refuses any existing destination, and extracts
   only to the new sibling Dvinholm-023-acceptance folder. After verification
   it launches the **test copy**, then opens ACCEPTANCE-0.2.3.txt in Notepad.

The test copy now uses its own `acceptance-user-data` directory inside
`Dvinholm-023-acceptance` for ECU profiles, CAN baud rate and J2534 DLL path.
The receipt keeps that isolation active when the EXE is relaunched directly.
While acceptance mode is active, hardware connection and built-in update
actions are disabled. The installed v0.2.2 data in
`%LOCALAPPDATA%\Dvinholm` is not used or modified by the test copy.

The user must still observe the window and tick the manual checklist: a
process launch is **not** proof that WinUI rendered successfully. The batch
file does not overwrite the installed 0.2.2 application, register auto-run,
or publish any release. If it rejects files, use the log message to diagnose
the mismatch and do not bypass verification. Existing Dvinholm-023-acceptance
folders are never overwritten. Any unchecked item remains **not verified**.

Advanced manual alternative (PowerShell 5.1, copy the 40-digit SHA from the
same Actions run):

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\kit\eng\Prepare-StableAcceptance023.ps1 -ArchiveDirectory .\candidate -ExpectedCommit REPLACE_WITH_40_CHARACTER_COMMIT_SHA -DestinationDirectory .\Dvinholm-023-acceptance

Eight offline UI checks: Windows 11 LTSC launch/icon/close/restart; no demo
controls; correct disconnected state and disabled VIN/CAN/DTC without J2534;
software multi-ID input (7E0,7E8); normal empty CAN CSV; ECU user-profile
persistence; CAN speed persistence; original v0.2.2 untouched. These checks
do not prove frame reception or actual ECU compatibility.

An installed v0.2.2 -> v0.2.3 Stable update can only run after v0.2.3 has
a published asset. Pre-release CI verifies updater replacement and rollback
on disposable offline fixtures. Treat an actual installed update as a
**post-publication** smoke check unless an authorized private rollout exists.

The separate unsigned experimental MSIX is not an installer for Stable.

## Automatic acceptance-kit safety tests

The staging workflow additionally checks the **real** staged ZIP in eight scenarios: valid isolated extraction/report, refusal to overwrite an existing folder, rejection of invalid or mismatched SHA, tampered or missing checksum, extra artifact files, and missing destination parent. These tests never open WinUI and cannot replace actual Windows 11 LTSC manual acceptance.

## One-click kit CI check

Staging writes BUILD_COMMIT.txt from the exact workflow checkout SHA and runs START-ACCEPTANCE.cmd in --prepare-only mode on Windows against the **same downloaded candidate**. This checks layout, SHA-256, x86, DLL metadata and production extraction without a desktop launch. The Windows user must still manually confirm the actual GUI and behavior.
