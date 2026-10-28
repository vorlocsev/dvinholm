# XC90 P2/CEM: transport-risk research before any on-car work

**Development 0.3.0, OFFLINE ONLY.** This feature does not detect the
adapter, select a bus speed, identify an ECU, send CAN or J2534 calls,
authenticate a CEM, acquire a PIN or read/write its configuration.

## Published evidence and its limits

The following historic service-manual reproduction includes a warning
about HIGH-SPEED CAN **bus speed by model year** for Volvo P2 vehicles
including XC90:

- [2007 XC90 V8: reading extended DTC information, fault-tracing
  communication notes](https://charm.li/Volvo/2007/XC90%20AWD%20V8-4.4L%20VIN%2085%20B8444S/Repair%20and%20Diagnosis/Powertrain%20Management/Transmission%20Control%20Systems/Testing%20and%20Inspection/Reading%20and%20Clearing%20Diagnostic%20Trouble%20Codes/Reading%20off%20Extended%20Diagnostic%20Trouble%20Code%20%28DTC%29%20Information%2C%20Transmission%20Control%20Module%20%28TCM%29/).
  It describes **250 kbit/s for model years through 2004**, and
  **500 kbit/s from model year 2005**, warns that the wrong
  profile/rate can block CAN traffic or cause DTCs, and mentions
  the CEM among potentially affected modules.

This is a third-party reproduction of historical workshop guidance;
it is NOT a module-specific diagnostic speed/address table.
These values concern the identified **HS-CAN segment only**;
they must NEVER be assumed to describe LS-/MS-CAN, K-line, a
specific CEM transport, or ISO-TP arbitration identifiers.

- [Volvo Cars DiCE / J2534 information](https://www.volvospecialtools.com/index.cfm?event=info.dice)
  states that DiCE is recommended for D2/GGD vehicles;
  J2534-1 is described for specific emissions-related software
  downloads and **cannot be used for VIDA diagnostics**;
  J2534-2 has broader HS/MS-CAN software-download coverage.
  This is about manufacturer's VIDA use, not proof that a generic
  third-party J2534 API supports P2/CEM diagnostics.

- [Volvo TJ 14902 CEM recovery, historically reproduced](https://lemon-manuals.la/Volvo/2009/XC90%20FWD%20L6-3.2L%20VIN%2098%20B6324S/Repair%20and%20Diagnosis/Powertrain%20Management/Computers%20and%20Control%20Systems/Technical%20Service%20Bulletins/Customer%20Interest/Computers%2FControls%20-%20CEM%20Download%20Interrupt%20Recovery/)
  references VIDA logical CEM nodes `40` before MY2005 and
  `50` for later P2, in a manufacturer-download recovery context.
  They are **NOT CAN IDs, UDS DIDs, ECU addresses or commands**.

## What the new feature does

In Dvinholm Development open **P2 configuration candidates (offline)**.
The existing year field is shared by separate research tools:
the **customer-value** worksheet only covers **MY2003-2006**,
while the **legacy transport advisory** displays historical
first-generation **MY2003-2014** scope.

Within the same dialog, select the *claimed* network, *claimed*
communication-interface category and nominal speed, then press
**Review legacy transport risks (offline)**. The
`VolvoP2CemTransportResearchPreflight` reuses
`VolvoP2CemHsCanSpeedAdvisory` and
`VolvoP2CemVidaNodeAdvisory`; it does not try the choices
on an actual bus. The model-year checkbox is still only a
**self-reported manual assertion**, not identification.

It emits a caution if a *manually proposed* HS-CAN speed differs
from the year-based published guidance, flags J2534-1 as
not suitable for VIDA diagnostics, and reports logical nodes only
as historical text labels. A rate match still CANNOT prove a
physical module, DiCE/J2534 support, protocol, ISO-TP address,
readable configuration or authorization. Changing any input
invalidates the on-screen report. No speed is automatically
selected, no adapter initialized, no profile/file modified.

## Acceptance and real-work prerequisites

Eight deterministic offline tests cover the MY2003-2004 vs
MY2005-2014 split, mismatch warnings, unconfirmed/out-of-period
years, other networks, omitted nominal rate, explicit J2534-1
diagnostic restriction, and denial of every live-operation flag
for all claimed adapter/network combinations.

Before connecting a physical XC90 P2/CEM, require separately
documented actual vehicle year, verified physical bus route,
specific diagnostic addressing/protocol, interface/driver
capability, the appropriate read-only negative-response and
timeout behavior, and an **authorized bench test with recovery
plan**. Any security/configuration write requires additional
licensed/service authorization and tested backup/restore.
No such capability is granted here.

Stable v0.2.4 and its updater remain unchanged. No new Stable
release is requested or published by this task.
