# Passive CAN RX metadata research summary (Development 0.3.0)

This feature supports gathering **transport research evidence**, especially
while investigating Volvo XC90 P2/CEM diagnostics. It does not claim a
verified Volvo CEM address, bus or diagnostic service.

## How to use

1. Use CAN Monitor with a lawfully connected J2534 adapter, or a synthetic
   virtual adapter during development. Only connect at a confirmed and safe
   network speed. Opening this summary itself **does not** connect, start
   monitoring or send CAN frames.
2. Press **RX summary** beside the CAN Monitor frame rate. The summary reads
   up to 5,000 frames already retained in the monitor history, **regardless
   of the current software ID filter**. It does not request new frames.
3. Review the per-ID and 11/29-bit-format frame counts plus DLC
   distribution. The report separately shows how many rows were evicted
   and tracked subscriber drops **since the last Clear**, including
   completed monitor subscriptions and subsequent restarts. UNKNOWN means
   no subscription has been observed since Clear or an active counter cannot
   be sampled. Clear rebaselines an active subscription without restarting it.
4. Press **Copy RX metadata** only when willing to put these CAN IDs/counts
   on the clipboard. Closing the dialog does not copy or save anything.
   Existing **CSV log** remains a different, potentially sensitive,
   raw-frame export.

The summary never reads the `CanMonitorRow.Data` payload property.
Consequently it does **not** expose frame bytes, VIN, PIN, UDS payload or
configuration data, even when those bytes are present in the monitor.
It includes at most **64 sorted CAN ID+format groups**, labels synthetic
demo data, rejects malformed/unbounded metadata, and never calls J2534.

This is a **bounded observational summary, not a complete traffic capture**.
The monitor may have discarded old rows or missed RX frames; drop counts
can become unknown when the subscriber is no longer active. RX-only
reporting does not prove that another application, a tester, or Dvinholm
did not transmit during the original capture. CAN IDs, DLC histograms,
timestamps (although not shown here) and message counts can still
fingerprint a vehicle. Review before sharing.

No inference of CAN request/response direction, UDS or Volvo CEM variant
is made from RX arbitration IDs. A source-verified physical transport,
exact firmware/hardware identity, authorized service prerequisites and
DiCE/J2534 bench testing are still required before revision-specific CEM
configuration access can be enabled.

Eight synthetic tests cover empty history, deterministic per-ID DLC
counts, 11-bit versus 29-bit separation, software filter independence,
payload exclusion, malformed metadata rejection, display bounds, and
RX-only virtual J2534 integration with zero transmissions. Physical
vehicle acceptance and installed Windows UI validation remain future work.

Stable v0.2.4 is not modified or published by this change.

## RX drop accounting correction (Development 0.3.0)

A previously stopped monitor no longer loses its final drop count in the
research summary. Restarting monitoring adds new subscriber drops to previous
ones within the same Clear epoch; it does **not** reset the accumulated count
while retaining older RX history. Clearing the monitor rebaselines the active
subscription or forgets a completed one. Counts are subscriber queue drops,
not an exhaustive number of all missed physical bus frames. If counters are
missing, regress or overflow, the accounting reports UNKNOWN and refuses to
invent a number.

Eight additional deterministic synthetic tests exercise unknown, active,
stopped, restarted and cleared sessions, malformed counters, and overflow.
No ECU service, J2534 command or automatic export is introduced.
