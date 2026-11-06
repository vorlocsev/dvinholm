# ADR-DV-001 | Dvinholm-first Virtual Car test laboratory contract

Status: PROPOSED, documentation only. Date: 2026-11-06.
Owners: Dvinholm development and separately versioned Virtual Car laboratory.
This proposal defines a future architecture, not a claim of existing functionality.

## Principle and priority

**Dvinholm is the production application and the primary development goal.**
Virtual Car exists exclusively to supply repeatable and falsifiable testing of
Dvinholm's J2534 transport, CAN/ISO-TP, read-only diagnostics, CEM/ECU
identification and, eventually, separately authorized configuration workflows.
Virtual Car is NOT a source of production Volvo protocol truth by itself.

The client shall not know whether a J2534 adapter is hardware or laboratory.
Dvinholm must use the exact normal production path:
J2534Session -> CanBus -> CanRxDispatcher -> IsoTpChannel -> UdsClient and
the appropriate explicitly identified P2/D2 reader. A successful fake response
may prove software plumbing but never proves a real CEM, VIN, PIN or
configuration operation is supported.

## Existing baseline / release realities

- Dvinholm main currently contains net10 WinUI 3, an explicit x86 target,
  native J2534 loading, IJ2534Adapter / IJ2534SessionAdapter and a synthetic
  in-process adapter in read-only regression tests.
- The agreed unified self-contained x64 WinUI 3 migration is being developed
  separately in Dvinholm PR #702. Its UI/physical-J2534 acceptance and
  publication must not be assumed from the older main x86 source.
- The Virtual Car development repository currently builds a C Windows x86
  J2534 DLL and a Go control application; there is no x64 Virtual Car DLL,
  shared IPC controller or SQLite scenario database in the reviewed baseline.
- Dvinholm Stable/Production must not include test-only fixtures, synthetic
  responses or private research assets.

## Target topology

    Dvinholm (WinUI 3 .NET 10, target x64)
       | production J2534 API, same code as physical devices
       v
    VirtualCarJ2534-x64.dll (thin J2534 adapter, native C ABI)
       | authenticated local Named Pipe (control plane separate)
       v
    VirtualCar.Core (isolated x64 Go process: orchestration and timing)
       | read immutable compiled scenario (no SQL queries per CAN frame)
       v
    VirtualCar Lab DB (SQLite metadata) + private content-addressed artifacts

    Other legacy x86 diagnostics -> VirtualCarJ2534-x86.dll -> same core.
    VirtualCar Control UI -> separate local controller API -> same core.

Dvinholm MUST NOT reference or embed Virtual Car Go/C/Rust libraries or its
SQLite database, and must be able to run without Virtual Car installed.
The DLL architecture must match the client process bitness: Dvinholm's
future x64 process requires a separate actual PE32+ x64 DLL; an x86 J2534
DLL cannot be loaded directly by x64 Dvinholm. The lab may use x86 and x64
thin DLLs against a common out-of-process x64 engine.

## Contract boundary: J2534, not a vendor-specific side API

- Preserve native PassThru 04.04 exports/ABI, struct layout,
  stdcall x86, calling conventions on x64, process-local
  channel/device/filter IDs and explicit error codes.
- Correct PassThruOpen/Close, Connect/Disconnect, ReadMsgs/WriteMsgs,
  filter, Ioctl, ReadVersion, StopPeriodic, timeout and cancellation.
- Timestamps use monotonic time and preserve ordering, first-message
  and ISO15765 metadata. Every response is bound to the exact session,
  bus, address, filter and current scenario generation.
- The J2534 adapter is not permitted to select a fixture by VIN string
  alone or secretly respond from a generic ECU when an exact fixture is
  missing. Absent evidence => deterministic no_fixture/unsupported status.
- Per-channel bounded receive queues; overflow, stale responses and
  daemon disconnect are explicit failures, not successful diagnostic reads.
- The test harness may inject precisely scoped faults, including
  disconnect, late response, flow-control timeout and dropped CAN frame.

## Internal-only contract (not consumed by Dvinholm)

Private Named Pipe control methods, versioned from v1:
- Hello(protocol_version, capabilities, client_kind)
- ListScenarios / SelectScenario(id, revision) / GetCurrentScenario
- ResetToSnapshot / StartSession / StopSession
- ConfigureFaultSchedule / FetchRunSummary / SubscribeSafeEvents
- StartTestRun(test_case_id) / CancelTestRun

Commands are authenticated/authorized to the current Windows user SID and
limited to the local machine; control plane and high-rate data plane use
separate pipe endpoints. A diagnostic client may not invoke management-only
operations from the J2534 data endpoint. Message size, list counts and
per-method deadlines must be bounded. Define protocol negotiation and fail
closed on unsupported versions. The control service must not expose a
network port or accept uncontrolled scripts.

## Canonical Dvinholm acceptance scenarios

1. x64 Dvinholm loads an actual PE32+ virtual J2534 DLL; x86 diagnostics
   continue loading the separate PE32 x86 DLL.
2. Connect/disconnect/reconnect, baud 125000/500000 and 11/29-bit CAN
   preserve protocol/metadata; no channel leakage across connections.
3. CAN Monitor sees exactly the same RX as the diagnostic reader and the
   RX dispatcher does not steal responses from ISO-TP.
4. Single and multi-frame ISO-TP, flow-control BS/STmin, unrelated frames,
   FIFO and separate FC timeout match the existing Dvinholm contract.
5. Known, explicitly synthetic read-only UDS DID succeeds end-to-end;
   unknown DID or missing fixture does not fabricate a positive response.
6. Loss of backend, queue overflow, source revision change, cancellation and
   timeout invalidate pending read work rather than returning a stale result.
7. Capture -> snapshot -> inspect/diff retains redaction and provenance,
   and cannot become a restorable ECU configuration or production permission.
8. Tests are deterministic in CI; a separate signed-off Windows GUI test
   and physical J2534/real ECU test remain unclaimed until performed.

Additional project-specific scenarios should follow evidence-gated P2
discovery, VIN, CEM PIN and configuration research, NEVER conflating
synthetic simulator acceptance with physical vehicle support.

## Evidence and report format

Every test result records:
  product=Dvinholm, tested_commit, virtualcar_commit, adapter_sha256,
  scenario_revision_sha256, test_case, passed/failed, classification
  (synthetic|captured|verified_runtime|unknown), failure_category,
  transport_diagnostics, run timestamps and whether hardware was absent.
Reports do not include VIN, PIN, keys, EEPROM, unrestricted CAN payloads
or real car identifiers by default.

Exact distinction must remain visible:
  transport_delivered != decoded_valid != script_selected
                      != vehicle_identity_accepted != ECU_inventory_accepted.

Production auto-release gates must never accept synthetic Virtual Car
success as a substitute for separately authorized physical testing.

## Migration phases

A. Establish versioned contract + SQLite migration design in Virtual Car.
B. Refactor current Go control into a headless core with explicit APIs,
   reusing tested C transport rather than rewriting C and Go at once.
C. Implement small native x86/x64 J2534 pipe shims and exact ABI tests.
D. Port synthetic and provenance-labelled vehicle/ECU scenarios.
E. Run Dvinholm E2E through the *native DLL*, not just its in-memory
   IJ2534Adapter; compare with old virtual-adapter tests.
F. Build automated result matrices and repeatable artifact manifests.
G. Only later consider Rust implementation of pure protocol components
   if benchmarks/maintainability justify it. No rewrite is a prerequisite.

## Explicit non-goals

No automatic installation in Dvinholm Stable; no production dependence on
Virtual Car; no external networking; no CAN/ECU write on real hardware;
no invented MY2009 CEM positive frames; no automatic disclosure of private
VIDA XML, VIN/PIN or CEM dumps; no Virtual Car release publication.

Related design in the independent lab repository:
docs/architecture/VIRTUALCAR-CORE-SQLITE-V1.md (proposed).
