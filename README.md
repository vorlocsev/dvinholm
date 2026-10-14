# Dvinholm — J2534 CAN Diagnostics

Windows CAN monitor and read-only UDS diagnostic client for Volvo interfaces that expose the SAE J2534 API.


## Latest verified CI build (2026-10-14)

- Main commit: `1c9f1977ce54b3d03388c1675cc43af709d6a12d`.
- [Post-merge GitHub Actions run #38612886605](https://github.com/vorlocsev/dvinholm/actions/runs/38612886605) passed Restore, Build, Test, Publish, and Upload artifact.
- Artifact: `Dvinholm-Windows-x86`, 66,752,947 bytes. GitHub-reported digest: `sha256:70b48e99371fc6b7f7cfb46635ccd0016123a14cf5c52e5b103e1bb2962acd3d`. This service-reported digest has not been independently recalculated from a locally downloaded ZIP.
- This artifact is a portable self-contained x86 build, not an installer. It has not yet received a fresh manual smoke test on this exact run.
- This CI result is not a published/versioned release. No tag or public GitHub Release has been created.

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
- Import, validate, select, and export JSON ECU profile catalogs. The catalog starts empty; no unsupported vehicle-specific CAN IDs or DIDs are guessed.
- Discover observed UDS responders and select 11-bit or 29-bit CAN addressing.
- Read a configured Data Identifier (UDS `0x22`), read VIN from DID `0xF190`, and read DTC records using UDS `0x19` / subfunction `0x02`.
- Use ISO-TP over Classic CAN for single-frame and multi-frame requests and responses, including Flow Control, Block Size, STmin, timeouts, cancellation, and sequence-number validation.

UDS functionality is currently read-only. A profile or DID marked as a candidate does not guarantee that a particular ECU supports it. Do not treat observed responses or profile metadata as proof of support.

No immobilizer/PIN extraction, brute forcing, security bypass, or automated security-key calculation is implemented.

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

1. Open the repository's [GitHub Actions](https://github.com/vorlocsev/dvinholm/actions) page and select a successful run on `main`.
2. In **Artifacts**, download `Dvinholm-Windows-x86`.
3. Extract the entire archive to a normal local folder. Do not run the executable from inside the ZIP.
4. Launch `Dvinholm.App.exe` from the extracted folder.
5. Install the adapter manufacturer's 32-bit J2534 driver separately. Connect the interface only after following the manufacturer's instructions.

The application is self-contained, so a separate .NET runtime installation is not normally required. Windows and driver requirements still apply. The artifact is intended for evaluation; CI success does not mean the application has been verified with a physical adapter or a specific vehicle.

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

## Roadmap to first software MVP release

Status is based on repository code, automated tests, and explicitly recorded manual checks. Updated 2026-10-14. Core CAN/ISO-TP/UDS transport and J2534 lifecycle/recovery have broad virtual-adapter regression coverage. The Windows 11 IoT LTSC 26100.9550 startup/no-device smoke check is confirmed. Physical-adapter compatibility remains unverified and is intentionally not a blocker for this first software MVP.

### Phase 0 — close current audit and stabilize main
- [x] Latest main checkpoint after PR #212: commit `65ea0af8447bd746a16261199eba4952b1753c6c`; full post-merge Windows CI passed: [run #38611124491](https://github.com/vorlocsev/dvinholm/actions/runs/38611124491).
- [x] Current run uploaded non-expired `Dvinholm-Windows-x86` (66,752,774 bytes). GitHub artifact metadata digest: `sha256:d836c8f4c965d6fbbfe47b76b18c39a1f3c5874cb63f00466a44a9998392ed98`. This is the service-reported digest, not an independently recalculated local ZIP hash.
- [x] Drafted `RELEASE_NOTES.md` with MVP scope, known limitations, current CI evidence, and a pre-publication checklist. It is a draft, not a published release announcement; no version tag or public release exists.

- [x] Complete the source-level diagnostic-data exposure review: no persistent diagnostic logger or automatic telemetry was found in the reviewed paths; explicit VIN reads and user-initiated CAN CSV export may contain identifying data.
- [x] PR #202 merged as `a8ddf1c735865566af0c5be222be1e9b956b3528`; exact-head CI passed: [run #38508715796](https://github.com/vorlocsev/dvinholm/actions/runs/38508715796).
- [x] Post-merge `main` CI passed on PR #202 merge commit: [run #38509199082](https://github.com/vorlocsev/dvinholm/actions/runs/38509199082).
- [x] PR #203 merged as `8a886fb36f66b6c14f3befefea22294667bfc28d`; post-merge `main` CI passed: [run #38510453531](https://github.com/vorlocsev/dvinholm/actions/runs/38510453531).
- [x] PR #206 merged as `0b3ca883511e4a4f3db3c465777bf670265ff38b`; exact PR-head CI passed: [run #38600469100](https://github.com/vorlocsev/dvinholm/actions/runs/38600469100). This documentation-only change records profile and failed-connect manual checks.
- [x] Rechecked current `main`, README roadmap, and AI handoff before continuing.

**Exit gate:** PR #202 is merged, its exact head passed CI, and post-merge `main` CI is green.

### Phase 1 — finish ECU profile user workflows

- [x] Manually import a valid JSON profile catalog and confirm the profile name appears in the selection list after resolving the initial display issue (user-confirmed 2026-10-14). Full request/response ID, addressing-format, and candidate-DID reflection still needs verification.
- [x] Manually import a malformed JSON file and confirm the UI shows an import error and the previously imported profile remains present/selectable afterward (user-confirmed 2026-10-14).
- [x] Manually import syntactically valid JSON with an invalid CAN request ID (`requestId: "ZZZZ"`); the UI reported an import error and the previously imported valid profile remained present (user-confirmed 2026-10-14).
- [x] Import a JSON catalog containing duplicate profile IDs; user confirmed the import was rejected (2026-10-14). Preservation of the prior catalog after this specific rejection was not explicitly confirmed.
- [x] Import a valid empty JSON catalog (`[]`); the UI accepted it and displayed no profiles, and the empty list remained after restarting the app (user-confirmed 2026-10-14). This confirms empty-catalog import and persistence. Clearing of the previously selected profile’s dependent CAN ID/DID fields was also confirmed during the replacement/clearing check.
- [x] Restart the app and confirm the imported profile remains present (user-confirmed 2026-10-14). This verifies persistence for the tested profile; broader catalog/multiple-profile persistence is not yet covered.
- [x] Export the catalog and import the exported JSON again; user confirmed the profile was restored (2026-10-14). Whether re-import creates a duplicate entry was not explicitly confirmed.
- [x] Manually verify profile replacement/clearing: import and select the synthetic test profile, import `[]`, confirm profile-dependent UI state clears, then import the test profile again and confirm it appears/works (user-confirmed 2026-10-14).
- [ ] Fix only demonstrated UX or state defects and add focused regression tests for each confirmed defect.
- [x] Select the synthetic `Example ECM (test only)` profile and verify its request CAN ID (`7E0`), response CAN ID (`7E8`), 11-bit addressing format, and candidate DID (`F187`/`F190`) are reflected correctly in the UI (user-confirmed 2026-10-14). The user currently has only one profile, so switching between two profiles remains an optional, unverified check rather than a release blocker.
- [ ] Keep the built-in catalog empty of unverified vehicle-specific CAN IDs/DIDs; user-supplied candidate profiles must not be presented as proof that an ECU supports an operation.

**Exit gate:** import, validation failure, persistence across restart, export/re-import, selection, and empty/replacement behavior work end-to-end or any unsupported UI-only portion is explicitly documented.

### Phase 2 — repeatable software smoke and recovery checks

- [x] Source audit confirms the no-registered-adapter UI path has an explicit status message and preserves manual DLL entry: `No registered J2534 DLL found. You can still enter the DLL path manually.` `J2534DeviceDiscoveryTests` cover device-path normalization and isolated registry-root failures. A full no-provider UI run remains unverified because the user's machine currently detects adapter drivers.
- [x] Audit existing backend coverage before adding tests: `J2534SessionLifecycleTests` already cover invalid inputs, staged connect failures, cleanup/metadata reset, retry after failed connection, read-error sanitization, reconnect, and cleanup failure. `CanRxDispatcherStopDisposeRaceTests` and `CanBusDisposeFromCallbackTests` cover deferred disposal/read-error callback races; `UdsClientDisposeLifecycleTests` and related ISO-TP tests cover active-request cancellation/disposal.
- [x] Manually tested failed connection recovery using a deliberately nonexistent DLL path (no adapter hardware required): the user confirmed the Connect button becomes available again after the failed attempt. The error-recovery button state is verified; monitor/UDS disabled state and retry after correcting the path were not separately confirmed. Read-error and active-diagnostic cancellation UI recovery still need a suitable simulated UI seam or a documented limitation.
- [ ] Avoid duplicating existing lifecycle, timeout, cancellation, late-response, and reconnect-race tests; fill only verified gaps.
- [x] Run the full Windows x86 CI pipeline on exact checkpoint SHA `5e787c121dff7d7dce348a77d0233fd3d65608a0`; restore, build, tests, x86 publish, and artifact upload passed: [run #38600469100](https://github.com/vorlocsev/dvinholm/actions/runs/38600469100). A release candidate still requires its own exact-SHA CI run.

- [x] Final source-level regression inventory reviewed (2026-10-14): existing tests already cover J2534 session lifecycle/retry/cleanup, CAN receive-worker disposal races, ISO-TP timeout/cancellation/flow-control/sequence recovery, UDS late/unrelated/negative/pending responses, profile JSON validation, and virtual-adapter single-/multi-frame flows. No duplicate backend test was added without evidence of a gap. The no-provider UI state and UI-only read-error/cancellation control recovery remain explicitly unverified and are not claimed as passed.

**Exit gate:** the core software paths have repeatable regression coverage, no known blocking defect remains, and the complete CI pipeline is green.

### Phase 3 — packaging and clean-install verification
- [x] Latest main CI artifact is available from [run #38609609211](https://github.com/vorlocsev/dvinholm/actions/runs/38609609211), built from `d650eae04cc19b8925d0f51ebba1bd467aa78b7d`. User-confirmed clean-folder launch/relaunch and window-title smoke checks refer to the preceding artifact; they are not represented as a new manual test of this documentation-only checkpoint.

- [x] Confirm the successful `main` CI run uploaded a non-expired artifact named `Dvinholm-Windows-x86` (66,752,802 bytes; run #38509199082).
- [x] Inspect the artifact ZIP directly: archive integrity passed (`testzip` returned no errors), 476 entries found, and `Dvinholm.App.exe` plus `RestartAgent.exe` are present. Both PE headers identify machine `0x014c` (x86). SHA-256: `4ee5d67302d31c38f83d07884673d28674b2fbed8088cfdf6cc1bf0b752fdc27`.
- [x] Download the current CI artifact, extract it into a fresh separate folder, launch `Dvinholm.App.exe`, close it, and launch it again successfully on Windows 11 IoT LTSC 26100.9550 (user-confirmed 2026-10-14). No missing-dependency/startup error was reported.
- [x] Downloaded and launched the CI artifact built after the main-window caption fix; user confirmed the check was successful (2026-10-14). The intended title is `Dvinholm — J2534 CAN Diagnostics`, replacing the generic `WinUI Desktop` caption.
- [x] Confirm first-launch behavior with no physical J2534 device connected: the app opens and reports that no device is present (user-confirmed in the Windows smoke check). This is not a physical-adapter test.
- [ ] Verify the UI behavior when no J2534 provider is registered. The current machine detects adapter drivers, so do not disable/uninstall them just to exercise this state; source audit confirms manual DLL path entry remains available.
- [x] Reviewed README instructions against the portable self-contained artifact and documented extraction, launch, 32-bit DLL requirements, troubleshooting for connection failure, and the limits of virtual-adapter CI. The artifact is not an installer.
- [x] State clearly that current CI uses a virtual adapter and does not certify a physical interface, a specific vehicle, ECU addressing, electrical compatibility, or vendor-driver behavior.

**Exit gate:** a new user can obtain the artifact, follow the instructions, launch the app, understand the no-adapter state, and identify the limits of what has been validated.

### Phase 4 — release candidate and public release

- [ ] Freeze the release-candidate scope; defer nonessential features and vehicle-specific assumptions.
- [ ] Run the complete CI workflow on the exact release-candidate commit and verify restore, build, all tests, x86 publish, and artifact upload.
- [ ] Review the release artifact and installation instructions one final time; ensure the source tag/commit matches the validated candidate.
- [ ] Create a versioned Git tag and GitHub Release with concise release notes: supported read-only features, install/driver requirements, known limitations, and tested environments.
- [ ] Include the virtual-adapter test limitation and the explicit statement that real-hardware compatibility has not yet been verified.
- [ ] After publishing, verify that the release page and artifact are accessible and that the documented installation steps match the published files.

**Release acceptance criteria**
1. No known release-blocking software defects.
2. Exact release-candidate SHA passes the complete Windows x86 CI pipeline.
3. Profile workflows and startup/no-adapter behavior have been checked and documented.
4. Release artifact, version tag, instructions, and release notes agree.
5. Read-only scope and unverified hardware compatibility are plainly stated.

**Definition of done:** first software MVP release is published and reproducible from a tagged, CI-verified commit. This does not claim physical J2534 or vehicle compatibility.

### Future release cycle — physical adapter validation (not a first-release blocker)

- [ ] Validate passive CAN capture with a real 32-bit J2534 adapter.
- [ ] Verify repeated connect/disconnect and recovery on DiCE and/or Mongoose JLR Pro hardware.
- [ ] Check vendor DLL loading, actual baud-rate behavior, native read errors, and supported hardware filters.
- [ ] Validate read-only VIN/DTC requests only against ECUs with confirmed support and documented addressing.
- [ ] Record adapter model, driver/DLL version, vehicle/ECU, bus rate, IDs, operation, and outcome; redact VINs and identifying information before sharing.

**Exit criterion:** repeatable, recorded results on at least one physical adapter. Until then, hardware compatibility remains unverified.

### Out of scope

The project remains focused on monitoring and read-only diagnostics. Immobilizer/PIN extraction, brute force, security bypass, automated security-key calculation, ECU writes, and actuation are not roadmap items.
