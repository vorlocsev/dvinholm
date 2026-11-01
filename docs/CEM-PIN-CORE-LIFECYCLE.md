# CEM PIN Core: bounded operation lifecycle (Development)

This is the first **code** increment toward an authorized CEM PIN workflow,
rather than another static configuration-research view. The new
`CemPinAttemptCoordinator` implements cancellation-aware, single-flight
asynchronous operation orchestration, sanitized results and a provider
interface. The interface carries **non-secret status receipts only**.

## What actually works

- The coordinator prevents a second operation while a first authorized
  provider operation is still active.
- Cancellation before invocation or during the provider operation returns a
  fixed non-secret status. The concurrency gate releases only after the
  provider terminates, including when it ignores cancellation.
- Provider errors never expose exception messages, identifiers or credentials;
  a later operation can be attempted after the previous one terminates.
- No PIN is accepted, stored, returned, displayed or claimed by this core.
- Eight synthetic asynchronous tests exercise those boundaries.

## Critical integration boundary

The **production constructor registers no provider** and uses the existing
`VolvoP2CemServiceCapabilityMatrix`, which always denies PIN acquisition
until independently reviewed physical CEM identity, a lawful method and
authorized bench support are implemented. Test-only injected providers and
authorization delegates are not connected to WinUI or a live provider.
The follow-up [session guard](CEM-PIN-SESSION-GUARD.md) binds an attempt to
the existing J2534 session and ECU context, requires consent, and adds
cooperative deadlines. It grants no physical identity or entitlement.

A successful synthetic metadata receipt is **not** PIN extraction. The
`FinishedWithoutPin` status intentionally cannot be mistaken for success.
The PIN coordinator has no vehicle commands, seed/key algorithms, live brute-force attempts, key
programming or configuration writes.

Live PIN attempts still require independent physical revision identity and
a documented, lawfully authorized provider route, plus secure result delivery.
The experimental P/N reader described below is separate and grants no PIN access.
Hardware tests, installed Windows UI tests and
PIN acquisition remain outstanding. Frozen Stable v0.2.4 and separate
Virtual Car release project are untouched.

## Bounded candidate enumeration

Following the user's explicit change allowing brute-force logic,
`CemPinCandidateRange` independently implements the numeric enumeration part:
six packed-decimal byte pairs, explicit ordinal/count bounds, a 64-bit cursor,
cancellation and caller-owned buffers. It covers the entire 100^6 space without
32-bit cursor wrap. Pair order is explicit; no CEM-specific permutation or
transport parameter is inferred. Eight synthetic checks cover numeric behavior
and continued production denial. This class generates candidates only; it
neither verifies nor obtains a PIN and is not registered in a live provider.

The reviewed [cracker source at 2d5de86](https://github.com/vtl/volvo-cem-cracker/blob/2d5de865d21094889a6c1c689797d5a636e909fc/volvo-cem-cracker.ino)
also contains hardware timing measurements and programming-mode operations.
The numeric enumerator contains none of those timing, address or security operations. The user's
other conditions remain: a verified physical CEM revision and transport route
are required before live attempts. Buffer owners must clear candidates after
use and must not log or persist candidate values or ordinals.

## Experimental normal-mode P/N read (Development)

The user selected the pinned cracker commands as the implementation
specification. This is recorded as **source approval**, not a physical test.
`CemPartNumberReader` sends one normal-mode part-number query through the
existing J2534Session/CanBus/CanRxDispatcher. It does not use UDS or ISO-TP
for this proprietary frame format and does not create a second transport.

The native adapter now accepts explicit 29-bit channel selection; the
default remains 11-bit. The Development connection selector chooses this
before connecting. Adapter implementations without the new capability fail
closed rather than silently using 11-bit. The reader requires the actual
connected channel to be 500 kbit/s and 29-bit with message-filter APIs.
It installs a temporary source-scoped mask filter, subscribes before sending,
validates two full-length, BCD-correct frames from the same full RX ID, and
removes the filter before returning. No retry, bus scan, K-line operation,
programming mode, reset, PIN candidate or configuration write is sent.

In Development the home CEM card opens the diagnostic workspace. Stop the
monitor, remove user hardware filters, and use **Read CEM P/N (experimental
HS-CAN)** after selecting/connecting the channel. A consent dialog precedes
the single request. New action/consent/result text follows the Windows RU/EN
UI culture. Results appear only in the explicit diagnostic result panel,
not in home activity or saved settings. Clear, cancel, input edits, read
errors and disconnect invalidate pending output. The operation shares the
existing diagnostic gate, cancellation and shutdown path. Hardware filtering
and monitor start are disabled while it runs. A failed filter cleanup marks
the session unusable until disconnect/reconnect, preventing later diagnostic
calls on an unknown receive configuration.

The observation is a part number only: it does not authenticate the module,
identify application/diagnostic software, validate MY2009 V8 fitment or enable
the PIN provider. Late traffic has no protocol nonce, so this limited parser
must not be used as physical identity attestation. Eight synthetic tests
exercise the real session/bus dispatcher with a virtual adapter, including
channel/filter prerequisites, source bytes, malformed replies, timeout/retry,
cancellation, invalidation, error redaction, cleanup failure and UI defaults.
Physical DiCE/J2534/CEM and interactive WinUI acceptance remain outstanding.
