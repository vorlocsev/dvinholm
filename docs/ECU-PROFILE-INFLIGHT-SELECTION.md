# ECU profile changes during read-only UDS work (Development)

## Why

The ECU profile dropdown applies request/response addresses to WinUI text boxes using a deferred UI-queue callback, to avoid selection reentrancy. The prior read-result and snapshot guards invalidated their generation when those **text fields changed**, but not at the instant the dropdown selection changed.

A rapid **A → B → A** sequence, or two profiles using the same request/response CAN IDs, could therefore leave the input fields untouched until the queue ran. A late VIN/DID/DTC read or snapshot could then appear to belong to the most recently selected profile. A catalog replacement or a search filter hiding the selected profile also needed an explicit invalidation independent of TextChanged.

## Behavior

- As soon as the user selects a different ECU profile, the active diagnostic-result generation is invalidated and the private VIN/DID/DTC result panel is cleared. **The update happens before the queued field-application callback.**
- Any in-flight read or snapshot gets a cooperative cancellation request. An ISO-TP/CAN frame already transmitted is not withdrawn, and cancellation cannot reverse a file write that has begun.
- The profile/filter generation advances for each manual selection. Deferred callbacks from earlier selections are ignored; the current selection is applied once by the latest callback.
- A filter that hides the selected profile, or replacing the entire profile catalog, explicitly invalidates previous results and requests cancellation even when clearing the fields emits no TextChanged event.
- After a profile applies, read-only UDS button readiness is recomputed even when the new CAN IDs are textually identical to the previous ones. Existing connected/busy/address checks still apply.
- Filtering while preserving the same selected profile does **not** trigger a new ECU request and does not deliberately overwrite manually edited UDS fields.

## Eight offline checks

1. Fast A → B → A invalidates the old read generation.
2. Immediate selection invalidation precedes deferred field application.
3. Invalidation precedes cooperative cancellation; no CAN access in the helper.
4. Older queued selection callbacks cannot apply after a newer selection.
5. Filtering out the selected profile explicitly invalidates context.
6. Replacing the catalog invalidates context before replacing selection.
7. Applying identical address text still refreshes read-only action readiness.
8. An old snapshot is rejected even if the identical profile object is reselected.

These are offline logic and source-wiring tests. They are **not** installed WinUI acceptance tests and do not establish physical J2534/DiCE behavior, ECU identity, CEM PIN retrieval or configuration-write support.

No VirtualCar changes. Stable v0.2.4 remains untouched. No release is published by this Development PR.
