# Latest handoff update — 2026-10-13 (after PR #159)

## Verified repository state

- Current `main` commit: `33476e722797d83407a702c4661eaf20010e4061` — [PR #159 merge](https://github.com/vorlocsev/dvinholm/pull/159).
- PR #158 added readable labels for the eight UDS DTC status bits and was merged as `6fa62b67b2262845b74f57442dad9493dba7ee89`: [PR #158](https://github.com/vorlocsev/dvinholm/pull/158).
- PR #159 refreshed this handoff with the #158 implementation and validation details. Its exact head `e1e1a2c354dda3a82615f3a9ed9540cb9d610ac7` passed CI: [run #38347041731](https://github.com/vorlocsev/dvinholm/actions/runs/38347041731).
- Post-merge CI for `main` at `6fa62b67b2262845b74f57442dad9493dba7ee89` passed: [run #38346990382](https://github.com/vorlocsev/dvinholm/actions/runs/38346990382).
- Post-merge CI for current `main` SHA `33476e722797d83407a702c4661eaf20010e4061` is running: [run #38347486844](https://github.com/vorlocsev/dvinholm/actions/runs/38347486844). Confirm its conclusion before marking this merge fully verified.
- No GitHub release is published yet. The repository's open issue list was empty at this check.
- Real DiCE/Mongoose and in-vehicle validation remain deferred to a future release cycle. CI with `VirtualJ2534Adapter` does not establish physical-hardware compatibility.

## Recently completed

### PR #158 — readable UDS DTC status flags
- DTC results retain the raw `status=0xXX` byte and append readable labels for the eight standard ISO 14229 status bits.
- Tests cover zero status, each individual bit, combined flags, and all bits set.
- Display-only interpretation: no root cause/severity inference and no additional diagnostic request.

### PR #159 — AI worklog refresh
- Updated the handoff with #158's implementation, exact tested SHA, CI evidence, and next steps.
- Merge commit: `33476e722797d83407a702c4661eaf20010e4061`.

## Next steps

1. Verify post-merge CI run [#38347486844](https://github.com/vorlocsev/dvinholm/actions/runs/38347486844).
2. Complete software-MVP user-workflow review: adapter discovery failure, connect/disconnect, diagnostic cancellation, negative UDS responses, and incomplete ECU discovery.
3. Exercise ECU profile import/export and profile selection through repeatable user workflows; unit coverage exists for validation and persistence, but that alone is not an end-to-end UI test.
4. Refresh README roadmap status to reflect implemented diagnostics/reporting and profile catalog work; do not mark manual UI smoke tests complete without evidence.
5. Once software-MVP criteria are satisfied, run the full CI pipeline on a release candidate and publish versioned release notes. Physical adapter testing remains a future-release item.

---

# Dvinholm — AI Project Handoff / Work Log

> **Purpose:** This is the persistent handoff file for an AI assistant continuing development of Dvinholm. Read it before making changes, verify its claims against the current repository and GitHub Actions, then update it whenever work is completed, a PR is merged, CI changes, or priorities change.
>
> **Last updated:** 2026-10-13  
> **Repository:** https://github.com/vorlocsev/dvinholm  
> **Main branch at last verification:** `33476e722797d83407a702c4661eaf20010e4061`  
> **Current state:** PRs #155, #156, #158, and #159 are merged. Post-merge CI for #158 passed ([run #38346990382](https://github.com/vorlocsev/dvinholm/actions/runs/38346990382)); post-merge CI for current main commit #159 (`33476e722797d83407a702c4661eaf20010e4061`) was in progress at the last check ([run #38347486844](https://github.com/vorlocsev/dvinholm/actions/runs/38347486844)). Real adapter validation remains deferred.

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
