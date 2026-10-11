# Dvinholm — J2534 CAN Diagnostics

Windows CAN monitor and read-only UDS diagnostic client for Volvo interfaces that expose the SAE J2534 API.

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

The product is **Dvinholm**, a Windows diagnostic application built with C# and WinUI 3. The current implementation remains in the legacy `VolvoJ2534.App` source/project paths; these technical identifiers are intentionally retained in this branding pass to avoid a risky namespace-wide rename. There is one J2534 backend; the old C++/Win32 backend was removed to avoid duplicate implementations.

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

Dependencies are version-pinned for reproducible CI builds. The application currently references `Microsoft.WindowsAppSDK` 2.5.1. Tests use `Microsoft.NET.Test.Sdk` 17.12.0, `xunit` 2.9.3, and `xunit.runner.visualstudio` 2.8.2. The Windows App SDK package was checked against Microsoft's current stable release listing; avoid broad automatic upgrades without running the full Windows CI suite. The test SDK is behind current NuGet releases and should be upgraded in a separate dependency-only change with CI verification.

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
dotnet publish .\src\VolvoJ2534.App\VolvoJ2534.App.csproj -c Release -r win-x86 --self-contained true
```

The process is x86, so it can load 32-bit J2534 DLLs but not 64-bit-only DLLs.

## Tests and CI

The test project is `tests/VolvoJ2534.App.Tests` (legacy technical name). It includes hardware-independent tests using `VirtualJ2534Adapter` for CAN transport, ISO-TP flow control/reassembly/recovery, and UDS read operations. GitHub Actions restores, builds, runs tests, publishes the application, and uploads the build artifact on Windows.

Tests against the virtual adapter do not replace validation with the actual J2534 hardware and its vendor driver.

## Next steps

- Add ECU profiles only when CAN addressing and diagnostic support have a reliable source.
- Expand read-only UDS coverage with documented, ECU-appropriate behavior and tests.
- Validate J2534 behavior against real adapters where available, especially vendor-specific filters and timing.
