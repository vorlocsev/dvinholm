# ECU configuration and authorized PIN workflows (after Stable 0.2.4)

Status: **Development architecture / offline groundwork only**. No tested vehicle
configuration writer, security access, PIN retrieval or immobilizer function exists.

## Goal and reference tools

Dvinholm will grow from read-only J2534 diagnostics into a carefully gated,
vehicle-specific ECU configuration tool. The requested priorities are
**authorized PIN retrieval** and **vehicle configuration changes**. SectorV,
P3Tool and VDASH are research references for user-facing capabilities, NOT
verified protocols, interchangeable APIs or permission to copy proprietary code.
Actual commands and ECU identification must come from independently validated,
documented or lawfully observed behavior, with support scoped by vehicle,
module, software version and physical adapter.

## Stage A: read-only ECU configuration baseline (first Development PR)

- Reuse `EcuProfile` addressing and the existing UDS `0x22`
  `ReadDataByIdentifier` path over ISO-TP/J2534.
- Read the profile's explicitly selected DIDs in stable ascending order,
  validate each DID echoed by the ECU, and retain the exact returned bytes as
  uppercase hexadecimal without guessing meanings.
- Mark provenance: profile ID, name, request/response CAN IDs, standard/extended
  addressing, verification flag, UTC capture timestamp.
- Abort the whole readback if a DID fails, is malformed or cancellation is
  requested. Reject empty and overly large DID sets. Never present a partial
  result as a complete configuration.
- Keep the snapshot in memory only; there is no file persistence, GUI control,
  ECU writer, PIN function, security-access seed/key algorithm or vehicle
  compatibility claim in this stage.

## Next planned stages (not yet implemented)

1. **Module identification and support matrix:** collect read-only ECU
   identifiers, software revisions and known-safe DID lists, distinguishing
   confirmed from hypothesized values. Add offline/virtual-adapter tests.
2. **Snapshot export and comparison:** deterministic, strict in-memory
   DID-by-DID diff is now the second offline Development building block.
   The diff refuses mismatched CAN addressing, profile or DID sets and never
   treats equal addressing as verified ECU identity. File export remains
   **unimplemented**; later user-initiated local export needs integrity
   metadata, strict size limits, privacy/redaction options, and safeguards
   against overwriting the only backup.
3. **Authorized PIN workflow research:** map precisely which module, model
   year and authorization proofs are necessary. Keep authentication secrets out
   of logs, exported snapshots and CI fixtures. No PIN guessing, brute-force,
   seed/key bypass or unsupported immobilizer changes.
4. **Explicit configuration-write gates:** per-ECU allowlist, pre-write readback,
   verified backup, byte-level change preview, clear risk/compatibility warnings,
   user confirmation, safe power/battery conditions, request timeout policies
   and post-write verification. Stop on mismatched ECU software or identity.
5. **Virtual ECU tests followed by supervised hardware acceptance:** use 6–8
   checks per feature milestone. Physical J2534 and on-car work stay deferred
   until separately authorized and equipment is available.

A synthetic DID readback is not a restorable backup. VIN or module metadata can
contain personal data; later export functionality must be opt-in and local-first.
Published Stable 0.2.4 and its update channel remain unaffected.
