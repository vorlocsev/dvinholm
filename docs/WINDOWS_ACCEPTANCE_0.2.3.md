# Stable 0.2.3 Windows 11 LTSC acceptance

This kit prepares the real, staged **unpublished** Stable 0.2.3 ZIP for
human Windows testing. It does not start WinUI or update installed v0.2.2.

1. Open the successful **Stable v0.2.3 (automated staging, manual publish)**
   GitHub Actions run on main for the exact selected commit.
2. Download **Dvinholm-v0.2.3-STAGED-NOT-PUBLISHED** and extract the outer
   Actions ZIP into a new folder named candidate. It must contain exactly
   Dvinholm-Windows-x86.zip and SHA256SUMS.txt. Do not extract the inner ZIP.
3. From the SAME RUN download **Dvinholm-v0.2.3-WINDOWS-ACCEPTANCE-KIT**
   and extract to a separate folder named kit (scripts are under eng/).
4. Copy the 40-character commit SHA from the same workflow page.
5. In Windows PowerShell 5.1, run from the folder containing kit and candidate:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\kit\eng\Prepare-StableAcceptance023.ps1 -ArchiveDirectory .\candidate -ExpectedCommit REPLACE_WITH_40_CHARACTER_COMMIT_SHA -DestinationDirectory .\Dvinholm-023-acceptance

The helper verifies the actual archive with eight ZIP/DLL/commit/SHA-256
gates before extraction, and refuses an existing target folder. The resulting
folder contains Dvinholm.App.exe and ACCEPTANCE-0.2.3.txt. Launch the EXE
from that isolated folder to perform the checklist. Never overlay it on
v0.2.2. Any unchecked item remains **not verified**.

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
