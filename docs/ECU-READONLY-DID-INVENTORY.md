# Completed ECU DID inventory in Diagnostics (Development)

The existing **Read + save DID snapshot** action already sends a
user-approved set of generic UDS `0x22` reads over the selected J2534
CAN profile. Until now, cancelling the optional **Save As** dialog
discarded the opportunity to inspect the completed read without writing
a JSON file containing sensitive vehicle bytes.

After a **fully successful** capture, the existing Diagnostics result
panel now shows a bounded read-only inventory in memory:

- Number of successfully read DIDs, in ascending numeric order.
- Each DID identifier and the **byte count** of its opaque reply.
- Explicit warning that the observed bytes do not establish physical
  CEM identity, verified firmware or a restorable configuration backup.

It does **not** show raw DID values, VIN, profile metadata, CAN IDs,
file paths or source notes. Even DID identifiers and lengths can help
fingerprint a vehicle, so this text stays in the user-requested
Diagnostics panel and is never forwarded into the global status/Aurora
activity or automatically copied/exported. The user can close the panel
or clear the result.

The inventory appears **before** the separate, optional Save As step.
Choosing Cancel in Save As preserves the in-memory inventory and creates
no file. Explicit JSON export is still supported through the existing
guarded save workflow and still requires the pre-read user consent.

Changing ECU selection, DID, CAN format/addressing or pressing Cancel
uses the existing diagnostic generation invalidation and clears the
result. An incomplete read, malformed DID response, late cancellation
or changed session never displays an inventory.

The formatter independently checks UTC time, CAN address boundaries,
1..128 distinct DIDs, uppercase even-length hex values and the current
Classic CAN ISO-TP response ceiling of 4092 DID bytes. It **never**
attempts to decode undocumented CEM options or claim that a stored
profile is genuine Volvo hardware.

Eight offline synthetic tests cover deterministic sorting/lengths,
sensitive data redaction, nonauthorization warnings, malformed and
duplicate values, CAN and UTC metadata validation, capacity limits and
UI placement before Save As. No additional CAN/J2534 request, PIN search
or configuration write is added. Real DiCE/J2534/CEM acceptance still
requires physical testing. Stable v0.2.4 and the independent Virtual Car
project are unchanged.
