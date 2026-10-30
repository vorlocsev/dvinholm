# XC90 P2 CEM variant pre-read check (Development)

A **read-only seven-gate summary** appears at the top of the existing
**Cross-check CEM revision source JSON (offline)** result. It accepts the
same manually exported VIDA identity card and 1..16 source-scoped revision
claims as the existing multi-source reviewer; it does **not** accept a
PIN, VIN, secret, CAN frame, adapter identity or write parameter.

## What it checks

1. Whether any selected research source covers the *claimed* model year.
2. Whether year-scoped hardware, software and diagnostic-software part-number
   **strings** agree, conflict, or use repeated source labels.
3. Independently authenticated exact physical CEM identity (not available).
4. Verified exact diagnostic transport, bus and request/response addresses
   for that revision (not available).
5. Documented revision-specific configuration read semantics (not available).
6. Authorized physical read-only J2534/DiCE bench acceptance (not performed).
7. Reviewed CEM-specific configuration read implementation (not implemented).

The first two checks can identify missing, contradictory or agreeing **claims**
only. The remaining five always remain blocked. Even two perfectly matching
JSON records are editable research notes, not independent physical ECU
identification, documented read services or a legal service entitlement.

The report provides a concise next research action without echoing hardware
part numbers, source citations, raw DID bytes, VIN or PIN. It does not
connect to the vehicle, choose a CAN speed, issue a request, authenticate
a session, read a configuration, create a restorable backup or write anything.

The original detailed multi-source report, the five-service read preflight,
and documented read-surface limitations remain visible immediately below
the new summary. Eight synthetic unit checks cover agreement, disagreement,
duplicate citations, year scope, unsafe claims, redaction/UI integration and
immutable blocked results. Windows installed GUI testing and authorized
physical CEM/DiCE/J2534 acceptance remain separate.

**Stable v0.2.4 remains unchanged.** No production CEM identity entry or
configuration DID allowlist is introduced.
