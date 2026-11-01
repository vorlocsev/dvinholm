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
reports, ordinary settings and clipboard. Expiry, cancellation, context/input changes,
connection loss and shutdown clear it. Rendering necessarily creates a
managed UI string; clearing the view cannot promise immediate erasure of
all CLR/UI copies. Owned candidate buffers are explicitly zeroed. Origins
are Synthetic for tests and AdapterUnattested for native sessions. Native
replies are not physical provenance attestation.

## Encrypted persistence (Development)

The user's follow-up explicitly enables saving found PINs, superseding the
initial prohibition on persistence. Every accepted native result is saved
automatically in `%LOCALAPPDATA%\Dvinholm\cem-pin-vault`, separately from
normal settings. The operation consent explains this behavior. A save failure
is reported explicitly while the temporary result remains available.

Each find receives a unique GUID record, even for the same part number.
Its PIN, observed P/N, timestamp, schema and AdapterUnattested origin are
encrypted together using Windows DPAPI current-user protection. The record
GUID is included as additional entropy, so renaming/substituting a blob
does not create another valid record. No VIN or arbitrary user label is
collected. Synthetic, expired and metadata-only results cannot be saved.

Only ciphertext is staged to disk, flushed, checked and moved to a unique
destination. Failed writes do not replace existing records. Crash leftovers
may leave encrypted `.tmp` files; they are not loaded as records. Errors do
not expose file paths or native exception text. Listing skips unavailable
records with a visible warning and does not delete them automatically.

**Saved PINs** supports listing, explicit loading and deletion without a CAN
connection. Loading keeps the PIN hidden until explicit reveal and creates
a new expiring in-memory result. The saved file remains until deleted.
Stored results are labelled saved and do not verify the currently connected
vehicle. P/N alone is never used to match a record automatically to a CEM.
Deletion removes the selected encrypted record, not forensic disk copies.

DPAPI uses the Windows user's credentials; this is not a portable plaintext
export and does not defend against programs running as that same user.
Moving the files to another Windows account/computer or losing the required
profile keys can make them unreadable. No application-owned encryption key
is stored alongside the files. See Microsoft's
[CryptProtectData documentation](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata).
The storage and native protection implementation are excluded from Stable.
Eight synthetic-value tests use real Windows DPAPI and private temporary
folders to check round-trip, isolation, rejection, corruption, interrupted
writes, deletion, UI defaults and material disposal.

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
