# Volvo XC90 P2/CEM compatibility evidence register

Status: **engineering research; no production-ready P2/CEM identity
fingerprints, authenticated PIN procedures, configuration memory maps or
ECU writes are validated in Dvinholm**.

This document is the initial source-of-truth for the **0.3.0 read-only**
compatibility gate. It intentionally does not publish guessed CAN IDs, seed/key
methods, PIN algorithms, DID semantics or write sequences.

## Evidence needed to mark a CEM variant supported

| Dimension | Required evidence | Current status |
| --- | --- | --- |
| Vehicle platform/model | Independently confirmed XC90 first-generation / P2; actual model year | Research target, not a validated per-year matrix |
| CEM identity | Hardware part/revision **and** software version, at least two independently documented read-only identifiers | **Not verified** |
| Bus transport | J2534 device, CAN standard/extended ID **or** K-Line capability; exact request and response addressing from reproducible evidence | **Not verified for P2/CEM** |
| Read-only data | Exact DIDs or documented read mechanism, lengths, byte meaning, failure responses, evidence source | **Not verified as CEM configuration** |
| Diagnostic snapshot | Explicit local export with sensitive-data consent; SHA-256 check; no claim that it is recoverable firmware | Generic backend/UI available in Development; virtual/physical CEM not accepted |
| Configuration editing | Firmware-revision-scoped parameter dictionary, dependencies, checksum and validation | **Not implemented** |
| PIN/service authorization | Verified legitimate model-specific entitlement/acquisition route, exact ECU prerequisites and consent | **Not implemented** |
| Configuration writes | Confirmed protocol and power, locking, rollback/verification procedure, J2534 and ECU bench test | **Not implemented** |

## Compatibility registration workflow

1. Identify a vehicle as an XC90 P2 candidate from documented metadata. Do
   **not** use model year alone to infer the CEM generation, PIN type or
   addresses.
2. Record the source (manufacturer documentation, a lawfully observed trace
   or independently permitted ECU study), ECU hardware and software revisions,
   model-year applicability, transport requirements and failure conditions.
3. Add a **multi-DID, exact-byte** `EcuIdentityEvidenceRule` with explicit
   provenance only after independent validation. VIN DID `F190` is not a
   cross-vehicle variant fingerprint. Unknown or conflicting responses stay
   unsupported.
4. Record separately sourced, read-only DID mappings. An observed DID value
   without a verified meaning is an **opaque diagnostic observation**, not
   a complete car configuration.
5. Run 6–8 deterministic tests for each variant addition, including
   wrong software version, CAN/address mismatch, year boundaries, no-response
   and unknown/ambiguous cases. Physical validation is a separate gate.
6. Keep PIN and configuration write availability **disabled**. A valid CEM
   read-only fingerprint never grants security access or authorizes ECU
   programming. These functions require independent release gates.

## Status in Development

- `EcuConfigurationReadback`: 0x22 read-only opaque DID capture;
  requires a selected user profile and live session for active reads.
- `EcuIdentityEvidenceMatcher`: independently sourced exact read-only
  ECU identification primitive.
- `VolvoP2CemSupportMatrix`: version/year-gated evaluation for **XC90/P2/CEM
  only**, with an intentionally empty `ProductionEntries` catalog.
- Snapshot inspection/comparison: historical data only; never determines
  live firmware identity or a safe configuration write.
- PIN retrieval and configuration writing: **not supported**.

Reference applications [SectorV](https://sectorv.ru/),
[P3Tool](https://p3tool.org/), and [VDASH](https://d5t5.com/)
remain functional research references, not validated protocol sources for
this application. P3Tool's P3 feature design must not be transplanted to P2.

The existing published **Stable v0.2.4** remains frozen. No 0.3.0 Stable
release should be approved solely on synthetic CI test results. Real J2534
hardware, ECU test benches and user Windows UI acceptance remain explicit
future gates for the relevant functionality.
