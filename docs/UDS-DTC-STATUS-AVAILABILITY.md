# UDS DTC status availability, read-only diagnostics

Development feature for the existing Diagnostics **Read DTC** operation.

## Background

A positive UDS `ReadDTCInformation` service `0x19`, subfunction
`0x02`, contains:

- `0x59 0x02` (positive response and reportDTCByStatusMask);
- a one-byte **DTCStatusAvailabilityMask** describing which status
  bit positions this responding ECU supports;
- zero or more four-byte records (24-bit DTC code, one-byte raw status).

Previously Dvinholm parsed the individual DTC records but discarded
the availability mask. The display therefore interpreted every set
raw status bit as if the responding ECU advertised support for it.

## Updated behavior

The existing **Read DTC** action still sends **one** read-only
`0x19/0x02` request with the user's existing status mask (`0xFF` in
the current UI). It now keeps the status availability mask in a
`UdsDtcReadResult` alongside the original immutable record list.

The dedicated Diagnostics result panel presents both the **requested**
status mask and the **ECU-supported** status mask. Status descriptions
are derived from the intersection `raw DTC status & availability mask`.
When the ECU returns bits outside its declared support, the display
preserves the original raw status byte but explicitly marks those
unsupported bits, rather than presenting them as supported flags.

A zero-record reply still shows the availability mask and explicitly
warns that a single ECU response is **not a vehicle-wide fault-free
result**. Existing low-level `ReadDtcByStatusMask` and
`ParseDtcResponse` APIs remain available, now delegating to the
availability-preserving path, for backward compatibility.

Eight offline synthetic tests cover ordinary and zero-mask replies,
a full mask, unsupported and inconsistent bits, multi-DTC ordering,
the immutable legacy parser, malformed frames and the actual
Diagnostics UI wiring. Physical J2534/DiCE and vehicle testing were
not performed.

No new diagnostic service, write command, CEM configuration, PIN
operation or manufacturer-specific DTC meaning has been introduced.
Stable v0.2.4 artifacts and the independent Virtual Car project
remain unchanged.
