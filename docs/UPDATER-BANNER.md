# Standalone Dvinholm Updater Banner

A separate lightweight GUI for Windows 11 and Windows 11 IoT LTSC.
It works **before** installing Dvinholm Lite, .NET 10 Desktop,
Windows App SDK or VC++ x86. The only prerequisites for the banner
itself are the Windows-provided PowerShell 5.1 and .NET Framework
WinForms. It uses no third-party PowerShell module.

Download **Dvinholm-Updater-Banner-Windows-x86.zip** from the current
Development ci-N prerelease. Extract the three files into their own
directory and double-click **Updater-Banner.cmd**.

The **Installed** label is validated against the selected channel's actual
Lite directory, required ordinary files, metadata and x86 PE header. It
reports **Unverified installation** rather than trusting a stale manifest if
files are missing, corrupt or from another channel. Stable displays only the
version number (for example, `0.2.4`) for both Installed and Available;
Development continues to display `ci-N`. Canonical GitHub tags like
`v0.2.4` are still used for download verification and transaction journals.

The banner provides a **Stable / Development** selector, installed and
available versions, three dependency statuses, a check button, a
separately confirmed **Download / Install** button and three independent
Microsoft installer/update buttons. Downloads are only started after
the user clicks and confirms; Windows UAC and vendor setup UI remain visible.

**Already installed:** when the selected channel's verified Lite version
matches the latest public release, **Check updates** reports *Up to date*,
and **Download / Install** performs no download or file replacement. If the
latest published version is numerically **older** than the managed
installation, installation is blocked; **Restore previous** is the separate,
explicitly confirmed offline rollback path. Stable compares semantic
`vX.Y.Z` versions, while Development compares numeric `ci-N` build
numbers. Only the currently selected channel's managed installation is
considered. CI covers eight offline current/newer/older/first-install and
wrong-channel-tag conditions; no real Windows GUI acceptance is implied.

The **Download / Install** worker now performs an additional offline
preflight **before any GitHub release request or ZIP download**. It requires
positive local detection of .NET 10 Desktop x86, Windows App Runtime and VC++
x86. If a dependency is missing or cannot be determined, installation stops
without creating a download/staging directory. Use the corresponding
Microsoft installer button, then **Recheck deps** and retry. The existing
checks of the downloaded Lite files and runtimeconfig remain mandatory before
the installed version is changed. Seven offline negative/positive preflight
checks run in CI; actual Microsoft installer behavior still requires Windows
manual acceptance.

The banner remembers your last selected channel in the per-user
`%LOCALAPPDATA%\Dvinholm\UpdaterBanner\channel-preference.txt` file.
Only the literal values `Stable` and `Development` are accepted; an
invalid or inaccessible preference falls back to Development. An explicit
`-Channel` parameter overrides the saved choice when starting the script.
**Recheck deps** runs only the local dependency inventory, even when the
computer is offline; it does not query GitHub. Use **Check updates** to
refresh the available build. The local recheck clears the previously shown
available-version label until the next explicit or channel-switch check.

- **Stable** resolves the latest public GitHub vX.Y.Z release.
- **Development** resolves the newest public ci-N prerelease with a
  SHA-pinned main branch target and a Lite archive.
- Releases must contain a uniquely named Lite x86 ZIP and SHA256SUMS.
  Both asset SHA-256 digests are independently checked against the GitHub
  REST API; the downloaded sums file must also match the ZIP contents.
- Untrusted archive paths, duplicate names, links, oversized entries,
  invalid x86 executables and missing files fail closed. A downloaded Lite
  runtimeconfig is checked on the **actual local Windows host before
  replacing any binaries**.
- Microsoft-only runtime setup is delegated to the existing dependency
  manager. It checks a Microsoft signature on installers, checks .NET
  SHA-512 release metadata, and leaves installers/UAC visible.
  Windows CI separately checks eight synthetic .NET 10 Desktop x86 installer
  metadata scenarios through `Manage-Lite-Dependencies.ps1 -Mode SelfTest`:
  valid selection, missing/preview versions, duplicate matching releases,
  wrong architecture, duplicate files, untrusted URLs and invalid SHA-512.
  This test runs without internet access or executing any installer; it does
  not replace a real dependency-installation test on Windows.
- No scheduled or unattended install; no elevated or silent commands.

Managed installation folders are distinct:

    %LOCALAPPDATA%\Dvinholm\UpdaterBanner\Stable\current
    %LOCALAPPDATA%\Dvinholm\UpdaterBanner\Development\current

A successful update leaves one prior version in the channel's
`previous` directory. Existing manual Dvinholm installations are **not**
modified automatically. This prevents accidental downgrades when switching
channels. Application ECU profiles and settings remain in the existing
separate user-data location. The banner's **Start Dvinholm** button now delegates to a separate
per-channel PowerShell worker, which rechecks recovery markers and validates
the selected `current` folder's Lite files, channel metadata and x86 PE
header **immediately before launching** the dependency-aware startup command.
The GUI does not launch an unchecked `current` batch file directly. A
missing/corrupt installation, cross-channel metadata, or unresolved
install/rollback blocks startup without changing installed files. These are
structural safety checks, not a substitute for cryptographic verification of
an installation after it has been modified on disk.

**Restore previous** performs an offline, separately confirmed rollback of
the **currently selected channel only**. Close Dvinholm before invoking it.
The launcher validates both managed versions' manifest channel, expected
Stable/Development tag syntax, required Lite files and x86 PE header before
moving anything. It refuses cross-channel, missing, invalid or identical
backups. A successful rollback swaps `current` and `previous`, allowing
the replaced build to be restored again. A per-channel Windows mutex prevents
two banner workers from modifying the same installation simultaneously.
On an interrupted swap, the launcher attempts to restore the original
directory names and reports if manual recovery is necessary.

**Repair rollback** is a separate, explicitly confirmed, **offline**
recovery action for a rollback interrupted by process termination or power loss.
It never silently repairs a directory on startup. It accepts exactly one
`rollback-<32 lowercase hexadecimal characters>` staging directory and only
two recognizable incomplete layouts: either `current` is absent while
`previous` and the staging directory are valid, or `previous` is absent
while `current` and the staging directory are valid. It verifies both
surviving Lite installations and their channel/versions before moving files.
In either case it restores the original `current` and `previous` pairing.
If a repair is interrupted midway, the same explicit operation can be retried.
Normal **Download / Install** and **Restore previous** are refused while any
`rollback-*` entry exists, so a second operation cannot overwrite recovery
evidence. Ambiguous, untrusted, duplicate or invalid states are left untouched
for manual inspection. No GitHub request or Microsoft installer is involved.

**Repair install** handles a second case: interruption during a Lite
installation after the downloaded ZIP has passed integrity, extraction and
dependency checks. Before the first directory rename, the updater now writes
a small per-channel `install-transaction.json` journal identifying the
original current/previous versions and the target version. A separately
confirmed offline repair validates this journal, the selected channel,
staged folders and Lite x86 signatures, then:

- If promotion has not finished, restores the original managed installation
  and retains the staged candidate under `abandoned-<id>` for manual review.
- If the new version has already reached `current`, keeps it installed,
  verifies the previous backup and finishes safe housekeeping.
- Rejects ambiguous, missing, malicious or cross-channel evidence without
  guessing. Installation and rollback are blocked until the transaction
  has been repaired.

Recovery is repeatable if interrupted between recovery moves. The installer
keeps the journal until promotion is complete; a retired second backup is
cleaned up **after** the committed installation is safe. The journal does
not include user ECU profiles or settings. An unjournaled `pending-*` or
`older-*` from older Banner versions is deliberately **not** repaired
automatically; preserve those folders for manual analysis.

CI also includes eight isolated launch-candidate checks: valid/current-only
installations, absent/corrupt files, invalid x86 PE, wrong-channel metadata,
and both incomplete update and rollback. They do not run Dvinholm itself.

CI includes eight synthetic offline interrupted-install checks alongside
eight rollback-repair checks. Real GUI execution and power-cut recovery
still require Windows acceptance; these checks do not prove hardware-level
crash consistency.

During an ordinary update, the older `previous` backup is retained in a
temporary channel directory until the new `current` is successfully moved
into place. Failure before successful replacement restores the prior backup
where possible. Only one directly selectable prior build is retained after
a successful update, while cleanup failures can leave an extra `older-*`
folder for manual review. Rollback itself never downloads files or
installs dependencies. Windows PowerShell CI includes eight additional
synthetic offline rollback checks; installed Windows GUI and power-loss
recovery scenarios require manual acceptance.

## Windows acceptance (7 checks)

Use the latest **Development** standalone Banner ZIP from the GitHub
Development prerelease. Extract all three files, then run
`Updater-Banner.cmd` on the target Windows 11 / IoT LTSC machine.
A physical J2534 adapter is **not** needed. Do not create artificial
`pending-*` / `rollback-*` directories in a production installation.

1. **Standalone startup:** the WinForms Banner opens before .NET 10,
   Windows App SDK or VC++ x86 installation; initial local inventory runs
   without needing GitHub connectivity.
2. **Channel selection:** choose Stable, close/reopen, confirm Stable stays
   selected. Switch back to Development; both roots stay independent.
3. **Dependencies:** select **Recheck deps** offline, confirm the three
   statuses refresh; install a missing Microsoft runtime only after the
   confirmation prompt, then recheck.
4. **Availability:** use **Check updates** online. Confirm current and
   available labels reflect the selected channel.
5. **Lite installation:** confirm **Download / Install**; verify installed
   version and successful startup with **Start Dvinholm**. Do not
   interrupt Microsoft installation or an app process.
6. **Rollback:** after two different Development versions are installed,
   use **Restore previous**; confirm it works offline and that the
   replaced version remains in `previous`.
7. **Recovery indication:** use a disposable test profile or isolated test
   directory to simulate an interrupted operation. Confirm **Recovery**
   prompts for **Repair install** or **Repair rollback**, disables Install,
   Restore and Start, and refuses ambiguous states. Never delete recovery
   evidence until inspected.

CI verifies synthetic directory states, not a real power outage. It also
instantiates the **actual Windows PowerShell 5.1 WinForms controls** in a
noninteractive `-Mode UiSmokeTest` run and checks seven button-state
invariants (startup, ready, both repair modes, ambiguous recovery, busy
helper and construction). This smoke check never calls `ShowDialog`,
GitHub, Microsoft installers or Dvinholm; it does not prove visible GUI
rendering on a user's Windows desktop.

After packaging, Windows CI also extracts the **actual standalone ZIP** into
a temporary directory and runs seven checks against its shipped bytes:
exactly three expected files, successful extraction, SHA-256 source matches,
a local sibling launcher, offline self-tests, the WinForms smoke checks,
and read-only dependency inventory. The isolated directory is removed.
This guards against shipping an archive that matches the source at packaging
time but cannot execute as a standalone distribution. It still does **not**
substitute for manual Windows GUI acceptance or a real power-cut test. Report actual Windows checks
separately from CI and from physical J2534 tests.
Do not mark this acceptance complete until each check has been performed.

## Local recovery indication

The banner checks on-disk channel state **before** querying GitHub at
startup or after switching channels. `Recovery: Ready` enables normal
operations. `Recovery: Repair install` or `Repair rollback` enables
only the relevant confirmed recovery action. `Recovery: Inspect` blocks
install, rollback and launch; preserve the folders for manual inspection.
The status is advisory: the worker validates journal and file integrity
again before any repair. The GUI now updates button availability whenever a
**completed** worker-state snapshot is read, even if it arrives after the
process-exit notification. Intermediate progress snapshots may update text
but cannot unlock Install, Launch or Restore. Missing, malformed or
incomplete worker completion leaves these buttons disabled until a fresh
**Recheck deps**. Windows CI includes six simulated WinForms worker-status
ordering/validation checks in addition to the seven original button-state
checks. **Check updates** is an explicit online action
and is disabled while unresolved recovery evidence is present.

**Important:** the banner checks installation/availability, not physical
J2534 hardware support. A successful offline CI suite cannot prove actual
Windows GUI launch on the Panasonic Toughpad. The App SDK release line is
currently pinned to 2.5 to match Dvinholm. Updating this policy requires
a separate runtime/bootstrap compatibility review. If Windows PowerShell
5.1 or the inbox .NET Framework is disabled, this script-based banner
cannot launch; it is free of **additional** runtime prerequisites, not
free of Windows OS components.

Stable v0.2.4 remains immutable; this feature changes Development only.
