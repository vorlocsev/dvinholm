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
  <a href="https://github.com/vorlocsev/dvinholm/releases/tag/v0.2.1"><img alt="Stable v0.2.1" src="https://img.shields.io/badge/stable-v0.2.1-167cc1"></a>
  <img alt="Windows x86" src="https://img.shields.io/badge/platform-Windows%20x86-203f60">
  <img alt="UDS read-only" src="https://img.shields.io/badge/UDS-read--only-008b8b">
</p>

<p align="center">
  <a href="https://github.com/vorlocsev/dvinholm/releases/download/v0.2.1/Dvinholm-Windows-x86.zip"><strong>Download stable release</strong></a>
  &nbsp;·&nbsp; <a href="https://github.com/vorlocsev/dvinholm/actions">Latest CI builds</a>
  &nbsp;·&nbsp; <a href="docs/USER_GUIDE_RU.md">Руководство (RU)</a>
  &nbsp;·&nbsp; <a href="ROADMAP.md">Roadmap</a>
  &nbsp;·&nbsp; <a href="CHANGELOG.md">Changelog</a>
</p>

> [!IMPORTANT]
> **Hardware validation is pending.** Windows UI and software tests have been exercised without a physical adapter; compatibility with Volvo DiCE, Mongoose, individual drivers, ECUs, or vehicles has not yet been demonstrated. Dvinholm is an independent project and is **not affiliated with or endorsed by Volvo Cars** or any adapter manufacturer.

## Quick start

1. [Download **Dvinholm v0.2.1** for Windows x86](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.1/Dvinholm-Windows-x86.zip) or choose a successful current `main` build from [GitHub Actions](https://github.com/vorlocsev/dvinholm/actions).
2. **Extract the entire ZIP** into a writable local directory; it is a portable build, not an installer.
3. Run `Dvinholm.App.exe`. A separate .NET runtime is normally not needed for this self-contained build.
4. For adapter use, install the **32-bit J2534 driver** supplied by the hardware manufacturer, then select the registered DLL or enter its path in Dvinholm.
5. Start with **passive CAN monitoring** once a compatible adapter and vehicle are available. UDS commands are read-only.

The branded window, taskbar, and executable icon is packaged with v0.2.1. On 2026-10-19, a user confirmed that the icon is visible in the Windows application; individual display locations were not separately specified.

## Latest stable release — v0.2.1 (2026-10-19)

- **Release:** [Dvinholm v0.2.1](https://github.com/vorlocsev/dvinholm/releases/tag/v0.2.1).
- **Windows x86 portable ZIP:** [Dvinholm-Windows-x86.zip](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.1/Dvinholm-Windows-x86.zip). This is not an installer; extract the full archive before launching `Dvinholm.App.exe`.
- **Archive size:** 67,042,782 bytes. **SHA-256:** `5245d7691482da6434262b5767a0b23ed757df9136fb38b9aed196523a47bf09` — [SHA256SUMS.txt](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.1/SHA256SUMS.txt).
- **What's new:** branded icon and README, persisted CAN speed, two clearly labeled synthetic ECU profiles, profile-selection fixes, and additional updater/cancellation/rollback regression coverage. See [CHANGELOG.md](CHANGELOG.md).
- **Validation:** [stable release CI #39184108681](https://github.com/vorlocsev/dvinholm/actions/runs/39184108681) passed Windows build, tests, publish, packaged icon check, release metadata verification, and a separate re-download/SHA-256 check. Release commit: `eb51b998cee3557b661b5cfa9626126ddaea6be4`. Physical adapter/vehicle compatibility is **unverified**.
- **Windows visual check:** user-confirmed icon visibility in v0.2.1 on 2026-10-19. CI separately verifies packaged icon data; individual titlebar/taskbar/Explorer locations were not reported.

Previous stable release: [Dvinholm v0.2.0](https://github.com/vorlocsev/dvinholm/releases/tag/v0.2.0), published 2026-10-17.


## Supported interfaces

- Volvo DiCE
- Mongoose JLR Pro

Install the interface drivers and vendor J2534 DLL separately. The application is built as x86 to support legacy 32-bit J2534 drivers.

## Current functionality

- Discover registered 32-bit J2534 DLLs or enter a DLL path manually.
- Open and close the J2534 device and connect to CAN at 500, 250, or 125 kbit/s.
- Live CAN capture, software CAN ID filtering, optional J2534 hardware ID filters, frame-rate/count statistics, and CSV export.
- Report firmware, vendor DLL, and J2534 API versions when the driver supports `PassThruReadVersion`.
- Detect optional vendor exports for hardware filters, version queries, and extended error details. Export detection is not a hardware conformance test.
- Import, validate, search, select, and export JSON ECU profile catalogs. Search supports names, catalog IDs, source notes, CAN IDs and candidate DIDs; a separate filter shows profiles **marked verified by the catalog author**, without implying actual hardware compatibility. Profile details are selectable and scrollable. The catalog starts empty; no unsupported vehicle-specific CAN IDs or DIDs are guessed.
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

`EcuProfile` stores request/response CAN IDs, 11-bit vs 29-bit addressing, candidate readable DIDs, source notes, and a verification flag. `EcuProfileCatalog` supports case-insensitive lookup and JSON import/export. Imports validate required fields, addressing, DIDs, and duplicate IDs before accepting the catalog. The catalog intentionally starts empty: no vehicle-specific identifiers or ECU capabilities are inferred without a reliable source.

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

The CI pipeline publishes a self-contained x86 application artifact. It is a portable build output, **not an installer**.

1. Download the [stable v0.2.1 Windows x86 ZIP](https://github.com/vorlocsev/dvinholm/releases/download/v0.2.1/Dvinholm-Windows-x86.zip), or open [GitHub Actions](https://github.com/vorlocsev/dvinholm/actions), choose a successful `main` run, and download the `Dvinholm-Windows-x86` artifact.
2. Extract the entire archive to a normal writable local folder. Do not run the executable from inside the ZIP.
3. Launch `Dvinholm.App.exe` from the extracted folder.
4. Install the adapter manufacturer's 32-bit J2534 driver separately. Connect the interface only after following the manufacturer's instructions.

The application is self-contained, so a separate .NET runtime installation is not normally required. Windows and driver requirements still apply. The artifact is intended for evaluation; CI success does not mean the application has been verified with a physical adapter or a specific vehicle.

### Check for and install the latest CI build

The application checks GitHub Actions for the newest successful Windows x86 **main-branch** build with a public updater asset. The version row shows the running build's CI number and seven-character commit SHA. When a newer build is available, choose **Replace files and restart**.

Updates are selected from successful `push` runs on **main** with a corresponding public updater asset; pull-request and other-branch runs are excluded. Since GitHub Actions artifact ZIP downloads require authentication, successful main-branch CI runs publish the same Windows x86 ZIP as a public GitHub prerelease asset tagged `ci-<run-number>`. The updater downloads that asset anonymously—no GitHub token is stored in the app—and verifies its size and SHA-256 digest when GitHub provides one, checks archive paths, and stages the extracted files before installation.

When an update is applied, a helper waits for the running app to exit, mirrors the staged files into the **same directory from which Dvinholm was launched** using Robocopy `/MIR`, and therefore replaces changed files and deletes obsolete application files there. It then removes old per-build cache directories (while preserving the active directory if it is inside `Builds`) and stale staging folders, then restarts Dvinholm. ECU profile data remains separate at `%LOCALAPPDATA%\\Dvinholm\\ecu-profiles.json` and is preserved. If the update helper fails, it records details in `%LOCALAPPDATA%\\Dvinholm\\update.log`.

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
- [Current roadmap](ROADMAP.md) — planned work and verified milestones.
- [Development history](docs/DEVELOPMENT_HISTORY.md) — archived MVP and historical version plans.
- [Engineering handoff log](AI_WORKLOG.md) — detailed CI evidence and project notes.
- [Changelog](CHANGELOG.md) — release history.
