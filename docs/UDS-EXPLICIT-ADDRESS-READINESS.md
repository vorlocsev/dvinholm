# Explicit UDS address readiness in Diagnostics (Development)

## Why

A disconnected Dvinholm window previously prefilled request `7E0` and response `7E8` and enabled all existing read buttons when any J2534 adapter connected. These are conventional generic ECM examples, **not** independently verified addresses for an observed vehicle/ECU, and could give a misleading impression that a passive RX observation establishes an active TX address.

## Behavior

- The request and response fields now start **empty**. Example IDs remain in documentation and profile-edit placeholders, but are never armed in the diagnostic workspace by default.
- Existing **Read VIN / Read DTC** actions become available only if the adapter is connected, no UDS read or ECU discovery is active, offline acceptance mode is off, the CAN ID format is selected, and the two manually supplied addresses are syntactically valid and distinct.
- **Read DID** also requires a valid 16-bit hexadecimal DID. An invalid DID does not block VIN or DTC, because those existing operations do not use the DID field.
- Field edits or format changes immediately recalculate readiness and invalidate old diagnostic outputs. Running requests cannot be rearmed by a mid-read edit.
- A bounded text hint explains why reads are disabled, and clearly distinguishes valid syntax from verified ECU identity. Passive discovery can fill an *observed RX* address but still leaves the required TX address blank.
- The existing backend `TryGetUdsClient` validation remains in place as a second line of defense. No new CAN/UDS request, PIN/authorization operation or configuration write is added.

## Offline acceptance

Eight deterministic tests cover disconnection, missing IDs, valid standard/extended syntax, invalid/equal/out-of-range IDs, busy/acceptance states, DID format, initial XAML controls and live UI edit gating.

**Not validated by these tests:** physical vehicle/CEM ID mapping, ECU identity, installed Windows UI, DiCE/J2534 physical access or hardware recovery. Valid ID syntax is not a safety or entitlement claim.

This Development-only PR does not change VirtualCar or published Stable v0.2.4, and does not publish a release.
