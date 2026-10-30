# Passive ECU discovery: payload-free Aurora activity (Development)

## Issue

The public `SetStatus` method also writes the home screen's last-activity summary. Passive ECU discovery previously forwarded `ex.Message` for cancellation and incomplete results, and a formatted transport exception for unexpected failures. Although the two current specialized discovery exceptions format locally generated diagnostics, that does not establish a safe public-activity boundary. Vendor/transport exceptions may contain paths, raw data, or identifiers.

## Behavior

- The scan still **only observes CAN RX**; it does not transmit requests or claim to identify a particular ECU/CEM.
- Five fixed activity outcomes cover successful observation, cancellation with partial observations, dropped-frame incomplete scan, cancellation before observations were ready, and unexpected failure.
- No exception message, raw CAN frame, inferred TX address, driver DLL path, VIN or local filename is copied to the Aurora activity text by the discovery handler.
- The **dedicated discovery status and response list** retain safe counts and the dropped-frame warning. Incomplete/cancelled candidates remain **non-selectable**; the normal manual request-ID confirmation requirement is unchanged.
- The scanner remains serialized with read-only UDS operations, with its existing cancellation, cleanup and `_udsGate` release logic unchanged.

## Eight offline checks

1. Completed scan is not labeled authenticated ECU identification.
2. Cancelled scan warns partial observations are not selectable.
3. Receive-queue drops are labeled incomplete.
4. Pre-start cancellation does not fabricate observations.
5. Unexpected failure produces a fixed adapter recovery hint.
6. All five public activity strings exclude example private VIN, paths, exception text and CAN addresses.
7. Unknown activity outcome fails closed.
8. Real handler uses only fixed outcomes, preserves partial scan status, and keeps incomplete results non-selectable.

Existing virtual-J2534 passive RX tests still check **zero transmitted frames**. These offline tests do not establish installed Windows GUI behavior or real DiCE/J2534/CEM identity and transport compatibility.

No new ECU service, CEM PIN or configuration read/write operation, no VirtualCar changes, and no modification to published Stable 0.2.4.
