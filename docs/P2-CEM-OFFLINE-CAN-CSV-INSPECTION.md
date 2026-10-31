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

The dialog summarizes two groups of **possible byte patterns**: aggregate
normal-addressing Classic CAN ISO-TP shapes (SF, FF, CF, FC, other) and
Volvo D2-looking RX envelope candidates (E3, F9, fragment-like).
Neither section includes actual CAN IDs, individual timestamps, raw bytes,
VIN, DID, PIN, filenames or user profile labels.

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

Eight xUnit checks cover normal export round-trip, synthetic provenance,
bad schemas, CSV injection and addressing, invalid payload/DLC/quoting,
5000-row and byte limits, actual local file/BOM/UTF-8 inspection, and
virtual-J2534 reception with **zero transmitted frames** and UI binding.

Installed Windows UI acceptance, genuine DiCE/J2534 acquisition and actual
XC90 CEM service mapping are **not claimed**. Only Dvinholm changes.
Virtual Car is a separate project used as an earlier research reference;
none of its code, releases or workflows are modified here.
