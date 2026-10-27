# Dvinholm release milestones after Stable v0.2.4

Planning baseline: 2026-10-27. **Milestone assignments are targets, not
published release dates or guarantees of vehicle compatibility.** The last
published Stable at plan creation is v0.2.4 (`a9e6188`). Development is
independent of the frozen v0.2.4 tag and assets.

| Milestone | Primary deliverable | Release gate |
| --- | --- | --- |
| **0.2.5** (only if required) | Regression fixes for the supported 0.2.4 diagnostics/updater | A confirmed defect, user acceptance, complete release audit and explicit publication approval |
| **0.3.0** | Read-only Volvo P2/CEM identification, version-aware support matrix, documented DID readback, opt-in local snapshot export and diff | Explicit verified module/DID provenance; 6–8 offline checks per change; GUI/virtual-J2534 acceptance; unknown variants stay unsupported |
| **0.4.0** | PIN/service-authorization UX and model-specific supported authorization routes | Clearly identify an authorized and legal method, supported ECU/software and user consent; no brute force, bypass or speculative PIN extraction |
| **0.5.0** | Configuration editor with validated option catalog, original bytes, diff and preflight | Read-only preview by default, dependable backups, no unsupported values or automatic ECU write |
| **0.6.0** | Guarded configuration write and read-after-write verification for a narrow supported module set | Documented ECU-specific write protocol, power/session/recovery gates, explicit approval and **real hardware/ECU acceptance** before Stable writes |
| **0.7.0** | Wider P2 variant/ECU coverage, interoperability and initial P3 research | Tested model/software/adapter matrix, no assumption that P3 commands apply to P2 |
| **1.0.0** | Fully documented and tested diagnostics + authorized configuration workflow for a **declared supported subset** | End-to-end acceptance, audited backups/rollback constraints, manuals and independently confirmed publication approval |

## Execution order and engineering rules

1. **XC90 first generation (P2) first**, then other verified P2 configurations;
   P3 is a later independent track. Existing J2534/CanRxDispatcher/ISO-TP/UDS
   read-only functionality must continue working.
2. Obtain manufacturer/version-specific evidence before marking any ECU, DID
   or operation supported. Read-only observations alone do not grant write or
   service authorization, and no real CEM signatures ship without evidence.
3. Keep 6–8 deterministic offline/virtual adapter checks per PR; require all
   seven existing PR workflows to succeed on the exact final commit and run
   independent checks on the resulting Development `main` commit.
4. Do not count a green CI as physical hardware acceptance. A compatible ECU
   and real J2534 are explicitly required prior to production write support;
   these tests remain deferred until equipment and authorization are ready.
5. For every Stable publication, report readiness and ask the user to approve
   the release. Demo/preview/test actions must not leak into Stable packages.
6. Published Stable v0.2.4 remains unchanged; new versions are **not**
   published by updating this roadmap.

## Foundation already implemented in Development

- PR #525: in-memory read-only configuration DID capture over the existing
  UDS `0x22` API, failure/cancellation handling and eight offline tests.
- Follow-on identity evidence matcher: considers only explicitly verified,
  sourced, exact two-or-more-DID fingerprints with matching CAN IDs and
  addressing. It never infers P2 support from a VIN, manufacturer name or a
  heuristic. The initial production signature catalog is deliberately empty.
- Next: documented ECU software/hardware identifiers and compatibility data,
  then user-initiated local snapshot export/comparison with privacy safeguards.

Functional references are [SectorV](https://sectorv.ru/),
[P3Tool](https://p3tool.org/) and [VDASH](https://d5t5.com/). They guide
capability discovery, not undocumented protocol assumptions or code copying.
See also [ECU configuration roadmap](ECU-CONFIGURATION-ROADMAP.md) and
[backlog issue #523](https://github.com/vorlocsev/dvinholm/issues/523).
