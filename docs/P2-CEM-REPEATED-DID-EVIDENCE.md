# XC90 P2/CEM: repeated historical DID observations

Status: **Development 0.3.0, fully offline research utility**. Not a
manufacturer-verified CEM fingerprint or ECU-configuration mapping.

## Practical workflow

1. Collect two legitimately authorized **read-only** diagnostic
   snapshots of a vehicle/module using explicitly specified profile
   DIDs, ideally in comparable conditions. Existing Dvinholm
   `Read + save snapshot` is user-consented and sends only UDS 0x22
   reads over an already selected J2534 device. Never guess CEM
   CAN addresses or DIDs from a screenshot or marketing text.
2. Select the two saved Dvinholm diagnostic JSON files with **Compare
   snapshots**. Both are separately inspected for bounded schema,
   exact SHA-256 payload checksum, UTC date, CAN format, DID metadata
   and sensitive/non-restorable flags. Inspection alone sends no ECU
   request.
3. The existing historical DID comparison still reports changed
   DID identifiers. The additional **repeated-DID observations**
   summary reports every common DID in ascending order, its
   **byte length** in both captures and whether the raw value matched
   between those two files. It never displays the values themselves.
4. When dates differ, observations are interpreted chronologically,
   even if the user picked the later file first. When timestamps are
   identical, the standard comparison still works but no sequence
   is claimed.

## What this does and does not establish

- A value unchanged across **two** files is only a *two-sample
  observation*. It is **not** proof of immutable module identity,
  a service-readable hardware/firmware version, VIN or compatibility.
- A changed DID does not by itself identify a car-configuration
  parameter. Length changes are flagged for research, not decoded.
- Matching profile ID, request/response CAN IDs and DID set are
  prerequisites for the comparison, **not** proof both files originated
  from one ECU, one car, or genuine Volvo software.
- SHA-256 detects inconsistent snapshot payload bytes, but cannot
  authenticate file authorship or prevent a modified file from being
  rehashed. No real XC90 P2/CEM production fingerprints are in Dvinholm.
- The UI does **not** include raw DID bytes, profile names/IDs,
  file paths or PINs. CAN IDs, DID IDs and lengths still can
  fingerprint a vehicle: review before sharing screenshots.
- There is no PIN algorithm, service authentication, ECU write,
  genuine configuration editor or restorable backup. No physical
  CEM or J2534 compatibility is claimed from an offline test.

Eight synthetic tests cover DID equality/change classifications,
byte-length differences, deterministic sort, UTC sequencing, profile
and CAN mismatch, DID set/data rejection, private-data redaction
and immutable results. The UI/clipboard, virtual-J2534 and actual
Volvo P2/CEM hardware checks must be done separately on Windows.

## Next authentic evidence milestone

Prioritize lawfully obtained, independently verified read-only
CEM hardware/software identification, request and response transport
and DID semantics for one specific XC90 P2 model year. Only then
consider a narrowly sourced production identity rule and validated
option mapping. PIN/authorization and writes require independent
permission, backup and hardware safety gates.

Stable v0.2.4 is unchanged; any future Stable release requires
the user's separate approval.
