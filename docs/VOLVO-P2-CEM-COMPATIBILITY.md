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

## User-selected manual CEM identity research JSON inspection (Development)

The **P2 configuration candidates (offline)** viewer now has an
**Open CEM research JSON...** action. It uses a user-directed Windows
Open picker and a bounded strict UTF-8 parser to inspect a JSON
record written by the manual VIDA worksheet's Save As action.
The preview remains **read-only/unverified**; no import into ECU
Profiles, no CEM fingerprint enrollment, no vehicle/session connection,
PIN acquisition, configuration change or unattended persistence.
Unknown/duplicate JSON properties, unsafe capability flags,
invalid year or part-number fields, empty/oversized files,
invalid UTF-8 and BOM-prefixed data are rejected.
No private paths or source values are logged in status messages.

Eight synthetic tests exercise valid roundtrip, read-only display,
empty and oversized content, unknown/duplicate JSON fields,
privilege-escalation flags, invalid UTF-8, BOM and cancellation
before disk access. Source provenance remains self-reported;
no hardware identity or transport specification is inferred.

## VIDA logical CEM node versus physical CAN address (source audit 2026-10-28)

Source tier A, **Volvo's own 2021 Workshop System Requirements 320EN20**
([official Volvo PDF](https://workshopsupportguide.volvocars.biz/media/35358/VIDA-WS-Requirements-320EN20-en-GB.pdf),
section 6): VIDA supports DiCE and J2534 communication tools; Volvo
**recommends DiCE** for D2/GGD vehicles. For VIDA, **J2534-1 is
software download only and explicitly NOT diagnostic access**;
J2534-2 also covers software download over HS/MS CAN. These statements
are about **Volvo VIDA** support, not a claim that Dvinholm or every
third-party scan tool must share the same limitations. An unknown
third-party J2534 device is not an accepted first-generation XC90
CEM diagnostic interface solely because the DLL is detected.

Source tier B, **historical Volvo Technical Journal US14902.6.0
(2010)** reproduced on third-party workshop-manual sites: a CEM recovery
procedure refers to a **VIDA logical ECU node** rather than a bus
arbitration ID. A [reproduced MY2009 XC90 bulletin](https://lemon-manuals.la/Volvo/2009/XC90%20FWD%20L6-3.2L%20VIN%2098%20B6324S/Repair%20and%20Diagnosis/Powertrain%20Management/Computers%20and%20Control%20Systems/Technical%20Service%20Bulletins/Customer%20Interest/Computers%2FControls%20-%20CEM%20Download%20Interrupt%20Recovery/)
reports node label **40 for early P2 (through 2004)** and **50
for P2 from model year 2005**. A [different reproduction of the
same journal](https://workshop-manuals.com/volvo/xc90_fwd/l6-3.2l_vin_98_b6324s/powertrain_management/computers_and_control_systems/relays_and_modules_computers_and_control_systems/engine_control_module/component_information/technical_service_bulletins/computers/controls_cem_download_interrupt_recovery/page_9324/)
only states node **50 for P2** without the early-year distinction.
Treat this as **historical, context-dependent and not independently
resolved per CEM revision**, not a complete mapping. The cited procedure
is about firmware download failure recovery, **not** a safe way to
discover or program a vehicle; Dvinholm neither reproduces nor invokes
its recovery operations.

`VolvoP2CemVidaNodeAdvisory` now displays those **reported VIDA
logical node labels** in the existing *offline P2 configuration
candidate viewer*. Inputs to its pure assessment require an independently
confirmed XC90 P2 model year; a manually typed worksheet year does not
qualify. All assessment paths have **null** CAN request/response IDs
and read DID, with **false** live connection, identity read,
configuration read/write and PIN capabilities. The underlying
vehicle network, pass-thru configuration, ISO-TP address format,
module firmware and active VIDA/DiCE procedure remain **unknown**.
The logical strings `40` and `50` are **not** hexadecimal `0x40`
and `0x50` requests to pass into the J2534 driver.

Eight offline tests cover unverified/out-of-scope years, early/later
source reports, explicit VIDA J2534-1 diagnostic limitations,
DiCE recommendations, J2534-2/other devices not granting support,
and visible research-only UI labels. No actual CAN traffic,
configuration access or physical interface test occurs in these tests.

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

## Offline XC90 P2 catalog viewer (Development 0.3.0)

The ECU Profiles panel now has a separate **P2 configuration candidates
(offline)** action. It opens a read-only, scrollable research panel with
the eight newly named candidate groups and per-candidate provenance/year
limitations. It is **not** a decoded CEM configuration or a real CarConfig
editor. No vehicle identity, PIN, CAN ID, firmware ID, decoded value,
desired value, file path, authorization token or snapshot is consumed.

The panel states: no connected vehicle has been identified; actual
current values are unavailable; desired values and all ECU writes are
disabled; service authentication/PIN are unavailable. The candidate
catalog is still solely based on a **vendor advertisement**, not
independent XC90 P2 coverage verification. The UI does not make any
connection, diagnostic request, PIN attempt, edit, backup or save.

A shared dialog guard prevents overlaps with the existing historical
snapshot Inspect/Compare panels. Seven existing compact profile/snapshot
actions remain untouched in their two-row layout; the new research
button is added after the capped scrollable profile metadata view.
The development PR includes **8 offline tests** for display contents,
unknown values, evidence/year disclosure, disabled operations, safety
warnings, and actual XAML button/layout contracts. Separate installed
Windows visual/virtual-J2534 verification is still required.

## Offline P2 CEM PIN/service readiness review (Development 0.3.0)

The ECU Profiles panel now includes **P2 CEM PIN readiness (offline
review)** below the static configuration candidate viewer. The new
`VolvoP2PinAuthorizationReview` is an explicit four-stage *evidence
checklist*, **not** an ECU search task or an implementation of PIN
retrieval. The stages cover: (1) independently confirmed CEM identity,
(2) physical adapter/transport evidence, (3) a legitimate verified
P2-specific service/PIN procedure, and (4) entitlement, authorization
scope and explicit consent. None are claimed to have passed. It
distinguishes configuration access rights from immobilizer secrets.

The dialog shows the **review step number, not PIN-search progress**:
there is no estimated completion time, diagnostic probe, PIN result,
credential entry, brute-force algorithm, file access, CAN request or
vehicle programming. After all four stages, the workflow ends as
`ReviewFinishedUnsupported`; it does not enable any real operation.
**Cancel review** immediately terminates the local checklist as
`Cancelled`, never implying that a vehicle operation was running.
All service access/read/write permissions stay hard-disabled,
including after review completion.

The WinUI action shares a non-reentrant ContentDialog gate with
the research catalog and historical DID inspection/comparison.
The compact seven-button ECU Profiles grid and existing read-only
diagnostics remain unchanged. Eight offline tests cover steps, their
blocked capabilities, terminal completion, cancellation, scope
separation and the actual XAML layout/action contract. Physical J2534,
CEM hardware and Windows user acceptance remain unperformed.

This is user-interface architecture only. Implementing verified
authorization later needs independently documented ECU/firmware
compatibility, legitimate permission/credential handling, hardware
testing, clear consent and separate security/safety review. **Do not
confuse an offline walkthrough with a PIN algorithm or a PIN result.**

## XC90 P2 CEM service-operation evidence gap matrix (Development 0.3.0)

`VolvoP2CemServiceCapabilityMatrix` adds a **read-only, offline research
assessment**, independent from both the existing generic UDS reader and
the educational PIN readiness dialog. It lists six *not implemented*
P2/CEM-specific operation categories: identify CEM, read vehicle
configuration, review authorized PIN acquisition, service authentication,
preview a configuration change and write configuration. These are
future feature categories, **not callable ECU commands**.

Each category is assessed against explicit missing requirements:

- **Confirmed model year and matching CEM identity.** A P2/CEM support
  assessment made from synthetic data is not production verification.
- **Configuration read/encoding evidence.** A read-only DID snapshot is
  opaque and is not a model-specific configuration dictionary.
- **Legitimate model-specific service route and scoped entitlement.**
  Configuration-service authorization is kept separate from
  immobilizer/key material; unknown or immobilizer scope never qualifies.
- **Physical J2534/CEM acceptance.** Required before live vehicle
  operations. Offline preview alone does not send data.
- **Tested backup/recovery plan.** Required additionally before writing.
- **Operational service implementation.** Deliberately and
  unconditionally missing for every P2-specific operation.

The result is an immutable list of blockers and a generic explanation.
All `May...` permissions remain **false**, even when a synthetic input
claims every other requirement has been documented or passed. Reference
strings are only bounded, opaque research pointers; they are *not*
independent verification, PINs, credentials, vehicle data or executable
security material. They are not echoed in UI/report messages.

The production CEM signature catalog remains empty. No physical
J2534 tests, approved service protocol, PIN extraction algorithm,
credential storage, actual option mapping, editor integration or
ECU write has been added. **Eight offline tests** cover six denied
services, year and identity failure, read mappings, credential scope,
physical/backup gates and the all-claims-true terminal denial.

The new model is backend scaffolding only. It does **not** change any
existing WinUI permission, CAN speed, diagnostic session, read-only UDS
command or the published Stable v0.2.4 release. Do not mark hardware,
authorization or functional PIN capability as supported based on this
research status.

## Revision-scoped configuration parameter research (Development 0.3.0)

`VolvoP2CemParameterEvidenceCatalog` provides an **offline, fail-closed
metadata preflight** for future source-backed candidate parameter records.
Its production collection is **empty**. Proposed and human-reviewed
claims can be scoped to a CEM variant, exact claimed hardware/software
revision labels and an independently confirmed advertised model-year
window. This only validates string/interval consistency and
research provenance notation; neither an author-supplied review flag
nor synthetic read-only CEM identity independently authenticates ECU
firmware or validates an actual option mapping.

The bounded full-catalog check rejects unknown candidate IDs,
missing or invalid revision labels/sources, unreviewed value-meaning
evidence for human-reviewed claims, conflicting overlapping year
intervals, invalid review types and catalogs over 64 entries. No
real Volvo values, memory locations, CAN/DID IDs, parameter bytes,
PINs, security routines or write encodings are present. Assessment
never copies untrusted source notes into messages.

The six `May...`/`HasVerified...` permission flags remain **false**
in every state, including a matching human-reviewed claim. Eight
synthetic offline tests cover zero production mappings, proposed
and reviewed notation, model-year boundaries, identity/revision
mismatches, full-catalog rejection, overlaps and immutable redacted
diagnostics. See [the research gate specification](P2-CEM-PARAMETER-REVISION-RESEARCH.md).
Real hardware, source authentication and readback acceptance are
separate prerequisites; Stable v0.2.4 is not changed.

## User-directed, privacy-reduced DID metadata intake (Development 0.3.0)

The existing **Inspect snapshot** WinUI dialog can now show a separate
historical **P2/CEM research metadata** section and offers **Copy
sanitized research metadata**. The optional text records the historical
capture UTC, observed CAN frame type and request/response IDs, DID
numbers and **DID value byte lengths only**. Raw hexadecimal bytes,
VIN, sensitive profile name/ID, selected file path and credentials are
neither displayed in this research report nor copied to Clipboard.
However CAN/DID metadata can still fingerprint an individual vehicle;
explicit copying is a user choice, and the OS clipboard may outlive
the app. Dismissal creates no clipboard entry.

This uses the existing checked schema-v1 historical read-only JSON
inspector (strict UTF-8, file size cap, ordering, flags and SHA-256).
Neither file integrity nor user-provided CAN/DID metadata authenticates
an actual XC90 CEM, model year, firmware, supported option, PIN or
service permission. There are **no new vehicle requests, changes,
writes or backups**, and the production CEM signature catalog remains
empty.

Eight synthetic offline tests verify metadata formatting, raw-value
and profile redaction, evidence limitations, 11/29-bit support, strict
schema/UTC/CAN/DID limits. Installed Windows Clipboard UX and virtual
adapter checks remain separate gates. See
[the offline research intake guide](P2-CEM-SNAPSHOT-RESEARCH-INTAKE.md).

## Repeated historical DID observations (Development 0.3.0)

The existing **Compare snapshots** offline panel now provides an
additional **two-capture research summary**, built from separately
inspected Dvinholm diagnostic JSON files. It reports DID IDs in
ascending order, byte lengths in each capture, whether each value
changed and counts of unchanged/changed/length-changed observations.
The raw DID values, profile metadata and file paths are never shown.
Chronological labels require distinct UTC captures; equal timestamps
leave the existing generic DID comparison available.

Repeated matching DID values do **not** independently verify genuine
XC90 P2/CEM identity, firmware revision or option meaning. Even the
same profile/CAN addressing cannot prove both files came from the
same physical ECU. User-supplied snapshots can be rehashed and
are not authenticated evidence. No new ECU requests, PIN operations,
configuration changes, backups or writes are introduced.

Eight deterministic synthetic offline tests cover unchanged/changed
DIDs, lengths, sorting, UTC ordering, incompatible profiles/CAN,
invalid/duplicate DID data, privacy and immutable denial flags. See
[repeated DID research guidance](P2-CEM-REPEATED-DID-EVIDENCE.md).
Actual CEM hardware evidence and Windows acceptance remain pending.

## Manufacturer-documented XC90 CEM service-menu categories

Volvo Cars **Technical Journal 20278.6.11** (2016-09-06),
[NHTSA-hosted PDF page 16, XC90 page 3](https://static.nhtsa.gov/odi/tsbs/2016/SB-10087303-5448.pdf),
separately documents six CEM service-menu settings: home-safe
lighting, daytime running lamps, tailgate wiper, fuel filler locking
delay, Bi-Xenon low-beam warning and triple-flash turn indicator.
This is **actual manufacturer source evidence for the named service
menus**, unlike the vendor-advertised eight display categories.
They are shown in a separate section of Dvinholm's existing **offline**
P2 research viewer.

**Not independently established:** applicability to each model
year/individual XC90 CEM hardware and software revision, per-feature
parameter IDs/offsets, current values, binary encoding, CAN/diagnostic
transport, security authorization, PIN retrieval or any operation in
Dvinholm. The Dvinholm research identifiers are invented labels,
not manufacturer protocol IDs. Six immutable source-backed names do
**not** populate the production CEM fingerprint or parameter catalog,
both of which remain empty. Eight offline tests cover provenance,
feature scope, separation from vendor advertising, immutability,
unsafe/unknown IDs, disabled operations and the UI research text.

Volvo's [official DiCE/J2534 explanation](https://www.volvospecialtools.com/index.cfm?event=info.dice)
also distinguishes VIDA diagnostic capabilities from pass-thru
programming: J2534-1 is expressly **not for VIDA diagnostics**,
J2534-2 handles additional network programming, and DiCE is
recommended for legacy D2/GGD applications. Driver detection is not
CEM diagnostic support. See [manufacturer CEM menu evidence and
adapter limits](XC90-OEM-CEM-MENU-EVIDENCE.md).

## Offline VIDA CEM part-number worksheet (Development 0.3.0)

The existing **P2 configuration candidates (offline)** dialog
offers **Enter CEM identity (offline)**. It collects three
**manually transcribed** part-number fields from an authorized
VIDA identity display, and a claimed first-generation XC90
model year: hardware P/N, software P/N and diagnostic software
P/N. Hardware serial numbers, VIN, credentials, PINs and raw
responses are not requested. The source indicator is a self-report,
not a verified vehicle attestation. A strict bounded preflight
rejects invalid years/unsafe part numbers and VIN-shaped values.

The user may review a local unverified worksheet and explicitly
copy it. No automatic clipboard/file persistence or ECU traffic
is introduced; copied identifiers may still be identifying
metadata. All identification/transport/read/configuration/PIN
permission flags remain **false**. The actual ECU request
format, addresses, diagnostic services and applicability for
a given XC90 CEM revision are **unknown**. Evidence basis:
[2007 XC90 CEM diagnostic functions, workshop-manual republication](https://lemon-manuals.org.ua/Volvo/2007/XC90%20FWD%20L6-3.2L%20VIN%2098%20B6324S/Repair%20and%20Diagnosis/Body%20and%20Frame/Body%20Control%20Systems/Description%20and%20Operation/Central%20Electronic%20Module%20%28CEM%29/Diagnostic%20Functions/)
and the [official Volvo DiCE/J2534 interface guidance](https://www.volvospecialtools.com/index.cfm?event=info.dice).
See [offline VIDA identity worksheet guide](P2-CEM-VIDA-IDENTITY-WORKSHEET.md).

Eight deterministic offline tests cover completeness, model
year, safe text format, VIN-like rejection, normalization,
source status and denial flags. Actual hardware/software
part-number values must still be independently checked on an
authorized physical CEM; the production mapping catalog is empty.

## Opt-in offline CEM identity JSON research record

The [manual VIDA worksheet](P2-CEM-VIDA-IDENTITY-WORKSHEET.md)
now also supports explicitly saving a bounded `.json` research
record using the path-only file picker and atomic writer.
The record preserves the manually claimed XC90 year and exactly
three normalized CEM part numbers, the user's claimed VIDA source,
and explicit **unverified**, **non-restorable** and **no ECU access**
flags. It never includes VIN, hardware serial, PIN, DID values,
passwords or private notes. The backend-only strict file inspector
rejects unexpected/duplicate keys, invalid values and unsafe
permissions; it does not infer actual ECU identity from a user
file. Files remain sensitive device metadata despite omitted VIN.

Eight new synthetic offline tests verify roundtrip, schema,
privacy, bounds, no enabled capabilities and safe explicit
file writing. No GUI-based import, active J2534 request,
CEM write or PIN service is added; real CEM evidence remains
unverified and physical testing deferred.

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
