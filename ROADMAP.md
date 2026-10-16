# Dvinholm development roadmap

Updated: 2026-10-16  
Current main checkpoint: `afef97288c1f9456130f2311b02181ab3b7ffca2`  
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
- [x] PR #235 adds a regression test proving a mid-stream download I/O failure cleans up both the temporary ZIP and staging directory; PR and post-merge CI passed.
- [x] PR #237 adds a regression test for cancellation after the first archive chunk; PR, branch, and post-merge main CI passed.
- [x] PR #239 strengthens SHA-256 mismatch cleanup coverage by asserting both temporary ZIP and staging directory removal; PR and branch CI passed. Post-merge main CI [#38916569004](https://github.com/vorlocsev/dvinholm/actions/runs/38916569004) passed.
- [x] PR #240 records the digest-mismatch test and roadmap checkpoint; PR and branch CI passed. Post-merge main CI [#38917668043](https://github.com/vorlocsev/dvinholm/actions/runs/38917668043) passed.
- [x] PR #241 strengthens corrupt-ZIP cleanup assertions for the temporary archive and staging directory; PR/branch CI and post-merge main CI [#38920429003](https://github.com/vorlocsev/dvinholm/actions/runs/38920429003) passed.
- [x] PR #242 adds missing-executable cleanup assertions; PR CI passed and the transient post-merge public CI asset publish HTTP 500 passed on retry [#38921421584](https://github.com/vorlocsev/dvinholm/actions/runs/38921421584).
- [x] PR #243 adds updater replacement backup/rollback behavior; PR/branch CI and post-merge main CI [#38922622760](https://github.com/vorlocsev/dvinholm/actions/runs/38922622760) passed.
- [x] PR #246 executes the generated replacement PowerShell script in isolated Windows temp directories, injects a partial replacement failure, and verifies the old executable/marker are restored, the partial marker is absent, the rollback is logged, and relaunch is requested. PR CI [#38925619261](https://github.com/vorlocsev/dvinholm/actions/runs/38925619261) and branch CI [#38925610701](https://github.com/vorlocsev/dvinholm/actions/runs/38925610701) passed; merged as `91f000014e7d09546f868c12299d2235f13d1f3f`.
- [x] PR #248 prevents J2534 disconnect from racing a still-unwinding CAN Monitor worker: it requests monitor cancellation and awaits the monitor task before disconnecting the session. PR CI [#38927452306](https://github.com/vorlocsev/dvinholm/actions/runs/38927452306), branch CI [#38927445038](https://github.com/vorlocsev/dvinholm/actions/runs/38927445038), and post-merge main CI [#38928179572](https://github.com/vorlocsev/dvinholm/actions/runs/38928179572) passed; merged as `6804b8828879df51f24f10a837cb9808b6989f68`.
- [x] PR #250 preserves the CAN Monitor diagnostic when disconnect cleanup begins; PR CI [#38928375204](https://github.com/vorlocsev/dvinholm/actions/runs/38928375204) passed and it was merged as `c38e2e1de7c37c0fc9cfff26c7051ca74100a39b`. Post-merge main CI on that commit [#38929247943](https://github.com/vorlocsev/dvinholm/actions/runs/38929247943) passed.
- [x] PR #249 records the monitor/disconnect lifecycle checkpoint and was merged as `6ef6046bfd819b436772c8731495d7cc3cc5fb3b`. Its first PR CI attempt had one ISO-TP receive-queue overflow test failure (475/476); retry [#38928314219](https://github.com/vorlocsev/dvinholm/actions/runs/38928314219) passed. The test uses a virtual adapter; no hardware was involved.
- [x] PR #251 updates the monitor/disconnect checkpoint and UDS lifecycle audit; merged as `09a68831ceb855a582c9808f7ac3ce56a7562bbf`. PR-head CI [#38930299279](https://github.com/vorlocsev/dvinholm/actions/runs/38930299279) and branch CI [#38930277558](https://github.com/vorlocsev/dvinholm/actions/runs/38930277558) passed.
- [x] PR #252 records the PR #251 merge checkpoint; merged as `afef97288c1f9456130f2311b02181ab3b7ffca2`. PR-head CI [#38931312837](https://github.com/vorlocsev/dvinholm/actions/runs/38931312837) and branch CI [#38931290345](https://github.com/vorlocsev/dvinholm/actions/runs/38931290345) passed. Post-merge main CI [#38932062825](https://github.com/vorlocsev/dvinholm/actions/runs/38932062825) is still running.
- [x] Automated coverage exists for CAN receive-dispatcher lifecycle/races, J2534 session lifecycle/read errors, ISO-TP malformed frames/timeouts/cancellation/recovery, UDS VIN/DTC parsing and response handling, and virtual J2534 adapter scenarios.
- [x] User manually confirmed the updater success path and error/retry handling on Windows 11 LTSC IoT.
- [x] User manually confirmed ECU profile controls and CAN Monitor remain visible in the compact layout, including a narrower window width.

## Next software-only work

- [x] Monitor/disconnect lifecycle fixes are merged: #248 awaits CAN Monitor shutdown before J2534 disconnect, and #250 preserves a monitor warning during disconnect. PR, branch, and post-merge main CI checks passed for these source changes.
- [x] Documentation checkpoint after PR #249 passed post-merge main CI [#38930123098](https://github.com/vorlocsev/dvinholm/actions/runs/38930123098). PR #251 and PR #252 documentation updates passed PR-head/branch CI. Main checkpoint is now `afef97288c1f9456130f2311b02181ab3b7ffca2`; post-merge main CI [#38932062825](https://github.com/vorlocsev/dvinholm/actions/runs/38932062825) is running. The preceding main run on `09a68831ceb855a582c9808f7ac3ce56a7562bbf` failed in an updater rollback runtime test cleanup, not in compilation.

1. [ ] Review the current-main test inventory and CI test report to identify remaining untested release-critical paths; avoid duplicating already-covered lifecycle/timeout tests.
2. [x] Updater failure-path cleanup covers partial-download I/O failure (#235), cancellation after partial content (#237), SHA-256 mismatch (#239), corrupt ZIP (#241), and missing executable (#242). Replacement backup/rollback is implemented in #243 and runtime-tested on Windows in #246 using isolated temporary directories and stubbed copy/process commands. This verifies the scripted rollback scenario without touching an installed application; it does not replace manual in-place updater testing on the user's Windows installation.
3. [x] Reviewed monitor/disconnect ordering and merged PR #248 so the app waits for the monitor worker to exit before disconnecting the J2534 session; PR #250 ensures monitor warnings survive into disconnect status. PR/branch CI passed, and post-merge main CI for #250 passed. The later documentation-only main checkpoint still awaits CI.
4. [x] Reviewed UDS read-only command gating in `MainWindow.xaml.cs`: `TryGetUdsClient` refuses requests when `_connected` is false or CAN IDs are invalid; Read VIN/DTC/DID routes through this guard, and UDS/discovery share `_udsGate`. No guard was bypassed. Continue automated state-transition coverage if gaps are found.
5. [ ] Verify UDS read-only command gating and UI state transitions in code: Read VIN and Start Monitor should become enabled only when the required J2534/CAN session is active; do not bypass these safety gates.
6. [ ] Review cancellation/disconnect behavior while VIN/DTC reads are active; add deterministic virtual-adapter tests for any uncovered race. The close handler cancels UDS/discovery, waits up to two seconds, and only disposes the J2534 session when tracked operations have stopped; test this boundary more directly.
7. [ ] Re-run full CI after each change; update this roadmap and `AI_WORKLOG.md` only with confirmed results.
8. [ ] After the software-only checklist is complete, prepare a release-candidate checklist and confirm the exact published release archive separately from CI artifacts.

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


## CI checkpoint — PR #253 rollback-test cleanup (2026-10-16)

- [x] PR [#253](https://github.com/vorlocsev/dvinholm/pull/253) fixes the Windows updater rollback test cleanup by disposing the PowerShell process before deleting its temporary root and draining redirected stdout/stderr concurrently. Existing rollback assertions remain intact.
- [x] Latest PR-head CI [#38932272236](https://github.com/vorlocsev/dvinholm/actions/runs/38932272236) passed; PR #253 merged to `main` as `db3e8607ec0c28057649f7f616056a71b9bc991e`.
- [x] Post-merge main CI [#38933065485](https://github.com/vorlocsev/dvinholm/actions/runs/38933065485) passed on merge commit `db3e8607ec0c28057649f7f616056a71b9bc991e` (build, test, publish, and artifact steps succeeded).
- [x] Software-only UDS lifecycle audit reviewed the window-close handler and existing cancellation/session tests. Active and queued UDS cancellation, ISO-TP cancellation/recovery, and J2534 session lifecycle already have automated coverage. The UI `Closed` event itself is coupled to WinUI and does not have a direct unit-test seam; no duplicate or speculative test was added. A future targeted refactor may extract a small shutdown coordinator if a reliable regression test is needed.
- Physical J2534 adapter and vehicle checks remain deferred to a future release.


## UDS lifecycle audit checkpoint (2026-10-16)

- [x] Confirmed active/queued UDS cancellation and ISO-TP cancellation/recovery tests exist; reviewed the MainWindow.Closed shutdown sequence. It cancels discovery/UDS, waits for tracked workers, and only schedules session disposal when discovery, monitor, connect/disconnect, and UDS gate are all stopped.
- [x] No uncovered race was demonstrated in the testable transport/session layers, so no redundant test was added. The UI close handler remains a review-only boundary because it is coupled to WinUI; consider a focused shutdown coordinator extraction only if further changes make a deterministic test seam valuable.
- [x] Main CI after PR #253 passed: [run #38933065485](https://github.com/vorlocsev/dvinholm/actions/runs/38933065485).
- Physical J2534 adapter and vehicle testing remain deferred.

## Manual UI verification — connection retry (2026-10-16)

- [x] User confirmed the manual Windows check for retrying after a failed J2534 connection attempt succeeded; the application remained usable and the retry path behaved as expected.
- This is a UI/error-recovery confirmation only. No physical J2534 adapter or vehicle was used, and hardware compatibility remains unverified.
- [ ] Continue software-only verification of connection-state controls and error recovery.

## Manual UI verification — diagnostic controls require connection (2026-10-16)

- [x] User confirmed Read VIN, Read DTC, and Start Monitor remain unavailable without an active J2534 connection; failed connection attempts do not hang the app.
- This confirms UI gating only; no live CAN/UDS transaction or hardware compatibility was tested.
- [ ] Continue software-only release checks. Physical adapter and vehicle testing remains deferred.


## Manual UI verification — profile persistence across restart (2026-10-16)

- [x] User confirmed that a previously saved ECU profile remains present after closing Dvinholm normally and launching it again.
- This confirms local profile persistence across an ordinary restart only; it does not validate ECU communication or physical adapter compatibility.
- Physical J2534 adapter and vehicle testing remains deferred to a future hardware-validation cycle.
- [ ] Continue software-only release checks and record only confirmed outcomes.


## Manual UI verification — edited profile persists across restart (2026-10-16)

- [x] User confirmed that changing a safe profile field (profile name), saving it, then closing and relaunching Dvinholm preserves the updated name.
- This confirms edited profile data persists in local storage across restart; no ECU communication was involved.
- Physical J2534 adapter and vehicle testing remains deferred to a future hardware-validation cycle.
- [ ] Continue with a non-destructive software-only profile check; protect the user's only useful profile.


## Manual UI verification — edited profile JSON export (2026-10-16)

- [x] User confirmed exporting the edited ECU profile succeeds and the JSON includes the updated profile name with the expected fields/structure intact.
- This verifies local profile export/serialization only; no ECU communication was involved.
- Physical J2534 adapter and vehicle testing remains deferred to a future hardware-validation cycle.
- [ ] Verify import round-trip with a separate copy of the exported JSON, keeping the original profile untouched.


## Manual UI verification — profile JSON import round-trip (2026-10-16)

- [x] User confirmed importing the exported ECU profile JSON succeeds and the imported profile retains the expected name and fields.
- [x] Original profile remains available; local export/import round-trip verified without ECU communication.
- Physical J2534 adapter and vehicle testing remains deferred to a future hardware-validation cycle.
- [ ] Check profile isolation by switching between profiles once a second disposable/test profile is available; protect the user's only useful profile.


## Manual UI verification — profile isolation/switching (2026-10-16)

- [x] User confirmed the original ECU profile and a separate `TEST-ECU` profile are both available and open independently.
- [x] Switching profiles does not mix their names or data.
- This verifies local profile selection/data isolation only; physical J2534 adapter and vehicle testing remains deferred.
- [ ] Verify safe deletion behavior using only disposable `TEST-ECU`; preserve the original useful profile.
