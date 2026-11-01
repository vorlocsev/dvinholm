# CEM PIN: bounded source experiment (Development)

Development connects source-defined PIN enumeration to the existing
J2534Session/CanBus/CanRxDispatcher and a separate temporary result panel.
This is **partial implementation**, without physical CEM acceptance or a
claim of supported vehicle fitment. Published Stable v0.2.4 and Virtual Car
releases are unchanged.

The user explicitly approved enumeration and commands from the
[pinned source](https://github.com/vtl/volvo-cem-cracker/blob/2d5de865d21094889a6c1c689797d5a636e909fc/volvo-cem-cracker.ino).
Approval selects an experimental specification; it does not prove physical
revision, HW/SW identity, vehicle ownership or successful extraction.
Protocol facts are independently encoded; source implementation is not copied.

## User workflow

1. Connect at 500 kbit/s with 29-bit channel IDs. Stop monitoring and remove
   user hardware filters.
2. Use **Read CEM P/N (experimental HS-CAN)** and approve its single request.
   The reader validates two source BCD frames from the same full RX ID.
3. For a source-listed P2 L/H part number, enter a starting ordinal and an
   attempt limit of 1–10000, then choose **Read CEM PIN (experimental)**.
   Explicit authorization precedes transport access.
4. The provider queries the already available service context and requires
   its P/N to match the normal-mode observation. It checks bounded candidates
   with a total 60-second deadline and a 2-second reply deadline. Missing
   replies stop the operation.
5. A zero acknowledgement requires a second request with the same candidate
   and a separate zero acknowledgement. The result stays hidden until
   **Reveal for 15 seconds** is pressed. Its owned buffer expires within
   30 seconds of creation, even without UI polling.

The PIN view is separate from generic diagnostic text, activity, logs,
reports, settings and clipboard. Expiry, cancellation, context/input changes,
connection loss and shutdown clear it. Rendering necessarily creates a
managed UI string; clearing the view cannot promise immediate erasure of
all CLR/UI copies. Owned candidate buffers are explicitly zeroed. Origins
are Synthetic for tests and AdapterUnattested for native sessions. Native
replies are not physical provenance attestation.

## Protocol scope and limitations

The new provider sends only source service-context identification and PIN
checks through the existing HS-CAN channel. It uses P2 L/H order-1 mapping
and a 64-bit cursor over six packed-decimal pairs. Ambiguous P/N 30682982,
P1/early-P2 profiles and unknown numbers are excluded before transmission.
The source-listed P/N set is not verified MY2009 V8 support or SW identity.

No programming-mode entry, ECU reset, K-line initialization, timing attack,
key programming or configuration write is implemented. An ordinary vehicle
may not expose the required service context. The full source procedure is
therefore not implemented, and a 100^6 search is not claimed practical with
this J2534 route. No ETA, invented percentage or synthetic PIN is presented
as a vehicle result.

Acknowledgements require the same full RX ID, source header and zero tail.
This deliberately stricter parser stops on unknown shapes, changed rejection
status, queue loss, deadline or invalidation. Late traffic has no protocol
nonce; repeated acknowledgement is not cryptographic authentication.

The operation shares the diagnostic gate and cancellation/shutdown path.
The session guard binds consent and context to the captured bus and makes
invalidation permanent. The gate releases after provider termination and
filter cleanup. Failed cleanup invalidates transport until reconnect;
unpublished accepted material is disposed. Vendor exceptions are sanitized.

## Registry and validation

The default coordinator registers no provider and uses the closed capability
matrix. Only the explicit Development factory selects this source experiment;
editable research evidence cannot activate it. Stable compilation excludes
the live provider and keeps the controls collapsed.

Synthetic tests exercise actual bus dispatch, prerequisites, request mapping,
repeated acceptance, context mismatch, strict acknowledgements, cancellation,
invalidation, exhaustion, deadlines, cleanup faults and private-error
redaction. Separate tests cover secret expiry, erasure and explicit reveal.
Existing lifecycle, enumeration and normal-mode P/N tests remain applicable.
Tests use an in-memory adapter and cannot establish physical support.

Interactive Windows UI testing and physical DiCE/J2534/CEM acceptance remain
outstanding. A future physical record must identify actual HW/SW and vehicle,
establish authorized context, validate accepted and rejected replies, confirm
repeatability and check cancellation/disconnect/recovery without configuration
changes. Keep PIN/VIN/raw secret traffic out of that record.
