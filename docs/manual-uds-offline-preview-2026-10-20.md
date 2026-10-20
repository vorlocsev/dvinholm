# Offline UDS examples — Windows manual smoke plan

**Scope:** Newer Development CI builds, **not stable v0.2.2**. No J2534 adapter, ECU or vehicle needed. An example is neither a live UDS response nor evidence of ECU compatibility.

**Acceptance completed:** all 8 Windows checks below were user-confirmed on published **Development CI #2893** (`c86abc5`). Final full-restart verification: **2026-10-21**. Successes are recorded in sequential [PR #386 discussion comments](https://github.com/vorlocsev/dvinholm/pull/386); no actual J2534 device, ECU or vehicle was involved.

- [x] **Test 1:** Disconnect J2534 and stop CAN Demo. Locate **Preview UDS example (offline)** at the bottom of the window. Read VIN, Read DID and Read DTC must remain disabled without a session.
- [x] **Test 2:** Click Preview; the output must start with **OFFLINE SYNTHETIC UDS SAMPLE**, explicitly saying no adapter, ECU or vehicle was queried.
- [x] **Test 3:** Scroll to synthetic VIN (DID F190): fictional `TEST0000000000ABC` and raw hexadecimal bytes should appear.
- [x] **Test 4:** Scroll to `F187`: **ASCII preview (unverified): "DEMO-ECM"** and full DID/value raw bytes should appear.
- [x] **Test 5:** Scroll to two fictional DTC records: `0x123456` confirmed (`0x08`) and `0x00C0DE` pending (`0x04`), with no manufacturer-specific fault claims.
- [x] **Test 6:** Check that long preview text can be selected/scrolled without hiding CAN Monitor or ECU Profile controls.
- [x] **Test 7:** Start CAN Monitor **Start demo** then press UDS preview. It must state the offline preview requires a disconnected, idle session. Stop CAN demo before retrying.
- [x] **Test 8:** With no adapter, close and restart Dvinholm; verify that synthetic UDS output doesn't auto-load or activate real diagnostic actions. Manual preview still works.

Do **not** install an older Stable release for this test. These synthetic identifiers and results must not be interpreted as actual Volvo or other vehicle data.

**Result:** 8/8 user-confirmed Windows scenarios passed. Dvinholm keeps real UDS controls disabled when disconnected, and the synthetic preview starts only by explicit button press after restart. [PR #386](https://github.com/vorlocsev/dvinholm/pull/386) and [post-merge main CI #2893](https://github.com/vorlocsev/dvinholm/actions/runs/39495359322) passed. **No claim of real adapter, ECU or vehicle compatibility.**
