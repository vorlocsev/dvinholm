# Read DTC: selected status filter (Development)

The existing **Read DTC** control in Diagnostics uses read-only UDS
`ReadDTCInformation` service `0x19`, subfunction `0x02`.
Previously the application always requested status mask `0xFF`,
which is appropriate for a broad scan of **one manually addressed ECU**
but not always useful when narrowing an ECU-specific fault list.

The read-only Diagnostics panel now includes a single status-filter
selector immediately beneath the existing button:

| Selection | Request mask | Meaning |
| --- | --- | --- |
| All status bits (default) | `FF` | Any ECU-reported status match |
| Test failed | `01` | ECU reports the test failed |
| Pending DTC | `04` | ECU reports a pending DTC |
| Confirmed DTC | `08` | ECU reports a confirmed DTC |
| Warning indicator requested | `80` | ECU reports a warning indicator request |

These are **ISO 14229 status bits**, not Volvo-specific fault
categories, physical module identification, VIN claims, or meanings
assigned to undocumented codes. The field **does not send a request**
until the user explicitly clicks **Read DTC** with a connected,
source-checked pair of request/response CAN IDs.

The selected mask is checked against a fixed allowlist and captured
before the asynchronous read. A missing, modified or invalid selection
disables Read DTC and refuses a request; the software does **not**
silently fall back to `FF`. Changing the filter invalidates any
previous diagnostic panel result, including output still in flight.

The existing ECU-reported `DTCStatusAvailabilityMask` remains
independent of the **requested** status filter. The result display
continues to show both masks and handles unsupported ECU status bits
conservatively. A filtered, empty reply does **not** prove that the
vehicle is fault-free, and reading one ECU is not a full vehicle scan.

Eight offline synthetic tests cover all five choices, rejecting
malformed/unoffered tags and all other bytes, XAML choice wiring,
single-request parameter propagation, cancellation-safe result
invalidation, and fail-closed button behavior.

No new UDS service, PIN retrieval, ECU configuration writing, DTC
clearing, or physical adapter acceptance is implied. The separate
Virtual Car project and published Stable v0.2.4 are unchanged.
