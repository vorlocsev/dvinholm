# XC90 P2/CEM: read-only DID capture identity status

Development 0.3.0+, after the existing generic **Read + save DID snapshot** action.

## Why

A generic J2534/ISO-TP/UDS `0x22` response proves that a selected profile's
DIDs were returned, **not** that the responding ECU was a known Volvo XC90 P2
CEM or that CEM software/hardware revisions are supported. A user's ECU profile
`IsVerified` checkbox and the mere presence of familiar DID identifiers are
not independent physical evidence.

## Implementation

`VolvoP2CemCaptureIdentityReview` inspects the **completed in-memory
read-only snapshot**, after existing consent, UDS transport, DID-echo
verification and context-cancellation gates. It does not read a file, talk
to the adapter, select CAN IDs, make additional requests or change a profile.

It checks the timestamp, 11/29-bit CAN range, distinct request/response IDs,
1–128 unique DIDs, proper hexadecimal values and Classic CAN ISO-TP bounds.
It then looks only at `VolvoP2CemSupportMatrix.ProductionEntries`, which
intentionally remains **empty**, and reports a fixed, payload-free status:

> CEM identity UNKNOWN: no verified production CEM signatures.

The activity is shown after a successfully saved diagnostic snapshot or a
user-cancelled Save As after reading. The original `0x22` consent and export
workflow remains unchanged. A completed capture is not a recoverable ECU
backup; cancelling an export does not create a file.

The classifier can be exercised with explicitly injected **synthetic**
research signatures and an explicit model year in offline tests to validate
its future behavior. Such matches remain research evidence only: no physical
CEM identity, PIN, service authentication, configuration reading or write is
authorized. The production UI never injects a year or test signature.

## Privacy

The public activity does **not** output ECU profile IDs/names, DID payload
bytes, PIN/VIN, evidence sources, local paths or CAN addresses. It includes
only a fixed outcome message, even for malformed inputs or ambiguous matches.
No network request, clipboard access or persistence is added.

## Eight deterministic checks

1. Empty production identity catalog leaves status UNKNOWN.
2. A profile marked verified never becomes authenticated CEM identity.
3. Invalid CAN addressing or non-UTC timestamps are rejected.
4. Duplicate, malformed and oversized DID values fail closed.
5. Explicit model-year scope is required even for synthetic evidence.
6. CAN/revision mismatch cannot be mistaken for a signature match.
7. Synthetic read-only match is never physical authorization and leaks no values.
8. Ambiguous matches fail closed; the existing consent/export UI displays the
   status without adding an ECU request.

Exact-head Windows x86 CI and post-merge CI are required. Windows installed
UX and real DiCE/J2534/CEM acceptance remain unperformed. Stable v0.2.4 is
unchanged; Virtual Car is not touched.
