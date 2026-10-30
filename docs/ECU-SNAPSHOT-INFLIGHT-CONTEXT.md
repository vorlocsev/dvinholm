# ECU DID snapshot: in-flight selection and output privacy (Development)

The existing read-only **Capture Snapshot** operation requests profile-defined UDS 0x22 DIDs and, after consent, writes a local diagnostic JSON file. It is not a restorable ECU backup.

## Problem

Before this change, the operation checked the selected profile and CAN addressing immediately after the confirmation dialog, but after the asynchronous ECU read only checked whether the adapter remained connected. A user could change profile selection or CAN IDs while the read was outstanding, then be prompted to save results from the old selection. After Save As, it checked connection only. Late errors also forwarded exception messages, and the success activity included the selected filename.

## Updated UI behavior

- A new capture receives a unique diagnostic input generation before the consent dialog.
- After consent, after the ECU read, and again **after Save As but before starting the file write**, the UI checks that the exact same immutable profile object remains selected, the diagnostic generation is unchanged, and the connected session, mode and profile addresses still pass the existing read-only snapshot gate.
- Changing the selected profile, request/response CAN IDs, CAN format or DID invalidates the in-flight generation. Cancel UDS invalidates it before signalling cooperative cancellation.
- A stale result is discarded **without opening Save As or beginning an export** if context changed during the read, or without writing if context changed while Save As was open.
- Cancellation and errors from an invalidated operation cannot overwrite the newer global activity. Structured NRC values are reported through the trusted fixed UDS glossary; generic vendor and filesystem exception messages are not shown. Success activity no longer echoes the filename, which may itself contain vehicle data.
- Context is checked again after file-writing completes so a late success cannot replace a newer status.

## Important filesystem boundary

The app checks before starting file I/O. **If a context change or cancellation occurs after the atomic report write has already begun, the operation cannot guarantee that the selected file will not be committed.** The report still refers to the originally captured profile, not the newer UI selection. The UI does not claim that an in-progress file write was undone. No new physical J2534 tests or installed Windows UI test is implied.

## Eight offline tests

The suite checks unchanged input success, equivalent-but-distinct profile invalidation, generation edits, invalid generation, session/address invalidation, cleared selection, three guard points in the real handler and payload-free late-error/success activity. No vehicle request, VirtualCar change, CEM PIN attempt, configuration write or new diagnostic service was introduced.

Stable v0.2.4 remains unchanged. No release is published by this Development-only PR.
