# Offline CAN CSV inspection for P2/CEM research

Dvinholm now offers **Inspect CSV** next to **RX summary** in CAN Monitor.
This is a read-only analysis of a user-selected Dvinholm-exported CSV file,
independent of connected hardware and the live monitor state. The selected
file stays on the local PC. It is **not** imported as an ECU profile or
replayed to the CAN bus.

## What is accepted

- Only Dvinholm's canonical `timestamp,id,type,dlc,data` CSV with 11-/29-bit
  IDs and uppercase, space-separated Classic CAN payload bytes.
- The Development demo variant has a sixth `source` column; **every row
  must say `SYNTHETIC_DEMO`**, never an unverified user claim of a real car.
- At most 5000 rows and 512 KiB, UTF-8 without BOM. Strict numeric
  timestamp and DLC, consistent exact length, canonical CAN IDs, no
  injected CSV columns, multiline values or spreadsheet formula fields.
- Files may be LF/CRLF and may end with a single newline. The parser is
  deliberately *not* a permissive spreadsheet or third-party trace reader.

## Report and privacy

The dialog now shows three separate **heuristic** sections:
1. Aggregate Classic CAN normal-addressing ISO-TP *PCI shapes*
   (SF/FF/CF/FC/other), without CAN IDs.
2. **Candidate ISO-TP multiframe sequences**, tracking first-frame length,
   consecutive-frame sequence counters, possible completions, mismatches,
   interruptions and orphan CF frames. State is separated by 11-/29-bit
   format plus exact CAN ID internally, but the *offline CSV report exposes
   only all-group totals*, including groups past the 64-group live preview.
3. Volvo D2-looking RX envelope candidates (E3/F9/fragment-like), with
   unverified source and loss-counter warnings.

No section includes actual CAN IDs, individual timestamps, raw bytes, VIN,
DID, PIN, filenames or user profile labels. A plausible candidate length
match is **not** a reassembled or validated ISO-TP exchange. Frame ordering
comes from the selected CSV only; it cannot verify original bus ordering,
completeness, direction or the captured physical CEM. The CSV has no original
drop counter, so subscriber loss remains **UNKNOWN**.

**Timestamp boundary protection:** The aggregate CSV sequence tracker compares
adjacent raw J2534 `uint32` timestamps only to spot decreases. At a decrease,
it invalidates ALL active candidate sequences, across all CAN IDs/formats,
before processing the next row. This prevents falsely matching first and
consecutive frames across a logger restart, joined capture or clock wrap.
The redacted report shows the *number* of decreases and invalidated
candidates, but not the timestamp values or CAN IDs. It does **not** assume
time units, clock continuity, transport timeout or valid ISO-TP timing.
Equal/increasing timestamps merely lack that specific warning; they do NOT
authenticate frame order or an ECU. A natural 32-bit wrap is intentionally
handled as a boundary rather than a continuous ISO-TP transaction.

The existing live monitor sequence report remains unchanged, including its
per-ID display. Only the user-selected offline CSV aggregate view applies
this conservative discontinuity reset.

The JSON RX metadata schema and the existing per-CAN-ID live RX summary
remain unchanged. Nothing is copied until **Copy redacted summary** is
clicked; Close leaves clipboard alone. Import never appends or clears
frames, selects a bus, changes profile, opens a J2534 session, writes a
file or connects to a physical vehicle.

**Important:** A manually selected CSV does not authenticate its provenance.
Historic subscriber drops and overwritten/filtered frames are **UNKNOWN**
and must not be represented as complete bus evidence. A D2-looking header
does not prove a valid CEM response or a usable physical diagnostic
transport, PIN route or configuration service. The CSV file itself can
contain sensitive data; do not share it without review.

## Verification

The original eight xUnit checks cover normal export round-trip,
synthetic provenance, malformed schemas, CSV injection, payload/DLC/quoting,
5000-row and byte limits, actual local file/BOM/UTF-8 inspection, and
virtual-J2534 reception with **zero transmitted frames** and UI binding.
Eight additional synthetic tests check ID-free multiframe aggregate totals,
same-ID CF continuity, cross-ID/format isolation, mismatches and interruptions,
65-group completeness, unknown loss/demo provenance, and end-to-end local
CSV summary integration without changing the original per-ID live report.
Eight timestamp-boundary regression tests cover backwards timestamps,
normal/equal timestamps, global multi-ID invalidation, unrelated-ID
discontinuities, uint32 wrap, inactive-boundary accounting and CSV
round-trip versus unchanged live monitor behavior.

Installed Windows UI acceptance, genuine DiCE/J2534 acquisition and actual
XC90 CEM service mapping are **not claimed**. Only Dvinholm changes.
Virtual Car is a separate project used as an earlier research reference;
none of its code, releases or workflows are modified here.
