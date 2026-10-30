# Fail-closed read-only UDS channel startup (Development)

## Problem

The VIN/DID/DTC button gate validates manually supplied request/response addresses. But WinUI handlers must validate again **when creating the ISO-TP channel**, not rely on a button's prior enabled state. The old setup path validated each CAN ID's range without rejecting **identical TX and RX IDs**. It also returned a sanitized vendor exception message that could be copied into the global status and Aurora last-activity summary. Sanitization is not a guarantee that an exception contains no vehicle identifiers or private local paths.

## Changes

- Centralize a pure, no-bus `UdsChannelStartPreflight.TryPrepare` for the existing live channel creation. Require a connected session, 11-bit/29-bit format, nonblank request and response IDs, valid hexadecimal range, and **different** request/response identifiers.
- Do not assume that `7E0/7E8` or any other pair belongs to the user's ECU. These are only explicit syntax checks, **not** module identification, authorization or verification of a service.
- Clear parsed outputs on invalid pairs, including malformed or out-of-range IDs.
- On failure to construct the ISO-TP subscription, show only a **fixed**, payload-free channel initialization message. Never mirror exception messages, adapter DLL paths, raw responses or potentially sensitive vehicle details to the public Aurora activity.
- Keep the existing `TryGetUdsClient` return contract for the VIN/DID/DTC handlers; ECU DID Snapshot already treats channel initialization failure as a fixed private-status activity.
- No new UDS service, request, transport mode, CAN transmission, authentication, PIN recovery or configuration write.

## Eight offline checks

1. Disconnected state rejects apparently valid addresses.
2. Missing or invalid format rejects.
3. Missing/blank TX or RX does not install default addresses.
4. Malformed or out-of-range standard/extended identifiers reject.
5. Equivalent TX/RX values reject even with mixed case, leading zeros or a `0x` prefix.
6. Distinct 11-bit values retain their exact IDs.
7. Distinct 29-bit values retain their exact IDs.
8. Real handler source validates before subscribing, uses a fixed startup failure message and does not pass vendor exception text to `SetStatus`.

This does not test the installed WinUI app or real J2534/DiCE hardware. VirtualCar is unchanged. Stable 0.2.4 remains frozen, and this PR does not publish a release.
