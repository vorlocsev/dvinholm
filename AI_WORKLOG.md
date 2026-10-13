# Latest handoff update — 2026-10-13 (PR #179 merged; main CI pending)

## Verified current state
- Current main commit: `ea0f8f1c465faa35415d43025669da729000756f` — [PR #179](https://github.com/vorlocsev/dvinholm/pull/179) merged.
- PR #179 sanitizes driver-provided diagnostic text before presenting it to the user. Common Windows drive paths, UNC paths, and Unix local paths are replaced with `[local path redacted]`; control characters are removed and output is capped at 240 characters.
- Regression tests cover Windows, UNC, and Unix path redaction, preservation of ordinary error text, and output length.
- Both exact-head CI workflows passed on `dca44b9b5eea1c8847153c808243d2f13480ca3f`: [run #38437869422](https://github.com/vorlocsev/dvinholm/actions/runs/38437869422) and [run #38437896457](https://github.com/vorlocsev/dvinholm/actions/runs/38437896457).
- Post-merge main CI is queued for `ea0f8f1c465faa35415d43025669da729000756f`: [run #38438524015](https://github.com/vorlocsev/dvinholm/actions/runs/38438524015). Do not mark the merge fully validated until this run succeeds.
- The previous main CI for `8c65866f6a8d9689fcd00bdbc3e2c06d62f31736` passed: [run #38437660901](https://github.com/vorlocsev/dvinholm/actions/runs/38437660901).

## Next priorities
1. Verify post-merge main CI [#38438524015](https://github.com/vorlocsev/dvinholm/actions/runs/38438524015).
2. Refresh this handoff with the result, and merge the documentation-only update only after exact-head CI passes and its base is current.
3. Continue a case-by-case audit of adapter-returned diagnostic strings. The new sanitizer covers common path forms but is not a proof that every vendor error format is safe; preserve useful technical diagnostics.
4. Perform manual Windows UI smoke tests: first launch, missing/invalid adapter DLL, connect/disconnect, UDS cancellation, and ECU profile import/export.
5. Physical DiCE/Mongoose adapter checks remain deferred to a future release; virtual-adapter CI does not establish hardware compatibility.

---

# Latest handoff update — 2026-10-13 (PR #178 merged; main CI pending)

## Verified repository state
- PR #175 and PR #177 are merged. PR #177 merge commit: `55775c3f3191038712f875da07b389d2193269a5`: [PR #177](https://github.com/vorlocsev/dvinholm/pull/177).
- Main CI at `55775c3f3191038712f875da07b389d2193269a5` failed in `UdsLateMultiFrameResponseAfterTimeoutTests.ReadDataByIdentifier_LateMultiFrameResponseAfterTimeoutIsCorrelated`: build succeeded, 418 tests passed and one timing-sensitive test failed. [Failed run #38435044050](https://github.com/vorlocsev/dvinholm/actions/runs/38435044050).
- PR #178 widened timing margins in that regression test without changing production code. Both exact-head CI workflows passed on `7031a626f1050e160e9fdb26bc59fee5dc8e9956`: [run #38435912923](https://github.com/vorlocsev/dvinholm/actions/runs/38435912923) and [run #38435885261](https://github.com/vorlocsev/dvinholm/actions/runs/38435885261).
- PR #178 was merged as `eb0a5562b95c28cd0ce68d31d0a3d1569481770b`: [PR #178](https://github.com/vorlocsev/dvinholm/pull/178).
- Post-merge main CI for `eb0a5562b95c28cd0ce68d31d0a3d1569481770b` is currently running: [run #38436775032](https://github.com/vorlocsev/dvinholm/actions/runs/38436775032). Do not mark the merged main commit fully verified until it succeeds.
- This handoff refresh PR (#176) had green exact-head CI on `5c12401294954d5be9e54ae3c44825eba47b352c`: [run #38435975135](https://github.com/vorlocsev/dvinholm/actions/runs/38435975135) and [run #38435982594](https://github.com/vorlocsev/dvinholm/actions/runs/38435982594). Since it predates the PR #178 merge, refresh and revalidate before merging.

## Next priorities
1. Verify post-merge main CI [run #38436775032](https://github.com/vorlocsev/dvinholm/actions/runs/38436775032).
2. Refresh this handoff PR with the final main CI result, then merge the docs-only PR only after exact-head CI passes and its base is current/mergeable.
3. Continue a case-by-case audit of J2534 adapter-returned error strings, preserving useful diagnostics while avoiding local paths or machine-specific exception details.
4. Perform manual Windows UI smoke tests: first launch, missing/invalid adapter DLL, connect/disconnect, UDS cancellation, and ECU profile import/export.
5. Keep physical DiCE/Mongoose adapter checks deferred to a future release; virtual-adapter CI does not prove hardware compatibility.

---

# Latest handoff update — 2026-10-13 (PR #175 CI failure fixed; rerun pending)

## Verified state
- Main CI for merge commit `c29b71f11f95b8cde7498bbed59ca66384a47db0` passed: [run #38432161527](https://github.com/vorlocsev/dvinholm/actions/runs/38432161527).
- PR #175 exact-head CI failed at SHA `27f2648c0f980ebb8e92f563f77d7c603625483d`: 418 passed, 1 failed. Root cause: `DisconnectClearsMetadataWhenAdapterUnloadThrowsAndCanRetry` expected the sanitized text, but `DisconnectLocked` still propagated the raw exception from `Unload()`; `DisposeCanRetryAfterAdapterUnloadFailure` also retained the old assertion.
- Fixed `DisconnectLocked` to replace vendor unload exceptions with a stable generic message while always clearing adapter metadata, preserving retry behavior. Updated the second lifecycle assertion to match.
- Fix commits: `2ad3868ee14a0ac9d7013b0e8c3a2d9a868135cd` (source), `f7f65edaa320fb7e541caf8d7f6868598811b72c` (test).
- Exact-head CI reruns are pending: [run #38433235172](https://github.com/vorlocsev/dvinholm/actions/runs/38433235172) and [run #38433240602](https://github.com/vorlocsev/dvinholm/actions/runs/38433240602). Do not merge until the current head has green CI and the PR head SHA is confirmed.

## Next
1. Wait for both current-head CI runs and inspect any failures.
2. If green, verify PR #175 remains open/mergeable at its latest SHA, merge, and confirm post-merge main CI.
3. Continue a narrow review of adapter-returned error strings; avoid blanket suppression of useful diagnostic messages.
4. Keep Windows UI smoke tests and physical DiCE/Mongoose adapter checks outstanding; physical tests are deferred to a future release.

---

# Latest handoff update — 2026-10-13 (J2534 session exception privacy)

## Verified repository state
- PR #173 — redact the native DLL loader exception path — merged as `9dfff7c14d15362fb82ba5932abb5beff18e1f73`; exact-head CI passed: [run #38430200958](https://github.com/vorlocsev/dvinholm/actions/runs/38430200958).
- PR #174 — AI handoff refresh — merged as `c29b71f11f95b8cde7498bbed59ca66384a47db0); both PR CI runs passed: [run #38431325729](https://github.com/vorlocsev/dvinholm/actions/runs/38431325729), [run #38431344776](https://github.com/vorlocsev/dvinholm/actions/runs/38431344776).
- Post-merge main CI for PR #173 passed: [run #38431140577](https://github.com/vorlocsev/dvinholm/actions/runs/38431140577). Post-merge main CI for PR #174 was still running at last check: [run #38432161527](https://github.com/vorlocsev/dvinholm/actions/runs/38432161527).
- Current main at this handoff update: `c29b71f11f95b8cde7498bbed59ca66384a47db0`.

## Current change under review
- New branch: `fix/redact-j2534-session-exception-details`.
- `J2534Session.ConnectLocked` previously returned `Exception.Message` for unexpected adapter exceptions; vendor/loader exceptions can include local paths. It now returns a stable, actionable generic message and still reports cleanup failures separately.
- `TryUnloadAdapter` no longer copies arbitrary vendor exception text into user-facing cleanup errors.
- Added regression coverage that injects a path-bearing adapter exception and asserts the local path is not exposed. Updated cleanup lifecycle assertions for the sanitized cleanup message.
- CI must pass on the exact PR head before merging. Do not claim local-path privacy coverage is exhaustive: adapter methods that return their own error strings still need case-by-case review.

## Follow-up audit
- Passive ECU discovery is receive-only and has tests for response classification, cancellation/partial observations, and queue overflow; no new defect was found in this pass.
- UDS requests and passive discovery are serialized by the UI gate. Manual Windows UI smoke tests remain outstanding.
- Physical DiCE/Mongoose adapter validation is explicitly deferred to a future release; virtual tests do not establish hardware compatibility.

## Next priorities
1. Finish CI review and merge the J2534 session exception-redaction change only if the exact-head checks pass.
2. Continue case-by-case review of adapter-returned error strings without indiscriminately removing useful diagnostics.
3. Perform manual Windows smoke tests: first launch, missing/invalid adapter DLL, connect/disconnect, UDS cancellation, ECU profile import/export.
4. Keep physical DiCE/Mongoose tests deferred to a future release and track them as such.

---

# Latest handoff update — 2026-10-13 (after PR #171)

## Verified repository state
- PR #170 — CSV export success status now shows only the filename — merged as `930caf243e00756bc355dde124e49376891e9564`. Both exact-head workflows passed: [PR CI #38427177793](https://github.com/vorlocsev/dvinholm/actions/runs/38427177793) and [push CI #38427159625](https://github.com/vorlocsev/dvinholm/actions/runs/38427159625). Post-merge CI for that commit passed: [run #38428017878](https://github.com/vorlocsev/dvinholm/actions/runs/38428017878).
- PR #171 — file-operation error messages no longer surface raw exception details that may contain local paths — merged as `d3048e336cad5bdb235170aaa717e85d1b60c96d`: [PR #171](https://github.com/vorlocsev/dvinholm/pull/171). Both exact-head workflows passed: [PR CI #38428150610](https://github.com/vorlocsev/dvinholm/actions/runs/38428150610) and [push CI #38428132719](https://github.com/vorlocsev/dvinholm/actions/runs/38428132719).
- Post-merge CI for current main SHA `d3048e336cad5bdb235170aaa717e85d1b60c96d` is running: [run #38428848113](https://github.com/vorlocsev/dvinholm/actions/runs/38428848113). Do not mark this main commit fully validated until the run completes successfully.
- Physical DiCE/Mongoose adapter tests remain deferred to a future release. Interactive Windows UI smoke tests have not been performed in this environment and remain open.

## Follow-up audit
- Reviewed remaining status messages in `MainWindow.xaml.cs` and error construction in `J2534Session.cs`.
- Raw adapter/transport exception details are still shown for connect, disconnect, and receive errors because they may be necessary to diagnose vendor-driver failures. No blanket redaction was applied; review these messages only if a concrete sensitive-data leak is identified.
- ECU discovery and UDS operation failures likewise retain diagnostic detail; distinguish actionable protocol/driver errors from filesystem paths before changing behavior.

## Next priorities
1. Verify post-merge CI run [#38428848113](https://github.com/vorlocsev/dvinholm/actions/runs/38428848113).
2. Continue the software-MVP workflow audit with emphasis on connection lifecycle, receive-loop error recovery, UDS cancellation, and incomplete ECU discovery; add regression tests only for concrete gaps not already covered.
3. Keep manual Windows first-launch, adapter-discovery failure, connect/disconnect, diagnostic cancellation, and profile import/export UI smoke tests open until performed.
4. Run the full CI pipeline on a release candidate when software-MVP criteria are met. Physical adapter validation remains a future-release item.

---

# Latest handoff update — 2026-10-13 (file-operation error privacy)

## Verified state
- PR #169 merged as `88fd22a1b449ce96410e85a2d082cdbba0818401`; its exact-head CI passed: [run #38425845117](https://github.com/vorlocsev/dvinholm/actions/runs/38425845117).
- PR #170 merged as `930caf243e00756bc355dde124e49376891e9564`; both exact-head workflows passed: [pull request CI #38427177793](https://github.com/vorlocsev/dvinholm/actions/runs/38427177793) and [push CI #38427159625](https://github.com/vorlocsev/dvinholm/actions/runs/38427159625).
- Post-merge CI for main SHA `930caf243e00756bc355dde124e49376891e9564` was still running at handoff refresh: [run #38428017878](https://github.com/vorlocsev/dvinholm/actions/runs/38428017878). Verify its final conclusion before marking main fully validated.
- Real DiCE/Mongoose adapter testing remains deferred to a future release. Interactive Windows UI smoke tests remain open.

## Latest privacy review
- PR #169 changed ECU profile export status from full `file.Path` to `file.Name`.
- PR #170 changed CSV export success status to display only the selected filename.
- A follow-up source review found file-operation exception messages can also include local paths. The current follow-up replaces exception details in ECU profile load/import/export and CSV export status messages with task-specific guidance. This intentionally avoids displaying raw exception text in the UI; it does not add a diagnostic log, and full exception logging remains a separate design decision.
- Current follow-up branch: `fix/redact-file-operation-exception-details`; validate its exact-head CI before merge.

## Next priorities
1. Verify post-merge CI for PR #170.
2. Verify CI on the exact head of the file-operation error privacy follow-up before merge.
3. Continue reviewing connection/transport errors case by case; do not remove useful adapter troubleshooting details without evidence they expose sensitive data.
4. Keep manual Windows first-launch, connect/disconnect, cancellation, and profile UI smoke tests open until performed.
5. Keep physical-adapter validation deferred to a future release.

---



# Latest handoff update — 2026-10-13 (after PR #164)

## Verified repository state

- PR #162 — CSV export error handling — merged as `dfee67bfbb3f74c514a552598fc60be25c7b0145`: [PR #162](https://github.com/vorlocsev/dvinholm/pull/162). Both pre-merge CI workflows passed and post-merge CI passed: [run #38416605055](https://github.com/vorlocsev/dvinholm/actions/runs/38416605055).
- PR #163 — AI handoff refresh — merged as `a1c5cc76f5f5a48b9c1ddbbff9313de695221c64`: [PR #163](https://github.com/vorlocsev/dvinholm/pull/163). Both workflows passed on exact head `8a968c2848c35dbce1210db55b80c583b03ceafc): [run #38416772137](https://github.com/vorlocsev/dvinholm/actions/runs/38416772137) and [run #38416811071](https://github.com/vorlocsev/dvinholm/actions/runs/38416811071).
- PR #164 — ECU profile file-picker error handling — merged as `49a14ab35f878d79b3ecad390504dadfc4baf9e6`: [PR #164](https://github.com/vorlocsev/dvinholm/pull/164). Both workflows passed on exact head `23a86b383a2cad7c5d750810e3f3269f4fb6edaa`: [run #38416982961](https://github.com/vorlocsev/dvinholm/actions/runs/38416982961) and [run #38417015897](https://github.com/vorlocsev/dvinholm/actions/runs/38417015897).
- Post-merge CI for the current main commit `49a14ab35f878d79b3ecad390504dadfc4baf9e6` is in progress: [run #38418202970](https://github.com/vorlocsev/dvinholm/actions/runs/38418202970). Verify it before marking this merge fully validated.
- No open PRs were reported after merging #164.
- Physical DiCE/Mongoose and in-vehicle validation remain deferred to a future release cycle. Virtual J2534 CI does not establish physical-hardware compatibility.

## Recently completed

### PR #164 — ECU profile file-picker error handling
- Wrapped picker initialization and asynchronous picker operations for ECU profile import/export in the existing exception-handling path.
- Failures now appear in the status bar instead of escaping the async-void UI handlers.
- Cancellation remains a normal no-op; file read/write error reporting is retained.
- Both CI workflows passed for the exact PR head before merge.
- Merge commit: `49a14ab35f878d79b3ecad390504dadfc4baf9e6`.
- Post-merge CI pending: [run #38418202970](https://github.com/vorlocsev/dvinholm/actions/runs/38418202970).

### PR #163 — AI handoff refresh
- Updated `AI_WORKLOG.md` with PR #162's implementation and verification state.
- Merge commit: `a1c5cc76f5f5a48b9c1ddbbff9313de695221c64`.

## Next steps

1. Verify post-merge CI run [#38418202970](https://github.com/vorlocsev/dvinholm/actions/runs/38418202970).
2. Continue the software-MVP workflow audit: adapter discovery failure, connect/disconnect, diagnostic cancellation, negative UDS responses, and incomplete ECU discovery.
3. Exercise ECU profile import/export and profile selection through realistic repeatable user workflows; unit tests do not replace interactive UI smoke tests.
4. Keep README roadmap status honest; do not mark manual smoke tests complete without evidence.
5. When software-MVP criteria are met, run the full CI pipeline on a release candidate and publish versioned release notes. Physical adapter testing remains a future-release item.


---

# Dvinholm — AI Project Handoff / Work Log

> **Purpose:** This is the persistent handoff file for an AI assistant continuing development of Dvinholm. Read it before making changes, verify its claims against the current repository and GitHub Actions, then update it whenever work is completed, a PR is merged, CI changes, or priorities change.
>
> **Last updated:** 2026-10-13  
> **Repository:** https://github.com/vorlocsev/dvinholm  
> **Main branch at last verification:** `925c9629e7932fc418d5525593495ca8ce036e81`  
> **Current state:** PRs #155, #156, #158, #159, and #160 are merged. PR #160 head CI passed both workflows; post-merge CI for main commit `925c9629e7932fc418d5525593495ca8ce036e81` is pending/in progress ([run #38348052286](https://github.com/vorlocsev/dvinholm/actions/runs/38348052286)). Real adapter validation remains deferred.

## 1. Project purpose and guardrails

Dvinholm is a Windows WinUI 3 application for CAN monitoring and **read-only UDS diagnostics** over 32-bit SAE J2534 drivers, intended for Volvo interfaces such as DiCE and Mongoose JLR Pro.

Project constraints:
- Keep the application x86 to support legacy 32-bit J2534 DLLs.
- Keep the built-in ECU profile catalog empty unless vehicle-specific IDs/DIDs have a reliable source.
- A profile or observed CAN response is not proof that an ECU supports a particular diagnostic request.
- Keep the current feature set read-only. Do not add ECU writes/actuation, immobilizer/PIN extraction, brute force, security bypass, or automated security-key calculation.
- Use `VirtualJ2534Adapter` for repeatable automated tests.
- **Do not make physical DiCE/Mongoose adapter tests a blocker for the current software MVP.** Real hardware and vehicle validation belong to a future release cycle.
- Never claim tests, builds, artifacts, or hardware compatibility succeeded unless verified from actual results.

## 2. Current architecture and functionality

Architecture documented in `README.md`:
- `J2534Session` owns device lifecycle and native adapter access.
- `CanBus` provides the shared CAN abstraction and serializes transmit operations.
- `CanRxDispatcher` owns the single `PassThruReadMsgs` loop and distributes frames to subscribers.
- CAN monitor and ISO-TP/UDS consumers use the shared bus; they must not compete by reading directly from J2534.
- `IsoTpChannel` supports single-/multi-frame Classic CAN transfers, Flow Control, Block Size, STmin, sequence validation, deadlines, cancellation, and recovery.
- `UdsClient` supports DID reads (`0x22`), VIN (`0xF190`), and DTC reads (`0x19`, subfunction `0x02`), including negative-response handling and `ResponsePending (0x78)`.
- ECU profile catalog supports JSON import/export and validation; the catalog intentionally starts empty.

Key source and test locations:
- `src/Dvinholm.App/J2534Session.cs`
- `src/Dvinholm.App/CanBus.cs`
- `src/Dvinholm.App/CanRxDispatcher.cs`
- `src/Dvinholm.App/IsoTp.cs`
- `src/Dvinholm.App/IsoTpChannel.cs`
- `src/Dvinholm.App/UdsClient.cs`
- `src/Dvinholm.App/UdsEcuDiscovery.cs`
- `src/Dvinholm.App/EcuProfile.cs`
- `src/Dvinholm.App/MainWindow.xaml.cs`
- `tests/Dvinholm.App.Tests/`

## 3. Completed work and merged changes

### PR #143 — deferred disconnect/reconnect race
- Fixed a race where deferred cleanup from an old receive worker could tear down a reconnected J2534 session.
- CI run [#38308753118](https://github.com/vorlocsev/dvinholm/actions/runs/38308753118) passed; reported **394 passed, 0 failed, 0 skipped**.
- Merge commit: `c84727eab113b2da943d44e5582459a7c342f652`.

### PR #144 — roadmap documentation
- Updated roadmap documentation to reflect completed work and remaining tasks.

### PR #145 — Windows artifact installation/troubleshooting
- Documented how to retrieve and run the CI-published x86 artifact, driver/DLL bitness constraints, and troubleshooting.
- Merge commit: `6cf4356033aefd3f785de3dd2ca13de9cc19d81f`.
- Main CI run [#38311110213](https://github.com/vorlocsev/dvinholm/actions/runs/38311110213) succeeded.

### PR #146 — clear stale DID when changing profile
- When selecting an ECU profile with no candidate readable DIDs, the DID input is now cleared instead of retaining a DID from the previously selected profile.
- CI passed and PR was merged.
- Merge commit: `62b9fee3007ba249180dfba666e18b8885abc177`.
- Main CI run [#38312976717](https://github.com/vorlocsev/dvinholm/actions/runs/38312976717) succeeded.

### PR #147 — preserve partial ECU discovery results on queue overflow
- Added `UdsEcuDiscoveryIncompleteException`, which carries observed candidates and a `long DroppedFrameCount`.
- Discovery constructs partial candidates before reporting receive-queue overflow.
- UI now displays observed ECUs and an explicit incomplete-scan warning rather than discarding all results.
- Regression test verifies that the exception preserves a candidate with response ID `0x7E8` and a positive dropped-frame count.
- The test ensures an initial response frame has been observed before injecting enough traffic to overflow the queue.
- One CI failure was corrected: dropped-frame counts are `long`, not `int`.
- PR: https://github.com/vorlocsev/dvinholm/pull/147
- Merge commit / current main SHA: `648bb26d7785a1da387f07a651890eb275e987b3`.
- Latest main CI run [#38314520952](https://github.com/vorlocsev/dvinholm/actions/runs/38314520952) completed successfully.

### PR #149 — strict hexadecimal prefix parsing
- Fixed CAN ID and DID parsing so an optional `0x` prefix is removed only when it appears at the beginning of the input.
- Previously, `Replace("0x", ...)` removed every occurrence and could silently normalize malformed inputs such as `0x0x7E0` or `7E0x`.
- Added regression cases for repeated, trailing, and misplaced prefixes.
- PR: https://github.com/vorlocsev/dvinholm/pull/149
- Merge commit / main SHA: `d7f2ee4d78e566bd3581859962a0da3803677278`.
- PR CI run [#38334827794](https://github.com/vorlocsev/dvinholm/actions/runs/38334827794) completed successfully on head `815fe3d651a4613e0d311df5d327d80d30ad8f50`.
- Post-merge main CI run [#38335347983](https://github.com/vorlocsev/dvinholm/actions/runs/38335347983) completed successfully.

### PR #150 — update AI handoff after PR #149
- Updated the main SHA and recorded the strict hexadecimal-prefix parsing fix.
- PR: https://github.com/vorlocsev/dvinholm/pull/150
- PR CI run [#38335382708](https://github.com/vorlocsev/dvinholm/actions/runs/38335382708) completed successfully on head `2cb2d29966177df549196704e1f0d771c74bc2af`.
- Squash merge commit: `ae3e88dbf5fd21297d924724e52ca368f78f3248`.
- Post-merge main CI run [#38335912173](https://github.com/vorlocsev/dvinholm/actions/runs/38335912173) was running at the time of this update; verify its final result.

### PR #151 — handoff refresh after PR #150
- Updated the project handoff to reflect strict hexadecimal-prefix parsing.
- Merged main commit: `fc8f5723cb5b3cbc3335472d8bfbc115b2c6c582`; main CI [#38336331712](https://github.com/vorlocsev/dvinholm/actions/runs/38336331712) passed.

### PR #152 — invalid J2534 connection input tests
- Added regression coverage for invalid J2534 connection inputs.
- Merged main commit: `fd4ae52f138e9cc1b650b05cf184b3b765feba4b`; main CI [#38336803668](https://github.com/vorlocsev/dvinholm/actions/runs/38336803668) passed.

### PR #153 — clear stale ECU profile details
- Clears profile metadata when the profile catalog is replaced or no profile is selected, while preserving editable CAN IDs.
- PR CI runs [#38336837568](https://github.com/vorlocsev/dvinholm/actions/runs/38336837568) and [#38336845017](https://github.com/vorlocsev/dvinholm/actions/runs/38336845017) passed on head `657e3b106e8f740476ac0ee69999ab090521915a`.
- Squash merge / current main SHA: `79222621b6553444fa003756fcb110e4087eff0a`.
- Post-merge main CI [#38337294839](https://github.com/vorlocsev/dvinholm/actions/runs/38337294839) was queued at the time of this update.

### PR #155 — ECU profile catalog persistence
- Extracted JSON persistence into `EcuProfileCatalogStore`, including temporary-file write and replacement.
- Added tests for missing files, round-trip persistence, nested directories, replacement, malformed JSON, and blank paths.
- Merged commit: `6aa7a93fb76b3c25beae82c055105db3814003bb`.
- Post-merge main CI [#38344517556](https://github.com/vorlocsev/dvinholm/actions/runs/38344517556) passed.

### PR #156 — preserve partial ECU discovery results on cancellation
- Passive discovery now throws `UdsEcuDiscoveryCancelledException` carrying observed candidates when cancellation happens after the worker starts.
- UI retains partial candidates and explicitly labels the scan as cancelled/incomplete.
- Regression test uses the virtual J2534 adapter and verifies an observed responder is preserved without transmitting probe requests.
- PR CI passed on exact head `008dd6bc850e97cfbb08ad38ab136845d8ab0588`: [run #38344580641](https://github.com/vorlocsev/dvinholm/actions/runs/38344580641).
- Squash merge / current main SHA: `885bd063ffdc0ab11a2d7be93ccc6cecca586b8b`.
- Post-merge main CI [#38344935929](https://github.com/vorlocsev/dvinholm/actions/runs/38344935929) was still running at this update; verify its final conclusion.

### PR #81 — isolate CAN dispatcher from faulty subscription filters
- A subscription predicate exception is reported and skipped only for that subscription; other subscribers still receive the frame.
- Merged main commit: `d6865872846218317507394a27436c8c88207615`; post-merge main CI [#38159874429](https://github.com/vorlocsev/dvinholm/actions/runs/38159874429) passed.

### PR #82 — rate-limit repeated CAN filter errors
- Reports the first faulty-filter exception immediately, then limits repeated reports to one per second per subscription.
- Added a regression test publishing 100 frames through a throwing filter.
- Merged main commit: `cc67a32372430420da93394595b36d7edb61b872`.
- The latest main CI history confirms subsequent main commits passed; verify a dedicated PR #82 run if needing the exact head-level check. Tests use an in-memory adapter, not physical hardware.

## 4. Current CI and verification state

- Current `main` SHA: `885bd063ffdc0ab11a2d7be93ccc6cecca586b8b` (PR #156).
- Post-merge main CI [#38344935929](https://github.com/vorlocsev/dvinholm/actions/runs/38344935929) is in progress as of this update. Do not mark main fully verified until this run completes successfully.
- PR #156's full Windows x86 CI passed on its exact head [#38344580641](https://github.com/vorlocsev/dvinholm/actions/runs/38344580641).
- PR #155's post-merge main CI passed [#38344517556](https://github.com/vorlocsev/dvinholm/actions/runs/38344517556).
- Pipeline covers .NET restore, build, tests, publish, and artifact upload.
- CI uses a virtual J2534 adapter. It does not prove real driver, electrical, adapter, vehicle, or ECU compatibility.

## 5. Remaining work — prioritized roadmap

### Priority 1 — complete user-facing software MVP
- [ ] Audit connection-state and transport-error messages for actionable guidance and consistent recovery.
- [ ] Ensure ECU discovery clearly distinguishes complete, incomplete, cancelled, timed-out, and failed scans.
- [ ] Review negative UDS response presentation, including service, NRC, and readable description.
- [ ] Test realistic ECU profile workflows: import, validation failure, persistence across restart, export, selection, and empty-profile behavior.
- [ ] Add regression tests for any real defects discovered in these workflows.

### Priority 2 — repeatable smoke and lifecycle tests
- [ ] Add/verify hardware-independent smoke coverage for first-run behavior and no registered J2534 DLL.
- [ ] Cover connect/disconnect and cancellation with `VirtualJ2534Adapter`, where the UI/backend seams allow it.
- [ ] Check that repeated connect/disconnect, failed connect, and read errors leave no stale UI state or duplicate receive workers.
- [ ] Review diagnostic logs/error messages for usefulness while ensuring VINs and other identifying data are not unnecessarily exposed.
- [ ] Prefer testing a meaningful workflow end-to-end over adding isolated tests with little user value.

### Priority 3 — release readiness
- [ ] Run the full CI pipeline on the intended release candidate.
- [ ] Verify the published artifact is available and the installation instructions match its actual contents.
- [ ] Create a versioned GitHub Release with concise notes and known limitations.
- [ ] Clearly distinguish software verified by virtual-adapter CI from hardware compatibility that remains unverified.

### Future release cycle — physical adapters (not a current blocker)
- [ ] Validate passive CAN capture with a real 32-bit J2534 adapter.
- [ ] Check repeated connect/disconnect and recovery on DiCE and/or Mongoose JLR Pro.
- [ ] Check vendor DLL loading, real baud-rate behavior, read errors, and hardware-filter behavior.
- [ ] Attempt VIN/DTC reads only on ECUs with confirmed support and documented addressing.
- [ ] Record adapter model, driver/DLL version, vehicle/ECU, bus rate, IDs, operation, and outcome; redact VINs and identifying information before sharing.

## 6. Recommended next action

A focused audit of connection state, diagnostic cancellation, and recovery paths has been performed against current source and existing tests. The repository already has broad virtual-adapter coverage for connect/disconnect races, disposal during active UDS requests, cancellation after ResponsePending, timeout recovery, late responses, and reconnect isolation. Avoid duplicating these tests without a demonstrated gap.

Next:
1. Verify post-merge main CI [#38344935929](https://github.com/vorlocsev/dvinholm/actions/runs/38344935929) for SHA `885bd063ffdc0ab11a2d7be93ccc6cecca586b8b`.
2. Continue with a concrete user-visible gap in ECU profile workflows or connection/read-error recovery; inspect current code/tests before editing.
3. Add a targeted regression test for any confirmed defect and run the full Windows x86 CI.
4. Confirm the PR head SHA matches successful CI, merge, and verify post-merge main CI.
5. Keep this file updated with verified SHAs and links.

Physical DiCE/Mongoose tests remain deferred to a future release and are not a blocker for the current software MVP.

## 7. Rules for future AI assistants maintaining this file

- Read this file and the current `README.md` before continuing development.
- Re-check repository `main`, open PRs, recent commits, and current Actions runs; this file is a handoff, not an infallible live status API.
- Update **Last updated**, main SHA, completed work, CI evidence, and next actions after meaningful milestones.
- Preserve links to PRs and workflow runs. Record failures and their fixes honestly.
- Do not mark an item complete merely because code was written; require tests/CI evidence appropriate to the item.
- Keep physical adapter validation under the future-release section until explicitly rescheduled.
