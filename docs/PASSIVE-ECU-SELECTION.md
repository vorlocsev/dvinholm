# Passive ECU discovery: explicit, source-aware selection (Development)

Dvinholm's existing **Discover ECUs** action listens for UDS-shaped
response frames on the shared CAN receive dispatcher. A response is an
**observation**, not proof of a particular ECU module, validated
diagnostic request ID, firmware variant or authorized service.

## Updated behavior

- Starting a scan clears the previous candidate selection **and**
  request/response text fields so an old inferred address cannot be
  reused as if it belonged to the next scan.
- Completed scans populate the list and dropdown, but **do not
  automatically select the first candidate**. A user must explicitly
  choose an observed response.
- On selection, the observed response ID and CAN 11/29-bit format are
  shown. The **request ID is left blank** even when a conventional
  request/response relationship is inferred from the passive frame.
  The display labels that address **INFERRED ONLY** and asks for
  independent verification and manual entry before using existing
  read-only UDS commands.
- Incomplete or cancelled scans retain observed candidates and
  dropped-frame warnings, but **disable selection**. They must be
  repeated to obtain a usable candidate list.
- Disconnect or partial disconnect failure invalidates the previous
  scan and removes stale request/response fields.
- List headers now distinguish **Observed RX** from **Guessed TX**,
  and a bounded, selectable explanation panel shows observation
  count, request inference and limits. The ECU profile metadata panel
  is not overwritten with passive-scan details.

## Safety and test boundaries

This change **does not send a new request**, probe an ECU, authenticate
any response ID, change the J2534 dispatcher, alter the existing
read-only VIN/DID/DTC commands, or infer Volvo module names from CAN
addresses. The generic UDS read handlers still validate manually
entered addresses before attempting a request.

Eight deterministic offline tests cover 11/29-bit display, unknown
request addresses, incomplete/empty scan wording, no false ECU identity,
malformed metadata, real XAML labels/selection evidence, and
fail-closed scan/selection lifecycle wiring.

The tests do **not** establish Windows GUI acceptance or hardware
J2534 compatibility. No VirtualCar changes or new emulation tests
were made, and frozen Stable v0.2.4 remains unchanged.
