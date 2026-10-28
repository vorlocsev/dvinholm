# XC90 P2/CEM: revision-scoped historical DID evidence gate

Development 0.3.0. This feature examines **two files already selected by
the user** in the existing P2/CEM research dialog:

- A manual VIDA identity research card (three claimed hardware/software
  part numbers and a claimed XC90 model year).
- A separately saved, strictly inspected historical diagnostic DID snapshot.

Both documents can be edited; the diagnostic snapshot SHA-256 detects
accidental corruption but **does not authenticate the source** or establish
that either file refers to the same physical vehicle, module or firmware.

## Read-only evidence procedure

The pure `VolvoP2CemHistoricalDidEvidence` component:

1. Reuses the existing paired-input and sensitive-snapshot validation.
2. Enumerates at most 129 entries from a proposed catalog of CEM revision
   evidence, refusing catalogs exceeding 128 or with invalid entries.
3. Reuses `VolvoP2CemSupportMatrix.Assess` to require an exact model-year,
   CAN addressing and multi-DID **synthetic/offline** identity signature
   match. Unknown or ambiguous identities fail closed.
4. Reports only the count of DIDs that are **both previously observed in
   the historical snapshot and separately documented** for that matched
   candidate revision. No DID values, part numbers or CAN IDs are printed
   by this additional report.
5. Returns research-only classifications, **never an executable DID list**
   or a live diagnostic session; all read, PIN, configuration and
   authorization capability flags remain false.

Importantly, this classification depends on **whatever evidence was supplied
to the offline test**. A fictional or unauthenticated catalog can produce a
synthetic match; that result is *not* verification of any real ECU.

## In the Development application

Open **P2 configuration candidates (offline)**, then open a saved manual CEM
identity research card, select **Pair with DID snapshot...**, and pick an
existing diagnostic JSON snapshot. The read-only paired report includes an
additional section named `P2/CEM REVISION-SCOPED HISTORICAL DID RESEARCH`.

The built-in `VolvoP2CemSupportMatrix.ProductionEntries` catalog remains
**EMPTY** because no independently sourced, physical XC90 P2/CEM
hardware+software fingerprint, actual diagnostic addressing and read-only
configuration DID semantics have been verified. Therefore the current
application always reports missing production revision evidence in this
section. It cannot turn two matching files into a CEM identification.

Eight deterministic synthetic tests cover the empty production catalog,
fake exact matches, absent documented DID observations, firmware/address/year
mismatches, malformed nonmatching catalog entries, conflicting revisions,
invalid user file claims and redaction of sensitive data.

## Outstanding evidence and hardware acceptance

For revision-specific CEM configuration reading, independently establish the
precise CEM hardware/software revision, physical CAN or K-Line diagnostic
transport, authorization and reproducible read-only parameter semantics.
Validate on a lawfully accessed DiCE/J2534 bench with consent and capture
safety before proposing any live module-specific operation.

PIN extraction and configuration writing are **not implemented**. No
J2534/UDS frames, security access, key programming, backup restoration, ECU
configuration changes, automatic export or hidden file writes have been added.
Stable v0.2.4 remains unchanged.
