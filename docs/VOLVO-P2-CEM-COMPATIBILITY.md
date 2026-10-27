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

## Model-year HS-CAN rate: source-scoped safety research

A Volvo XC90 workshop fault-tracing reference describes different **high-speed
CAN (HS-CAN)** rates for the P2 vehicles in scope: up to and including model
year 2004, **250 kbit/s**; from model year 2005 onward, **500 kbit/s**.
It warns that using an incorrect vehicle profile before bus communication
can impair communication and trigger trouble codes. A separate 2004 XC90
network description identifies distinct low-speed CAN at 125 kbit/s.
These are **network-specific workshop observations**, not proof of a
particular CEM diagnostic address, protocol or supported adapter.

Sources (reviewed 2026-10-27):

- [XC90 workshop communication fault tracing: model-year HS-CAN difference](https://workshop-manuals.com/volvo/xc90_2.5t_awd/l5-2.5l_turbo_vin_59_b5254t2/relays_and_modules/relays_and_modules_accessories_and_optional_equipment/communications_control_module/component_information/testing_and_inspection/reading_and_clearing_diagnostic_trouble_codes/page_193/)
- [2004 XC90 CAN network: 250 kbit/s HS-CAN, 125 kbit/s LS-CAN](https://workshop-manuals.com/volvo/xc90/l5-2.5l_turbo_vin_59_b5254t2/powertrain_management/computers_and_control_systems/information_bus/component_information/description_and_operation/the_controller_area_network_%28can%29/page_4924/)
- [Volvo Special Tools: DiCE/J2534 support and J2534-1 limitations](https://www.volvospecialtools.com/index.cfm?event=info.dice)

Development adds a **pure, advisory-only** `VolvoP2CemHsCanSpeedAdvisory`
that only evaluates an independently confirmed XC90 P2 *model year*
(2003–2014), an explicitly identified HS-CAN network and a user-selected
baud rate. It flags a 250/500 kbit/s mismatch but never changes device
settings or permits transmission automatically. Unknown years, unknown
networks, LS-CAN and K-line must not reuse these HS-CAN values.

The app's generic `CAN speed` selector includes a warning tooltip; it
does **not** yet have a verified year/network identification flow, and no
manual Windows test of the tooltip has been performed. User-supplied year
alone never certifies a real CEM, a diagnostic transport, a VIN, or PIN and
write availability. P2/CEM compatibility remains **unknown** until a
model-/firmware-specific, independently sourced hardware procedure is
available. The production signature catalog is still empty.

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

## Evidence catalog preflight (Development)

Before any evidence entry is considered for **read-only identity assessment**,
`VolvoP2CemEvidenceCatalogValidator` checks the entire bounded catalog:

- At most 128 entries, each with a coherent model-year interval, P2/CEM
  scope, a clearly sourced and author-marked-verified identity.
- Valid distinct 11-bit or 29-bit CAN request/response IDs, and at least two
  distinct, bounded hexadecimal hardware/software identifiers. VIN DID
  `F190` is never a cross-vehicle identity fingerprint.
- A bounded duplicate-free read-only DID list with independent source notes
  when mappings are declared; VIN is excluded from module-variant mappings.
- No overlap of the **same variant ID** across year intervals. Different
  firmware revisions can share CAN IDs but must be uniquely distinguished by
  actual verified identifiers.

If the entire catalog contains a malformed or conflicting entry, its
read-only assessment fails closed rather than selecting a seemingly valid
subset. This is a **structural and provenance-notation gate only**:
`IsVerified` and evidence-source strings are statements by the evidence
author, not cryptographic proof or independent verification. The production
catalog remains **empty**. CEM/PIN/write operations are not enabled.

## XC90 P2 configuration option candidates (Development 0.3.0)

`VolvoP2ConfigOptionCatalog` records **eight vendor-advertised display
categories only**: auxiliary heater, headlamp washers, region, parking
assistance, automatic locking, service interval, wheel size and vehicle
speed policy. Identifiers are newly assigned Dvinholm research labels;
they are **not Volvo parameter IDs**, another application's IDs or a
verified factory configuration dictionary.

Evidence: the user-provided readable SectorV product screenshots from
2026-10-27 and the vendor homepage, https://sectorv.ru/. The claim
"from model year 2006" is **vendor advertising for P2**, not
independently validated coverage for every XC90, CEM firmware, option
or compatible adapter. Earlier first-generation XC90 years
(2003–2005) are **outside that advertisement**, not proven incapable.

The model deliberately has **no CAN IDs, DIDs, raw memory offsets,
parameter bytes, PIN acquisition routine, security algorithm or
read/write commands**. Each candidate records unverified module
ownership and firmware applicability, an explicit source, year scope
and an initial safety/compliance review flag. Even after a model
year is independently confirmed, the assessment returns
`VendorAdvertisedOnly`, never permission to issue requests or writes.
Unsupported/unknown years and unknown option IDs fail closed.

Before any option graduates from this research catalog:
1. Independently verify exact CEM hardware/software revision,
   applicable vehicle/model year, responsible ECU and protocol.
2. Obtain legitimate documented read mappings, raw lengths, allowed
   values, safety/legal constraints and dependency evidence.
3. Independently validate backup, authorization, power/interruption
   handling and readback/recovery on compatible test hardware.
4. Add separately reviewed commands only behind explicit hardware and
   user-consent gates. No current catalog flag authorizes such commands.

Eight synthetic unit tests cover metadata uniqueness, provenance,
disabled operations, immutability, missing model year, out-of-range
years, advertised-only applicability and strict ID/safety labels.
This is **backend research scaffolding**, not an exposed settings
editor and not an on-car compatibility claim.

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
