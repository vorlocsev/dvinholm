# Read-only ECU DID snapshot progress (Development)

The existing `Read + save DID snapshot` action already uses only generic UDS
`ReadDataByIdentifier (0x22)` over an explicitly selected J2534 ECU
profile. This update makes multi-DID reads more intelligible without
adding any ECU-specific protocol, secret extraction or write operation.

## User experience

1. Before **any** diagnostic request, the existing consent dialog states
   the **count** of profile-defined candidate DIDs. Consent remains mandatory
   and cancellation sends no request.
2. As each DID response is validated and converted to opaque hexadecimal,
   the readback backend optionally reports `completed / total` counts.
   The main window shows that progress in the ordinary status line, but
   never passes CAN IDs, DID numbers, VIN, raw values, profile names,
   vendor exception text or chosen filenames to the activity.
3. Progress is not a saved or complete snapshot: if a later DID fails,
   cancellation arrives or the selected profile/session/CAN IDs change,
   the entire capture still fails closed.
4. Queued dispatcher progress callbacks are guarded by the original
   operation context and a UI-only `readInProgress` lifetime flag. Once
   reading has ended (successfully or otherwise), previously queued
   progress cannot overwrite a subsequent Cancel, Save As, error or
   completion status.

`EcuConfigurationReadback.Capture` keeps its previous behavior for
callers that do not pass the optional counts-only `reportProgress`
callback. It emits a progress event only after **successful DID echo,
length and cancellation checks**, never before the ECU response.
No partial snapshot is returned. All existing consent, atomic export
and redaction rules remain.

Eight offline synthetic tests verify ascending DID progress,
payload-free callback arguments, invalid echo rejection, later DID
failure, cancellation before/during/after a read, legacy no-callback
behavior and the actual WinUI callback/lifetime guard placement.

This is generic ECU diagnostic UX, **not** verified P2/CEM configuration
reading, physical CEM authentication, a restorable backup, CEM PIN
acquisition or configuration writing. No actual DiCE/J2534 hardware
acceptance was performed. Stable v0.2.4 and the independent Virtual Car
project remain unchanged.
