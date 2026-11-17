# CEM PIN operation lease (Development, partial implementation)

Reviewed 2026-11-01. This increment extends the existing coordinator from
PR #680; it does not introduce a second transport or an executable PIN method.

## Implemented behavior

`CemPinSessionGuard.Capture` binds one attempt to the existing `J2534Session`,
its current `CanBus` and an opaque ECU-context object. Explicit owner consent
is mandatory. A disconnected capture never becomes valid after connection;
reconnection never revives the previous lease. Read errors permanently
invalidate it **and fault the shared J2534 transport** with a non-blocking
atomic flag. A fresh guard remains invalid until the explicit adapter
Disconnect/Connect lifecycle completes; merely clearing a synthetic read
error must not re-arm CEM requests. The read-error handler verifies that the
current transport is the same CAN bus captured by the lease: a stale guard
left subscribed across reconnect must not poison a newer, unrelated session. Disposing the lease unsubscribes its
read-error handler and invalidates it. The caller owns the lease and must dispose it after completion.

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

Local regression on a two-core Windows host initially passed 2184/2187 tests.
Three existing fixture issues were corrected without changing updater or
transport code: ZIP timestamps have no UTC offset, ISO-TP cancellation must
not depend on pool-timer availability, and passive scan traffic must not rely
on a delayed test continuation arriving within a 500 ms window. The latter
now feeds periodic synthetic RX traffic on a dedicated worker and still
requires exactly the two valid responder IDs, rejects both malformed IDs and
asserts zero transmitted frames. Final validation results belong to the PR's
exact-head CI and local test record, not to synthetic hardware support claims.
The final local focused run passed 35/35 checks (CEM PIN plus the three
affected regression classes) on Windows x86 with .NET SDK 10.0.403.

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

Queued WinUI receive-error callbacks also capture `J2534Session.ConnectionEpoch`.
Every disconnect/replacement connection (including failed replacement) advances
that epoch before native teardown. If a queued error belongs to an old epoch,
its UI callback does not clear a newly connected session's diagnostic result.
These guards are synthetic and static checks, not interactive WinUI acceptance.

For read errors, J2534Session now stamps events with the generation captured
when each CAN receive bus is created. Both the WinUI callback and CEM lease
use this original bus generation, so a delayed old receive callback cannot
be confused with an error from a newly connected bus.

A CEM lease now acquires its CAN bus and originating read-error epoch in one
`J2534Session.CaptureConnectedBusSnapshot()` operation under the connection
lifecycle lock. Capture waits for an in-progress synthetic Connect (including
a failed Open) rather than combining an older bus with a newer epoch. This
snapshot does not start an ECU request or verify any physical CEM.

A subsequent receive-fault race hardening increment makes native RX invalidation
**epoch-scoped and lock-free**: a CEM guard stamps the originating CAN epoch,
not a global fault flag after a non-atomic bus comparison. If reconnect finishes
before a delayed old callback runs, that stamp cannot disable the new bus.
Fault stamps only advance monotonically, so a still-later old callback cannot
undo a newer bus's fault. An affected epoch remains blocked until explicit
Disconnect/Connect; capture refuses a faulted bus. Three extra offline tests
cover delayed old faults, out-of-order faults and reconnect recovery, with
zero synthetic transmitted CAN frames. No physical CEM operation is initiated.

The next offline increment distinguishes native adapter Read failures from
non-native receive diagnostics. A native read returning an error or throwing
now stamps its originating J2534 generation as faulted before any callback,
even when no CEM lease is subscribed. A late old-bus fault still cannot poison
a replacement connection. The session rejects fresh diagnostic snapshots until
an explicit disconnect/reconnect. Malformed CAN messages and failures of an
individual subscription filter remain nonfatal for the shared J2534 session
when no CEM lease is active. An active CEM PIN guard still conservatively
invalidates its own operation on any read-error notification. Added isolated
tests cover failure without a lease and nonfatal decode notification; existing
callback isolation and deferred teardown fixtures assert immediate fail-closed
status. No physical adapter was exercised.

A CI-discovered regression showed a short teardown window: after incrementing
the connection epoch, the old read-fault stamp no longer matched while the old
CAN bus had not yet been disposed. The session now marks the transport
invalid before incrementing the epoch or waiting for the native read worker.
The flag is cleared only after bus detach and adapter cleanup. A deterministic
offline test holds an in-flight fake Read while Disconnect retires the epoch
and asserts the old bus is never reported connected during teardown. No CEM
request is sent; no physical J2534 hardware is used.

The following offline lifecycle hardening closes a pre-dispatch window in
CemPinAttemptCoordinator. The first CEM lease check precedes the deadline and
cancellation-monitor setup; a reconnect, consent/context change or cancellation
can occur during that preparation. A second lease check and caller-cancellation
check now run immediately before invoking a PIN provider. The diagnostic gate
still remains held until any already-started provider terminates; each provider
must revalidate before every native request. Three synthetic regression tests
verify stale context, cancellation during final validation and a private
callback exception. Each denies provider invocation and PIN publication, with
no J2534 access or ECU transmissions.

The candidate itself now carries a revocable post-completion lease. The
coordinator binds an accepted in-memory candidate to the caller's
CancellationToken, its operation cancellation state and the original CEM
session/ECU/consent guard. HasPin, Origin and TryReveal recheck the lease;
failed checks dispose and cryptographically zero the owned six-byte buffer.
A late caller cancellation or ECU/context change therefore cannot leave an
old result available for subsequent reads after the single-flight coordinator
releases its gate. Saved vault records remain a distinct, explicitly
unverified offline workflow, not a live authorized CEM lease. This does not
retroactively undo a reveal or completed encrypted save that occurred before
revocation. Synthetic unit tests cover byte erasure/private callback errors;
virtual-adapter tests cover candidate revocation after completion without
physical ECU traffic.

The encrypted PIN vault now revalidates the revocable candidate at several
publication boundaries: before DPAPI encryption, after staging the temporary
encrypted file, before its final atomic rename, and after that rename. A
candidate revoked during staging therefore leaves no new .dpapi record or
temporary file. If revocation is detected immediately after the final rename,
Save attempts to remove only the new UUID-named record; existing records are
never overwritten or removed. Failures remain sanitized. This is a
best-effort check at filesystem boundaries, not a guarantee against a
revocation that occurs after Save has successfully returned or a filesystem
that refuses rollback deletion. No physical CEM or adapter tests are involved.

After a crash or process termination, Save's finally block cannot remove
its encrypted staging file. A subsequent authorized Save now performs a
bounded, best-effort cleanup of stages older than 24 hours, limited to 256
top-level .tmp entries and the exact <32-char GUID>.<32-char GUID>.tmp
naming scheme. Fresh stages, unrelated files, reparse points and all final
.dpapi records are preserved. File locks, permissions and enumeration errors
do not prevent the new Save. The cleanup never decrypts or logs abandoned
stage contents. Offline DPAPI tests cover a stale stage, a fresh stage,
unrelated .tmp, and a failed write after maintenance. A crash during Save
can still leave an encrypted temporary file until a later Save.

Vault failure-containment checks now cover file locks, obsolete malformed
staging names, and interrupted staging that throws UnauthorizedAccessException.
A locked old temporary file must be preserved without stopping a valid Save;
an unrelated/malformed temporary filename is never deleted; an access-denied
writer must not leave a partial stage or damage an existing encrypted record.
The best-effort stale-stage cleanup now also ignores SecurityException from
enumeration or per-file metadata access. ReadBounded explicitly zeroes its
partially read buffer before propagating any short-read or I/O exception.
These are synthetic Windows filesystem/DPAPI tests only.
