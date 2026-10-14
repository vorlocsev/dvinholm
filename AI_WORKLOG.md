# Latest handoff update — 2026-10-14 (post-merge main CI verified; artifact recorded)

## Verified current checkpoint
- PR [#213 — Draft first software MVP release notes and checklist](https://github.com/vorlocsev/dvinholm/pull/213) is merged. Current `main` head: `1c9f1977ce54b3d03388c1675cc43af709d6a12d`.
- Post-merge workflow [run #38612886605](https://github.com/vorlocsev/dvinholm/actions/runs/38612886605) completed successfully on that exact commit. Restore, Build, Test, Publish, and Upload artifact all passed.
- Artifact: `Dvinholm-Windows-x86`, 66,752,947 bytes, non-expired at the time checked. GitHub-reported digest: `sha256:70b48e99371fc6b7f7cfb46635ccd0016123a14cf5c52e5b103e1bb2962acd3d`. This digest is GitHub metadata; no independent local ZIP hash/integrity check has been performed on this artifact.
- Artifact run: [download from GitHub Actions run #38612886605](https://github.com/vorlocsev/dvinholm/actions/runs/38612886605). It is a portable self-contained Windows x86 build, not an installer.
- This is a green CI checkpoint, not a versioned release candidate. No version tag or public GitHub Release exists.
- User's manual Windows checks were performed on earlier artifacts; they do not constitute a fresh manual smoke test of this exact artifact.
- Physical J2534 adapter validation remains deferred to a future release cycle.

## Next actions
1. Review/choose the release version and scope; keep the read-only MVP boundary.
2. If claiming a release candidate, independently download and inspect the exact artifact ZIP and executable architecture, and record the evidence.
3. Ensure release notes, tag, commit, and artifact all refer to the same exact candidate SHA.
4. Keep no-provider UI and read-error/cancellation UI recovery paths marked unverified where noted.
5. Do not create a version tag or public release without explicit user approval.

---

# Latest handoff update — 2026-10-14 (main CI green; release notes drafted)

## Verified current checkpoint
- PR [#212 — Record green main CI and current artifact](https://github.com/vorlocsev/dvinholm/pull/212) is merged into main as 65ea0af8447bd746a16261199eba4952b1753c6c.
- Post-merge main CI [run #38611124491](https://github.com/vorlocsev/dvinholm/actions/runs/38611124491) completed successfully on that exact SHA. Restore, Build, Test, Publish, and Upload artifact all passed.
- Current run artifact: Dvinholm-Windows-x86, 66,752,774 bytes, non-expired. GitHub metadata digest: sha256:d836c8f4c965d6fbbfe47b76b18c39a1f3c5874cb63f00466a44a9998392ed98. This is the digest reported by GitHub; no independent local ZIP hash verification was performed in this checkpoint.
- The artifact is a portable self-contained x86 build, not an installer.
- Added RELEASE_NOTES.md as a draft, documenting the proposed software MVP scope, current evidence, known limitations, and publication checklist. No version number, tag, or public GitHub Release has been created.
- Physical J2534 adapter validation remains deferred to a future release cycle.

## Next actions
1. Review the release-notes draft and decide the version/scope before calling anything a release candidate.
2. Independently download and inspect the exact artifact if making a final release-candidate claim; compare local archive integrity/hash and executable architecture with the GitHub-reported metadata.
3. Keep the exact candidate SHA and artifact linked together; rerun the full workflow if any source or packaging change is made.
4. Resolve only genuinely blocking release issues. Do not force the no-provider UI path by uninstalling/disabling the user's installed drivers.
5. Create a tag/public release only after explicit approval; do not infer permission to publish from “continue.”

---

# Latest handoff update — 2026-10-14 (main CI green; current artifact recorded)

## Verified current checkpoint
- PR [#211 — Document final software-only regression audit](https://github.com/vorlocsev/dvinholm/pull/211) is merged. The current `main` head is `d650eae04cc19b8925d0f51ebba1bd467aa78b7d`.
- Post-merge `main` CI [run #38609609211](https://github.com/vorlocsev/dvinholm/actions/runs/38609609211) completed successfully on that exact SHA. Restore, Build, Test, Publish, and Upload artifact all passed.
- The run uploaded non-expired artifact `Dvinholm-Windows-x86`, 66,753,021 bytes. GitHub's artifact metadata reports digest `sha256:785cde6193925fe7bdc58430a93557e112803e53393495f58295e2c87c0fba8b`. This is the service-reported artifact digest; this update does not claim an independent local ZIP integrity check for this newest artifact.
- The artifact is available from the [successful main workflow run](https://github.com/vorlocsev/dvinholm/actions/runs/38609609211). It is a portable self-contained x86 build, not an installer.
- No version tag or public GitHub Release has been created. The latest green `main` build is a verified checkpoint, but a versioned release candidate still needs an explicit scope/version decision and its own exact-SHA validation.

## User-confirmed manual Windows checks (2026-10-14)
- Valid JSON ECU profile import works and the profile name now appears in the selector.
- Malformed JSON and syntactically valid JSON with invalid request CAN ID `ZZZZ` are rejected; the previously imported valid profile remains available.
- Duplicate profile IDs are rejected. The user did not separately verify preservation of the previous catalog for this specific duplicate-ID case.
- An empty catalog `[]` is accepted and persists after restart; the selected profile's dependent fields clear when the catalog is cleared.
- Export followed by re-import restores the profile. Duplicate handling on re-import was not separately checked.
- The synthetic `Example ECM (test only)` profile shows request ID `7E0`, response ID `7E8`, 11-bit addressing, and candidate DID `F187`/`F190`.
- On Windows 11 IoT LTSC 26100.9550, the app starts, discovers installed J2534 drivers, and reports the absent physical device without hanging. A clean-folder artifact launch/close/relaunch and the explicit window title were also user-confirmed on the preceding artifact.
- No physical J2534 device was available. Physical adapter and vehicle compatibility remain deferred to a future release cycle.

## Next actions
1. Treat the latest main CI as green, but do not call it a published release or a separately tagged release candidate.
2. Keep the release scope read-only and limited to demonstrated software behavior; retain the empty built-in ECU profile catalog.
3. For the release-candidate stage, choose a version/scope, create a versioned candidate commit/tag only when appropriate, and run the full workflow on that exact SHA.
4. Review the latest artifact locally only if a new packaging or binary change is introduced; prior user smoke checks do not automatically validate every subsequent artifact.
5. Keep UI-only no-provider and read-error/cancellation control-state paths marked unverified where noted in the roadmap. Do not ask the user to uninstall or disable drivers merely to force the no-provider path.
6. Keep physical J2534 adapter validation deferred. Virtual-adapter CI does not certify vendor-driver behavior or vehicle compatibility.

---

# Latest handoff update — 2026-10-14 (partial manual ECU-profile UI check)

## User-confirmed result
- The user manually imported a JSON ECU profile catalog in the Windows UI and confirmed the workflow works after resolving an initially missing profile name in the selection list.
- This is evidence for the valid-import/profile-name display path only. It does **not** verify persistence after restart, export/re-import, malformed JSON/fields, duplicate IDs, empty catalog, or clearing/replacement behavior.
- No source code changed in this checkpoint; no CI result is claimed for the manual UI observation.

## Next actions
1. Continue manual profile validation with malformed JSON and invalid fields; verify actionable errors and no silent acceptance.
2. Test save/restart persistence and export/re-import.
3. Test catalog replacement/clearing and stale CAN ID/DID prevention.
4. Fix only demonstrated defects, add focused regression tests, and require exact-head Windows x86 CI.
5. Keep clean-install verification outstanding. Physical adapter validation remains deferred to a future release cycle.

---

# Latest handoff update — 2026-10-14 (PR #203 merged; artifact inspected)

## Verified current state
- PR [#202 — Record diagnostic-data audit and roadmap to MVP release](https://github.com/vorlocsev/dvinholm/pull/202) is merged into `main` as `a8ddf1c735865566af0c5be222be1e9b956b3528`; exact-head CI passed: [run #38508715796](https://github.com/vorlocsev/dvinholm/actions/runs/38508715796).
- PR [#203 — Record post-roadmap CI and artifact verification](https://github.com/vorlocsev/dvinholm/pull/203) is merged into `main` as `8a886fb36f66b6c14f3befefea22294667bfc28d). Exact-head PR CI passed: [run #38509696244](https://github.com/vorlocsev/dvinholm/actions/runs/38509696244); post-merge `main` CI passed: [run #38510453531](https://github.com/vorlocsev/dvinholm/actions/runs/38510453531).
- Artifact `Dvinholm-Windows-x86` from run #38509199082 is non-expired and has size 66,752,802 bytes. The downloaded ZIP passed archive integrity verification, contains 476 entries, and includes `Dvinholm.App.exe` and `RestartAgent.exe`.
- PE headers for both executables report machine `0x014c` (x86). Artifact SHA-256: `4ee5d67302d31c38f83d07884673d28674b2fbed8088cfdf6cc1bf0b752fdc27`.
- This is a self-contained portable build artifact, not an installer. Archive integrity and x86 PE headers do not prove clean-install or physical-adapter compatibility.
- The app project declares `PlatformTarget=x86`, `RuntimeIdentifier=win-x86`, `SelfContained=true`, `WindowsAppSDKSelfContained=true`, and `WindowsPackageType=None`.
- No public GitHub Releases were found in the last repository review.

## Work completed in this continuation
- Confirmed post-merge CI for PR #203 completed successfully on the exact `main` merge commit.
- Downloaded and inspected the published Windows artifact: archive integrity, file inventory, launch executable presence, and PE architecture verified.
- Updated the README release roadmap and this handoff record with the verified evidence. Manual UI and clean-install checks remain explicitly incomplete.

## Remaining blockers / next actions
1. Manually exercise the ECU profile workflow in Windows UI: valid import, selection, invalid JSON/fields/duplicate IDs, empty catalog, persistence after restart, export/re-import, replacement/clearing, and stale-field prevention. Do not mark complete from source inspection alone.
2. Perform clean-install/first-launch checks on the supported Windows baseline and confirm the published artifact's launch procedure on a clean environment.
3. Review the README install/troubleshooting instructions against the artifact and record any issues observed during clean-install testing.
4. After the above gates, freeze release scope and create a versioned tag/release candidate with CI on that exact SHA; no public release should be claimed until it is actually published.
5. Keep real DiCE/Mongoose validation deferred to a future release cycle; virtual-adapter CI does not certify physical hardware compatibility.

---

# Latest handoff update — 2026-10-14 (PR #200 merged; Windows LTSC smoke test recorded)

## Verified current state
- PR [#200 — Record verified Windows LTSC startup smoke test](https://github.com/vorlocsev/dvinholm/pull/200) merged into `main` as `60ad78d51559fdb382e8d5eae6575fe49c42ce8c`.
- Both exact-head CI runs passed: [run #38507056036](https://github.com/vorlocsev/dvinholm/actions/runs/38507056036) and [run #38507047898](https://github.com/vorlocsev/dvinholm/actions/runs/38507047898).
- README roadmap now records the user's manual smoke check on Windows 11 IoT LTSC 26100.9550: the app starts, detects and permits selection of installed J2534 drivers, and reports the missing physical device without hanging.
- This confirms startup and the no-device path only. It does not establish clean-install coverage or compatibility with a physical DiCE/Mongoose adapter.
- Previous registry-discovery fixes (#195, #196, #198) and their CI results remain recorded below. PR #197/#198/#199 updates and post-merge CI were verified in the preceding handoff.

## Focused audit findings
- `EcuProfileCatalogStore.Save` writes to a sibling temporary file and replaces the destination only after a successful write; its `finally` block performs best-effort cleanup without masking the original failure. Existing tests cover round-trip, directory creation, replacement, malformed input, blank paths, and temp-file cleanup.
- The profile-import UI intentionally applies a validated catalog in memory before persisting it. If local persistence fails, it reports the partial result explicitly rather than claiming the catalog was saved. Avoid duplicating the existing store tests; future work should target a demonstrable uncovered UI or protocol behavior.
- The main window catches import/export exceptions and displays actionable but non-sensitive guidance. A broader diagnostic-log review remains on the roadmap.

## Next steps
1. Continue a targeted audit of connect/disconnect, UDS cancellation, and user-visible failure paths; only add a regression test when a concrete untested behavior is identified.
2. Keep clean-install/first-launch coverage on the roadmap for supported Windows versions.
3. Run the release-candidate pipeline and publish a versioned release with notes when the release checklist is ready.
4. Keep physical DiCE/Mongoose testing deferred to a future release cycle; virtual-adapter CI is not physical-hardware validation.

---

# Latest handoff update — 2026-10-14 (PR #197 and #198 merged; post-merge CI pending)

## Verified current state
- PR [#198 — Handle registry security exceptions during J2534 discovery](https://github.com/vorlocsev/dvinholm/pull/198) merged into `main` as `b821dd5b927dce918e5d140d14be40a54325bbf7`.
  - Exact-head CI passed: [run #38505982457](https://github.com/vorlocsev/dvinholm/actions/runs/38505982457) and [run #38505973732](https://github.com/vorlocsev/dvinholm/actions/runs/38505973732).
  - Registry discovery now tolerates `SecurityException` alongside unauthorized-access and I/O failures, including failure to open the registry root and failure on an individual vendor entry.
  - Regression coverage exercises all three root-read failure types and verifies that discovery can continue with another root.
- PR [#197 — Update roadmap and AI handoff for J2534 discovery fixes](https://github.com/vorlocsev/dvinholm/pull/197) merged as `e5041c74cf107b886f0bdc3fe3afe0d9e136191d`.
  - Exact-head CI passed: [run #38505934699](https://github.com/vorlocsev/dvinholm/actions/runs/38505934699) and [run #38505925624](https://github.com/vorlocsev/dvinholm/actions/runs/38505925624).
- Post-merge CI for current `main` is not yet confirmed:
  - [run #38506492221](https://github.com/vorlocsev/dvinholm/actions/runs/38506492221) for `b821dd5b927dce918e5d140d14be40a54325bbf7`.
  - [run #38506505103](https://github.com/vorlocsev/dvinholm/actions/runs/38506505103) for `e5041c74cf107b886f0bdc3fe3afe0d9e136191d`.
- Physical DiCE/Mongoose adapter tests remain deferred to a future release cycle and are not a current software-MVP blocker. User confirmed Windows 11 IoT LTSC 26100.9550 startup, adapter-driver discovery/selection, and graceful missing-device reporting, but has not tested with a physical adapter.

## Next steps
1. Confirm both post-merge `main` CI runs above finish successfully.
2. Inspect PR #194 (Windows 11 IoT LTSC manual smoke-test documentation) and decide whether its status/content should be updated based on the user's observed results.
3. Continue the targeted audit on a concrete user-facing gap in ECU profile import/export/selection or actionable connection/read-error reporting; inspect source and existing tests before changing code.
4. Keep the roadmap honest: virtual-adapter CI and successful startup do not establish physical DiCE/Mongoose hardware compatibility.

---

# Latest handoff update — 2026-10-14 (PR #195 and #196 merged)

## Verified current state
- PR [#195 — Reject malformed normalized J2534 DLL paths](https://github.com/vorlocsev/dvinholm/pull/195) merged into `main`.
  - Both exact-head CI runs passed: [#38504955044](https://github.com/vorlocsev/dvinholm/actions/runs/38504955044) and [#38504946299](https://github.com/vorlocsev/dvinholm/actions/runs/38504946299).
  - Merge commit: `f705160f60f3c88fa48d849eef457da988979756`.
  - Rejects DLL paths that normalize to empty and trims whitespace inside quoted paths.
- PR [#196 — Isolate J2534 registry root discovery failures](https://github.com/vorlocsev/dvinholm/pull/196) merged into `main`.
  - Both exact-head CI runs passed: [#38505408610](https://github.com/vorlocsev/dvinholm/actions/runs/38505408610) and [#38505420071](https://github.com/vorlocsev/dvinholm/actions/runs/38505420071).
  - Merge commit: `5074ceacb852af44a102651bac666d92c2a6263d`.
  - A read failure in the `PassThruSupport.04.04` registry root is isolated so discovery continues with `PassThruSupport.02.02`; added regression coverage for unauthorized access and I/O failures.
- Physical DiCE/Mongoose adapter tests remain deferred to a future release cycle and are not a current software-MVP blocker.

## Next steps
1. Verify latest post-merge CI on `main` after commit `5074ceacb852af44a102651bac666d92c2a6263d`.
2. Continue auditing a concrete user-facing gap in ECU profile import/export/selection or actionable connection/read-error reporting; inspect source and existing tests before changing code.
3. Keep README roadmap honest and preserve the explicit distinction between virtual-adapter CI and physical hardware validation.
4. PR #194 (Windows 11 IoT LTSC manual smoke-test documentation) remains open as last checked; inspect before deciding whether to close/merge.

---

# Latest handoff update — 2026-10-14 (PR #192 merged; lifecycle/protocol audit resumed)

## Verified current state
- PR [#192 — Test isolation between J2534 read-error subscribers](https://github.com/vorlocsev/dvinholm/pull/192) is merged into `main`.
- Exact-head CI passed: [run #38479265252](https://github.com/vorlocsev/dvinholm/actions/runs/38479265252).
- Post-merge `main` CI passed on merge commit `a7d53215309be8cd1e5da350f5cb1c8bfdc60231`: [run #38479905456](https://github.com/vorlocsev/dvinholm/actions/runs/38479905456).
- The test confirms that a throwing `ReadError` subscriber does not prevent later subscribers from receiving the notification.
- PR #191 post-merge CI also passed: [run #38479178610](https://github.com/vorlocsev/dvinholm/actions/runs/38479178610).

## Audit checkpoint
- Rechecked the test inventory for CAN dispatcher/bus disposal, ISO-TP flow control, timeout/cancellation/recovery, and UDS response-pending behavior. The repository already has focused regression tests across these cases; avoid creating duplicates without a specific uncovered branch or failure.
- ECU profile catalog persistence has tests for missing files, nested-directory creation, round-trip, replacement, malformed JSON, and invalid paths.
- User confirmed Windows 11 IoT LTSC startup and missing-device recovery. This does not validate physical J2534 hardware behavior.

## Next actions
1. Continue from the current `main` and inspect concrete untested edge cases in the implementation, not just add more tests by count.
2. For each identified gap, add a deterministic virtual-adapter regression test, update this handoff, and require exact-head CI before merging.
3. Keep manual UI checks (including profile import/export and UDS cancellation) as pending when not personally observed.
4. Physical DiCE/Mongoose validation remains deferred to a future release and is not a blocker for the current software MVP.

---

# Latest handoff update — 2026-10-14 (PR #191 merged; next read-error isolation test)

## Verified state
- PR [#191](https://github.com/vorlocsev/dvinholm/pull/191) merged as `ac03fb58587513e8e544dcfb50cb3b1fa15efee1`.
- Exact-head CI for PR #191 succeeded: [run #38478544199](https://github.com/vorlocsev/dvinholm/actions/runs/38478544199).
- Post-merge CI for PR #190 succeeded: [run #38478370417](https://github.com/vorlocsev/dvinholm/actions/runs/38478370417).
- Post-merge CI for PR #191 is queued/running: [run #38479178610](https://github.com/vorlocsev/dvinholm/actions/runs/38479178610). Confirm success before treating the latest main head as validated.

## Current candidate
- Branch: `test/j2534-read-error-subscriber-isolation`.
- Adds a test proving one throwing `J2534Session.ReadError` subscriber does not prevent subsequent subscribers from receiving the same read failure.
- Await exact-head CI and open/merge only after confirming it passes.

## Remaining validation
- User confirmed startup and missing-device recovery on Windows 11 IoT LTSC 26100.9550.
- Physical adapter testing remains deferred to a future release; virtual-adapter CI does not establish DiCE/Mongoose hardware compatibility.
- Continue targeted lifecycle and UDS/ISO-TP test audit; do not duplicate already covered scenarios.

---

# Latest handoff update — 2026-10-14 (PR #190 merged; callback-disposal test in PR #191)

## Verified current state
- PR [#190](https://github.com/vorlocsev/dvinholm/pull/190) is merged to `main` as `f8835013b8004334e49e7c99da897aed0b075d2b`.
- Exact-head CI for PR #190 passed: [run #38476343163](https://github.com/vorlocsev/dvinholm/actions/runs/38476343163).
- Post-merge CI for PR #190 is running: [run #38478370417](https://github.com/vorlocsev/dvinholm/actions/runs/38478370417). Do not treat the merged commit as fully validated until this run succeeds.
- The user confirmed app startup and no-device recovery on Windows 11 IoT LTSC 26100.9550; no physical adapter was connected.

## Current change — PR #191
- [PR #191](https://github.com/vorlocsev/dvinholm/pull/191) adds a regression test for calling `J2534Session.Dispose()` from the native read-error callback.
- The test checks that unload is deferred until the callback returns, metadata is cleared, and the disposed session rejects reconnect.
- Exact-head CI must pass before merge.

## Next priorities
1. Verify exact-head CI for PR #191 and post-merge CI for PR #190.
2. Merge PR #191 only after its exact-head CI succeeds.
3. Continue targeted audit for concrete lifecycle/protocol gaps; avoid duplicating existing tests.
4. Keep manual UI checks and physical DiCE/Mongoose validation as outstanding future-release tasks; virtual-adapter CI does not prove physical hardware compatibility.

---

# Latest handoff update — 2026-10-13 (PR #189 merged; J2534 DLL-load failure coverage)

## Verified current state
- PR [#189](https://github.com/vorlocsev/dvinholm/pull/189) is merged to `main` as `d1801015759279e679f31bf2ff02d20b14b953ce`.
- Exact-head CI passed after making the synchronous cancellation test use dedicated long-running workers: [run #38475455802](https://github.com/vorlocsev/dvinholm/actions/runs/38475455802).
- Post-merge CI for the new main commit is running: [run #38476182155](https://github.com/vorlocsev/dvinholm/actions/runs/38476182155). Do not call the merge fully validated until it succeeds.
- User confirmed the app starts and remains responsive on Windows 11 IoT LTSC 26100.9550 without a physical adapter. This verifies startup/no-device recovery only; physical DiCE/Mongoose validation remains deferred.

## Current change — J2534 DLL load failure cleanup
- Added `FailurePoint.Load` to the lifecycle fake adapter and included load failure in `FailedConnectionReleasesAdapterAndClearsMetadata`.
- This closes a specific test gap: the session's failure branch when the vendor DLL cannot be loaded was not represented by the lifecycle fake, although Open/Connect/version-read failures were.
- The test checks that the connection attempt fails, metadata stays clear, receive never starts, and cleanup is attempted. Exact-head CI is required before merge.

## Next priorities
1. Validate the exact head of the DLL-load cleanup test PR and merge only after required checks pass.
2. Verify post-merge main CI for PR #189.
3. Continue targeted audit only for concrete untested lifecycle/protocol gaps; avoid redundant tests.
4. Complete manual UI checks when practical: invalid adapter DLL, CAN monitor start/stop, UDS cancellation, and ECU profile import/export.
5. Physical DiCE/Mongoose testing remains deferred to a future release; virtual-adapter CI does not establish hardware compatibility.

---

# Latest handoff update — 2026-10-13 (LTSC smoke test confirmed; PR #189 in CI)

## Verified current state
- Current main commit: `e0bf90c49984f6e885ebde22fa0d7596d755cac6`, merged from [PR #188](https://github.com/vorlocsev/dvinholm/pull/188).
- Post-merge main CI passed: [run #38473688013](https://github.com/vorlocsev/dvinholm/actions/runs/38473688013).
- User manually confirmed the published x86 app starts on **Windows 11 IoT LTSC 26100.9550**, discovers and allows selection of installed J2534 drivers, reports that the device is absent when connecting without physical hardware, and remains responsive after the failed connection attempt.
- This confirms startup and the no-device error/recovery UI path only. No physical J2534 adapter was available; DiCE/Mongoose hardware validation remains deferred to a future release.

## Current change — PR #189
- [PR #189](https://github.com/vorlocsev/dvinholm/pull/189) adds a regression test for a failed J2534 Open followed by a successful retry.
- The test checks that a failed attempt clears connection state and adapter metadata, and that a later successful attempt can connect and disconnect cleanly.
- Exact-head Windows CI run [#38474567407](https://github.com/vorlocsev/dvinholm/actions/runs/38474567407) failed after build succeeded: 428 tests passed and `IsoTpRequestGateCancellationTests.ConcurrentRequest_CancellationWhileWaitingForChannelDoesNotSendRequest` timed out waiting for cancellation. This is an unrelated timing-sensitive test using `Task.Run` for synchronous blocking requests. Updated it to use dedicated long-running workers to avoid thread-pool starvation; the updated exact-head CI must pass before merge. Do not infer success from a queued or in-progress run.

## Next priorities
1. Verify exact-head CI after the cancellation-test reliability fix; merge PR #189 only after all required checks pass.
2. Continue targeted coverage review for a concrete connection/recovery gap; avoid adding redundant tests for lifecycle paths already covered by the virtual adapter.
3. Complete remaining manual UI checks where practical: invalid DLL path, CAN monitor start/stop, UDS cancellation, and ECU profile import/export.
4. Publish a versioned release after software tests and packaging checks are green.
5. Keep physical DiCE/Mongoose validation in a future release; virtual-adapter CI does not establish hardware compatibility.

---

# Latest handoff update — 2026-10-13 (PR #186 merged; main CI verified)

## Verified current state
- Current main commit: `6169fb96c6137a84298bc43d928197da118c7bf0`, merged from [PR #186](https://github.com/vorlocsev/dvinholm/pull/186), following [PR #185](https://github.com/vorlocsev/dvinholm/pull/185).
- PR #185 adds regression tests for removal of control characters and empty/whitespace/null driver diagnostic input.
- Both exact-head CI workflows passed on `b93d2be1699228eced1f73a098dc1e7ad6edafb4`: [run #38445747278](https://github.com/vorlocsev/dvinholm/actions/runs/38445747278) and [run #38445753609](https://github.com/vorlocsev/dvinholm/actions/runs/38445753609).
- Post-merge main CI for PR #185 passed: [run #38447304696](https://github.com/vorlocsev/dvinholm/actions/runs/38447304696). PR #186's two exact-head checks passed: [run #38447441281](https://github.com/vorlocsev/dvinholm/actions/runs/38447441281) and [run #38447465100](https://github.com/vorlocsev/dvinholm/actions/runs/38447465100). PR #186 is merged as `6169fb96c6137a84298bc43d928197da118c7bf0`; main CI for that exact commit passed: [run #38461560275](https://github.com/vorlocsev/dvinholm/actions/runs/38461560275).
- PR #184's post-merge main CI passed: [run #38444768532](https://github.com/vorlocsev/dvinholm/actions/runs/38444768532); main CI before PR #185 passed on `27353c478c8908eb5645feab5df0c4c09f953bb5`: [run #38444850871](https://github.com/vorlocsev/dvinholm/actions/runs/38444850871).

## PR #185 implementation
- Added tests confirming control characters are removed from driver diagnostics.
- Added tests for empty, whitespace-only, and null input.
- Existing tests continue to cover common Windows, UNC, and Unix path redaction, useful text preservation, and bounded output.
- This sanitizer only covers common path patterns and control characters; do not claim universal redaction of arbitrary vendor-specific secrets.

## Next priorities
1. Continue targeted review of connection recovery, CAN-filter lifecycle, and UDS cancellation only where a concrete coverage gap exists; the reviewed virtual-adapter tests already cover passive UDS filtering, queue overflow/partial results, cancellation preservation, adapter cleanup retry, reconnect, and read-error callback lifecycle.
2. Perform manual Windows UI smoke tests: first launch, missing/invalid adapter DLL, connect/disconnect, UDS cancellation, and ECU profile import/export.
3. Physical DiCE/Mongoose testing remains deferred to a future release; virtual-adapter CI does not establish hardware compatibility.

---

# Latest handoff update — 2026-10-13 (main CI verified; sanitizer boundary tests in progress)

## Current verified state
- Current main commit: `27353c478c8908eb5645feab5df0c4c09f953bb5`.
- Post-merge CI for this main commit passed: [run #38444850871](https://github.com/vorlocsev/dvinholm/actions/runs/38444850871).
- Post-merge CI for PR #184 merge commit `689997227b787cb38c5ffed86459c3b2f8bfc66d` passed: [run #38444768532](https://github.com/vorlocsev/dvinholm/actions/runs/38444768532).
- PR #182 is merged as `27353c478c8908eb5645feab5df0c4c09f953bb5`; its branch checks also passed: [run #38444817089](https://github.com/vorlocsev/dvinholm/actions/runs/38444817089) and [run #38444805983](https://github.com/vorlocsev/dvinholm/actions/runs/38444805983).
- PR #183 and #184 are merged; exact-head and post-merge checks recorded below in the historical handoff sections.

## Current task
- Existing `J2534NativeTests` cover Windows/UNC/Unix path redaction, preservation of useful text, and output length.
- This follow-up adds boundary tests for control-character removal and blank/null diagnostics; validate the exact PR head with CI before merging.
- The sanitizer covers common path formats only; do not describe it as universal secret redaction.

## Remaining release-readiness items
1. Continue targeted review of connection recovery and CAN-filter/cancellation edge cases where a concrete coverage gap exists.
2. Run manual Windows UI smoke tests: first launch, missing/invalid adapter DLL, connect/disconnect, UDS cancellation, and ECU profile import/export.
3. Physical DiCE/Mongoose testing remains deferred to a future release. Virtual-adapter CI does not establish real-hardware compatibility.

---

# Latest handoff update — 2026-10-13 (PR #184 merged; post-merge CI pending)

## Verified current state
- PR #183, sanitizing unexpected UI exception details, is merged as `94d2749487a48cf04dea85cdf6f98f9e151b06bb`: https://github.com/vorlocsev/dvinholm/pull/183
- Main CI for PR #183 merge commit passed: [run #38442172350](https://github.com/vorlocsev/dvinholm/actions/runs/38442172350).
- PR #184, **sanitize UDS client initialization errors**, is merged: https://github.com/vorlocsev/dvinholm/pull/184
- Both exact-head CI workflows passed on `944e2e82c0c2e15fb2f536634020aea90d60603d`: [run #38442322027](https://github.com/vorlocsev/dvinholm/actions/runs/38442322027) and [run #38442343261](https://github.com/vorlocsev/dvinholm/actions/runs/38442343261).
- PR #184 merge commit is `689997227b787cb38c5ffed86459c3b2f8bfc66d`. Post-merge main CI must still be checked before calling this commit fully validated.
- PR #182 documents the prior PR #181 work and now contains this handoff refresh. Its latest exact-head CI passed on `83772c9761d3fb377d7f267f006e18908ac0d500`: [run #38442229306](https://github.com/vorlocsev/dvinholm/actions/runs/38442229306) and [run #38442236240](https://github.com/vorlocsev/dvinholm/actions/runs/38442236240). Its base is behind main after PR #184; refresh/revalidate before merging.

## PR #184 implementation
- `TryGetUdsClient` no longer passes raw exception messages from ISO-TP/UDS client initialization into the UI.
- It uses the existing safe exception formatter and a stable fallback message.
- This is one more targeted hardening step, not a guarantee that every possible diagnostic string is safe.

## Next priorities
1. Verify post-merge main CI for PR #184.
2. Refresh PR #182 against current main and rerun exact-head CI before merging the handoff update.
3. Continue auditing exception paths, CAN filter errors, adapter cleanup, and connection recovery.
4. Perform manual Windows UI smoke tests: first launch, missing/invalid adapter DLL, connect/disconnect, UDS cancellation, and ECU profile import/export.
5. Physical DiCE/Mongoose adapter checks remain deferred to a future release; virtual-adapter CI does not establish hardware compatibility.

---

# Latest handoff update — 2026-10-13 (PR #183 merged; post-merge CI pending)

## Verified current state
- PR #181 is merged; its exact-head CI passed, and post-merge main CI for `f16180e39bcc85f1971d8eafe1ae95cc446b1b84` passed: [run #38441234579](https://github.com/vorlocsev/dvinholm/actions/runs/38441234579).
- PR #183, **sanitize unexpected UI exception details**, is merged: https://github.com/vorlocsev/dvinholm/pull/183
- PR #183 exact-head CI passed on `0c61cfd7bb350975c9d6be607a4e557697ce1d2e`: [run #38441464431](https://github.com/vorlocsev/dvinholm/actions/runs/38441464431) and [run #38441487671](https://github.com/vorlocsev/dvinholm/actions/runs/38441487671).
- PR #183 merge commit is `94d2749487a48cf04dea85cdf6f98f9e151b06bb`. Post-merge main CI is running: [run #38442172350](https://github.com/vorlocsev/dvinholm/actions/runs/38442172350). Do not mark the merged commit fully validated until this run succeeds.
- PR #182 updates this handoff file after PR #181. Its prior exact-head CI passed on `230f67f657c5c58aa13c0cb4bc8fb169ef9d203a`: [run #38441323773](https://github.com/vorlocsev/dvinholm/actions/runs/38441323773) and [run #38441342943](https://github.com/vorlocsev/dvinholm/actions/runs/38441342943). This documentation branch is now behind main due to PR #183; refresh/revalidate before merging.

## PR #183 implementation
- Unexpected errors shown for ECU discovery, connection/disconnection, and generic UDS failures now pass through the existing diagnostic sanitizer.
- Specific UDS negative-response and timeout messages remain unchanged so useful protocol diagnostics are preserved.
- The change does not prove every vendor-specific diagnostic string is safe; continue the audit case by case.

## Next priorities
1. Verify post-merge main CI [run #38442172350](https://github.com/vorlocsev/dvinholm/actions/runs/38442172350).
2. Refresh PR #182 against current main and rerun exact-head CI before merging the documentation update.
3. Continue the audit of exception paths and recovery behavior without exposing raw vendor exception details or suppressing useful UDS errors.
4. Perform manual Windows UI smoke tests: first launch, missing/invalid adapter DLL, connect/disconnect, UDS cancellation, and ECU profile import/export.
5. Physical DiCE/Mongoose adapter checks remain deferred to a future release; virtual-adapter CI does not establish hardware compatibility.

---

# Latest handoff update — 2026-10-13 (PR #181 merged; post-merge CI pending)

## Verified current state
- PR #181, **redact CAN bus cleanup exception details**, is merged: https://github.com/vorlocsev/dvinholm/pull/181
- Merge commit / current main at this update: `f16180e39bcc85f1971d8eafe1ae95cc446b1b84`.
- PR #181 branch head before merge: `43d8b6243ae1b63751ae1abbfa675457808ddf76`.
- Both exact-head CI workflows passed on that PR head: [run #38440546383](https://github.com/vorlocsev/dvinholm/actions/runs/38440546383) and [run #38440538829](https://github.com/vorlocsev/dvinholm/actions/runs/38440538829).
- Post-merge main CI is queued for merge commit `f16180e39bcc85f1971d8eafe1ae95cc446b1b84`: [run #38441234579](https://github.com/vorlocsev/dvinholm/actions/runs/38441234579). Do not mark the merge fully validated until this run succeeds.
- The first test run on an earlier head failed because existing dispatcher lifecycle tests expected raw exception text. Updated those tests to assert the stable, redacted message; both final exact-head workflows are green.

## PR #181 implementation
- `J2534Session` no longer includes raw CAN bus cleanup exception details in a user-facing error.
- `CanRxDispatcher` converts unexpected adapter read exceptions—including unexpected `OperationCanceledException` thrown by a vendor adapter—to a stable generic message, while preserving receive-loop recovery behavior.
- Existing lifecycle tests were updated to expect the redacted read error.
- `MainWindow.xaml.cs` now passes receive-error text through `J2534Native.SanitizeDriverDiagnostic` before displaying it in the status bar, preventing common local paths/control characters from leaking through that UI path while retaining sanitized diagnostic text.
- Removed unused exception variables introduced by generic error handling.

## Next priorities
1. Verify post-merge main CI [run #38441234579](https://github.com/vorlocsev/dvinholm/actions/runs/38441234579).
2. Continue a case-by-case audit of error paths that display adapter-provided text. The sanitizer covers common path forms, not every possible vendor-specific secret or diagnostic format; preserve useful technical details where safe.
3. Perform manual Windows UI smoke tests: first launch, missing/invalid adapter DLL, connect/disconnect, UDS cancellation, and ECU profile import/export.
4. Physical DiCE/Mongoose adapter checks remain deferred to a future release; virtual-adapter CI does not establish hardware compatibility.

---

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


---

# Latest handoff update — 2026-10-14 (diagnostic-data exposure audit)

## Verified current state
- Main at the start of this audit: `b760e05d1b080b3128595566a960db1dc1a9dddf` (PR #201 merged).
- Main CI for that SHA passed: [run #38507992192](https://github.com/vorlocsev/dvinholm/actions/runs/38507992192).
- Audited current `MainWindow.xaml.cs`, `J2534Native.cs`, `CanBus.cs`, `UdsClient.cs`, `UdsEcuDiscovery.cs`, and `IsoTpChannel.cs` for diagnostic logging, exception presentation, and persisted data.

## Findings
- No persistent diagnostic logger or automatic telemetry path was found in the reviewed application code.
- Driver diagnostics pass through `SanitizeDriverDiagnostic`, which redacts common Windows, UNC, and Unix-style local paths, strips control characters, and bounds output length. This is targeted path sanitization, not a universal secret-redaction guarantee.
- Unexpected UDS initialization/discovery errors use the existing safe formatter. Negative UDS responses show a structured service/NRC description.
- VIN is displayed as the result of the user's explicit Read VIN action; CAN frames can be persisted only through the user's explicit CSV export action. These are intentional diagnostic outputs, but exports may contain identifying data and should be redacted before sharing.
- No concrete defect was identified in this source-level audit. The README roadmap records the audit as complete with the above scope and limitation; repeat it if persistent logging or telemetry is introduced.

## Remaining priorities
1. Realistic interactive ECU profile import/export/selection checks remain pending; unit tests do not replace a manual UI workflow.
2. Repeatable application-level smoke tests for first launch, no registered adapter, connect/disconnect, and diagnostic cancellation remain release-readiness work where test seams permit.
3. Run release-candidate CI and publish versioned release notes when the software MVP checklist is ready.
4. Physical DiCE/Mongoose validation remains deferred to a future release cycle; virtual-adapter CI does not establish physical hardware compatibility.


---

# Roadmap to first software MVP release — 2026-10-14

This sequence is also reflected in the README roadmap. Do not repeat completed transport/lifecycle work unless a concrete regression or defect appears.

## Phase 0 — close audit and stabilize main
- [x] Source-level diagnostic-data exposure review completed; no persistent logger or automatic telemetry found in the reviewed code.
- [x] PR #202 merged as `a8ddf1c735865566af0c5be222be1e9b956b3528`; exact-head CI passed: [run #38508715796](https://github.com/vorlocsev/dvinholm/actions/runs/38508715796).
- [x] Post-merge `main` CI passed on the merge commit: [run #38509199082](https://github.com/vorlocsev/dvinholm/actions/runs/38509199082).

## Phase 1 — ECU profile workflows
- [x] Manually exercise valid import and persistence of the imported profile across an application restart (user-confirmed 2026-10-14).
- [ ] Exercise invalid fields/duplicate IDs, empty catalog, export/re-import, and catalog replacement/clearing.
- [ ] Verify changing or clearing a profile cannot leave stale CAN IDs/DIDs selected in the UI.
- [ ] Add regression coverage only for confirmed defects. Keep vehicle-specific built-in CAN IDs/DIDs unpopulated without reliable sources or measured evidence.

**Gate:** end-to-end profile workflows work or any UI-only gaps are clearly documented.

## Phase 2 — repeatable software smoke/recovery checks
- [ ] Cover first-run and no-registered-J2534-provider behavior where test seams permit.
- [ ] Exercise connect/disconnect, failed-connect cleanup, read-error recovery, and diagnostic cancellation with the virtual adapter.
- [ ] Verify the UI/session returns to a truthful recoverable state after cancellation/failure.
- [ ] Avoid duplicate lifecycle/timeout/late-response/reconnect tests already in the suite.
- [ ] Require successful restore, build, test, x86 publish, and artifact upload on the exact candidate SHA.

**Gate:** no known blocking software defects and repeatable coverage for core software paths.

## Phase 3 — packaging and installation
- [ ] Inspect published artifact architecture and contents; verify the README install path matches it.
- [ ] Run clean-install/first-launch checks on the supported Windows baseline. Existing Windows 11 IoT LTSC 26100.9550 evidence is startup/no-device only, not a clean-install matrix.
- [ ] Verify no-adapter/no-provider behavior and manual selection of a vendor 32-bit J2534 DLL.
- [ ] Document dependencies, troubleshooting, read-only scope, and limitations of virtual-adapter CI.

**Gate:** a new user can install/launch the published artifact and understand missing-adapter and no-ECU-response states.

## Phase 4 — release candidate and public release
- [ ] Freeze release scope; defer nonessential features and vehicle-specific assumptions.
- [ ] Run full Windows x86 CI on the exact release-candidate commit.
- [ ] Confirm tag/commit matches the validated artifact.
- [ ] Publish a versioned GitHub Release with supported features, installation requirements, known limitations, and tested environments.
- [ ] Explicitly state that real J2534 hardware, specific vehicles/ECUs, electrical compatibility, and vendor-driver behavior remain unverified.
- [ ] Verify the public release page and artifact are accessible and instructions match the published files.

**Release acceptance:** no known blocking defects; exact candidate CI is green; profile and no-adapter workflows are checked/documented; release artifact/tag/docs align; read-only scope and hardware-validation status are clear.

## Future cycle — physical hardware (not a first-release blocker)
- [ ] Passive CAN capture with a real 32-bit J2534 adapter.
- [ ] Repeated connect/disconnect/recovery on DiCE and/or Mongoose JLR Pro.
- [ ] Verify baud rate, native read errors, vendor DLL behavior, and hardware filters.
- [ ] Read VIN/DTC only on ECUs with confirmed support and documented addressing.
- [ ] Record adapter/driver/ECU details and redact identifying data.

## Execution rules
1. Re-check actual GitHub main/PR/Actions state before each step; this handoff may be stale.
2. Do not mark a phase complete based on code changes alone; require appropriate tests or recorded manual evidence.
3. For code fixes, add targeted regression tests, wait for exact-head CI success, merge, and verify post-merge main CI.
4. Physical-adapter validation remains a future-release task unless the release scope is explicitly changed.


---

# Latest handoff update — 2026-10-14 (malformed ECU profile JSON UI check)

## User-confirmed manual result
- The user attempted to import a deliberately truncated/malformed JSON catalog named invalid-profile.json, containing an unfinished object.
- The UI displayed an import error. This confirms the malformed JSON is surfaced to the user as an error.
- Not yet verified: whether the existing catalog/profile selection remains unchanged after the failed import; whether the app remains fully usable afterward; handling of invalid fields, duplicate IDs, and an empty catalog.
- Do not claim a crash-free or data-preservation result beyond the user's confirmation that an error appeared.

## Next actions
1. Verify that the previously imported valid profile is still present and selectable after the failed import.
2. Test a syntactically valid JSON catalog with invalid fields and duplicate IDs, plus an empty catalog.
3. Test save/restart persistence and export/re-import.
4. If any behavior is defective, reproduce it, add a focused regression test, run exact-head Windows x86 CI, and only then merge the fix.
5. Continue to defer physical-adapter validation to a future release.


## Follow-up manual result — malformed JSON import
- User confirmed the previously imported valid ECU profile remained present after the malformed JSON import error.
- This verifies error presentation plus preservation of the visible prior profile/selection in this manual scenario. It does not cover invalid-but-well-formed fields, duplicate IDs, empty catalogs, restart persistence, or export/re-import.


## Follow-up manual result — invalid CAN request ID import (2026-10-14)
- The user imported syntactically valid JSON with `requestId: "ZZZZ"` and reported that the UI displayed an import error.
- This confirms that this invalid CAN ID input was rejected in the manual UI scenario. It does not yet confirm whether the previously imported valid profile remains present/selectable after this specific failure.
- Other invalid fields, duplicate IDs, empty catalog, restart persistence, and export/re-import remain unverified.

- User follow-up: confirmed the previously imported valid profile remained present after the invalid CAN ID (`ZZZZ`) import error. This is separate from, and in addition to, the malformed-JSON preservation check.


## Follow-up manual result — profile persistence across restart (2026-10-14)
- User confirmed the previously imported valid ECU profile remained present after fully closing and restarting Dvinholm.
- The basic single-profile persistence smoke check is complete. This does not verify multiple profiles, catalog replacement/clearing, or export/re-import.
- Next manual check: export the current profile/catalog to JSON, then import that exported file and confirm the profile name and selectable entry are restored without duplicate or stale entries.


## Follow-up manual result — ECU profile export and re-import (2026-10-14)
- User reported that after exporting the profile/catalog and importing the exported JSON, the profile was restored.
- Mark the basic export/re-import workflow as complete for the tested profile.
- The user did not explicitly report whether a duplicate entry appeared; duplicate handling remains unverified, as do invalid fields, duplicate IDs, empty catalog, and replacement/clearing behavior.
- Next: check invalid fields/duplicate IDs and an empty catalog, then test profile replacement/clearing for stale CAN IDs or DIDs. Keep physical-adapter testing deferred to a future release.


## Follow-up manual result — duplicate ECU profile IDs (2026-10-14)
- User attempted to import a JSON catalog containing duplicate profile IDs and confirmed the import was rejected.
- Duplicate-ID validation is confirmed in this manual UI scenario. Preservation of the prior catalog after this particular rejection was not explicitly reported, so do not claim it was verified.
- Still open: other invalid fields, empty catalog, replacement/clearing and stale CAN ID/DID state. Basic single-profile import, malformed JSON rejection, invalid request-ID rejection, restart persistence, and export/re-import have been user-confirmed.
- Next manual check: import an empty catalog (or a valid catalog with an empty profile list, according to the supported JSON schema) and observe whether the UI handles the empty state cleanly. Then test profile replacement/clearing. Physical-adapter validation remains deferred to a future release.


## Follow-up manual result — empty ECU profile catalog (2026-10-14)
- User imported the syntactically valid JSON empty array `[]`; the application accepted it and displayed no profiles.
- Empty-catalog import/display is confirmed. This does not yet establish whether the app's prior catalog is intentionally replaced, persisted as empty after restart, or recoverable after an empty import; only the visible empty state was confirmed.
- The README Phase 1 checklist now marks empty-catalog import/display complete. Other invalid fields and profile replacement/clearing behavior remain open.
- Next manual check: import the one-profile test catalog again, select it, then import `[]` and verify the selection and dependent CAN ID/DID UI fields are cleared rather than left stale. If possible, restart after importing `[]` and confirm the empty catalog persists. Then test replacing the empty catalog with the test profile.
- Physical-adapter validation remains deferred to a future release and is not a blocker for the first software MVP.


## Follow-up manual result — empty catalog persists across restart (2026-10-14)
- After importing the empty catalog `[]`, the user restarted Dvinholm and confirmed the profile list remained empty.
- Confirmed: empty-catalog import/display and persistence across restart. This does not by itself verify whether stale profile selection, CAN IDs, or DID values are cleared from every dependent UI field.
- README Phase 1 now records the persistence result. Next targeted UI check: import/select the synthetic `Example ECM (test only)` profile, then import `[]` and inspect all profile-dependent selection/CAN ID/DID fields for stale values. Then import the synthetic profile again to verify recovery from empty state.
- Physical-adapter validation remains deferred to a future release and is not a blocker for the first software MVP.


## Follow-up manual result — profile clearing and recovery from empty catalog (2026-10-14)

- User confirmed the profile workflow works end-to-end: selecting the synthetic test profile, importing the empty catalog `[]`, verifying profile-dependent state is cleared, and importing the test profile again successfully.
- Marked the Phase 1 profile replacement/clearing UI check complete in README. Together with earlier checks, manual evidence now covers valid import/display, malformed JSON rejection with prior profile preserved, invalid request ID rejection with prior profile preserved, duplicate-ID rejection, empty catalog display and persistence, restart persistence for a profile, export/re-import, and recovery from empty state.
- No code defect was reported in this workflow, so no application-code change or new regression test is justified by this manual result alone.
- Remaining Phase 1 work: verify imported request/response IDs, addressing mode, and candidate DIDs are reflected correctly; consider multiple-profile persistence and whether re-importing an existing profile duplicates it. Then proceed to Phase 2 software smoke/recovery gaps, avoiding duplicate existing virtual-adapter tests.
- Keep physical J2534 adapter validation deferred to a future release and outside the first software MVP blocker list.


## Follow-up — scope the remaining profile-field UI check (2026-10-14)

- User confirmed there is no second profile available in the UI. Do not treat switching between two profiles as a blocker or ask the user to create a second profile just to continue.
- The remaining manual check is limited to the single synthetic `Example ECM (test only)` profile: confirm request ID `7E0`, response ID `7E8`, 11-bit addressing, and a candidate DID such as `F187` or `F190` appear in the corresponding fields after selecting it. No physical adapter or diagnostic request is needed.
- The README Phase 1 checklist now explicitly separates single-profile field mapping from the optional multi-profile switching check. Existing user-confirmed empty-catalog clearing/recovery result remains recorded as complete.
- Once field mapping is confirmed, proceed to Phase 2 by auditing existing virtual-adapter smoke/recovery coverage and CI gaps; do not duplicate tests already present. Physical J2534 hardware validation remains deferred to a future release.


## Follow-up manual result — selected profile field mapping (2026-10-14)

- User confirmed the displayed fields match the synthetic `Example ECM (test only)` profile: request CAN ID `7E0`, response CAN ID `7E8`, 11-bit addressing, and one of the profile's candidate DIDs (`F187`/`F190`).
- Phase 1 single-profile import/selection/field mapping is now manually checked alongside validation failures, persistence, export/re-import, and empty-catalog replacement/clearing.
- Switching between two profiles remains unverified because only one profile is currently available; do not block the MVP on that optional check.
- Next: audit the existing virtual-adapter and recovery tests for actual gaps before changing code. Do not duplicate coverage already present. Physical J2534 adapter testing remains deferred to a future release.


## Phase 2 audit — existing lifecycle and recovery test coverage (2026-10-14)

Reviewed the current `main` branch test/source files before proposing new tests.

Existing automated coverage confirmed by source inspection:
- `J2534SessionLifecycleTests`: invalid DLL path/baud inputs; failures at load/open/connect/version stages; cleanup and metadata clearing; retry after failed connection; unexpected exception sanitization for version/read errors; cleanup failure handling; reconnect replacement and disconnect metadata reset.
- `CanRxDispatcherStopDisposeRaceTests` and `CanBusDisposeFromCallbackTests`: read-error callback disposal, stop/dispose races, and ensuring external disposal waits for receive-worker completion without deadlock.
- `UdsClientDisposeLifecycleTests`, `IsoTpDisposeCancellationTests`, `IsoTpChannelWaitCancellationTests`, and related ISO-TP tests: cancellation/disposal of active requests, rejection of future requests, and cancellation while waiting for a busy channel.
- `VirtualJ2534AdapterTests` already exercise the virtual adapter through the real CAN/ISO-TP stack, including a normal read-only UDS response, filtering another ECU's response, and multi-frame response handling.

No tests were run during this source audit; this is a coverage review, not a claim of passing CI. Avoid adding redundant backend lifecycle/cancellation tests. The remaining likely gaps are first-run/no-provider behavior and UI-level status/button recovery after failed connect, read error, and cancellation. Next inspect the relevant UI state transitions and test seams, then add only focused coverage for a concrete gap. The manual profile-field mapping check was user-confirmed complete. Physical adapter validation remains deferred to a future release.


## Follow-up audit — adapter discovery UI and CI confirmation (2026-10-14)

- CI for the current documentation PR head `cdc2aad4d2a60070e6f80e7fb11b2bf887d920aa` completed successfully: workflow run `38597362723`, build job `115967424685`. This confirms the docs branch's current build/CI, not a new app-code change.
- Source inspection confirms `RefreshJ2534Devices` displays an explicit message when no registered adapter DLL is found and leaves the manual DLL path entry available. `J2534DeviceDiscoveryTests` already cover normalization of registry paths and resilience when one registry root throws access/I/O/security exceptions.
- User's current Windows machine detects adapter drivers, so the no-registered-provider UI state cannot be claimed as manually verified. Do not force the user to uninstall/disable drivers just to exercise this branch.
- Next practical manual check is failed-connection recovery with a deliberately nonexistent DLL path; this is safe without physical hardware. Expected: a clear connection failure, Connect returns to enabled state, monitor and diagnostic controls remain disabled, and correcting the path permits another attempt. Do not send diagnostic traffic.
- UI-level read-error/cancellation recovery remains a test-seam question; backend lifecycle/cancellation coverage already exists. Avoid speculative changes and duplicate tests. Physical adapter validation remains deferred to a future release.


## Manual result — failed-connect UI recovery (2026-10-14)

- User tested connecting with a deliberately nonexistent J2534 DLL path and confirmed that the **Connect** button became available again after the failed attempt.
- Marked the button-recovery portion of the Windows smoke checklist complete. Do not overstate the result: the user did not separately confirm the monitor/UDS controls remained disabled or that a corrected DLL path was retried.
- No application code change is warranted from this result; the observed recovery matches the existing connect-task cleanup path. No physical adapter or vehicle connection was involved.
- Next: inspect packaging/release readiness and confirm CI on the latest documentation head. Keep real-adapter verification deferred to future releases; UI read-error and diagnostic-cancellation recovery remain open until there is a reliable simulated UI test seam.


## Packaging/readme audit — connection-failure guidance (2026-10-14)

- Reviewed the published Windows artifact instructions and troubleshooting section on `main`: README correctly identifies `Dvinholm-Windows-x86` as a portable self-contained output rather than an installer, calls out the 32-bit J2534 DLL requirement, and warns that virtual-adapter CI does not certify physical hardware or a specific vehicle.
- Added a concise troubleshooting note that the Connect button should recover after a failed DLL load/connection and the user can correct the path/setup before retrying. This reflects the user-confirmed manual result without claiming unverified monitor/UDS control states.
- Existing package evidence is from main CI run #38509199082 (ZIP integrity, x86 PE headers, expected executables); this does not constitute a clean-install matrix. A clean-install/first-launch check and exact candidate-SHA CI remain release gates.
- Current main has no release tag/release candidate established by this audit. Next: verify CI for the updated documentation branch head, then continue software-only packaging checks. Physical adapter validation remains a future-release task.


## PR #206 merged — CI verified (2026-10-14)

- Merged documentation-only PR #206 to `main` as `0b3ca883511e4a4f3db3c465777bf670265ff38b` after confirming exact PR-head CI passed: [run #38600469100](https://github.com/vorlocsev/dvinholm/actions/runs/38600469100).
- Verified the CI job completed Restore, Build, Test, Publish, and Upload artifact successfully; artifact `Dvinholm-Windows-x86` was uploaded and has not expired.
- Updated the README roadmap to record PR #206 and close the exact-checkpoint-SHA CI item. This is not a final release-candidate CI run; a future candidate SHA must pass the complete pipeline independently.
- Remaining Phase 3 release gate is clean-install/first-launch verification and confirming instructions against the artifact; no installer is produced. Do not claim physical J2534 or vehicle compatibility. Real-adapter validation remains deferred to a future release.


## Manual packaging smoke result — clean-folder first launch (2026-10-14)

- User confirmed success after downloading the current `Dvinholm-Windows-x86` CI artifact, extracting it into a fresh separate folder, launching `Dvinholm.App.exe`, closing it, and launching it again on Windows 11 IoT LTSC 26100.9550.
- No startup/dependency error was reported during this clean-folder launch check. With no physical J2534 device connected, the app opened and reported that no device was present. This is a software packaging/first-launch smoke check, not a hardware compatibility test.
- README Phase 3 now records the first-launch/no-device check as complete. The no-registered-provider UI branch remains unverified because the user's system detects adapter drivers; do not ask the user to disable/uninstall drivers solely for this check.
- Remaining release gates: finish the software-only UI smoke/recovery review where meaningful, freeze the first MVP scope, then run the full CI pipeline on the exact release-candidate SHA and confirm the published artifact matches it. No public GitHub Release has been created by this step.
- Real J2534 adapter, vehicle/ECU compatibility, electrical behavior, and vendor-driver validation remain deferred to a future release and are not first-MVP blockers.


## Manual smoke result — application window title (2026-10-14)

- User confirmed the fresh Windows x86 CI build launched successfully after the `MainWindow.xaml` title change.
- The application window now uses the explicit title `Dvinholm — J2534 CAN Diagnostics` instead of the generic `WinUI Desktop` caption. The title change is merged to `main` in commit `5188e899556d6df153cb82dadb346041ba18e73a` (PR #209).
- This confirms the title/launch smoke check only; it does not add evidence for physical J2534 adapter or vehicle compatibility.
- Next: complete a focused final software-only regression/release-readiness review, then run and verify the full CI pipeline on the exact release-candidate commit before tagging/publishing. Physical-adapter validation remains deferred to a future release.


## Final software-only regression inventory — 2026-10-14

- Reviewed the current test inventory and key test files on `main` after the title-smoke documentation PR merged. Existing coverage includes J2534 session lifecycle and failed-connect retry/cleanup, CAN receive-dispatcher and serialized-send disposal races, ISO-TP flow-control deadlines/BS/STmin/sequence validation, cancellation and late-frame recovery, UDS negative/unrelated/pending/late-response handling, DTC/VIN parsing and multi-frame responses, ECU profile JSON validation/persistence, and end-to-end virtual-adapter CAN/ISO-TP/UDS flows.
- Decision: no additional backend test was added from this source audit because the likely candidates are already covered and no concrete uncovered backend defect was identified. This was a source-level review; it is not a substitute for the current main CI run or an exact release-candidate CI run.
- Still unverified: the UI branch when no J2534 provider is registered, and UI-only control-state recovery after read errors or cancellation. The user’s Windows machine detects adapter drivers, so no drivers should be removed/disabled merely to test this state. These limitations are documented and are not being represented as passed checks.
- Latest documentation PR #210 was merged as `f7cc6932e7aa6d413717fe03b56ec9ab7bcc0a9b`. Post-merge `main` CI run #38607858415 was still in progress when this entry was drafted; its final result must be checked before treating main as green.
- Next: confirm post-merge CI, freeze first-MVP scope, and run the full Windows x86 CI pipeline on the exact release-candidate commit. Do not tag or publish a public release without an explicit release decision. Physical J2534 adapter validation remains deferred to a future release.
