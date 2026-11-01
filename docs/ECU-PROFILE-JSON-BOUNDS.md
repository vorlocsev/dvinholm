# Bounded ECU profile JSON import (Development)

ECU Profiles remain editable local data, not authenticated vehicle
identification, a verified CEM diagnostic address or permission to transmit.
A user-provided profile `isVerified` flag is not independent evidence.

## Limits and compatibility

- **2 MiB maximum UTF-8 JSON bytes** for a selected import and for the
  saved primary/backup catalog at startup. Reading the selected file now
  checks its length before allocating the buffer, then reads exactly that
  many bytes and rejects a file that unexpectedly grows during inspection.
- **512 profile records** maximum per catalog. The bound is enforced before
  JSON model deserialization. Exports cannot create a catalog that violates
  the same profile-count/JSON byte limits.
- **8 JSON nesting levels** maximum, sufficient for current profile
  objects and their candidate DID arrays.
- File loading uses **strict UTF-8**; malformed bytes are rejected.
  Existing UTF-8 BOM catalogs continue to open. The profile JSON schema,
  CAN IDs, DID spelling and established case-insensitive profile ID rules
  are unchanged.

The selected-file path in the WinUI import dialog, the saved primary catalog
and its `.bak` recovery reader all use the same bounded reader. Invalid
inputs cannot enter the importer persistence callback; the current selected
profiles remain unchanged. A damaged oversized primary may still be
recovered from an independently valid backup without altering the damaged
primary.

Eight synthetic xUnit tests cover plain/BOM files, oversize rejection,
invalid UTF-8, over-limit and exact-limit profile counts, JSON depth,
backup recovery and the real WinUI reading contract.

This is an application resilience change only. It does not add a CEM
fingerprint, authorize a PIN/service route, send a CAN/UDS/J2534 request,
read live CEM configuration or write to a vehicle. No physical DiCE or
other J2534 acceptance has been performed for this change.
