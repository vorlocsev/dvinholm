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
2. A separate user-authored, source-scoped CEM revision claim JSON, such
   as the entirely synthetic example below.

The app revalidates both local files. Cancel either picker to leave
everything unchanged. It shows only the claimed year/year range and
SAME/DIFFERENT for each of the three part-number fields. It does not
print individual part numbers, private paths, URLs or raw DID data.
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
