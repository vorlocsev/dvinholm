# XC90 P2/CEM revision source cross-check (Development 0.3.0)

Development adds a read-only, two-file comparison for the exact CEM
hardware, software and diagnostic software part-number strings that the
manual VIDA identity worksheet already records. This answers a narrower
question: **do two user-entered research claims agree about a candidate
revision for the same claimed XC90 model-year scope?**

It does **not** authenticate either source, prove the files belong to one
physical CEM, identify a working CAN/K-Line transport, verify firmware
applicability, decode CEM configuration, acquire a PIN, or enable programming.

## Windows workflow

Open **P2 configuration candidates (offline)** and choose the new
**Cross-check CEM revision source JSON (offline)** action. Select two
separate files in sequence:

1. An existing manually saved CEM VIDA research card created from
   **Enter CEM identity (offline)** in the application.
2. One to sixteen separately selected, user-authored, source-scoped CEM
   revision claim JSON files, such as the entirely synthetic example below.
   The Windows file picker supports multiple selection. A single source is
   still supported for compatibility with the original workflow.

The app revalidates both local files. Cancel either picker to leave
everything unchanged. It shows only the claimed year/year range and
a redacted aggregate: how many source claims cover the year, how many
match the three part-number strings, how many contradict them and how many
fall outside the year range. Duplicate source-reference labels are rejected
as corroboration even when the underlying strings match. It does not print
individual part numbers, private paths, URLs or raw DID data.
Nothing is saved, copied automatically or sent to any device.

The built-in catalog of authenticated CEM revisions is still empty.
Source records are independent user-supplied research and cannot enroll
a "verified" ECU or change the selected profile.

## Synthetic revision source example

The following values are **not Volvo part numbers** and **not usable
ECU identities**. They demonstrate the document shape only.


```json
{
  "format": "dvinholm-p2-cem-revision-source-claim",
  "schemaVersion": 1,
  "isUnverifiedResearchClaim": true,
  "isRestorableBackup": false,
  "mayAccessEcu": false,
  "modelYearFrom": 2007,
  "modelYearThrough": 2007,
  "claimedHardwarePartNumber": "HW-001",
  "claimedSoftwarePartNumber": "SW-001",
  "claimedDiagnosticSoftwarePartNumber": "DIAG-001",
  "sourceReference": "Synthetic source for offline research only"
}
```

The file must be UTF-8 without BOM, no larger than 4 KiB. A strict
parser rejects unexpected or duplicate JSON properties, wrong types,
overlapping/contradictory model-year range, noncanonical or VIN-shaped
part-number strings, unsafe capability flags, and invalid source
references. The source citation is a claim, not an authentication
signature, and is deliberately not displayed in the report.

A SAME result means the **three text strings matched** for a covered
claimed year. It does not prove their source, vehicle, physical hardware
or firmware compatibility. The real CEM identity, diagnostic addressing,
PIN authorization, configuration map, power/recovery requirements and
DiCE/J2534 vehicle acceptance remain unverified.

Eight synthetic tests exercise matching, scope, individual part-number
differences, strict JSON, unsafe flags, forged manual identity claims,
privacy redaction and bounded file reads. Installed Windows UI and
physical hardware tests are separate future acceptance gates. Stable
v0.2.4 remains unchanged and no release is authorized by this work.

## Bounded multi-source revision triage (Development 0.3.0)

The new `VolvoP2CemRevisionSourceSetReview` reuses the existing strict
per-file validator and same-year triple-part-number comparison. It is
**not** a source-of-truth registry: each source is user-editable. It allows
1–16 files and refuses oversized source sets, unsafe permission flags,
malformed records or forged manual identity. The classification is:

- No sources cover the claimed model-year scope.
- One or multiple in-scope source strings agree with the manually entered
  part numbers, **unverified only**.
- At least one in-scope source contradicts the manual claim, **no first-match
  selection**.
- Duplicate case-insensitive source-reference labels, **not independent
  corroboration**.

Out-of-scope records are counted separately and do not themselves imply
a hardware mismatch. Even distinct labels may derive from one source,
so this cannot prove publisher independence or authenticate an ECU. The
result never includes part numbers, source texts or paths; no files are
written, no profile is modified, and no diagnostic request or PIN action
is enabled. Eight synthetic offline tests protect this boundary.

## Configuration-read preflight from the source set (Development 0.3.0)

Following the aggregate revision-source review, the same offline dialog now
shows a **five-gate read eligibility report**, built using the existing
`VolvoP2CemServiceCapabilityMatrix` for the `ReadConfiguration` category.
The report retains mandatory blockers for independent vehicle year,
authenticated physical CEM revision, documented configuration read mapping,
authorized physical DiCE/J2534 acceptance and an implemented read service.

Source-file agreement, repeated citations, user-asserted VIDA provenance and
historical opaque DID snapshots cannot satisfy any of these verification
gates. The new preflight explicitly reports duplicate/conflicting sources,
redacts part numbers, citations, CAN IDs, VIN and raw values and never
constructs a J2534/UDS command. An actual revision-specific CEM protocol,
configuration DID list, PIN or recoverable backup is **not implemented**.
Eight synthetic tests cover fail-closed behavior, exact blockers, source
conflict/deduplication, invalid claims, immutability and WinUI wiring.
