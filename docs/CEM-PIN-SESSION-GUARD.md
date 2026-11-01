# CEM PIN operation lease (Development, partial implementation)

Reviewed 2026-11-01. This increment extends the existing coordinator from
PR #680; it does not introduce a second transport or an executable PIN method.

## Implemented behavior

`CemPinSessionGuard.Capture` binds one attempt to the existing `J2534Session`,
its current `CanBus` and an opaque ECU-context object. Explicit owner consent
is mandatory. A disconnected capture never becomes valid after connection;
reconnection never revives the previous lease. Read errors permanently
invalidate it. Disposing the lease unsubscribes its read-error handler and
invalidates it. The caller owns the lease and must dispose it after completion.

The current-context callback must be thread-safe and return a new opaque token
whenever ECU selection, transport addressing, evidence or consent changes.
It must not read WinUI controls from the background monitor or contain a VIN.
This lease proves neither physical CEM identity nor service entitlement.
The existing capability matrix remains the separate authorization boundary.

The coordinator requires a valid lease, checks it before dispatch and after
completion, and monitors it every 50 ms during a provider call. A lost session
or changed context requests cancellation and discards the receipt. This is
cooperative cancellation, not an atomic lock over native driver calls. Future
providers must check cancellation before every request and use bounded native
calls; the guard cannot retract a request already sent. No such production
provider is registered.

The deadline defaults to two minutes; callers can select a positive deadline
up to one hour for a documented method. Deadline expiration requests
cancellation. **The concurrency gate remains held until the provider actually
terminates**, even if it ignores cancellation. Returning early and allowing a
second operation on a still-active ECU is deliberately prohibited. Such a
provider may exceed the deadline; this is not a forced driver termination.
Authorization callback exceptions are also sanitized and release the gate.

No PIN bytes, credentials, VIN, dumps, files, logs or network calls are added.
Synthetic completion remains `FinishedWithoutPin`, never a real PIN result.
There is no new research screen or enabled Read PIN button.

## Source review and remaining blockers

| Source | Confirmed public information | Consequence for Dvinholm |
| --- | --- | --- |
| [Volvo VIDA communication tools](https://vidaresources.volvocars.biz/help-wbt/en/84bd9b036de4d0cbc0a801cc13408738.htm) | DiCE supports D2/GGD vehicles; the page distinguishes J2534-1 and J2534-2 scope in VIDA. | A generic J2534 connection or successful UDS reply does not establish a P2 CEM route. Reuse the existing stack only after the actual protocol and adapter capabilities are verified. |
| [D5T5 PIN information](https://d5t5.com/article/volvo-cem-pin-code) | Advertises VDASH for some P2 including XC90 V8; VDD has V8 exclusions. | A product workflow is described, but this page supplies no integration contract or revision-specific commands for Dvinholm. Do not automate its private endpoints or assume consent authorizes its protocol. |
| [P3Tool](https://p3tool.org/) | The public entry point is an account login. | No public P2 V8 service API or revision-specific contract was obtained in this review. P3 feature labels cannot establish P2 compatibility; no closed code was read or copied. |
| [vtl/volvo-cem-cracker README](https://github.com/vtl/volvo-cem-cracker) | Describes an OBD cracking project and model coverage claims. | Its brute-force route is explicitly outside this task. No algorithm, command, CAN ID or memory address is copied. |
| [kForth/CEM-B-Pin-Tool README](https://github.com/kForth/CEM-B-Pin-Tool) | Requires an already-read firmware image from an early P2 CEM-B. | Does not establish a live DiCE/J2534 method for a physical MY2009 V8 CEM. No firmware is uploaded or parsed. |

These sources do not validate an exact hardware/application/diagnostic software
revision, a callable authorized provider, or physical acceptance. The built-in
CEM support catalog remains empty. XC90 P2 V8 MY2009 remains a research target,
not a supported revision. There is no known verified PIN to reveal or persist.

To advance identification and a real provider, obtain an independently checked
hardware/software fingerprint, documented read-only protocol/addressing,
adapter capability evidence and a licensed service integration contract with
entitlement verification and result provenance. A generic VIN DID cannot fill
these gaps. Secure temporary PIN presentation and RU/EN live UI integration
remain unimplemented until there is a real provider result contract.

## Validation and physical acceptance protocol

Eight added automated checks cover consent/unknown-revision denial with zero
frames, stale connections, changed ECU context, read error/disconnection,
deadline/retry, non-cooperative cancellation with single-flight retention,
private callback-error redaction, and missing lease/invalid deadlines. They use
the existing in-memory J2534 adapter through real session lifecycle code.
All authorization bypasses are internal synthetic test seams, absent from UI.
The original eight coordinator tests now supply synthetic leases explicitly.

Windows x86 compilation and automatic tests are separate from interactive
Windows GUI acceptance. Neither constitutes a physical DiCE/J2534/CEM test.
No installed application, published Stable artifact or Virtual Car release is
modified by this increment.

Future physical acceptance, only after the documented route is approved:

1. Record adapter model, firmware, driver version, x86 capability and transport support.
2. Record owner consent and service entitlement without secrets in the report.
3. Verify exact CEM hardware/application/diagnostic identity independently of VIN.
4. Check the documented read-only route and reject unknown revisions before transmission.
5. Run the approved provider with stage reporting and verify result provenance privately.
6. Exercise cancellation, timeout, read failure and disconnect on a bench first.
7. Verify no secrets in logs, settings, snapshots or artifacts; test temporary reveal.
8. Verify CAN Monitor/UDS and GUI recovery, then record exact build SHA and verdict.

Until these checks pass, claim only partial implementation, not CEM PIN support.
