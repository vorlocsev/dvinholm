# XC90 P2/CEM research: virtual J2534 read-only E2E acceptance

**Scope:** Development 0.3.0, synthetic-only verification of existing
transport and DID snapshot plumbing. No claim is made that the hardcoded
synthetic CAN IDs or DIDs match any Volvo XC90/CEM. No vendor J2534 DLL,
real vehicle, key, PIN or ECU programming is used.

## What is actually exercised

The test file `EcuSnapshotVirtualJ2534EndToEndTests.cs` wires the
already-existing `VirtualJ2534Adapter` (in-memory implementation
of `IJ2534Adapter`) into production `CanBus`, the single shared
`CanRxDispatcher`, `IsoTpChannel` and `UdsClient`. Then it
invokes the **production**
`EcuConfigurationReadback.Capture(EcuProfile, UdsClient)`
over UDS ReadDataByIdentifier `0x22`.

This is a more complete simulated acceptance path than injecting
an arbitrary `Func<ushort, CancellationToken, byte[]>`
read function into the snapshot layer. There is still no installed
Windows UI test or actual J2534 device-driver test.

Eight deterministic scenarios:

1. Two synthetic DIDs are requested in sorted order. The CAN
   Monitor subscription receives the same response frames.
2. A multi-frame DID reply is reassembled only after ISO-TP
   Flow Control is sent back through the virtual adapter.
3. A synthetic extended (29-bit) CAN address is preserved through
   the virtual PassThru encode/decode path.
4. A successfully captured DID value requires explicit consent
   to create a diagnostic JSON snapshot; import/inspection
   keeps its sensitive and non-restorable flags, without CEM
   support being asserted.
5. A UDS negative `0x7F/0x22` response fails the read rather
   than creating a complete snapshot.
6. Pre-cancelled capture sends no request to the adapter.
7. Cancellation after the first successful UDS response
   prevents the next DID request.
8. If the second DID fails, the entire capture is rejected;
   no partial configuration baseline is returned.

All fixtures are synthetic; the test's addresses `0x7E0/0x7E8`
and `0x18DA10F1/0x18DAF110` are deliberately **generic test
conversations**, not verified Volvo diagnostic addresses.
The example DIDs likewise do not establish a real CEM
hardware/software identification map.

## Limits before real P2 functionality

These tests only establish that Dvinholm's internal read-only
pipeline behaves as expected under simulated responses.
Before enabling CEM support on a real first-generation XC90,
obtain independent, version-scoped evidence of the exact
module hardware/software revision, supported interface
(especially DiCE versus J2534), bus/session/addressing and
**non-invasive read-only diagnostic operations**. Explicitly
record unsupported or unknown variants. A successful simulator
test never authorizes PIN search, vehicle configuration editing,
ECU writing, immobilizer/security access or a Stable release.

The previous [VIDA identity worksheet](P2-CEM-VIDA-IDENTITY-WORKSHEET.md)
collects user-entered part-number notes, but does not
authenticate hardware, prove live transport or provide
restorable configuration data. Physical adapter and Windows
manual acceptance remain separate future milestones.
