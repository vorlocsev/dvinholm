# UDS read results: cancellation and late completion guards (Development)

This change extends the context-invalidated VIN/DID/DTC result handling from PR #632. All requests remain the **existing, explicitly initiated read-only** UDS requests. It does not add CAN IDs, module identification, CEM PIN, security access, write services or physical-hardware behavior.

## Corrected behavior

- **Cancel UDS** now invalidates the UI generation **before** asking the running request to cancel. Cancellation can be cooperative and may race with a normal return; neither a late normal result nor any late failure/cancellation text may reopen the private result panel or overwrite the user's newer activity.
- A field edit, explicit Clear, ECU change, passive rescan, disconnect or dashboard return invalidates that generation. If a request is active, the visible status immediately states that its eventual output will be discarded. Idle edits do not generate a fake request activity event.
- A completed read checks its generation before exposing sensitive VIN/DID/DTC details. Any stale completion returns silently, preserving the newer Clear/context/cancellation activity.
- Negative UDS responses, timeouts, cooperative cancellation and unexpected failures apply their activity text **only while their original generation is still current**.
- The request's finally block still releases the UDS gate and restores controls, even if the completion was suppressed.

## Acceptance

Eight deterministic offline tests cover cancelled successful completion; cancelled failure status; fresh request after cancellation; invalidate-before-cancel event ordering; payload-free immediate context-change activity; stale-success return before UI mutation; all four failure/cancellation status guards; and cleanup/gate release after all outcomes.

**Not claimed:** installed Windows GUI acceptance, J2534/DiCE/ECU compatibility or actual vehicle reads. VirtualCar is not modified. Frozen Stable 0.2.4 remains unchanged. No release is created by this Development PR.
