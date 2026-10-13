# Dvinholm — AI Project Handoff / Work Log

> **Purpose:** This is the persistent handoff file for an AI assistant continuing development of Dvinholm. Read it before making changes, verify its claims against the current repository and GitHub Actions, then update it whenever work is completed, a PR is merged, CI changes, or priorities change.
>
> **Last updated:** 2026-10-13  
> **Repository:** https://github.com/vorlocsev/dvinholm  
> **Main branch at last verification:** `79222621b6553444fa003756fcb110e4087eff0a`  
> **Current state:** Software MVP in progress; PR #153 is merged. Main CI for `79222621b6553444fa003756fcb110e4087eff0a` is queued (run [#38337294839](https://github.com/vorlocsev/dvinholm/actions/runs/38337294839)); the latest fully verified main commit is `fd4ae52f138e9cc1b650b05cf184b3b765feba4b`, whose main CI [#38336803668](https://github.com/vorlocsev/dvinholm/actions/runs/38336803668) passed. Real adapter validation is intentionally deferred.

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

### PR #81 — isolate CAN dispatcher from faulty subscription filters
- A subscription predicate exception is reported and skipped only for that subscription; other subscribers still receive the frame.
- Merged main commit: `d6865872846218317507394a27436c8c88207615`; post-merge main CI [#38159874429](https://github.com/vorlocsev/dvinholm/actions/runs/38159874429) passed.

### PR #82 — rate-limit repeated CAN filter errors
- Reports the first faulty-filter exception immediately, then limits repeated reports to one per second per subscription.
- Added a regression test publishing 100 frames through a throwing filter.
- Merged main commit: `cc67a32372430420da93394595b36d7edb61b872`.
- The latest main CI history confirms subsequent main commits passed; verify a dedicated PR #82 run if needing the exact head-level check. Tests use an in-memory adapter, not physical hardware.

## 4. Current CI and verification state

- Latest fully verified `main` commit: `fd4ae52f138e9cc1b650b05cf184b3b765feba4b`; post-merge main CI [#38336803668](https://github.com/vorlocsev/dvinholm/actions/runs/38336803668) passed.
- Current `main` commit: `79222621b6553444fa003756fcb110e4087eff0a` (PR #153); post-merge CI [#38337294839](https://github.com/vorlocsev/dvinholm/actions/runs/38337294839) was queued when this file was updated.
- Pipeline covers .NET restore, build, tests, publish, and artifact upload.
- A previously reported suite result was **394 passed, 0 failed, 0 skipped** in run #38308753118. Do not assume the exact count is unchanged without inspecting the latest run's test logs.
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

Continue with a focused audit of profile-selection state and smoke-test seams. Current profile JSON parsing/validation and catalog round-trip tests are present; persistence and UI-level selection behavior still need stronger deterministic coverage. Inspect current code/tests before editing and identify a concrete user-visible gap. Then:
1. Run the relevant tests and full Windows CI.
2. Inspect logs if anything fails; fix the cause rather than weakening the test.
3. Confirm the PR head SHA matches the successful CI run.
4. Merge only after the relevant checks succeed.
5. Verify the post-merge `main` workflow.
6. Update this handoff file with the change, PR/commit, CI URL and result, and the new next action.

Do not create a long chain of tiny speculative changes. Batch closely related fixes where safe and keep the roadmap aligned with the actual code and test evidence.

## 7. Rules for future AI assistants maintaining this file

- Read this file and the current `README.md` before continuing development.
- Re-check repository `main`, open PRs, recent commits, and current Actions runs; this file is a handoff, not an infallible live status API.
- Update **Last updated**, main SHA, completed work, CI evidence, and next actions after meaningful milestones.
- Preserve links to PRs and workflow runs. Record failures and their fixes honestly.
- Do not mark an item complete merely because code was written; require tests/CI evidence appropriate to the item.
- Keep physical adapter validation under the future-release section until explicitly rescheduled.
