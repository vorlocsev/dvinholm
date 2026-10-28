# Standalone Dvinholm Updater Banner

A separate lightweight GUI for Windows 11 and Windows 11 IoT LTSC.
It works **before** installing Dvinholm Lite, .NET 10 Desktop,
Windows App SDK or VC++ x86. The only prerequisites for the banner
itself are the Windows-provided PowerShell 5.1 and .NET Framework
WinForms. It uses no third-party PowerShell module.

Download **Dvinholm-Updater-Banner-Windows-x86.zip** from the current
Development ci-N prerelease. Extract the three files into their own
directory and double-click **Updater-Banner.cmd**.

The banner provides a **Stable / Development** selector, installed and
available versions, three dependency statuses, a check button, a
separately confirmed **Download / Install** button and three independent
Microsoft installer/update buttons. Downloads are only started after
the user clicks and confirms; Windows UAC and vendor setup UI remain visible.

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
- No scheduled or unattended install; no elevated or silent commands.

Managed installation folders are distinct:

    %LOCALAPPDATA%\Dvinholm\UpdaterBanner\Stable\current
    %LOCALAPPDATA%\Dvinholm\UpdaterBanner\Development\current

A successful update leaves one prior version in the channel's
`previous` directory. Existing manual Dvinholm installations are **not**
modified automatically. This prevents accidental downgrades when switching
channels. Application ECU profiles and settings remain in the existing
separate user-data location. The banner's Start button launches the
selected Lite build via its dependency-aware startup command.

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

**Important:** the banner checks installation/availability, not physical
J2534 hardware support. A successful offline CI suite cannot prove actual
Windows GUI launch on the Panasonic Toughpad. The App SDK release line is
currently pinned to 2.5 to match Dvinholm. Updating this policy requires
a separate runtime/bootstrap compatibility review. If Windows PowerShell
5.1 or the inbox .NET Framework is disabled, this script-based banner
cannot launch; it is free of **additional** runtime prerequisites, not
free of Windows OS components.

Stable v0.2.4 remains immutable; this feature changes Development only.
