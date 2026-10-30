# Payload-free diagnostic failure activity (Development)

The general status text also feeds the Aurora home page's last-activity summary. Earlier UDS read failures could pass arbitrary exception messages from a transport, adapter, or malformed response into this shared activity. Those messages are not guaranteed to be free of sensitive VIN, DID data, local paths or vendor-specific text.

## Read-only UI behavior

- Successful VIN, DID and DTC readbacks still appear only in the existing bounded Diagnostics result panel, with a fixed activity summary.
- A rejected UDS read displays the **structured service byte and negative response code** with the existing fixed, locally defined NRC explanation. No arbitrary exception text is displayed.
- A timeout uses a fixed hint to check the adapter and ECU addresses.
- A generic error uses a fixed connection/settings hint, without emitting exception messages, stack traces or inner exceptions.
- Cancellation emits a fixed message. All four error/cancel paths still check the original request generation before writing status, so a late failure cannot replace a newer Clear or selection change.
- No new UDS request, CAN ID, ECU write, CEM PIN operation, configuration change, J2534 driver or VirtualCar change.

## Eight offline checks

1. Known NRC preserves code and fixed glossary.
2. Unknown NRC is explicitly unidentified.
3. Malicious payload/path in exception metadata is not displayed.
4. Timeout message is ignored.
5. Unexpected exception and inner message are ignored.
6. Cancellation message is ignored.
7. Invalid read type and null exception fail closed.
8. Real WinUI handler source contracts require every failure branch to use the safe formatter after its generation guard.

Offline CI does **not** establish installed Windows UI behavior or physical J2534/DiCE compatibility. Stable v0.2.4 is frozen and no release is authorized by this PR.
