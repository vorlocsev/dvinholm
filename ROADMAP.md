# Dvinholm development roadmap

Updated: 2026-10-16  
Current main checkpoint: `a21b0bb07eaea83785e6bb4299130f4943d9ebc8`  
Latest stable release: [v0.1.1](https://github.com/vorlocsev/dvinholm/releases/tag/v0.1.1)

This roadmap separates software/CI verification from physical adapter validation. Passing tests do not establish compatibility with a real interface or vehicle.

## Completed / verified

- [x] Stable portable Windows x86 release v0.1.1 published.
- [x] CI build, test, publish, and artifact packaging passed for release v0.1.1.
- [x] Main-branch warning cleanup merged and post-merge CI passed.
- [x] Updater metadata hardening merged in PR #233; PR CI and post-merge main CI passed.
- [x] Updater checks release URLs against the expected GitHub project path and HTTPS host.
- [x] Updater validates SHA-256 metadata when supplied and skips malformed metadata instead of trusting it.
- [x] Updater can request the release asset API's binary response; tests cover this download path.
- [x] Automated coverage exists for CAN receive-dispatcher lifecycle/races, J2534 session lifecycle/read errors, ISO-TP malformed frames/timeouts/cancellation/recovery, UDS VIN/DTC parsing and response handling, and virtual J2534 adapter scenarios.
- [x] User manually confirmed the updater success path and error/retry handling on Windows 11 LTSC IoT.
- [x] User manually confirmed ECU profile controls and CAN Monitor remain visible in the compact layout, including a narrower window width.

## Next software-only work

1. [ ] Review the current-main test inventory and CI test report to identify remaining untested release-critical paths; avoid duplicating already-covered lifecycle/timeout tests.
2. [ ] Review updater failure cleanup end-to-end: incomplete/invalid download, digest mismatch, archive extraction failure, and restart/rollback behavior. Add focused regression tests for uncovered paths.
3. [ ] Verify UDS read-only command gating and UI state transitions in code: Read VIN and Start Monitor should become enabled only when the required J2534/CAN session is active; do not bypass these safety gates.
4. [ ] Review cancellation/disconnect behavior while VIN/DTC reads or monitoring are active; add deterministic virtual-adapter tests for any uncovered race.
5. [ ] Re-run full CI after each change; update this roadmap and `AI_WORKLOG.md` only with confirmed results.
6. [ ] After the software-only checklist is complete, prepare a release-candidate checklist and confirm the exact published release archive separately from CI artifacts.

## Deferred to a future hardware-validation cycle

- [ ] Test with physical Volvo DiCE and/or Mongoose J2534 hardware.
- [ ] Confirm actual device open/close, CAN traffic, VIN/DTC responses, hardware filters, and disconnect/reconnect behavior against a real ECU/vehicle.
- [ ] Record adapter model, driver version, vehicle/ECU details, and observed limitations before claiming hardware compatibility.

## Release gates

- All required CI checks pass on the exact merge commit.
- Software-only regression tests pass, including updater integrity and failure recovery.
- User-facing updater errors are recoverable and leave the installed application in a consistent state.
- Read-only UDS functions remain explicitly gated by connection state.
- Release notes clearly distinguish software/virtual testing from physical adapter verification.
