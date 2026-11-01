# ISO-TP TX/RX CAN ID separation (Development)

The existing WinUI UDS preflight already rejects a request CAN ID equal to
its response CAN ID. However, the lower-level `IsoTpChannel` may be
constructed directly from other diagnostics, including future read-only
Volvo P2 CEM work. It formerly allowed the same CAN arbitration ID for
both directions.

The channel's `Options.Validate()` now independently requires different
request and response CAN IDs. The check runs **before** the constructor
subscribes to CAN receive traffic and before any request/Flow Control frame
can be sent. This avoids a possible adapter-echo/loopback ambiguity in
normal-addressed ISO-TP. Both 11-bit and 29-bit CAN arbitration formats
are covered. The existing range, timeout and flow-control checks remain
unchanged.

Eight synthetic tests cover same-ID standard and extended channels, zero
and maximum identifier boundaries, valid distinct directions, invalid
CAN ranges and direct construction on the existing **in-memory**
`VirtualJ2534Adapter` test fixture. No physical adapter or real ECU
was involved. These tests do not claim that any chosen CAN pair is a
verified Volvo CEM service mapping.

This is a transport safety guard, **not** a newly supported CEM
configuration read, PIN workflow, security access or write operation.
The production CEM identity catalog remains empty. Stable v0.2.4 and
the separate Virtual Car project are unchanged.
