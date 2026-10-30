# Diagnostics workspace: separated UDS read results (Development)

Read VIN, Read DID and Read DTC already send their respective **read-only**
UDS requests. Their completed responses previously went through the general
status bar, which also updates **Last activity** on the Aurora home page.
This meant a VIN or full raw DID payload could appear on the home screen,
and multi-line DTC output competed with connection and error messages.

## New behavior

- A dedicated **Diagnostic read result (VIN / DID / DTC)** expander at the
  bottom of the existing workspace holds the latest completed response.
  It is selectable and scrollable with a bounded 170-pixel viewport.
- The **status bar** and Aurora last-event summary receive only a fixed
  operation label and a short completion message. They receive **no**
  VIN, raw DID bytes or DTC records from these three read handlers.
- Starting a new read clears and collapses the old detail first, so a
  failed or canceled attempt cannot leave a stale VIN/DID result under
  an unrelated status.
- Successful reads open the expander automatically. No result is saved
  to disk, copied to the clipboard or uploaded by this change.
- The result panel warns that VIN and raw DID values can be identifying
  and that ECU-reported DTCs do not establish vehicle-wide diagnosis.

The existing UDS command paths, CAN addressing, J2534 session, request
timeouts, cancellation, DID formatter, VIN parser, DTC formatter, snapshot
export, CAN monitor and ECU selection remain unchanged.

## Eight offline acceptance checks

1. VIN data appears only in detail, never the status/activity summary.
2. Raw DID hex remains visible only in the diagnostic result.
3. Multi-line DTC responses preserve record order and status.
4. Operation labels and summaries are fixed, not driver-supplied text.
5. Empty results cannot be presented as a completed read.
6. Unknown request categories fail closed.
7. Real MainWindow XAML has a bounded, selectable private result panel.
8. The actual UDS handlers reset stale text and never call SetStatus with
   a response payload.

Windows manual visual acceptance and physical J2534/CEM testing are still
separate. This does not enable any PIN discovery, CEM configuration read
or write, DTC clearing or ECU programming. VirtualCar and Stable v0.2.4
are untouched.
