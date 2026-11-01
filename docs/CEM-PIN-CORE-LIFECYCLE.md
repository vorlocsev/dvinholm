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
There are no vehicle commands, seed/key algorithms, brute force, key
programming or configuration writes.

The next increment must validate independent physical revision identity and
a documented, lawfully authorized provider route before adding real-world
transport or secret-handling. Hardware tests, installed Windows UI tests and
PIN acquisition remain outstanding. Frozen Stable v0.2.4 and separate
Virtual Car release project are untouched.
