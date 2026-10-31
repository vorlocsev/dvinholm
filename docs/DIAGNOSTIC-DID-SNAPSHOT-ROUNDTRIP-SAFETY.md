# Diagnostic DID snapshot: safe export/inspection round-trip (Development)

This change hardens the **existing generic read-only UDS 0x22 DID snapshot**.
It is NOT a CEM configuration-read protocol, a restorable ECU backup, a
vehicle-configuration decoder, or a PIN/authentication implementation.

## New export preconditions

- A snapshot MUST have a non-default, offset-zero UTC capture timestamp,
  matching the existing strict historical JSON inspector. A local-offset or
  missing time is rejected before any file operation.
- An exported DID value MUST fit the current Classic CAN ISO-TP/UdsClient
  budget: at most 4092 value bytes (8184 uppercase hexadecimal characters).
  Three transport-payload bytes are reserved for the positive-response SID
  and the echoed DID. This is a limit on the existing capture format, not a
  vehicle-specific DID specification.
- Existing strict requirements remain: explicit sensitive-export approval,
  valid 11/29-bit request/response IDs, unique DIDs, nonempty valid hex, at
  most 128 DIDs, bounded profile metadata, canonical sorting and SHA-256
  corruption detection.
- Save remains an explicit, atomic user-selected file operation outside
  the application directory. A validation error cannot truncate a
  previously saved file.

## Backwards compatibility

The **inspector still accepts** older, syntactically valid version-1 exports
with DID value lengths within its historical 16,384-hex-character ceiling.
This read-only compatibility does not enable creation of those oversized
values from the current Classic CAN capture path. SHA-256 only checks
accidental alteration; it **does not authenticate** vehicle origin, CEM
identity, source independence or ECU ownership.

## Verification scope

Eight offline synthetic checks cover invalid/missing UTC timestamps, valid
UTC round-trip, exact transport boundary, one-byte overflow rejection,
legacy historical inspection, file save+reopen, and unchanged existing
file on invalid save. No physical DiCE/J2534 capture or installed Windows
UI acceptance is claimed.

The saved file may contain VIN or other sensitive ECU data. It is
**not restorable** and must be explicitly reviewed before sharing. This
feature does not select CEM CAN IDs, claim firmware compatibility, enable
CEM configuration reads, generate PIN requests or send write commands.
Stable v0.2.4 and Virtual Car remain unchanged.
