# XC90 P2 CEM vendor PIN compatibility claims (offline)

**Development 0.3.0, reviewed 2026-10-28.** This does not define an
ECU service, brute-force algorithm, configuration format or actual compatible
CEM. It records two related but different vendor statements.

## PIN decoding, not key programming

- [D5T5: How to obtain the CEM/ECM PIN](https://d5t5.com/article/volvo-cem-pin-code)
  advertises VDASH CEM PIN decoding for some P2, including 3.2 six-cylinder,
  4.4 V8 and 147 kW D5. It advertises VDD for SOME P2 produced 2005-2014,
  explicitly excluding those three engines and with no success guarantee.
- [D5T5: Volvo keys and VDASH](https://d5t5.com/article/volvo-keys-and-vdash)
  separately describes P2 key-programming restrictions for XC90
  3.2 / 4.4 V8 / 147 kW D5 and 120 kW D5 MY2013+.
- CEM PIN decoding, vehicle option changes and key programming are different
  operations. These public descriptions are vendor/product claims, not
  independently verified Volvo CEM transport or revision-specific protocols.
  Statements about VDASH/DiCE/J2534 do not grant Dvinholm functionality.

## Implementation boundary

The pure VolvoP2CemVendorPinRouteResearch model classifies manual claimed
model-year/engine/tool combinations for source review. Only model years
2005-2014 are compared for these narrowly sourced claims, and even a
checked year is not authenticated. Unknown engine/year/tool fails closed.
No VIN, credential, PIN, raw ECU bytes, network identifiers or keys are
accepted. A key-programming caution does NOT imply a PIN-decoding exclusion.

A static source-limited overview appears inside the existing PIN readiness
dialog. Eight deterministic synthetic tests cover the engine exceptions,
year boundaries, independent key caution and unconditional denial of every
ECU/PIN/key/configuration operation. No J2534 calls or file writes.

Before any live support, obtain independent evidence of a specific real
XC90 CEM hardware/software revision, verified diagnostic interface, exact
authorized read-only transport, service entitlement and tested recovery.
Published Stable 0.2.4 remains frozen.

## Interactive offline variant comparison in PIN readiness

Inside **XC90 P2 CEM PIN readiness (offline only)** in Development,
a user can enter a claimed model year (four ASCII digits), mark whether
they separately checked it, select an **engine** and choose **VDASH**
or **VDD** as an advertising source. **Compare vendor claims (offline only)**
shows the source-scoped classification and an explicitly separate key
programming caution. The whole comparison uses local strings, no network,
vehicle, adapter, saved profile, VIN, PIN, credentials, or file.

Any edit to year, checked-state, engine or product invalidates the previous
result until the user presses Compare again. Unknown, unchecked, invalid,
out-of-range or unclassified inputs have an UNKNOWN verdict; malformed raw
year text is never echoed. Even a matching advertisement never proves CEM
hardware/software revision, supported transport, PIN entitlement or a
configuration read/write method. The existing four-step readiness checklist
still finishes **UNSUPPORTED**. No live operation is enabled.

Eight additional synthetic tests cover the V8, 3.2 and D5 variants,
VDASH/VDD differences, separate MY2013+ key warning, unknown input,
unsafe text redaction and unconditional denial. Installed Windows GUI
tests and physical DiCE/J2534/CEM tests remain separate and unperformed.
