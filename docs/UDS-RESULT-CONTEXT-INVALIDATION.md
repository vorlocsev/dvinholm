# Diagnostics: invalidating stale VIN/DID/DTC results (Development)

The dedicated read-only diagnostic result panel added in PR #630
holds the latest completed VIN, DID or DTC text in memory. Those
outputs can contain a vehicle identifier or raw data and must not
silently persist when the selected ECU address or DID changes.

## Behavior

- **Clear diagnostic result** is now an always-visible button in
  the diagnostic workspace, outside the collapsible detail panel.
  It clears the displayed output without contacting the vehicle.
- Changing UDS request ID, response ID, CAN 11/29-bit format or DID
  immediately clears the prior result and collapses the detail panel.
- Explicitly choosing an observed ECU, starting another passive ECU
  scan, disconnecting or attempting a new adapter connection also
  invalidates the old result.
- Returning to the Aurora dashboard clears the private payload
  from memory-based UI controls. The dashboard continues to get
  only a fixed, payload-free last-activity message.
- Starting a new VIN/DID/DTC read creates a new UI-only generation.
  If an input changes or the user presses Clear while the asynchronous
  read is still working, its late result is **discarded** rather than
  displayed against the changed context. Clearing the display does
  not cancel or transmit an ECU operation.

No new UDS service, CAN/J2534 access, VIN parser, DTC interpretation,
ECU programming, snapshot persistence, clipboard export or diagnostic
command is added. Existing read-only requests and their cancellation
behavior remain unchanged.

## Acceptance

Eight deterministic offline tests cover initial and successive result
generations, repeated edits, explicit-clear invalidation, always-visible
XAML clear action, request/response/format/DID input hooks, guard order
around asynchronous completion, and scan/navigation/adapter invalidation.
The earlier eight diagnostics result tests were updated to check the
centralized reset method without reducing privacy guarantees.

CI compilation and offline tests do **not** replace actual Windows
GUI acceptance or physical J2534 tests. VirtualCar and frozen
Stable v0.2.4 remain untouched.
