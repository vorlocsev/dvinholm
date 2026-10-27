# XC90 P2/CEM: shareable offline DID research intake

Dvinholm Development 0.3.0 offers an optional **Copy sanitized research
metadata** action inside the existing **Inspect snapshot** dialog. This
is a real, user-directed tool for collecting basic observations that can
be discussed during independent P2/CEM compatibility research.

## Workflow

1. Use the existing read-only UDS **Read + save snapshot** flow only
   with an authorized vehicle and explicit consent; or use an already
   exported, synthetic Dvinholm diagnostic JSON for offline acceptance.
2. Select **Inspect snapshot** while disconnected if desired.
3. The existing bounded 4 MiB strict-UTF8 JSON inspector validates
   schema, non-restorable/sensitive flags, UTC capture, record ordering,
   DID values, CAN addressing and payload SHA-256.
4. See an additional **P2/CEM compatibility research intake** section
   in the historical snapshot dialog. It gives only the UTC timestamp,
   observed 11/29-bit CAN request/response IDs, DID identifiers, and
   DID **byte lengths**, never any payload bytes.
5. Choose **Copy sanitized research metadata** only after reviewing
   the contents. **Close** does not copy anything, and no file or ECU
   operation occurs. Clipboard is OS-managed and may remain available
   after exiting Dvinholm; paste and distribute only knowingly.

## Privacy and evidence limitations

No raw DID value, VIN, profile name/ID, proprietary configuration bytes,
file path, credential, PIN or owner information is copied or included
in the display. Note that CAN addressing and DID presence/lengths may
still indirectly fingerprint a vehicle, so this is **privacy-reduced
rather than anonymous**.

The SHA-256 in a diagnostic snapshot checks its payload integrity only;
a modified file can be rehashed. A historical file is neither verified
ECU identity nor a restorable configuration backup. The source's
authenticity, vehicle make/platform, model year, CEM revision, readable
option meanings, permissions and real J2534 hardware compatibility all
remain **unconfirmed**.

The text is **not** a supported-vehicle declaration, actual vehicle
config, service/PIN result, writable parameter map or firmware
fingerprint. The production XC90 P2/CEM evidence catalog is still empty.
No code in this feature issues a CAN request, attempts PIN/security
access, writes the ECU, or changes session/profile state.

## Automated acceptance and next evidence

Eight deterministic offline xUnit tests check the generated historical
report's timestamp, CAN and DID lengths, sensitive-value/profile
redaction, unsupported service/restore warnings, 11/29-bit formatting,
schema and UTC failures, malformed CAN/DID structure and bounded
hex values. The real WinUI clipboard action remains subject to
installed Windows 11 acceptance. Virtual-J2534 and physical P2/CEM
acceptance are separately tracked and not claimed.

The next useful step is gathering **independently verified actual CEM
hardware/software identification and legal diagnostic transport/DID
documentation**, then bench-validating those data before registering any
real production CEM signatures or configuration mappings.

Published Stable v0.2.4 remains untouched. No release without
explicit user approval.
