# J2534 and CAN Monitor public error boundaries (Development)

## Why

`MainWindow.SetStatus` also updates Aurora home activity. A native J2534 DLL can return arbitrary last-error strings, including file paths, vehicle data or private machine details. Connection and hardware filter failure paths previously forwarded driver error strings or sanitized exception messages into that shared activity. Regex-based path sanitization is not a guarantee against unrecognized private data. Also, if the CAN monitor failed to create an RX subscription, its cancellation source could already have been marked active, leaving the monitor button behaving as if capture were running.

## Changes

- Connection refusal, unexpected connect exceptions and disconnect exceptions use specific, fixed, actionable messages, never vendor error text.
- If an existing CAN monitor task faults during disconnect, it is still awaited before adapter disposal and gets a fixed warning without the exception payload.
- For monitor start, construct the cancellation source locally and assign it to the active field **only after** RX subscription succeeds. If subscription fails, dispose the temporary token source and leave monitor controls unarmed.
- Hardware filter add/remove rejection and unexpected adapter exceptions use fixed public messages. Preserve the current filter ID on failure, changing it only after confirmed adapter success.
- Keep existing CAN ID validation, monitor RX history/reader serialization, adapter reset logic, hardware filter behavior and native diagnostic source untouched. This PR adds no CAN transmit or ECU service.
- Successful connection still shows the previously existing driver version and capabilities string. **That separate successful-connection metadata path is not covered by this failure-activity change** and requires its own privacy review.

## Eight offline checks

1. Rejected connections show a bounded recovery hint.
2. Unexpected connect exceptions have a distinct fixed hint.
3. Disconnect and monitor-shutdown failures remain distinguishable.
4. Failed monitor subscription does not claim active capture.
5. Hardware filter errors distinguish apply, remove and unexpected failure.
6. All fixed messages exclude private synthetic VIN/path and unknown codes fail closed.
7. Real connection handler does not forward vendor errors or sanitized exception payloads.
8. Real monitor and hardware filter handlers only arm state after confirmed success; failure paths are guarded.

Offline test success does **not** imply that physical DiCE or other J2534 devices were exercised. No VirtualCar change or published Stable 0.2.4 mutation. No release publication.
