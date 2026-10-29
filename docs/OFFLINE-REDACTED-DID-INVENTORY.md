# Offline redacted DID inventory (Development)

Dvinholm's **Inspect snapshot** dialog can now export a deliberately
**lossy TXT inventory** from a previously saved, valid diagnostic DID JSON
snapshot. This is a research convenience, **not a recoverable ECU backup**
or evidence that the ECU supports CEM configuration or PIN authorization.

## Using the feature

1. Download and run a Development Lite build containing this feature.
2. Open **ECU Profiles → Inspect snapshot** while disconnected if desired.
3. Select the JSON originally saved using **Read + save snapshot**.
4. Read the SHA-256-checked historical inspection and choose
   **Save redacted DID inventory TXT**, not the clipboard action.
5. Use the path-only Windows **Save As** dialog. Cancel creates no file.
6. Review the resulting TXT before sharing: **DID numbers and byte lengths
   can still fingerprint a module/vehicle**. It does not claim anonymity.
7. Confirm the report is stored outside the Dvinholm application directory.
   Existing reports are written using a same-directory temporary file and
   atomically replaced only after a complete write.

## Explicitly omitted

- Raw DID payload values, including VIN bytes and unknown personal data
- Raw-payload hashes, which could enable cross-record correlation
- ECU profile ID/name, vehicle-specific CAN request/response IDs
- Exact capture timestamp and source filename/path

Retained fields are only DID **identifiers**, their **payload byte counts**,
the total count and warnings. A DID identifier may itself be distinctive.
The report is derived solely from a locally validated historical snapshot,
without a new J2534 session, CAN transmission, PIN, configuration read or write.

The existing diagnostic JSON remains untouched and may contain sensitive
vehicle data. Its SHA-256 verifies accidental corruption, **not authenticity**.

## Automated acceptance and limitations

Eight Windows x86 offline tests check the canonical report, privacy omissions,
unsupported/restorable schemas, duplicate/out-of-order DID records, invalid or
oversized bytes, empty/invalid timestamps, blocked application-tree destinations,
and safe complete replacement of user-selected reports.

**Manual Windows acceptance is still needed:** dialog button layout, explicit
Save As/Cancel behavior, file content, permission errors and no UI freeze.
Tests do not prove physical J2534/DiCE/CEM behavior. Stable v0.2.4 unchanged.
