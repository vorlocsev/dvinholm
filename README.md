<p align="center">
  <img src="assets/dvinholm-banner.webp" alt="Dvinholm — ECU Diagnostics" width="100%">
</p>

<h1 align="center">Dvinholm</h1>
<p align="center">
  <strong>SAE J2534 · CAN · ISO-TP · UDS diagnostics</strong><br>
  Independent, read-only Windows x86 ECU diagnostics toolkit
</p>

<p align="center">
  <a href="https://github.com/vorlocsev/dvinholm/actions/workflows/ci.yml"><img alt="Windows CI" src="https://github.com/vorlocsev/dvinholm/actions/workflows/ci.yml/badge.svg?branch=main"></a>
  <a href="https://github.com/vorlocsev/dvinholm/releases/tag/v0.2.4"><img alt="Stable v0.2.4" src="https://img.shields.io/badge/stable-v0.2.4-167cc1"></a>
  <img alt="Windows x86" src="https://img.shields.io/badge/platform-Windows%20x86-203f60">
  <img alt="UDS read-only" src="https://img.shields.io/badge/UDS-read--only-008b8b">
</p>

<p align="center">
  <a href="https://github.com/vorlocsev/dvinholm/releases/download/v0.2.4/Dvinholm-Windows-x86.zip"><strong>Download stable release</strong></a>
  &nbsp;·&nbsp; <a href="https://github.com/vorlocsev/dvinholm/actions">Latest CI builds</a>
  &nbsp;·&nbsp; <a href="docs/USER_GUIDE_RU.md">Руководство (RU)</a>
  &nbsp;·&nbsp; <a href="ROADMAP.md">Roadmap</a>
  &nbsp;·&nbsp; <a href="CHANGELOG.md">Changelog</a>
</p>

> [!IMPORTANT]
> **Published Stable v0.2.4 offers Portable and Lite Windows x86 archives.** Current Development CI produces a single framework-dependent standard ZIP without the Lite suffix. A CI prerelease is not Stable. See [CI Lite-only policy](docs/CI-LITE-ONLY.md).
>
> **Hardware validation is pending.** Windows UI and software tests have been exercised without a physical adapter; compatibility with Volvo DiCE, Mongoose, individual drivers, ECUs, or vehicles has not yet been demonstrated. Dvinholm is an independent project and is **not affiliated with or endorsed by Volvo Cars** or any adapter manufacturer.

## Dvinholm for Windows x86

**Main Development distribution:** `Dvinholm-Windows-x86.zip` (contains
`Launcher.exe`, `Dvinholm.exe`, `Binaries/`). This is the earlier
framework-dependent Lite build, now the single standard distribution,
**without Lite in its user-facing name**. The Launcher manages .NET Desktop 10
x86, Windows App Runtime, VC++ x86, Stable/Development updates and rollback.
The legacy ZIP bearing `-Lite` in its filename remains temporarily as
a compatibility feed for already-installed updaters, not another edition.
No separate self-contained Portable is produced.

The Aurora Launcher also has **Adapter drivers**:
it detects registered 32-bit J2534 libraries, identifies **MongoosePro JLR**
registrations, and checks Windows USB/PnP for **Volvo DiCE**. The menu
provides official vendor support/download pages, lets the user choose a
manufacturer-signed local installer and requests explicit confirmation.
Volvo DiCE's VIDA driver does **not** imply J2534 API compatibility with
Dvinholm. A detected J2534 DLL is not proof that the hardware is connected.

[Distribution and driver details](docs/UPDATER-BANNER.md).

## Latest stable release — v0.2.4 (2026-10-26)

**Published Stable v0.2.4** is available as a [self-contained Portable ZIP](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.4/Dvinholm-Windows-x86.zip) or a [framework-dependent Lite ZIP](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.4/Dvinholm-Windows-x86-Lite.zip). The [SHA256SUMS.txt](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.4/SHA256SUMS.txt) from that **same published release** contains the authoritative hashes; do not substitute checksums from another build.

Demo/preview/test-only controls are **excluded from Stable**. The frozen source revision is `a9e61883a413d4afaebb86fb468987c904250092`. Actual installed updater and physical adapter acceptance remain separate tests.

## Quick start

**Latest published Stable:** [Dvinholm v0.2.4](https://github.com/vorlocsev/dvinholm/releases/tag/v0.2.4), published 2026-10-26 from frozen commit `a9e6188`. Both Portable and Lite Windows x86 ZIPs are available. The `main` **Development** channel is experimental and separately built, even though its binary version may also say 0.2.4.

1. Download [**Stable Portable**](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.4/Dvinholm-Windows-x86.zip) (self-contained) or [**Stable Lite**](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.4/Dvinholm-Windows-x86-Lite.zip) (requires .NET 10 Desktop Runtime x86, Windows App SDK x86 and VC++ x86). Check the matching [SHA256SUMS.txt](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.4/SHA256SUMS.txt).
2. Extract the entire chosen archive to its own writable folder. Do not mix Portable, Lite and Development files.
3. For frozen Stable v0.2.4, use the entrypoint shipped in that archive (historical layout: `Dvinholm.App.exe`). **Current Development CI** instead packages `Launcher.exe`, `Dvinholm.exe` and `Binaries/`; launch `Launcher.exe` for prerequisites, updates and app startup.
4. A J2534 DLL being registered is not proof of compatible DiCE/Mongoose hardware. Use authorized physical testing with verified protocol and ECU addressing, beginning with passive CAN observation where appropriate.
5. PIN/CEM service authentication and configuration writes remain disabled pending exact-module evidence and authorized hardware testing.

### Development CI channel

Current [Development CI](https://github.com/vorlocsev/dvinholm/actions) packages a **framework-dependent** `Dvinholm-Windows-x86.zip` (without `-Lite` in the filename), not a self-contained Portable. The Development Launcher owns dependency checks and Stable/Development updates. Older Lite-named CI packages are historical or compatibility feeds. The latest green CI is not automatically a published Stable release. Windows installed GUI and physical J2534 tests remain separate acceptance gates.

**Stable v0.2.4 validation:** frozen `a9e61883a413d4afaebb86fb468987c904250092` passed [Stable staging](https://github.com/vorlocsev/dvinholm/actions/runs/40240287577), with eight user-confirmed offline Windows 11 LTSC GUI checks documented in the published release notes. A real installed 0.2.3 → 0.2.4 in-app update and physical J2534/CEM use have not been confirmed.

Previous Stable: [v0.2.3](https://github.com/vorlocsev/dvinholm/releases/tag/v0.2.3) (2026-10-24).

## Candidate adapter interfaces (hardware compatibility unverified)

- Volvo DiCE
- Mongoose JLR Pro

Install the interface drivers and vendor J2534 DLL separately. The application is built as x86 to support legacy 32-bit J2534 drivers.

## Current functionality

- Discover registered 32-bit J2534 DLLs or enter a DLL path manually.
- Open and close the J2534 device and connect to CAN at 500, 250, or 125 kbit/s.
- Live CAN capture, software CAN ID filtering, optional J2534 hardware ID filters, frame-rate/count statistics, and CSV export.
- **Offline Demo CAN Monitor (historical Stable v0.2.2 and Development only, NOT Stable v0.2.3):** a clearly labeled synthetic CAN feed for Windows UI checks without a J2534 device. It exercises live table updates, bounded history/refilter, count display, Clear, and synthetic-source-tagged CSV export; J2534 and UDS remain disconnected. [Windows demo checklist](docs/manual-can-demo-monitor-2026-10-20.md).
- **Offline UDS example preview (newer Development CI builds):** **Preview UDS example (offline)** shows a fictional VIN (DID F190), unverified F187 ASCII and DTC/status examples through existing read-only formatters. These are explicitly synthetic, with **no ECU request or vehicle diagnosis**. Newer Development builds also add **Export synthetic UDS TXT**, which opens a Windows Save dialog and writes only the fixed fictional example to a user-selected TXT file, not whatever diagnostic text may be in the status panel. The file carries prominent **SYNTHETIC** provenance and is never saved automatically. Both operations require a disconnected, idle session with CAN Demo stopped. Real UDS buttons stay disabled without J2534 hardware. On Development builds with report-path protection, synthetic TXT and CAN CSV export also check the user-selected destination and refuse to write report contents inside the running application's installation tree, because in-place updates can remove user-created files there. Choose Documents or another external folder. **Empty-placeholder fix (newer Development builds):** TXT/CSV report export uses a Windows Save As dialog that returns only the chosen filename; the app checks the destination **before** creating any report file. Report contents are written first to a temporary file in the destination directory and moved over the target only after the write succeeds. Rejected paths and Cancel leave no new zero-byte report. This behavior still needs user-confirmed Windows manual testing.
- Report firmware, vendor DLL, and J2534 API versions when the driver supports `PassThruReadVersion`.
- Detect optional vendor exports for hardware filters, version queries, and extended error details. Export detection is not a hardware conformance test.
- Import, validate, search, select, and export JSON ECU profile catalogs. Search supports names, catalog IDs, source notes, CAN IDs and candidate DIDs; a separate filter shows profiles **marked verified by the catalog author**, without implying actual hardware compatibility. Profile details are selectable and scrollable. The catalog starts empty; no unsupported vehicle-specific CAN IDs or DIDs are guessed. **Newer Development builds:** ECU profile **Export JSON** uses path-only Save As and atomic UTF-8 writing (matching the guarded TXT/CSV report exports), refuses the app installation tree, and preserves an existing destination on failed temporary writes. This JSON export is data-only; it never performs diagnostics.
- Discover observed UDS responders and select 11-bit or 29-bit CAN addressing.
- Read a configured Data Identifier (UDS `0x22`), read VIN from DID `0xF190`, and read DTC records using UDS `0x19` / subfunction `0x02`.
- Display **Read DID** replies with their DID and raw bytes, strict VIN interpretation for `F190`, and clearly marked optional printable ASCII previews for otherwise unknown payloads; no unsupported vehicle-specific units are inferred. The bounded, scrollable status panel allows selecting/copying long replies without obscuring CAN Monitor.
- Display **Read DTC** results with per-ECU record count, requested status mask, and one selectable line per raw 24-bit code with ISO 14229 status-bit explanations. No undocumented Volvo fault descriptions are guessed.
- Use ISO-TP over Classic CAN for single-frame and multi-frame requests and responses, including Flow Control, Block Size, STmin, timeouts, cancellation, and sequence-number validation.

UDS functionality is currently read-only. A profile or DID marked as a candidate does not guarantee that a particular ECU supports it. Do not treat observed responses or profile metadata as proof of support.

No immobilizer/PIN extraction, brute forcing, security bypass, or automated security-key calculation is implemented.

## Branding and application icon

<p><img src="assets/dvinholm-icon.png" width="92" alt="Dvinholm app icon" /></p>

The approved silver-and-blue circular arrow is paired with a northern river landscape. The image assets are stored in `assets/`; the Windows executable and title bar use the multi-resolution `src/Dvinholm.App/Assets/Dvinholm.ico` (16–256 px). The branded icon is applied by the app at startup and included in new CI-published packages. See the [project history](docs/DEVELOPMENT_HISTORY.md) for older release plans.

## Architecture

The product is **Dvinholm**, a Windows diagnostic application built with C# and WinUI 3. The application and test namespaces now use `Dvinholm.App` and `Dvinholm.App.Tests`; the source and project paths are aligned with the product name. There is one J2534 backend; the old C++/Win32 backend was removed to avoid duplicate implementations.

```text
WinUI 3 / MainWindow
        |
        v
   J2534Session
        |
        v
      CanBus  <---- serialized CAN transmit
        |
        +---- CanRxDispatcher ---- subscriptions
        |                            |       |
        |                            |       +-- ISO-TP / UDS
        |                            +---------- CAN monitor
        v
    J2534Native
        |
        v
 Vendor J2534 DLL
        |
        v
   DiCE / Mongoose
        |
        v
       CAN
```

`J2534Session` owns the device lifecycle. `CanBus` provides the shared send/receive abstraction and serializes transmit calls. `CanRxDispatcher` is the single owner of `PassThruReadMsgs` and fans decoded frames out to subscriptions, so the CAN monitor and ISO-TP do not compete for the same receive queue.

### ISO-TP

`IsoTp.cs` parses Single, First, Consecutive, and Flow Control frames, and segments/reassembles payloads up to 4095 bytes.

`IsoTpChannel` provides synchronous request/response transport with:

- configurable request/response CAN IDs and 11-bit or 29-bit addressing;
- single-frame and multi-frame transfers;
- Flow Control generation and reception;
- Block Size handling in both directions, including `BS = 0`;
- millisecond and 100–900 microsecond STmin handling, with reserved values rejected;
- Consecutive Frame sequence validation and wrap-around;
- per-stage and overall request deadlines;
- cancellation and bounded handling of Flow Control WAIT frames;
- recovery after timeout, cancellation, and malformed Flow Control or Consecutive Frames.

Example:

```csharp
using var channel = new IsoTpChannel(
    bus,
    new IsoTpChannel.Options(
        RequestId: 0x7E0,
        ResponseId: 0x7E8,
        RxBlockSize: 0,
        RxSeparationTime: 0,
        RequestTimeout: TimeSpan.FromSeconds(5)));

var response = channel.Request(new byte[] { 0x22, 0xF1, 0x90 });
```

Start the `CanBus` after connecting the J2534 channel and stop it before disconnecting. The dispatcher owns the receive loop; application features should use `CanBus` subscriptions rather than reading directly from J2534.

### UDS client

`UdsClient` currently provides:

- `ReadDataByIdentifier(ushort did)` — sends service `0x22` and verifies that the positive response contains the requested DID;
- `ReadVin()` — reads DID `0xF190` and validates the 17-character VIN format;
- `ReadDtcByStatusMask(byte statusMask)` — sends service `0x19`, subfunction `0x02`, and parses returned DTC codes and status bytes.

The client handles negative responses, ignores clearly unrelated responses, and recognizes `ResponsePending` (`NRC 0x78`) without extending the overall request deadline indefinitely. Security Access and write/actuation services are not implemented.

### ECU profiles

`EcuProfile` stores request/response CAN IDs, 11-bit vs 29-bit addressing, candidate readable DIDs, source notes, and a verification flag. `EcuProfileCatalog` supports case-insensitive lookup and JSON import/export. Imports validate required fields, addressing, DIDs, and duplicate IDs before accepting the catalog. **Development import is all-or-nothing:** the validated JSON catalog is persisted atomically before the UI selection or active CAN IDs change, so a failed read/validation/save leaves the previous catalog visible. The catalog intentionally starts empty: no vehicle-specific identifiers or ECU capabilities are inferred without a reliable source.

**New in current CI builds:** **New profile** and **Edit selected** open an in-app editor for local catalog entries, including ID, name, 11/29-bit CAN IDs, candidate DIDs and a required source note. The editor rejects duplicate profile IDs, invalid/out-of-range addresses and duplicate/invalid DIDs before saving atomically to `%LOCALAPPDATA%\\Dvinholm\\ecu-profiles.json`. Editing keeps the stable profile ID; **all new/edited profiles are marked unverified**, even if an imported record was previously marked verified. Cancel or a failed disk write does not replace the in-memory catalog. **Development backup recovery:** successful catalog saves attempt an atomic snapshot in `ecu-profiles.json.bak`. If the primary catalog is malformed or truncated, startup can show valid profiles from that backup without overwriting the damaged original. When the user later saves, the damaged primary bytes are archived as `ecu-profiles.json.corrupt-<id>.json` before replacement. A backup is best-effort and is not guaranteed when storage is unavailable; if both copies are invalid, the originals are retained. These controls do not transmit any CAN messages.

## Dependencies

Dependencies are version-pinned for reproducible CI builds. The application currently references `Microsoft.WindowsAppSDK` 2.5.1. Tests use `Microsoft.NET.Test.Sdk` 18.10.1, `xunit` 2.9.3, and `xunit.runner.visualstudio` 3.1.5 (xUnit v2-compatible adapter). The Windows App SDK package was checked against Microsoft's current stable release listing; avoid broad automatic upgrades without running the full Windows CI suite. The test SDK and Visual Studio adapter have been updated to current stable xUnit v2-compatible releases. CI must pass before merging.

## Build

Requirements:

- Windows 10 version 1809 or later / Windows 11 (x86 application; actual J2534 driver compatibility still applies)
- Visual Studio 2022 with the Windows App SDK / Windows SDK workloads
- .NET SDK 10.0.401 (pinned in `global.json`)
- Windows App SDK NuGet package 2.5.1 (pinned in the application project)

Build from PowerShell:

```powershell
dotnet restore .\Dvinholm.sln
dotnet build .\Dvinholm.sln -c Release --no-restore -p:Platform=x86
dotnet publish .\src\Dvinholm.App\Dvinholm.App.csproj -c Release -r win-x86 --self-contained true
```

The process is x86, so it can load 32-bit J2534 DLLs but not 64-bit-only DLLs.

## Download and run on Windows

The latest published Stable is **v0.2.4**. Use the [Portable ZIP](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.4/Dvinholm-Windows-x86.zip) or [Lite ZIP](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.4/Dvinholm-Windows-x86-Lite.zip) plus the matching [SHA256SUMS.txt](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.4/SHA256SUMS.txt). Extract to a separate folder and use the included executable. Portable bundles the runtime; Lite needs compatible x86 runtimes.

Development packages are framework-dependent and use `Launcher.exe`. Do not combine files from different archives. Software CI is not a physical J2534 acceptance test.

### Update channels: historical in-app updater and current Development Launcher

**Historical Stable v0.2.3/v0.2.4:** the following describes the earlier in-app updater. Newer Development builds use the separate Launcher. The update row has a source selector: **Development** (default: successful CI builds from `main`) or **Stable** (published stable releases). Click **Check update** after choosing the source. Switching sources invalidates any previous update offer and does **not** silently install a build. With **Stable**, the updater resolves the latest published stable GitHub release's tag to a fixed commit and requires the public portable Windows x86 ZIP to have a valid SHA-256 digest. **Development** continues to check the newest successful Windows x86 **main-branch** CI build with a published updater asset. The version row shows the current build's seven-character commit SHA, and **Replace files and restart** appears only when the selected source's commit differs. **Stable v0.2.3 includes the two-source UI; historical v0.2.2 has the original CI-only updater.** Selecting a different-commit **Stable** release and clicking **Replace files and restart** requires a separate confirmation dialog. **Cancel** exits without downloading or replacing any files. A different SHA does not by itself mean the chosen release is newer; when switching from Development to Stable, the selected Stable release may remove newer app features.

Updates are selected from successful `push` runs on **main** with a corresponding public updater asset; pull-request and other-branch runs are excluded. Since GitHub Actions artifact ZIP downloads require authentication, successful main-branch CI runs publish the same Windows x86 ZIP as a public GitHub prerelease asset tagged `ci-<run-number>`. The updater downloads that asset anonymously—no GitHub token is stored in the app—and verifies its size and SHA-256 digest when GitHub provides one, checks archive paths, and stages the extracted files before installation.

When an update is applied, a helper waits for the running app to exit, mirrors the staged files into the **same directory from which Dvinholm was launched** using Robocopy `/MIR`, and therefore replaces changed files and deletes obsolete application files there. It then removes old per-build cache directories (while preserving the active directory if it is inside `Builds`) and stale staging folders, then restarts Dvinholm. ECU profile data remains separate at `%LOCALAPPDATA%\\Dvinholm\\ecu-profiles.json` and is preserved. If the update helper fails, it records details in `%LOCALAPPDATA%\\Dvinholm\\update.log`. **Later Development builds add an independent recovery guard:** before any destructive mirror, the helper verifies its old-build backup, launches a separate PowerShell watchdog, waits for a readiness acknowledgement, and writes a rollback checkpoint outside the installation directory. If the helper is abruptly terminated while Windows and the watchdog remain running, the guard attempts to restore the previous build (or removes an incomplete first-install destination). It keeps the backup and checkpoint if recovery itself fails. **Newer Development CI builds additionally register a per-update HKCU Run entry before the destructive mirror**, pointing to a durable recovery script stored beside the old-build backup outside the installation directory. On the next Windows sign-in after a reboot, this script can detect a pending transaction and attempt to restore the previous build (or remove a partial first installation), independently of the original helper and watcher processes. The entry is removed only after a confirmed successful update, confirmed rollback, or safe pre-replacement cleanup. A still-running original helper is recognized by PID **and** start time to avoid premature rollback, and competing recovery processes use a mutex. This is a sign-in recovery mechanism, **not a guarantee of restoration before login or in the event of disk/registry corruption**. Isolated PowerShell tests do not replace real restart, login or in-place installation testing.

The update channel follows public per-CI prerelease assets rather than the latest stable GitHub Release. The Current label shows the CI run number and seven-character commit SHA when build metadata is available. If update preparation fails, the app stays open and reports the immediate error; full exception details are written to `%LOCALAPPDATA%\\Dvinholm\\update.log`. The application directory must be writable for in-place replacement. Physical J2534 adapter validation is not implied by CI success and remains deferred to a future release.

### Troubleshooting

- **No J2534 provider appears:** install or repair the adapter manufacturer's driver, confirm it provides a 32-bit J2534 DLL, then restart Dvinholm. You can enter the DLL path manually if discovery does not list it.
- **DLL load or connection error:** check that the selected DLL is the vendor's 32-bit build and that its driver dependencies are installed. A 64-bit-only DLL cannot be loaded by this x86 application. After a failed connection attempt, Connect should become available again; correct the DLL path or driver setup before retrying.
- **The app does not start:** extract the complete artifact again, keep the published files together, and confirm that Windows meets the minimum supported version.
- **Connection succeeds but no useful CAN/UDS data appears:** verify the documented bus rate and ECU request/response IDs. The built-in ECU profile catalog is intentionally empty; do not guess IDs or assume every ECU supports VIN/DTC requests.
- **A read times out or is rejected:** record the operation and reported error, stop capture if the driver is unstable, disconnect, and reconnect only after the adapter is ready. Begin physical testing with passive CAN monitoring.

Dvinholm currently supports read-only diagnostic operations. Do not use it to write to or actuate an ECU. Physical-adapter compatibility remains unverified until the future hardware-validation checklist has been completed.

## Tests and CI

The test project is `tests/Dvinholm.App.Tests` (legacy technical name). It includes hardware-independent tests using `VirtualJ2534Adapter` for CAN transport, ISO-TP flow control/reassembly/recovery, and UDS read operations. GitHub Actions restores, builds, runs tests, publishes the application, and uploads the build artifact on Windows.

Tests against the virtual adapter do not replace validation with the actual J2534 hardware and its vendor driver.

### Real-adapter validation checklist

Run this checklist on a stationary vehicle with a supported, correctly installed 32-bit J2534 driver. Use a stable power supply where appropriate and follow the vehicle manufacturer's service precautions. Begin with passive CAN monitoring; only send the read-only diagnostic requests listed below. Do not run active discovery or diagnostic requests while another diagnostic tool is controlling the same adapter or bus.

| Check | Procedure | Expected result / evidence |
|---|---|---|
| Driver discovery | Start Dvinholm and refresh registered J2534 DLLs; if needed, enter the vendor's 32-bit DLL path manually. | The intended DLL is selectable and the application reports connection errors clearly if loading fails. |
| Connect and disconnect | Connect at the documented bus rate for the vehicle/interface, then disconnect. Repeat several times. | No hung UI, duplicate receive worker, or stale connected state. Record adapter model, driver/DLL version, and selected baud rate. |
| Passive CAN monitor | Start capture with no UDS operation running; apply and clear a software ID filter. | Frames appear, the filter affects display only, and capture can be stopped and restarted. |
| CSV export | Capture a short sample and export it. | File contains the timestamp, CAN ID, frame type, DLC, and data columns; values match the displayed sample. |
| Hardware filter, if supported | Apply a single CAN ID filter, verify observed traffic, then remove the filter. | The UI reports whether the vendor call succeeded. Compare with software filtering; do not assume export detection proves filter conformance. |
| Read VIN | Select a documented request/response ID pair for the target ECU and request DID `0xF190`. | A valid 17-character VIN is shown only if the ECU supports this request; otherwise record the negative response or timeout without retry loops. |
| Read DTCs | For an ECU known to support UDS service `0x19`, request subfunction `0x02`. | Returned DTC/status records parse correctly, or the ECU's negative response is shown with its NRC. No clear/erase operation is performed. |
| Cancellation and recovery | Cancel a supported read while it is waiting for a response, then perform a passive capture and disconnect. | Cancellation returns control, and subsequent monitor/session operations do not hang. |
| Driver / adapter recovery | If the vendor driver reports a read error, stop capture, disconnect, and reconnect after the adapter is ready. | The error is surfaced and the application does not silently claim a successful diagnostic read. |

Record the vehicle/ECU identification, adapter and DLL versions, bus rate, request/response IDs, operation, and result. Redact VINs and other identifying data before sharing logs. A virtual-adapter CI pass validates software behavior only; it cannot certify electrical compatibility, vehicle-specific IDs, timing, or vendor-driver behavior.

## Project history and roadmap

- [Руководство пользователя на русском](docs/USER_GUIDE_RU.md) — установка, J2534, CAN Monitor, ECU profiles, UDS и обновления.
- [Ручная проверка поиска ECU-профилей](docs/manual-ecu-profile-search-2026-10-19.md) — девять шагов без физического адаптера после обновления до CI-сборки `main`.
- [Ручная проверка создания/редактирования ECU-профилей](docs/manual-ecu-profile-editor-2026-10-19.md) — десять шагов для Windows без адаптера.
- [Current roadmap](ROADMAP.md) — planned work and verified milestones.
- [Development history](docs/DEVELOPMENT_HISTORY.md) — archived MVP and historical version plans.
- [Engineering handoff log](AI_WORKLOG.md) — detailed CI evidence and project notes.
- [Changelog](CHANGELOG.md) — release history.


## Strict recovery audit receipt validation (Development)

The read-only native **Recovery status / Статус восстановления** inspector
displays only a recent, well-formed sign-in recovery receipt matching the
current installation. It validates a real UTC calendar timestamp, rejects
records more than 30 days old or over 5 minutes ahead of the current clock,
and requires a consistent outcome and reason: Started/AttemptStarted,
Succeeded/(RollbackRestored, CommittedVerified, NoTransaction), or
Blocked/(a known failure reason). Invalid records are omitted, not repaired
or trusted. The receipt remains advisory and cannot authorize restoration
or deletion. Eight Windows PowerShell 5.1 offline acceptance fixtures
cover valid success/blocked reports, impossible dates, future/stale
timestamps, contradictory outcomes, mismatched installation keys and
mixed worker folders. This is not evidence of an actual Windows reboot,
interactive GUI start or J2534 vehicle test. Stable v0.2.4 is unchanged.
