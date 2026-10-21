## Independent update-helper crash guard — 2026-10-21

- Verified main CI #2988 passed on prior Development baseline. Inspected `LatestBuildUpdater.BuildReplacementScript`: existing exception-based rollback could not execute after abrupt termination of the same updater PowerShell process midway through Robocopy `/MIR`.
- Added `-Watch -HelperPid` mode to the generated updater script, launched as a second independent PowerShell process. The helper waits for `guard.ready`, then writes a pending rollback checkpoint outside the installed application directory before changing any files. A surviving watchdog attempts to restore and restart the backed-up previous build, or removes an incomplete first-install destination. Successfully committed startup or verified exception rollback removes the marker; a failed restoration preserves marker and backup for inspection.
- Added eight offline xUnit checks including actual isolated PowerShell `-Watch` execution for incomplete first-install cleanup, missing old executable, no-checkpoint behavior, apostrophe paths and generated script syntax/ordering. No manual Windows UI run, production install, or hardware J2534 test was performed. Await CI to confirm compilation and test results.
- **Evidence boundary:** watchdog covers unexpected exit of the helper only while the guard and Windows survive. It does not solve power-loss, reboot, or simultaneous process termination. Stable release v0.2.2 remains unchanged.

---

## ECU catalog backup staging integrity — 2026-10-21

- Main CI #2979 for merged PR #401 passed, but a source review found the backup writer trusted `File.Copy` returning without verifying the staged bytes; a silent partial/altered copy could overwrite the previous good `.bak` after a successful primary commit.
- Added a test-only injectable backup staging delegate, with production still using `File.Copy`. Before `File.Move(..., overwrite:true)`, compare the complete temporary backup with committed primary bytes. On mismatch, no backup promotion; cleanup remains best effort and the committed primary remains valid.
- Eight offline xUnit cases cover failed/partial/empty/missing/altered backup copy, preserving earlier good backups, primary-commit success despite snapshot errors, and successful retry/recovery. [PR #402](https://github.com/vorlocsev/dvinholm/pull/402) passed Windows x86 CI #2983, merged as `0cae282`, and post-merge [main CI #2985](https://github.com/vorlocsev/dvinholm/actions/runs/39641711895) also passed the full automated suite, build and publishing. No Windows manual or physical J2534/ECU tests performed.
- Windows manual checks and physical J2534/ECU/vehicle tests remain deferred. Abrupt updater process termination needs a separate carefully scoped stage; existing updater code catches ordinary exceptions but not process termination. Stable v0.2.2 unchanged.

---

## Offline ECU profile catalog backup and corruption recovery — 2026-10-21

- Found that an invalid `%LOCALAPPDATA%\\Dvinholm\\ecu-profiles.json` only generated a startup error with no attempt to recover a previous valid catalog, and a later profile Save could overwrite the damaged file.
- New local recovery path in `EcuProfileCatalogStore`: after successfully committing the primary JSON, try snapshotting `ecu-profiles.json.bak` using same-directory temporary/rename. A failed backup cannot roll back or misreport the primary save; an older backup, if any, is kept.
- `LoadWithRecovery` prefers primary; if its JSON fails validation, it reads a validated backup into memory without altering the original. Startup status distinguishes recovered profiles from normal load. If both copies fail, users see the existing error and data is left untouched. When replacing a damaged main later, Save first archives its original bytes to a unique `.corrupt-*.json` sibling.
- Eight software-only xUnit cases cover initial/refreshed snapshots, damaged/truncated primary, invalid backup, both invalid, unavailable backup, and archival before restoration Save. No manual UI or real J2534/ECU/vehicle tests were performed or requested. CI evidence to follow; Stable v0.2.2 unchanged.

---

## All-or-nothing ECU profile JSON import (2026-10-21)

- Existing `ImportProfiles_Click` applied imported profiles in-memory and changed the WinUI selection before attempting to save `%LOCALAPPDATA%\\Dvinholm\\ecu-profiles.json`. A failure could leave an ephemeral imported catalog in the UI while the previous catalog persisted on disk.
- Added `EcuProfileCatalogImporter.ImportAndPersist`: deserialize/validate all entries, call atomic store persistence, return new catalog for UI activation only after success. Save/read/invalid JSON exceptions leave active profile state untouched. UI status distinguishes invalid data from inaccessible storage; the import JSON schema and author-provided `isVerified` values remain unchanged.
- Eight deterministic offline regression tests exercise valid commit, invalid JSON/CAN ID/DID/duplicate ID, interruption during staging, first import failing without a file, and successful retry. [PR #399](https://github.com/vorlocsev/dvinholm/pull/399) passed Windows x86 PR CI #2966, merged as `6a885d6`, and post-merge [main CI #2967](https://github.com/vorlocsev/dvinholm/actions/runs/39625478007) also passed all tests/build/package/stable audit. No manual Windows UI or physical J2534 adapter/ECU/vehicle tests were performed. Stable v0.2.2 unchanged.

---

## ECU profile persistence failure hardening — 2026-10-21

- Reviewed the persistence boundary used by the in-app ECU profile editor. The store already used same-directory temp/rename, but there was no deterministic way to simulate partial writes in CI and no guard against a staging operation returning with a missing/zero-byte temporary file.
- Added an internal stage-writer seam to `EcuProfileCatalogStore` (production uses the existing `File.WriteAllText`) and validates staged output before final rename. The edit path uses `EcuProfileEditor.SaveAndPersist` to return a replacement catalog only after persistence; a write failure leaves the old UI catalog eligible to remain selected.
- Eight new xUnit cases cover partial/missing/empty staging, failed final rename, invalid draft without storage calls, failed editing without any in-memory/on-disk mutation and recovery retry. No OS permission changes or hardware needed.
- [PR #397](https://github.com/vorlocsev/dvinholm/pull/397) merged as `4758931` after green PR CI #2955; post-merge [main CI #2956](https://github.com/vorlocsev/dvinholm/actions/runs/39620490799) passed Windows x86 build, all automated tests, package/publish and Stable audit. Eight newly added failure-injection cases passed within the suite. No manual Windows UI or physical J2534/ECU/vehicle validation was performed. Stable v0.2.2 remains unchanged.

---

## ISO-TP channel gate cancellation timing flake — 2026-10-21

- After successful PR #394 CI #2941 and its merge as `cb79f27`, main CI #2942 failed one of 679 tests in `IsoTpChannelProtocolTests.Request_CancellationWhileWaitingForChannelGateIsHonored`. The test already observed `OperationCanceledException` but failed a separate elapsed-time check (< 1s), which measures host scheduler load as well as cancellation.
- No ISO-TP production source change is implicated in the JSON export PR. A focused test-only follow-up substitutes assertions on the actual gate ownership and single frame sent while the second request is cancelled. A 5-second request timeout still ensures a missed cancellation cannot be counted as a pass by the exception type.
- [PR #395](https://github.com/vorlocsev/dvinholm/pull/395) passed Windows CI #2945 and merged as `f2009b2`; post-merge main CI #2946 passed all automated checks, Windows package publishing and stable audit. No production ISO-TP behavior changed, no manual or hardware tests were requested. Stable v0.2.2 unchanged.

---

## Safe ECU profile JSON export — software-only follow-up (2026-10-21)

- Source audit found an uncovered data-loss path in `ExportProfiles_Click`: the legacy WinRT save picker created the target before destination validation; direct `File.WriteAllTextAsync` could truncate an existing JSON profile backup on failure. Unlike the TXT/CSV report paths, JSON export was not guarded against the in-place updater's application directory.
- Development change: reuse `ReportSavePathPicker`, `ReportExportDestinationGuard`, and `ReportExportFileWriter` for JSON export; add `EcuProfileJsonExport` as a testable data-only coordinator. Respect Cancel/no placeholder, reject empty catalogs and installation-folder paths, and use complete same-directory temporary write before replacing existing JSON.
- Added eight offline xUnit cases for JSON schema preservation, UTF-8 encoding, overwrite, blocked root and subfolder, sibling path boundary, empty catalog and failed I/O. [PR #394](https://github.com/vorlocsev/dvinholm/pull/394) passed Windows CI #2941 and merged as `cb79f27`; the first main CI #2942 found an unrelated ISO-TP test scheduling flake, subsequently resolved in PR #395. Main CI #2946 passed. No manual Windows UI validation is claimed or required for this stage by user request.
- No ECU addressing or verification behavior changed; no J2534 device/vehicle tests are claimed. Stable release v0.2.2 remains untouched.

---

## In-app ECU profile create/edit flow — 2026-10-19

- Added immutable `EcuProfileDraft`/`EcuProfileEditor` for validating new and existing local profile values. Supports optional `0x` prefix on CAN IDs and DIDs, 11/29-bit addressing, space/comma/semicolon-separated DID list, duplicate checks and required source note.
- New/Edit WinUI ContentDialog supports validation errors without dismissing the form. Existing profile identity is fixed; any edit clears the self-reported `isVerified` flag. Saving calls the already atomic `EcuProfileCatalogStore.Save` before swapping the in-memory catalog.
- After saving, clear UI search/verified-only filters and select the saved profile. No bus requests are sent. Cancel or failed validation/storage leaves catalog intact.
- Added deterministic no-adapter tests for creation, JSON compatibility, duplicate IDs/DIDs, invalid CAN IDs, 29-bit support, replacement without mutating unrelated entries and author verification reset.
- [PR #369](https://github.com/vorlocsev/dvinholm/pull/369) passed Windows CI [#39323572877](https://github.com/vorlocsev/dvinholm/actions/runs/39323572877): 587/587 tests passed (0 skipped) and release audit succeeded; merged as `2989b0bb52515123ef27976b6cc83dc5df7df847`.
- Manual Windows dialog verification remains pending; a [ten-step offline checklist](docs/manual-ecu-profile-editor-2026-10-19.md) is available. Post-merge main CI [#39324283802](https://github.com/vorlocsev/dvinholm/actions/runs/39324283802) is tracked separately. Stable v0.2.1 unaffected; physical J2534 test deferred.

---
## Searchable ECU profiles with provenance display — 2026-10-19

- Added `EcuProfileCatalogSearch` for case-insensitive profile-name/id/source matching plus CAN ID / candidate DID hex search. Optional `0x` prefix is accepted, no external data fetched.
- The WinUI profiles panel now offers **Find ECU profile** and **Marked verified only** plus bounded scrollable/selectable details. Verification is explicitly a self-reported flag from JSON, not confirmation by Dvinholm. No vehicle-specific Volvo addresses were added.
- Filtering preserves a selected profile and manually edited UDS fields when still visible. Filtering out the selected profile clears CAN ID, DID and addressing fields to prevent accidentally using stale data; no CAN requests are sent by filtering.
- Added deterministic software-only regression tests for name, catalog ID, source, 11/29-bit request/response IDs, DID, combined verification flag, empty results, immutability and provenance descriptions.
- Offline manual test plan added in [docs/manual-ecu-profile-search-2026-10-19.md](docs/manual-ecu-profile-search-2026-10-19.md). No manual result is claimed yet.
- [PR #367](https://github.com/vorlocsev/dvinholm/pull/367) passed Windows CI [#39297846535](https://github.com/vorlocsev/dvinholm/actions/runs/39297846535), reporting 564/564 passing tests (0 skipped), and merged to main as `15f43e16fa9a697a857bdfa46e4bbd8e71e4e38c`. Post-merge CI [#39298463839](https://github.com/vorlocsev/dvinholm/actions/runs/39298463839) also passed all checks. Hardware adapter testing is deferred. Stable release v0.2.1 remains unchanged.

---

## Russian user guide and updater-channel label — 2026-10-19

- Added `docs/USER_GUIDE_RU.md` containing practical steps for the portable x86 release, disconnected UI, vendor 32-bit J2534 DLL, CAN Monitor/filters/CSV, passive discovery, ECU profiles, UDS DID/VIN/DTC, updater mechanics and limitations.
- Source audit confirmed the updater accepts only successful `push` CI runs on `main` with published assets; corrected the misleading WinUI text claiming builds from all branches.
- Linked the guide on the README landing page; code path and updater behavior are unchanged. [PR #366](https://github.com/vorlocsev/dvinholm/pull/366) passed CI [#39291963115](https://github.com/vorlocsev/dvinholm/actions/runs/39291963115) and merged as `9b42f31fcdf046cc6c006398ae0e1d80c90b0acd`. Post-merge `main` CI [#39292558370](https://github.com/vorlocsev/dvinholm/actions/runs/39292558370) passed.

---

## Multi-line UDS DTC display — 2026-10-19

- Added `UdsDtcDisplayFormatter` to show the count of ECU-returned records, the requested status mask and one line per 24-bit raw DTC with standard ISO 14229 status-bit explanation.
- The UI `Read DTC` handler now uses this formatter. The existing scrollable/selectable result panel handles multiple records without replacing CAN Monitor or inventing manufacturer-specific fault meanings.
- The empty result explicitly states that no records returned for this ECU/mask does not confirm a fault-free vehicle. Duplicate DTC records and ECU order are preserved.
- Deterministic xUnit coverage checks empty, one/many, combined flags, masks, zero status, duplicates, unknown-code handling and null input. [PR #365](https://github.com/vorlocsev/dvinholm/pull/365) passed Windows CI [#39290526032](https://github.com/vorlocsev/dvinholm/actions/runs/39290526032) and merged as `fae165f57aedf9067883311eee71d466ed65237b`. Post-merge main CI is pending; stable v0.2.1 untouched; hardware validation deferred.

---

## Readable UDS DID results — 2026-10-19

- Added `UdsDidDisplayFormatter` to present DID ID and full raw diagnostic reply while decoding ISO 14229's `F190` VIN strictly with the existing VIN validator.
- Generic printable data may be shown as a clearly labeled **unverified ASCII preview**; arbitrary binary data and vehicle-specific physical units are never guessed.
- Hooked the helper to WinUI `Read DID`; `Read VIN` remains a separate strict operation. The status TextBlock is now selectable/wrapping inside a bounded vertical ScrollViewer, so raw replies can be copied without crowding CAN Monitor. Unit tests cover valid/invalid VIN, text preview, binary, empty, malformed/mismatched DID and null responses.
- [PR #364](https://github.com/vorlocsev/dvinholm/pull/364) passed Windows CI [#39287171823](https://github.com/vorlocsev/dvinholm/actions/runs/39287171823) and merged as `ba192e1ff97ccd6923d08b43130967dab9fc96fb`. Post-merge `main` CI [#39290076530](https://github.com/vorlocsev/dvinholm/actions/runs/39290076530) passed; software-only tests require no physical J2534 adapter; stable `v0.2.1` unchanged.

---

## Passive ECU scan cancellation drop reporting — 2026-10-19

- Source inspection found that a cancelled passive discovery scan returned partial candidates but omitted the subscription's dropped-frame count, even though completed scans already report queue overflow.
- The change carries `DroppedFrameCount` into `UdsEcuDiscoveryCancelledException` and surfaces it in WinUI cancellation status and error description.
- Deterministic no-adapter unit cases cover clean cancellation vs cancellation with drops. The existing virtual-adapter cancellation test now checks that no drops were recorded in its controlled scenario.
- [PR #363](https://github.com/vorlocsev/dvinholm/pull/363) passed Windows CI [#39281948860](https://github.com/vorlocsev/dvinholm/actions/runs/39281948860), then merged as `fde996e7ca7555391519e2ab8c10f440f148534c`. [PR #362](https://github.com/vorlocsev/dvinholm/pull/362) (Windows icon confirmation) also passed CI [#39279472702](https://github.com/vorlocsev/dvinholm/actions/runs/39279472702) and merged as `4d1c20db42128217a9efa0e94962ed197d77cfd9`. Post-merge main checks are pending. No ECU commands were added, and physical J2534/vehicle testing stays deferred; stable release remains `v0.2.1`.

---

## Dvinholm v0.2.1 release preparation — 2026-10-19

- Published [Dvinholm v0.2.1](https://github.com/vorlocsev/dvinholm/releases/tag/v0.2.1) as a stable patch release after branding and icon integration, with the current `main` software fixes but without new physical-adapter claims.
- Updated application/CI version identifiers, release workflow, dedicated release notes, README download links, CHANGELOG and roadmap.
- Release workflow [#39184108681](https://github.com/vorlocsev/dvinholm/actions/runs/39184108681) passed Windows x86 build/tests/publish and icon verification, published `Dvinholm-Windows-x86.zip` plus `SHA256SUMS.txt`, then re-downloaded and verified the ZIP checksum. Published ZIP: 67,042,782 bytes; SHA-256 `5245d7691482da6434262b5767a0b23ed757df9136fb38b9aed196523a47bf09`.
- [PR #360](https://github.com/vorlocsev/dvinholm/pull/360) merged as `eb51b998cee3557b661b5cfa9626126ddaea6be4`; PR CI [#39183661356](https://github.com/vorlocsev/dvinholm/actions/runs/39183661356), post-merge `main` CI [#39184066818](https://github.com/vorlocsev/dvinholm/actions/runs/39184066818), and dedicated release CI all passed. GitHub confirms tag `v0.2.1` is neither draft nor prerelease and is the newest stable. On 2026-10-19 the user confirmed that the branded icon displays in Dvinholm v0.2.1 on Windows. This closes the general visual-smoke-test item; the specific titlebar/taskbar/Explorer placements were not separately identified. Physical-adapter and vehicle validation remains deferred.

---

## Repository branding, README refresh and Windows app icon — 2026-10-19

- User approved the silver circle-and-arrow emblem with a northern river landscape, and the wide Dvinholm ECU Diagnostics banner for GitHub.
- Added `assets/dvinholm-banner.webp`, `assets/dvinholm-icon.png`, and `src/Dvinholm.App/Assets/Dvinholm.ico` (16, 24, 32, 48, 64, 128, 256 px frames).
- `README.md` now has a branded hero, CI/release badges, fast Windows instructions and clear hardware-validation caveats. Historic MVP notes moved to `docs/DEVELOPMENT_HISTORY.md` instead of deleting them. Fixed outdated 'all branches' updater wording to match the current main-only implementation.
- Embedded icon through `ApplicationIcon`, copied ICO in portable publish output, and assigned the icon to the WinUI title bar through `AppWindow.SetIcon`.
- CI now asserts that publish output includes a structurally valid multi-size ICO and the executable.
- [PR #358](https://github.com/vorlocsev/dvinholm/pull/358) merged as `780768061ca5bc8972ddc22924f3c124f63f1cc3`. PR CI [#39181656596](https://github.com/vorlocsev/dvinholm/actions/runs/39181656596) and post-merge main CI [#39182025601](https://github.com/vorlocsev/dvinholm/actions/runs/39182025601) completed successfully, including build, tests, publish, 7-frame ICO package check and latest stable-release ZIP audit. A subsequent user report on 2026-10-19 confirmed the icon is visible in v0.2.1 on Windows; precise UI surfaces were not separately reported.
- Stable release `v0.2.0` not republished. Physical J2534/vehicle testing still deferred.

---

## Updater launch-failure rollback regression — 2026-10-19

- Opened [PR #357](https://github.com/vorlocsev/dvinholm/pull/357) to cover `Start-Process` throwing before the updated build launches, a distinct failure from an app that launches and exits immediately.
- Extended the existing Windows PowerShell rollback theory using an isolated simulated launch exception. The test checks restoration of the previous executable and marker, relaunch of that build, and preservation of the original error in `update.log`.
- PR #357 merged as `90c42b96c4e2b87ea74d5dca8006a7f75ffff97f`. PR CI [#39178639657](https://github.com/vorlocsev/dvinholm/actions/runs/39178639657) and post-merge main CI [#39178995450](https://github.com/vorlocsev/dvinholm/actions/runs/39178995450) passed. No production implementation changes; stable release `v0.2.0` remains unchanged. Physical-adapter/vehicle testing deferred.

---

## ECU profile validation and CI closure — 2026-10-18

- Confirmed post-merge main CI [#39162799935](https://github.com/vorlocsev/dvinholm/actions/runs/39162799935) completed successfully on `5d592d5ee1f3715d68dd670eda7e555822a31375`: build, tests, publish/package, and latest stable-release audit all passed.
- User verified the update completed successfully and a subsequent Check update reported no newer build. Both synthetic ECU profiles remained present with correct parameters.
- Manual profile checks are complete: standard IDs `7E0/7E8` (11-bit, DID `F190`); extended IDs `18DA10F1/18DAF110` (29-bit, DID `1234`); profile switching restored saved values; both profiles persisted through restarts; repeated add did not duplicate; JSON export/import worked; invalid JSON was rejected while preserving existing data.
- Updated `ROADMAP.md` to close the now-completed manual and CI checklist items. Stable release remains `0.2.0`; no release was published. Physical J2534 adapter/vehicle testing remains deferred.

---

## Synthetic ECU test profiles — 2026-10-18

- [x] Added an **Add test profiles** action that safely appends two clearly labeled, unverified synthetic profiles to the existing local catalog without replacing user profiles.
- [x] Standard 11-bit profile uses request/response IDs `7E0/7E8`; extended 29-bit profile uses `18DA10F1/18DAF110`. Their DIDs differ to make field refresh easy to observe.
- [x] Test profiles are persisted in the user's existing catalog and are explicitly marked as UI-test-only, not vehicle-verified.
- [ ] Verify CI; then test adding the profiles and switching between them on Windows. Stable release remains `0.2.0`.

---

## ECU profile selection refresh — 2026-10-18

- [ ] User reproduced a UI defect: after manually editing UDS Response ID, switching to another ECU profile and returning to the first profile leaves the manual value instead of restoring the first profile's saved response ID.
- [x] Updated profile-selection handling to apply the final ComboBox selection on the UI dispatcher queue and report the applied request/response IDs in status text.
- [ ] Verify CI and then have the user retest profile A → profile B → profile A using the latest successful CI build. Stable release remains `0.2.0`; no physical J2534 adapter or vehicle is available/required for this UI-only issue.

---

## CAN baud-rate persistence — 2026-10-18

- User reported that the selected CAN speed did not persist; source inspection confirmed the ComboBox had no save/load logic.
- Merged PR [#343](https://github.com/vorlocsev/dvinholm/pull/343) as `2787b85d77c2a6249598182775a5a9c320092c08`. Added `CanBaudRateStore` under `%LOCALAPPDATA%\\Dvinholm\\can-baud-rate.txt`; supported rates are 500000, 250000, and 125000 baud, with 500000 as the missing/invalid fallback.
- Added unit tests for missing settings, supported-rate round trips, invalid saved values, unsupported values, and blank paths.
- PR CI [#39150451313](https://github.com/vorlocsev/dvinholm/actions/runs/39150451313) passed build, tests, publish, and stable-release audit. Post-merge main CI [#39150903328](https://github.com/vorlocsev/dvinholm/actions/runs/39150903328) also passed and produced `Dvinholm-Windows-x86` (66,771,233 bytes; SHA-256 `f380aee03e1c036b6f1fe0a7fdd3e27aaed5ff4fef958df4f9b51d18a10e1d4d`).
- Manual Windows smoke tests passed after fully closing and restarting the app: 250 kbit/s, 125 kbit/s, and 500 kbit/s all restored correctly.
- Stable release remains `0.2.0`; no physical J2534 adapter or vehicle was needed.

---

## Updater rollback-copy failure regressions — 2026-10-18

- Merged PR [#336](https://github.com/vorlocsev/dvinholm/pull/336) as `7fb38e25936ca718b4701f4ec0f8141940048b22`. Added a Windows PowerShell runtime regression for robocopy exit code 8 during rollback; it verifies the failure is logged, backup/staging recovery files are retained, and the temporary helper script is removed. Main CI [#39140253444](https://github.com/vorlocsev/dvinholm/actions/runs/39140253444) passed.
- Merged PR [#337](https://github.com/vorlocsev/dvinholm/pull/337) as `369c3d38c20d86bec827914a42b38a5b67771dc5`. Added a regression for a rollback copy that reports success but does not restore `Dvinholm.App.exe`; the helper logs the failure, preserves recovery artifacts, and removes its temporary script. PR CI [#39140363953](https://github.com/vorlocsev/dvinholm/actions/runs/39140363953) passed.
- Post-merge main CI [#39140786191](https://github.com/vorlocsev/dvinholm/actions/runs/39140786191), [#39141242021](https://github.com/vorlocsev/dvinholm/actions/runs/39141242021), and [#39142078521](https://github.com/vorlocsev/dvinholm/actions/runs/39142078521) passed build/tests and the latest-stable-release audit.
- Main CI #39142078521 produced `Dvinholm-Windows-x86` (66,770,176 bytes; SHA-256 `0c2a02c876fd8ef1fbf8d1d787defa642224457caf329a2cb36705e9b6491a09`). The stable release remains `0.2.0`; this CI artifact is not a new stable release.
- No physical J2534 adapter or vehicle was used. Remaining validation: bounded software-only updater review and Windows UI smoke checks using the current CI artifact.

---

## Successful updater replacement cleanup regression — 2026-10-18

- Merged PR [#334](https://github.com/vorlocsev/dvinholm/pull/334) as `ff3f8eaeae3634f265718376a7b2ef8e6eefdecc`.
- Added a Windows-only PowerShell runtime regression test that mocks file-copy and process-launch operations while exercising the generated replacement script.
- The test checks the new build replaces the old one, starts the new executable, and removes the backup and staging directories after startup succeeds.
- PR CI passed in [run #2656](https://github.com/vorlocsev/dvinholm/actions/runs/39137524253) and [run #2657](https://github.com/vorlocsev/dvinholm/actions/runs/39137530241). Main CI before merge passed in [run #2655](https://github.com/vorlocsev/dvinholm/actions/runs/39137390573).
- Post-merge main CI [run #2658](https://github.com/vorlocsev/dvinholm/actions/runs/39138576615) is still running; keep it pending until its conclusion is success.
- Next: audit remaining updater rollback and cleanup failure paths using software-only tests; no physical J2534 adapter or vehicle test in this phase.

---

## PowerShell updater path-escaping regression — 2026-10-18

- Merged PR [#332](https://github.com/vorlocsev/dvinholm/pull/332) as `56fac7e6a9a9b88e5c60782931709384ca7f326b`.
- Added a regression test for apostrophes in staging, destination, helper-script, and backup paths. It verifies that generated PowerShell literals double embedded apostrophes and do not contain the corresponding raw unescaped path.
- PR CI runs [#39135634503](https://github.com/vorlocsev/dvinholm/actions/runs/39135634503) and [#39135642946](https://github.com/vorlocsev/dvinholm/actions/runs/39135642946) passed build/tests and stable-release audit.
- Post-merge main CI [#39136947275](https://github.com/vorlocsev/dvinholm/actions/runs/39136947275) was still running at the time of this entry; verify before closing the check.
- Next: continue software-only updater failure-path review. Physical J2534 adapter/vehicle testing remains deferred.

---

## Updater runtime-failure cleanup and recovery preservation — 2026-10-18

- Merged PR [#328](https://github.com/vorlocsev/dvinholm/pull/328) (`ac05588e26f4b9e255181d7a23cd6e76a04776c0`): runtime failures best-effort clean the staging directory unless rollback fails, always attempt helper-script cleanup, and append cleanup errors to the update log. If rollback fails, recovery staging and backup data are retained.
- PR CI [#39129093156](https://github.com/vorlocsev/dvinholm/actions/runs/39129093156) passed; post-merge main CI [#39133270837](https://github.com/vorlocsev/dvinholm/actions/runs/39133270837) passed build/tests, publish, and stable-release audit.
- Merged test PR [#329](https://github.com/vorlocsev/dvinholm/pull/329) (`2404addf2af1b71688e04b5fe55474da39f3b81b`): added assertions that staging is preserved if rollback fails while the temporary helper script is still removed. PR CI [#39133330458](https://github.com/vorlocsev/dvinholm/actions/runs/39133330458) passed.
- Post-merge main CI [#39133643286](https://github.com/vorlocsev/dvinholm/actions/runs/39133643286) passed build/tests, publish, and stable-release audit.
- Next: verify post-merge CI and continue software-only updater failure-path audit. No stable release or physical adapter/vehicle testing.

---

## Updater process-exit wait hardening — 2026-10-18

- Merged PR [#325](https://github.com/vorlocsev/dvinholm/pull/325) (`cc2cca05a2c66838ed3705c8a82b2daffbd07978`): updater helper wait for previous app process is bounded to 60 seconds. If the process remains alive, failure is logged before backup/replacement begins. Windows PowerShell regression test confirms installed files stay unchanged; PR CI [#39127243905](https://github.com/vorlocsev/dvinholm/actions/runs/39127243905) and post-merge main CI [#39127517071](https://github.com/vorlocsev/dvinholm/actions/runs/39127517071) passed.
- Merged PR [#326](https://github.com/vorlocsev/dvinholm/pull/326) (`7662476e99595a41beb5dc0fb47a675efda26817`): handles the race where the old process exits between `Get-Process` and `Wait-Process`. If the wait throws, a second process check permits continuation only when the old process is gone; otherwise the error is rethrown. Added a Windows PowerShell runtime regression test; PR CI [#39127650770](https://github.com/vorlocsev/dvinholm/actions/runs/39127650770) and push CI [#39127638650](https://github.com/vorlocsev/dvinholm/actions/runs/39127638650) passed.
- Post-merge main CI [#39127972826](https://github.com/vorlocsev/dvinholm/actions/runs/39127972826) passed build/tests and stable-release audit.
- Next: confirm main CI, then continue software-only updater failure-path review. No stable release or physical adapter/vehicle validation was performed.

---

## Partial rollback-backup cleanup — 2026-10-18

- Merged PR [#323](https://github.com/vorlocsev/dvinholm/pull/323) as `38d2133e69f646a49c99717cb80f1acb894bc3a9`.
- Fixed cleanup when copying the installed build to a new rollback backup fails before replacement begins. The helper removes the unusable partial backup while leaving the original installation untouched, and logs a cleanup failure rather than masking the original error.
- Added a Windows PowerShell runtime regression test: simulate backup robocopy failure, assert old executable and marker remain intact, backup directory is removed, and no application launch occurs.
- CI passed on PR head [#39125629739](https://github.com/vorlocsev/dvinholm/actions/runs/39125629739), push [#39125615157](https://github.com/vorlocsev/dvinholm/actions/runs/39125615157), and post-merge main [#39125940869](https://github.com/vorlocsev/dvinholm/actions/runs/39125940869). Build/tests and stable-release audit passed; main also completed publish.
- Documentation PR [#322](https://github.com/vorlocsev/dvinholm/pull/322) records PR #321's rollback-backup retention test; post-merge main CI [#39125736986](https://github.com/vorlocsev/dvinholm/actions/runs/39125736986) passed.
- Next: continue software-only updater failure-path review. No stable release or physical adapter/vehicle validation was performed.

---

## Rollback backup retention test — 2026-10-18

- Merged PR [#321](https://github.com/vorlocsev/dvinholm/pull/321) as `2f3446d5362cac3d122873007b88dfd9f3cbde9b`; PR CI [#39124778330](https://github.com/vorlocsev/dvinholm/actions/runs/39124778330) passed build/tests and stable-release audit.
- Converted the Windows PowerShell startup-failure regression into two cases. When the old app survives its startup window, rollback backup cleanup is expected; when the restored app also exits, the log must say rollback failed and the backup executable must remain for recovery.
- Post-merge main CI [#39125088281](https://github.com/vorlocsev/dvinholm/actions/runs/39125088281) passed build/tests, publish, and stable-release audit.
- No runtime behavior changed in #321. No stable release or physical J2534 adapter/vehicle test is included.

---

## Updater startup health check — 2026-10-18

- PR [#318](https://github.com/vorlocsev/dvinholm/pull/318) merged as `00d49cb75fdefe2f3ee90811c1fbf5cbdd619ce4`; both PR CI [#39123184595](https://github.com/vorlocsev/dvinholm/actions/runs/39123184595) and post-merge main CI [#39124043911](https://github.com/vorlocsev/dvinholm/actions/runs/39124043911) passed build/tests and stable-release audit.
- Added a five-second post-launch check in the PowerShell replacement helper. If the new process has already exited, update completion is treated as failure so the existing backup/rollback path is used rather than deleting the backup after a failed startup.
- The restored process is checked as well; if it exits during the startup window, the helper records rollback failure and retains the backup.
- Added a Windows-only PowerShell runtime test with a mock process that exits immediately; it asserts the old build is restored and relaunched.
- No stable release and no physical J2534 adapter/vehicle testing.

---

## Updater setup-failure cleanup — 2026-10-18

- Merged PR [#314](https://github.com/vorlocsev/dvinholm/pull/314) as `9ae0ab54dd9d0846974cb47f86e7a980fb4f00a9`. PR CI [#39121712511](https://github.com/vorlocsev/dvinholm/actions/runs/39121712511) passed build/tests and stable-release audit.
- Previously, `StartReplacement` began its catch/cleanup only after destination path normalization, creation of the local app-data directory, and construction of the replacement script. Failures in those earlier setup steps could leave the extracted staging directory behind.
- Moved the full setup sequence inside the guarded block; cleanup now accepts a missing script path and still attempts to remove staging. Added a regression test for an invalid NUL destination path, asserting the expected `ArgumentException` and staging cleanup.
- Post-merge main CI [run #39121998264](https://github.com/vorlocsev/dvinholm/actions/runs/39121998264) completed successfully; build/tests and stable-release audit passed on the exact merge commit.
- Next: confirm post-merge CI, then continue software-only updater audit. No stable release or physical adapter/vehicle testing.

---

## Updater release lookup errors and ZIP size guard tests — 2026-10-18

- Merged [PR #307](https://github.com/vorlocsev/dvinholm/pull/307) (`f75d8fb9d7050aff0cda0d74601570ae23ea2ddf)): tests cover exact expanded-size budget acceptance, negative entry rejection, aggregate over-limit rejection, and `long.MaxValue` input without overflow.
- Merged [PR #308](https://github.com/vorlocsev/dvinholm/pull/308) (`0a7bcd2df9b6a4d3650fc6da7735501503aad476`): only release-tag HTTP 404 is treated as a missing unpublished asset; other status codes now throw, preventing rate-limit/server errors from being silently converted into repeated lookups and a misleading no-build message.
- PR-head build/tests and latest-stable-release audit passed for #307 ([run #39119334834](https://github.com/vorlocsev/dvinholm/actions/runs/39119334834)) and #308 ([run #39119376885](https://github.com/vorlocsev/dvinholm/actions/runs/39119376885)); both push runs passed as well.
- Main CI for commit `3089b43d22d3c4c89fb48f60b2f8d1331f88f5d5` passed in [run #39119254855](https://github.com/vorlocsev/dvinholm/actions/runs/39119254855). Post-merge main CI then passed after PR #307 ([run #39119672274](https://github.com/vorlocsev/dvinholm/actions/runs/39119672274)) and PR #308 ([run #39119700082](https://github.com/vorlocsev/dvinholm/actions/runs/39119700082)).
- Documentation checkpoint PR [#311](https://github.com/vorlocsev/dvinholm/pull/311) merged; post-merge main CI passed in [run #39120748971](https://github.com/vorlocsev/dvinholm/actions/runs/39120748971).
- Next: continue software-only updater failure-path review. Do not publish a stable release or claim physical adapter/vehicle validation.

---

## Post-merge verification — PR #304 — 2026-10-18

- Main CI [run #39118506041](https://github.com/vorlocsev/dvinholm/actions/runs/39118506041) passed on merge commit `369da5ead23b3299b7f4f53a1a4ead3c2c9c3e8a`, including build/tests and latest-stable-release audit.
- Docs PR [#305](https://github.com/vorlocsev/dvinholm/pull/305) merged after current-head CI [run #39118570583](https://github.com/vorlocsev/dvinholm/actions/runs/39118570583) passed.
- Continue software-only updater review. No stable release or physical adapter/vehicle testing.

---

## First-install cleanup failure diagnostics — 2026-10-18

- Merged PR [#304](https://github.com/vorlocsev/dvinholm/pull/304) as `369da5ead23b3299b7f4f53a1a4ead3c2c9c3e8a`.
- First-install rollback now verifies whether the incomplete destination was actually removed. If deletion throws or the directory remains, the update log records a cleanup failure instead of claiming success.
- Added Windows-only PowerShell runtime regression coverage with simulated access denied. Latest-head build/tests and latest-stable-release audit passed in [PR run #39118180207](https://github.com/vorlocsev/dvinholm/actions/runs/39118180207) and [push run #39118177031](https://github.com/vorlocsev/dvinholm/actions/runs/39118177031).
- Post-merge main CI [run #39118506041](https://github.com/vorlocsev/dvinholm/actions/runs/39118506041) was still in progress at this checkpoint; verify its outcome before marking main green.
- Next: continue software-only updater audit. No stable release or physical J2534 adapter/vehicle test is included.

---

## Updater discovery rate-limit reduction — 2026-10-18

- Merged PR [#301](https://github.com/vorlocsev/dvinholm/pull/301) (`8afeba1a27f326f97ef87fde2f78072216e77c7d`): updater discovery skips workflow runs except completed successful pushes to `main`, the only event/branch that publishes public updater release assets.
- Added regression coverage that feature-branch and manual workflow runs trigger no release lookups. PR-head build/tests and stable-release audit passed in runs [#39116058181](https://github.com/vorlocsev/dvinholm/actions/runs/39116058181) and [#39116048252](https://github.com/vorlocsev/dvinholm/actions/runs/39116048252).
- Merged docs PR [#300](https://github.com/vorlocsev/dvinholm/pull/300) (`052e9d2a26ad5d030cdb6dd35934c887ff11c294`) recording PR #299 cleanup exception handling.
- Post-merge main CI is now green: [run #39116463864](https://github.com/vorlocsev/dvinholm/actions/runs/39116463864) and [run #39116480546](https://github.com/vorlocsev/dvinholm/actions/runs/39116480546) both passed build/tests and the stable-release audit.
- Next: confirm both main runs, then continue software-only updater audit (download integrity, replacement/rollback, cleanup). No stable release or physical adapter/vehicle test is included.

---

## Updater cleanup error handling — 2026-10-18

- PR [#299](https://github.com/vorlocsev/dvinholm/pull/299) merged as `a0f7162e2c984acb90970778795b004336aafd9e`. Both the PR workflow run #39115542015 and push run #39115532165 passed build/tests and stable-release audit.
- `LatestBuildUpdater.DownloadAndPrepareAsync` now routes temporary archive/staging cleanup through `TryCleanup(Action)`, which swallows `IOException` and `UnauthorizedAccessException` so cleanup does not hide the original download/validation/extraction failure. Added tests for both exception types.
- Post-merge `main` CI run #39115926016 was still in progress when this entry was prepared. Confirm its final status before marking main green. No release published; hardware validation remains deferred.

---

## Updater helper-start cleanup merged — 2026-10-18

- PR [#297](https://github.com/vorlocsev/dvinholm/pull/297) was squash-merged as `c8466f49e53ebb2332e9f40733f159af41729e7b` after [PR CI #39114708239](https://github.com/vorlocsev/dvinholm/actions/runs/39114708239) passed both build/tests and stable-release audit.
- `LatestBuildUpdater` now best-effort deletes the generated update-helper script and staging directory if writing or launching the helper fails. Cleanup exceptions are swallowed so they do not mask the original write/start error.
- Added `FailedHelperStartCleanup_RemovesScriptAndStagingDirectory` regression coverage. The existing PowerShell rollback integration test timeout was increased from 20 to 60 seconds after a CI timeout; the current PR CI passed with this adjustment.
- Post-merge `main` CI run #39115059949 was in progress when this entry was prepared; verify its final outcome before calling main green. No stable release or hardware validation was performed. Physical J2534 adapter/vehicle testing remains deferred.

---

## Follow-up — expanded-size arithmetic overflow guard (2026-10-18)

- Added a defensive check that compares each ZIP entry length against the remaining expanded-size budget before adding it. This avoids relying on checked arithmetic when processing hostile ZIP64 metadata.
- Added a regression test using entry lengths `[1, long.MaxValue]`; it must reject the archive with `InvalidDataException`, consistent with other over-limit input.
- CI passed on head `8b960c192ffc3b356429232814cc5408d5afa08d`: build, test, publish, and artifact upload succeeded; the v0.1.1 archive audit also passed ([run #39103682783](https://github.com/vorlocsev/dvinholm/actions/runs/39103682783)). This validates the overflow guard and tests in CI, not real updater replacement on a user's machine.
- PR #286 remains open pending explicit approval. No release, merge, adapter, or vehicle testing was performed.

---

## Follow-up — expanded-size guard tests made deterministic (2026-10-18)

- CI runs #39102009534 and #39102018577 both failed the synthetic ZIP test even after modifying both local and central-directory size fields: the expected `InvalidDataException` was not thrown; 483 other tests passed.
- Dropped the brittle ZIP metadata fixture. Extracted the existing aggregate size calculation into `LatestBuildUpdater.ValidateExpandedSize(IEnumerable<long>)`; the extraction path calls this helper before extracting files, preserving the 1,048,576,000-byte cap (1,000 MiB).
- Replaced the test with direct boundary cases: total above the limit rejected, exactly at the limit accepted, and negative declared size rejected. This validates the production guard without creating a huge file or depending on malformed ZIP-reader behavior.
- This is a small production refactor plus tests. CI passed on head `aa2a816e214e9f34ca5387595962fd8b4c1ed055`: all 486 tests passed and the published v0.1.1 archive audit succeeded ([run #39102771531](https://github.com/vorlocsev/dvinholm/actions/runs/39102771531)).
- Continue software-only checks. PR #286 remains open; do not merge any PR or publish a release without explicit approval. No physical J2534 adapter/vehicle testing.

---

## Follow-up — expanded ZIP size test fixture correction (2026-10-18)

- PR #285 CI run [#39101692614](https://github.com/vorlocsev/dvinholm/actions/runs/39101692614) failed at the new test: build succeeded, 483 tests passed, and `ExpandedArchiveOverLimit_IsRejectedBeforeExtractionAndCleansTemporaryFiles` failed because no exception was thrown.
- Root cause in the test fixture: only the central-directory uncompressed-size field was modified. The ZIP reader did not surface the intended expanded length from that inconsistent fixture.
- Created branch `fix/updater-expanded-size-test` and updated the fixture to modify both the local file header and central-directory uncompressed-size fields, keeping metadata consistent while the actual payload remains tiny.
- Production updater code was not changed. The fix is unverified until CI runs on the follow-up branch.
- Next: open a follow-up PR, inspect CI, then continue the software-only updater review. Do not merge any PR or publish a release without explicit approval. No physical J2534 adapter/vehicle testing.

---

## Follow-up — updater expanded archive size limit test (2026-10-18)

- Added `LatestBuildUpdaterExpandedSizeTests.cs` on branch `test/updater-expanded-size-limit`.
- The test constructs a small ZIP and modifies the central-directory uncompressed-size metadata to exceed the updater's 1,000,000,000-byte expanded-size limit. This exercises the pre-extraction guard without allocating a large file.
- Expected behavior: `InvalidDataException`; temporary staging directory and downloaded ZIP are cleaned up.
- Test-only change. CI pending; do not claim verified until CI completes.
- Next: inspect CI results, then continue the software-only release gate review. Do not publish a release or perform physical J2534/vehicle testing.

---

## Manual Windows verification — disconnected startup state (2026-10-18)

- [x] After relaunch, user confirmed the saved test DLL path remains visible and the app reports **not connected**.
- [x] This confirms the app does not auto-connect on startup. The Connect action was not used; no physical adapter was available or tested.
- [x] Continue with software-only release checks; do not claim physical J2534/vehicle compatibility.

---

## Manual Windows verification — J2534 DLL path persistence (2026-10-18)

- User confirmed the J2534 DLL path persisted after fully closing and reopening the app using the Windows x86 CI artifact on Windows 11 LTSC IoT 26100.9550.
- Test value was `C:\\Dvinholm-Test\\missing-j2534.dll`; no Connect action was taken, so no adapter connection was attempted.
- Fix is merged to `main` as `696aad80be40deac5acb6632d20b74e4471a2f9e` via [PR #281](https://github.com/vorlocsev/dvinholm/pull/281); branch CI passed [run #39077395043](https://github.com/vorlocsev/dvinholm/actions/runs/39077395043).
- Manual check outcome: PASS for persistence across restart. This does not verify physical adapter compatibility.
- Next: continue remaining software-only checks and review release gates before deciding on a release. Physical J2534 adapter/vehicle validation remains deferred.

---

## Follow-up — persist J2534 DLL path (2026-10-18)

- User confirmed the DLL path field is empty after closing/reopening stable v0.2.0 (4c7efcd).
- Created isolated branch fix/persist-j2534-dll-path.
- Added J2534DllPathStore, wired the text field to save changes under %LOCALAPPDATA%\\Dvinholm\\j2534-dll-path.txt, and load the saved value at startup. This does not attempt an automatic connection.
- Added tests for missing file, round-trip save/load (including parent directory creation), clearing the value, and rejecting a blank settings-file path.
- Status: implementation committed to the branch; CI and PR review still pending. Do not claim the fix is verified or released yet.
- Hardware/vehicle testing remains deferred because no physical J2534 adapter is available.

---

## Dvinholm v0.2.0 stable release — Windows launch confirmed — 2026-10-18

- User confirmed the exact published v0.2.0 Windows ZIP opens on Windows 11 LTSC IoT 26100.9550.
- Stable release: [v0.2.0](https://github.com/vorlocsev/dvinholm/releases/tag/v0.2.0); archive SHA-256 `c80e56a8cf594485cfeb82a1003907cff22c52716abf2e60e518d290b4f3deec`.
- Release CI passed 479 tests; latest main CI #39048794190 passed on `f889482e99565ae4093de2da3cc3c4bbe75308a2`.
- Next manual checks: updater button/state and retry paths; no-adapter/disconnected controls, including Monitor and VIN/DTC/DID availability. Do not infer these are verified from successful launch.
- Physical adapter/vehicle validation remains deferred; no hardware compatibility claim.

---

## Post-PR #275 main CI passed — 2026-10-17

- Documentation checkpoint PR #275 merged as `9d8dcbf9d7797b5a65d7fd9461c6472ba34ff931`; full prior contents of both project tracking documents were preserved.
- Post-merge [CI run #39045906011](https://github.com/vorlocsev/dvinholm/actions/runs/39045906011) passed on the exact merge SHA: `build` and `audit-v0-1-1-release` both succeeded.
- The stable v0.1.1 release archive audit passed. CI does not substitute for manual updater/button verification on the user's Windows installation.
- Next: continue software-only release-critical review; keep physical adapter/vehicle validation deferred; do not publish stable v0.2.0 without explicit permission. Do not merge stale PR #228 or #225 without explicit permission.

---

## PR #274 merged; updater CI run pagination — 2026-10-17

- PR [#274](https://github.com/vorlocsev/dvinholm/pull/274) (`fix: paginate updater CI run discovery`) was merged into `main` as `53d32922ac4d198d80efc7a4c2d2a6b2d2266c82`.
- Pre-merge CI passed on exact PR head `01d20a0a79c4ef5530b9df92dd6f6967591769f4`: [push run #39044585178](https://github.com/vorlocsev/dvinholm/actions/runs/39044585178) and [pull_request run #39044588419](https://github.com/vorlocsev/dvinholm/actions/runs/39044588419). Both build/test and v0.1.1 release ZIP audit jobs succeeded.
- Post-merge [CI run #39044984373](https://github.com/vorlocsev/dvinholm/actions/runs/39044984373) passed on the exact merge SHA. Build/tests and published v0.1.1 ZIP audit succeeded.
- `LatestBuildUpdater.GetLatestSuccessfulBuildAsync` now requests `page=1,2,...` with `per_page=100`; it continues after a full page and stops after a page with fewer than 100 workflow runs. It only considers successful completed `push` and `workflow_dispatch` runs, avoiding PR synthetic merge SHAs.
- Added regression test `LookupContinuesToSecondPageWhenFirstPageHasNoEligibleBuild`: page 1 contains 100 completed successful PR events (ineligible); page 2 contains a successful push with a valid `ci-201` release asset. Assertions verify the selected SHA/run number and requests to both pages.
- Manual Windows updater/button behavior has not been re-verified by this change; do not claim a runtime UI result. This API lookup change needs no physical J2534 adapter/vehicle test.
- Keep the user's release constraints: do not merge PR #228 without explicit approval; do not publish stable v0.2.0 without explicit authorization.

## Post-PR #272 CI passed; UDS lifecycle source audit — 2026-10-17

- PR [#272](https://github.com/vorlocsev/dvinholm/pull/272) merged as `8eb69feabc652984c4deaa6236efff249a70bd47`.
- Post-merge [CI run #38997313752](https://github.com/vorlocsev/dvinholm/actions/runs/38997313752) completed successfully on the exact merge SHA. The build/test job and audit of the published v0.1.1 ZIP both passed.
- Re-read current-main `MainWindow.xaml.cs` and the test inventory. UDS actions share `_udsGate`; closing the window signals discovery/UDS cancellation and background cleanup waits for tracked work and gate availability before disposing the session. This was source inspection only; no claim of a new WinUI runtime test.
- Existing tests already cover active/queued UDS cancellation, late response correlation, ISO-TP cancellation/recovery, and virtual J2534 flows. Avoid duplicating these tests without a demonstrated gap.
- Next: continue software-only checks; keep updater UI runtime verification outstanding until the user confirms it on Windows. Physical adapter/vehicle tests remain deferred. Do not merge PR #228 or PR #225 without explicit approval. Do not publish stable v0.2.0 without explicit authorization.

---

## Current main checkpoint — PR #271 merged; post-merge CI passed (2026-10-17)

- PR [#271](https://github.com/vorlocsev/dvinholm/pull/271) was squash-merged into `main` as `bffd39ded30af567089dacab1cbdf95f1347912a`.
- Post-merge [CI run #38990733864](https://github.com/vorlocsev/dvinholm/actions/runs/38990733864) succeeded on that exact commit: build/tests, publish pipeline, and audit of the published v0.1.1 ZIP all passed.
- Re-checked the current `LatestBuildUpdater.cs`: malformed/out-of-range run numbers are skipped; release URLs are constrained to the expected HTTPS GitHub project path; malformed digest metadata is rejected; release asset API downloads request binary content.
- Current updater flow also includes the user-initiated discovery/SHA comparison behavior merged in PR #270. CI is not a substitute for checking the WinUI behavior in the actual Windows runtime; that manual check remains outstanding.
- PR #228 remains open and unmerged; do not merge it without separate explicit approval. No stable v0.2.0 release is authorized. Physical J2534 adapter/vehicle checks remain deferred.
- Next: continue software-only review/regression coverage; preserve strict cleanup assertions and do not claim physical-hardware validation.

---

## Updater-state forward-port merged; post-merge CI passed (2026-10-17)

- PR [#270](https://github.com/vorlocsev/dvinholm/pull/270) was squash-merged to `main` as `b2a4661aee05251472ee93f78e1b0ea815dcd33f`.
- Post-merge [CI run #38989890059](https://github.com/vorlocsev/dvinholm/actions/runs/38989890059) completed successfully on that exact commit: build, tests, publish pipeline, and published v0.1.1 ZIP audit passed.
- Forward-ported behavior: update discovery is user-initiated at startup, the button label is **Check update**, existing SHA-based comparison remains in place, regression tests cover same-SHA and different-SHA decisions, and worklog-only pushes are skipped by CI.
- CI does not prove the WinUI updater flow at runtime. Manual Windows confirmation remains a follow-up when the app can be run.
- PR #228 remains open and unmerged; do not merge it without separate explicit approval. No stable v0.2.0 release is authorized. Physical J2534 adapter/vehicle checks remain deferred.

---

## Main CI #2296 retry result (2026-10-17)

- Main commit: `024addce1dbe68a6e573f7e67748e1946f0d4128`.
- Retry attempt 2 passed: build/tests and the v0.1.1 release archive audit succeeded. [Workflow](https://github.com/vorlocsev/dvinholm/actions/runs/38984726872).
- Attempt 1 had 475/476 tests pass. The only failure was `LatestBuildUpdaterFailureTests.ReplacementScript_RestoresPreviousBuildWhenReplacementFailsAtRuntime`, where Windows could not delete the test's temporary root because it was still in use. Attempt 2 passed; root cause remains unconfirmed, so record as a non-reproduced cleanup/lifetime flake rather than a verified product defect.
- No code change was made for this incident. If it recurs, inspect the process/file lifetime before changing cleanup behavior; preserve rollback assertions.
- No physical J2534 adapter or vehicle testing. PR #228 remains open and must not be merged without explicit approval. Stable v0.2.0 remains unpublished and requires explicit user authorization.

---

## Post-PR #260 main CI verification (2026-10-17)

- PR [#260](https://github.com/vorlocsev/dvinholm/pull/260) merged as `23e12e0f188178d469112df44fc1138e26100cab`.
- Main CI [#2268](https://github.com/vorlocsev/dvinholm/actions/runs/38979739897) completed successfully on that exact merge commit; build job passed and no failed steps were reported.
- Updated `ROADMAP.md` to use the exact current main SHA and record the successful post-merge CI checkpoint.
- Remaining release-review items: independently inspect the actual v0.1.1 release archive (asset metadata/digest is available but the bytes have not been inspected); active-session Monitor/UDS UI transition remains unverified because no physical adapter is available.
- No physical J2534 adapter or vehicle testing was performed. Hardware validation remains deferred; stable v0.2.0 remains unpublished and requires explicit user authorization.

---

## Follow-up — investigate Windows rollback-test cleanup failure (2026-10-16)

- The first main CI attempt for `09a68831ceb855a582c9808f7ac3ce56a7562bbf` failed with 475/476 tests passing. The sole failure was `LatestBuildUpdaterFailureTests.ReplacementScript_RestoresPreviousBuildWhenReplacementFailsAtRuntime`: cleanup could not delete its isolated temporary root because Windows reported it was still in use.
- Build succeeded. This is a test cleanup/lifetime failure, not evidence that the updater rollback assertions themselves failed.
- Created branch `test/dispose-powershell-before-cleanup` and changed the test to drain redirected stdout/stderr while PowerShell is running, scope/dispose the Process handle before the finally cleanup, and preserve the rollback assertions. No installed app or real hardware is touched.
- Fix commit: `61f782d74494e230faf9fbe76379fb0b2f702c99`. CI run [#38932131202](https://github.com/vorlocsev/dvinholm/actions/runs/38932131202) is queued; do not claim the fix passes until the new run completes.
- PR #252 was merged as `afef97288c1f9456130f2311b02181ab3b7ffca2`; its PR-head CI [#38931312837](https://github.com/vorlocsev/dvinholm/actions/runs/38931312837) and branch CI [#38931290345](https://github.com/vorlocsev/dvinholm/actions/runs/38931290345) passed. Post-merge main CI [#38932062825](https://github.com/vorlocsev/dvinholm/actions/runs/38932062825) is running.
- Next: finish CI for the cleanup fix, open a PR if successful, and only then resume the UDS shutdown coverage audit. Physical J2534 adapter and vehicle testing remain deferred.

---

## Latest checkpoint — PR #251 merged, post-merge verification pending (2026-10-16)

- PR [#251](https://github.com/vorlocsev/dvinholm/pull/251) was squash-merged as `09a68831ceb855a582c9808f7ac3ce56a7562bbf`.
- PR-head CI [#38930299279](https://github.com/vorlocsev/dvinholm/actions/runs/38930299279) and branch CI [#38930277558](https://github.com/vorlocsev/dvinholm/actions/runs/38930277558) passed. The prior main checkpoint `6ef6046bfd819b436772c8731495d7cc3cc5fb3b` also passed post-merge CI [#38930123098](https://github.com/vorlocsev/dvinholm/actions/runs/38930123098).
- The updated roadmap now records PR #251 and the exact current main merge SHA. Do not call the new merge commit green until its own post-merge workflow completes.
- Review of `MainWindow.xaml.cs` confirms close handling cancels UDS/discovery, waits for tracked operations with bounded timeouts, and avoids disposing the native session while operations remain active. Existing lower-level tests already cover active/queued UDS cancellation and several dispatcher/session disposal races; avoid duplicating those tests.
- Next: inspect current CI test inventory and test seams for a concrete uncovered UI shutdown race. Only add deterministic virtual-adapter coverage if it tests a distinct gap; do not extract a large lifecycle framework just for tests. Physical J2534 adapter and vehicle testing remain deferred.

---

## Latest checkpoint — monitor/disconnect fixes and documentation (2026-10-16)

- PR [#250](https://github.com/vorlocsev/dvinholm/pull/250) was squash-merged as `c38e2e1de7c37c0fc9cfff26c7051ca74100a39b`. It keeps a CAN Monitor warning visible when J2534 disconnect begins instead of immediately overwriting it with the generic disconnect status.
- PR CI [#38928375204](https://github.com/vorlocsev/dvinholm/actions/runs/38928375204) passed. Post-merge main CI on the source-fix commit passed in [#38929247943](https://github.com/vorlocsev/dvinholm/actions/runs/38929247943).
- Documentation PR [#249](https://github.com/vorlocsev/dvinholm/pull/249) was merged as `6ef6046bfd819b436772c8731495d7cc3cc5fb3b`. Its first CI attempt reported one failure in `IsoTpReceiveQueueOverflowRecoveryTests` (475/476); retry [#38928314219](https://github.com/vorlocsev/dvinholm/actions/runs/38928314219) passed. Treat this as a possible timing-sensitive test and watch for recurrence; do not hide or weaken the assertion.
- Source review confirms UDS read-only actions use `TryGetUdsClient`, which checks the active connection and CAN ID format. UDS requests and discovery are serialized by `_udsGate`; the close handler cancels UDS/discovery and avoids disposing the session while tracked operations remain active. More deterministic tests of the shutdown boundary remain a follow-up.
- Current main checkpoint is `6ef6046bfd819b436772c8731495d7cc3cc5fb3b`; post-merge main CI [#38930123098](https://github.com/vorlocsev/dvinholm/actions/runs/38930123098) is still running. Do not mark this exact checkpoint fully green until it completes.
- No physical J2534 adapter or vehicle was used; hardware validation remains deferred.
- Next: add deterministic virtual-adapter coverage for cancellation/close while UDS is active, then continue software-only release gates.

---

## Latest checkpoint — CAN Monitor shutdown before J2534 disconnect (2026-10-16)

- PR [#248](https://github.com/vorlocsev/dvinholm/pull/248) was squash-merged as `6804b8828879df51f24f10a837cb9808b6989f68`.
- The fix captures the monitor task, requests cancellation through `StopMonitor()`, then awaits that task before starting `_session.Disconnect()`. If the monitor task faults, the exception is reported and disconnect cleanup still proceeds.
- PR CI [#38927452306](https://github.com/vorlocsev/dvinholm/actions/runs/38927452306) and branch CI [#38927445038](https://github.com/vorlocsev/dvinholm/actions/runs/38927445038) passed on head `ac81ebc89f16ffe650a81fd1ba1d98dcafcaa588`. Post-merge main CI [#38928179572](https://github.com/vorlocsev/dvinholm/actions/runs/38928179572) passed.
- Source review confirmed Read VIN/Read DTC actions are guarded by `_connected` through `TryGetUdsClient`, and the UDS gate prevents disconnect during an active UDS request. UI read-only commands remain disabled until connection succeeds; do not bypass those guards.
- This change prevents a race between monitor subscription shutdown and native session disconnect. No physical J2534 adapter or vehicle was used; hardware verification remains deferred.
- Next: add deterministic virtual-adapter coverage for cancellation/close while UDS is active; physical adapter testing remains deferred.

---

## Latest checkpoint — updater rollback runtime test merged (2026-10-16)

- PR [#246](https://github.com/vorlocsev/dvinholm/pull/246) was squash-merged to `main` as `91f000014e7d09546f868c12299d2235f13d1f3f`.
- PR workflow [#38925619261](https://github.com/vorlocsev/dvinholm/actions/runs/38925619261) and branch workflow [#38925610701](https://github.com/vorlocsev/dvinholm/actions/runs/38925610701) passed on head `00b3f5b83d84211ac7806d8500513625a19c0fd6`; main CI after PR #245 also passed in [#38925560914](https://github.com/vorlocsev/dvinholm/actions/runs/38925560914).
- Added a Windows-only integration test that executes the actual generated replacement PowerShell script in isolated temp directories. Test doubles replace `robocopy` and `Start-Process`; replacement is made to fail after partially changing the destination. The test verifies the old executable and marker are restored, the partial marker is gone, rollback is logged, and relaunch of the restored executable is requested. It does not modify the installed app or launch the real application.
- Runtime rollback behavior is now exercised in CI rather than inferred only from generated-script text. It remains a controlled test with stubbed filesystem-copy/process behavior; it is not a manual test of the real updater replacing the user's live installation.
- Roadmap updated to main checkpoint `91f000014e7d09546f868c12299d2235f13d1f3f`. Physical J2534 adapter and vehicle testing remain deferred to a future release.
- Next: audit remaining release-critical paths, particularly Read VIN/CAN Monitor connection-state gating and cancellation/disconnect during active reads/monitoring. Do not bypass the requirement for an active J2534/CAN session.

---

## Latest checkpoint — PR #244 merged and CI verified (2026-10-16)

- PR [#244](https://github.com/vorlocsev/dvinholm/pull/244) merged as `8b5797a96791a26775e0a379c36d5ea20559137b`; it records the updater rollback behavior and the important limitation that current tests inspect the generated PowerShell text rather than executing rollback.
- PR CI [#38922788812](https://github.com/vorlocsev/dvinholm/actions/runs/38922788812) and branch CI [#38922769753](https://github.com/vorlocsev/dvinholm/actions/runs/38922769753) passed. Additional branch validation runs [#38922756834](https://github.com/vorlocsev/dvinholm/actions/runs/38922756834) and [#38922728308](https://github.com/vorlocsev/dvinholm/actions/runs/38922728308) passed.
- Post-merge main CI for PR #243 passed: [#38922622760](https://github.com/vorlocsev/dvinholm/actions/runs/38922622760). The previously failed main run for PR #242 passed on retry (attempt 2): [#38921421584](https://github.com/vorlocsev/dvinholm/actions/runs/38921421584); its first failure was only the public CI asset publish step returning GitHub HTTP 500.
- Current main checkpoint: `8b5797a96791a26775e0a379c36d5ea20559137b`.
- Next: create deterministic Windows-only runtime coverage for the updater's replacement-failure rollback in isolated temporary directories. Do not overwrite the real app, launch the actual application, or claim rollback verified until the script is actually exercised. Preserve physical J2534 adapter/vehicle testing as deferred.

---

## Latest checkpoint — updater replacement rollback (2026-10-16)

- PR [#243](https://github.com/vorlocsev/dvinholm/pull/243) passed PR CI [#38921860383](https://github.com/vorlocsev/dvinholm/actions/runs/38921860383) and branch CI [#38921855588](https://github.com/vorlocsev/dvinholm/actions/runs/38921855588), then was squash-merged as `bee02031b3e654d6b7932e0b4f7b32be76495f0b`.
- The replacement helper now attempts to copy the current build into a backup, verifies that backup contains `Dvinholm.App.exe`, and attempts to restore/relaunch it if in-place replacement fails.
- Automated tests assert the generated PowerShell contains the backup/rollback branches and that backup is ordered before replacement. These tests do **not** execute PowerShell, simulate a real copy failure, or prove recovery on Windows.
- The main CI run for the PR #242 merge [#38921421584](https://github.com/vorlocsev/dvinholm/actions/runs/38921421584) failed only at **Publish public CI asset**; build, tests, publish, and packaging steps succeeded. Investigate this CI publishing failure separately. The post-merge CI for #243 is queued as [#38922622760](https://github.com/vorlocsev/dvinholm/actions/runs/38922622760) and must finish before this main checkpoint is marked verified.
- Physical J2534 adapter and vehicle testing remain deferred.

---

## Updater replacement rollback hardening started (2026-10-16)

- PR [#242](https://github.com/vorlocsev/dvinholm/pull/242) passed pull-request CI [run #38920564624](https://github.com/vorlocsev/dvinholm/actions/runs/38920564624) and was squash-merged as `f2a9975353fcaf43ee111210af8cb8c0186a4710`.
- Post-merge main CI for the previous checkpoint [run #38920429003](https://github.com/vorlocsev/dvinholm/actions/runs/38920429003) passed; confirm the fresh main run for #242 before calling the latest checkpoint fully validated.
- The risk in `StartReplacement`: `robocopy /MIR` mutates the installed directory in place, so a copy failure can leave a partial installation. A separate PR is being prepared to back up the current directory, verify the backup executable, and restore/relaunch the previous build if replacement fails.
- The proposed unit test checks generated PowerShell script branches; it does not execute PowerShell or prove runtime rollback. CI must pass before merging. Real adapter/vehicle validation remains deferred.

---

## Corrupt ZIP and missing-executable cleanup follow-up (2026-10-16)

- PR [#241](https://github.com/vorlocsev/dvinholm/pull/241) passed PR and branch CI and was merged as `4106bdbb38fe5ec8e31ae553abc252607aea0ae3`. It strengthens the corrupt-ZIP test to verify that both the temporary archive and staging directory are cleaned.
- Post-merge main CI [#38920429003](https://github.com/vorlocsev/dvinholm/actions/runs/38920429003) is pending at this checkpoint; wait for success before marking the main SHA fully verified.
- New follow-up test on PR #242 strengthens the missing-executable case: after a valid ZIP is extracted but lacks `Dvinholm.App.exe`, both the temporary downloaded archive and staging directory must be removed.
- The test uses a local HTTP stub and generated ZIP content. It does not call GitHub or require a physical J2534 adapter; CI is pending.
- Next after CI: review replacement/restart rollback safety. Physical adapter/vehicle verification remains deferred to a future release.

---

## Updater SHA-256 mismatch cleanup test strengthened (2026-10-16)

- PR [#239](https://github.com/vorlocsev/dvinholm/pull/239) was merged as `adee816ea2bf5704537b25f8fe6720ad0e1137a1` after PR and branch CI succeeded.
- The checksum-mismatch test now verifies that both the staging directory and temporary downloaded ZIP are removed when the artifact's SHA-256 does not match its metadata.
- PR [#238](https://github.com/vorlocsev/dvinholm/pull/238) was also merged as `9bea78b83218b941fabe1a33a6781eb7e2d6fd92`; its PR and branch CI succeeded. The main-branch CI after the updater cancellation change passed in [run #38912910522](https://github.com/vorlocsev/dvinholm/actions/runs/38912910522).
- Main CI after the latest documentation and checksum-test merges is running: [run #38916569004](https://github.com/vorlocsev/dvinholm/actions/runs/38916569004). Do not mark the latest main checkpoint fully verified until it completes successfully.
- Next: review replacement-helper failure/recovery behavior and identify a safe automated test seam; do not attempt physical J2534 adapter or vehicle validation in this phase.

---

## Updater mid-stream cancellation cleanup test merged (2026-10-16)

- PR [#237](https://github.com/vorlocsev/dvinholm/pull/237) was squash-merged as `442f041f7afc355166419c489ac8569ae6e3a595`.
- Added `CancellationAfterPartialDownload_CleansTemporaryArchiveAndStagingDirectory`: the test lets the updater receive the first archive chunk, cancels during the next read, and verifies the temporary ZIP and staging directory are removed.
- PR validation [run #38912135263](https://github.com/vorlocsev/dvinholm/actions/runs/38912135263) and branch CI [run #38912118278](https://github.com/vorlocsev/dvinholm/actions/runs/38912118278) both succeeded. Post-merge main CI [run #38912910522](https://github.com/vorlocsev/dvinholm/actions/runs/38912910522) was in progress when this entry was written; verify it before declaring the merge fully validated.
- This covers cancellation after partial content has been read, complementing the pre-download cancellation and mid-stream I/O failure tests.
- Next: inspect remaining updater integrity and recovery paths (digest mismatch, extraction failure, replacement/restart rollback) and avoid duplicating existing tests.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred to a future release.

---

## Updater partial-download cleanup test merged (2026-10-16)

- PR [#235](https://github.com/vorlocsev/dvinholm/pull/235) was squash-merged as `2f788ca4b1a03452b8370e16c782466161532bba`.
- Added `PartialDownloadFailure_CleansTemporaryArchiveAndStagingDirectory`, which simulates an I/O failure after the first downloaded chunk and verifies both the temporary ZIP and staging directory are cleaned.
- PR-head CI succeeded: [PR run #1811](https://github.com/vorlocsev/dvinholm/actions/runs/38910157051) and [branch run #1810](https://github.com/vorlocsev/dvinholm/actions/runs/38910148879).
- Post-merge `main` CI [run #1812](https://github.com/vorlocsev/dvinholm/actions/runs/38910949365) was still in progress at the time of this entry; do not mark the merge fully validated until it completes successfully.
- Remaining updater review: confirm failure cleanup for cancellation, digest mismatch, extraction failure, and replacement/restart recovery without duplicating existing tests.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred to a future release.

---

## Updater metadata hardening merged and verified (2026-10-16)

- Ported the former stale/conflicting PR #225 work onto current `main` in PR [#233](https://github.com/vorlocsev/dvinholm/pull/233); PR #233 was squash-merged as `a21b0bb07eaea83785e6bb4299130f4943d9ebc8`.
- The PR validation workflow succeeded: [run #1801](https://github.com/vorlocsev/dvinholm/actions/runs/38850583003). The post-merge `main` build/test/publish workflow also succeeded: [run #1802](https://github.com/vorlocsev/dvinholm/actions/runs/38851091919).
- Updater hardening covers malformed run numbers, strict trusted release-download URLs, optional-but-validated SHA-256 digest metadata, and binary download through the GitHub release-asset API when an asset ID is available. Regression tests cover malformed metadata, fallback behavior, and binary asset download.
- The stale PR #225 was not merged; its relevant changes were carried forward and validated on current `main`.
- Latest main checkpoint at this entry: `a21b0bb07eaea83785e6bb4299130f4943d9ebc8`.
- No physical J2534 adapter or vehicle was used. Hardware verification remains deferred to a future release.

## Warning cleanup follow-up on current main (2026-10-16)

- Recreated the unused-exception-variable cleanup against current `main`, rather than relying on the older PR #226 branch.
- Removed unused exception variable names from the updater-check, saved-profile load/import/export, CSV export, J2534 connection, and adapter-unload catch clauses. Catch behavior and user-facing messages remain unchanged.
- Validation is pending CI for the fresh PR. No physical J2534 adapter or vehicle testing is included; those checks remain deferred.
- Historical PR #226 is still open; this follow-up is based on current main to avoid stale-base merge issues.

---

## Stable release published — Dvinholm 0.1.1 (2026-10-16)

- Verified the stable GitHub Release `v0.1.1` is published (not marked as a prerelease): https://github.com/vorlocsev/dvinholm/releases/tag/v0.1.1
- Windows x86 package: https://github.com/vorlocsev/dvinholm/releases/download/v0.1.1/Dvinholm-Windows-x86.zip
- Release commit: `2cb465918a18f7e136d51b1bdd9132d4293e0ade`; main CI run [#1781](https://github.com/vorlocsev/dvinholm/actions/runs/38837028529) succeeded.
- Physical J2534 adapter/vehicle verification remains deferred to a future release.
- Follow-up: revalidate open PRs against the current main branch before considering them for merge. Priorities are updater digest-metadata regression tests (#225) and a software-only review of why Read VIN / CAN Monitor controls remain unavailable without an adapter; do not bypass connection gating.
- Current Windows evidence covers updater success/error retry and compact layout visibility. It does not establish live CAN monitoring or VIN reads without connected hardware.

---

## CI checkpoint — compact ECU layout PR #227 (2026-10-16)

- Latest branch head `596f3da14d2663366d93202dfd612672ffb06419` passed both GitHub Actions workflows: push run [#1771](https://github.com/vorlocsev/dvinholm/actions/runs/38835346384) and pull-request validation [#1772](https://github.com/vorlocsev/dvinholm/actions/runs/38835349822).
- The preceding head's push and PR validation also passed (runs [#1769](https://github.com/vorlocsev/dvinholm/actions/runs/38835188813) and [#1770](https://github.com/vorlocsev/dvinholm/actions/runs/38835192316)). The earlier “CI still pending” note below is historical and superseded by this checkpoint.
- User-confirmed manual checks: CAN Monitor remains visible after selecting an ECU profile; profile selector and Import/Export JSON controls are not clipped at normal size or after narrowing the window.
- This validates CI and the reported layout behavior only; it does not test live CAN traffic or physical J2534 hardware. Physical-adapter verification remains deferred to a future release.
- PR [#227](https://github.com/vorlocsev/dvinholm/pull/227) remains open and unmerged. Do not merge without explicit user approval.

---

## Windows manual verification — compact ECU layout at narrower width (2026-10-16)

- User confirmed that the ECU profile selector and Import/Export JSON controls remain visible after reducing the application window width.
- Together with the prior checks (CAN Monitor stays visible after selecting a profile; profile-row controls are not clipped at the normal window size), the compact ECU layout's targeted manual visibility checks are complete for the tested window sizes.
- These checks cover UI layout only, not live CAN monitoring or physical J2534 hardware. Real-adapter testing remains deferred to a future release.
- Latest code/worklog commit CI is still pending at handoff; check runs [#1769](https://github.com/vorlocsev/dvinholm/actions/runs/38835188813) and [#1770](https://github.com/vorlocsev/dvinholm/actions/runs/38835192316) before calling the PR fully validated. PR [#227](https://github.com/vorlocsev/dvinholm/pull/227) remains open and unmerged.

---

## Windows manual verification — compact ECU profile controls (2026-10-16)

- User confirmed that the selected ECU profile row's controls are fully visible and not clipped, including the profile selector and Import/Export JSON actions.
- Combined with the preceding check that CAN Monitor remains visible after selecting a profile, the compact layout's targeted vertical-space regression is manually verified at the user's current window size.
- This verifies layout visibility only; it does not test live CAN monitoring or physical J2534 hardware. Real-adapter testing remains deferred to a future release.
- PR [#227](https://github.com/vorlocsev/dvinholm/pull/227) remains open and unmerged. Next: perform a brief resize check (if practical) to ensure the compact row remains usable at a narrower window width; then review the PR's current CI status before any merge decision.

---

## Windows manual verification — compact ECU profile layout (2026-10-16)

- User confirmed that **CAN Monitor is visible** after selecting an ECU profile in the compact layout, resolving the reported vertical-space regression for this check.
- This is a visual layout check only; it does not verify live CAN monitoring or physical J2534 hardware. Real-adapter testing remains deferred to a future release.
- PR [#227](https://github.com/vorlocsev/dvinholm/pull/227) remains open and unmerged. Next: continue checking that the profile row's controls are not clipped and that the layout remains usable at the current window size.

---

## Windows manual verification — updater same-SHA fix (2026-10-16, follow-up)

- User reports the updater button now displays `Check update` instead of `Replace files and restart` after updating to the corrected build, addressing the repeated-install prompt for the same SHA.
- CI #1763 (push) and #1764 (PR validation) for commit `2ffca912f2a80e12960b1bbadfcfaf1fad36da93` both succeeded:
  - https://github.com/vorlocsev/dvinholm/actions/runs/38834397342
  - https://github.com/vorlocsev/dvinholm/actions/runs/38834401620
- PR #227 remains open and unmerged. PR #228 remains open and unmerged. No PR was merged without explicit user approval.
- Physical J2534 adapter testing remains deferred to a future release.

---

## Windows manual updater regression — same SHA still offered Replace (2026-10-16)

- User reported `Current: 0.1.0 · f16912e` and `Available: CI #1761 · f16912e`, but the button still offered replacement after updating.
- Root cause: the compact-ECU-profiles branch still used the old comparison that treated a missing installed CI run number as an update, even when the commit SHA matched. The earlier SHA-only fix existed on PR #228 but was not present in this branch's build.
- Applied the SHA-only availability check to this branch too. Identical commit SHA now means no update regardless of whether the installed CI run number is embedded/parseable; a different or unknown SHA still offers replacement. The idle button label is `Check update`.
- Added regression tests for same-SHA/no-run-number and different-SHA cases.
- CI and Windows re-test are pending on this new branch commit. PR [#227](https://github.com/vorlocsev/dvinholm/pull/227) remains open and unmerged. PR [#228](https://github.com/vorlocsev/dvinholm/pull/228) also remains open and unmerged.
- Physical J2534 adapter testing remains deferred to a future release.

---

## UI layout validation checkpoint — PR #227 (2026-10-16)

- Checked commit `d8210f95c70b94cde6146441f10c5b24e41e454d`: both GitHub Actions build checks completed successfully ([run 38824990193](https://github.com/vorlocsev/dvinholm/actions/runs/38824990193), [run 38824981023](https://github.com/vorlocsev/dvinholm/actions/runs/38824981023)).
- The compact ECU-profile layout change is CI-validated, but Windows visual verification remains pending: confirm CAN Monitor remains visible while selecting an ECU profile and that the compact row does not clip controls.
- No physical J2534 adapter is required for this layout check; adapter-dependent functions such as Read VIN and starting a live monitor can remain unavailable without a connected device.
- PR [#227](https://github.com/vorlocsev/dvinholm/pull/227) remains open and unmerged.

---

# UI layout follow-up — compact ECU profiles (2026-10-16)

## User-reported issue
- The ECU Profiles section consumed too much vertical space. After selecting a profile, the CAN Monitor table was pushed below the visible window area.

## Changes on branch `ui/compact-ecu-profiles`
- Reduced the ECU discovery list's fixed height from 100 px to 76 px.
- Replaced the tall stacked “Selected ECU” card with a single compact row containing ECU details, profile selector, and Import/Export JSON actions.
- Removed the profile selector's separate header and clipped long ECU detail text with an ellipsis so it does not expand the row vertically.
- This is a layout-only change; connection-gated controls remain gated. No physical adapter test is required for this UI change.

## Validation status
- Source change committed; CI and Windows manual validation have not yet been confirmed.
- Next: review PR CI, then ask the user to launch the new artifact and confirm that CAN Monitor remains visible when selecting an ECU profile. If the table still moves out of view, inspect the remaining parent-grid sizing and adapt the layout further.
- Physical J2534 adapter validation remains deferred to a future release.

---

# Manual regression update — 2026-10-15 (obsolete-file cleanup confirmed)

- User confirmed that the in-place updater removes obsolete files from the application directory during replacement.
- The updater's key manual regression checks are now confirmed by the user: update succeeds without HTTP 401, ECU profiles are preserved, the updated app closes/reopens normally, and obsolete files are removed.
- This confirms the observed Windows update flow; it does not replace CI or broader failure-path testing.
- PR [#222](https://github.com/vorlocsev/dvinholm/pull/222) remains open and unmerged. Do not merge without explicit user approval. Physical J2534 adapter testing remains deferred to a future release.

---

# Manual regression update — 2026-10-15 (updated app relaunch confirmed)

- User confirmed that after the in-place update, Dvinholm closes and launches again normally.
- Combined manual evidence for this updater change: update completed successfully (no HTTP 401), saved ECU profiles remained present, and the updated application reopened successfully.
- Obsolete-file deletion has not yet been independently confirmed by the user; keep that check open rather than claiming it passed.
- Latest PR-head CI workflow run observed: [run #1576](https://github.com/vorlocsev/dvinholm/actions/runs/38659841631), still in progress at the time checked. Recheck before declaring the latest branch head green.
- PR [#222](https://github.com/vorlocsev/dvinholm/pull/222) remains open and unmerged; do not merge without explicit user approval. Physical J2534 adapter testing remains deferred.

---

# Manual regression update — 2026-10-15 (profiles preserved after in-place update)

- User confirmed that existing ECU profiles were preserved after the successful in-place application update.
- This closes the manual profile-persistence check for the current updater build.
- Remaining practical post-update check: confirm Dvinholm can be closed and reopened normally and, if observable, that obsolete application files were removed by the replacement step.
- CI runs for the work-log update are in progress; see [push run #1573](https://github.com/vorlocsev/dvinholm/actions/runs/38659748093) and [PR run #1574](https://github.com/vorlocsev/dvinholm/actions/runs/38659752664).
- PR #222 remains open and unmerged; do not merge without explicit user approval. Physical J2534 testing remains deferred.

---

# Latest handoff update — 2026-10-15 (public CI updater manually confirmed)

## Verified checkpoint
- User manually confirmed that the in-app update completed successfully on Windows after the updater switched away from the authenticated GitHub Actions artifact endpoint. This confirms the observed HTTP 401 failure is resolved for this update path.
- CI push run [#1571](https://github.com/vorlocsev/dvinholm/actions/runs/38658165715) succeeded on commit `5cf4783f95c6769b96c43c8c9a3426ba8f43c40d`; PR validation run [#1572](https://github.com/vorlocsev/dvinholm/actions/runs/38658169191) also succeeded.
- Public prerelease [ci-1571](https://github.com/vorlocsev/dvinholm/releases/tag/ci-1571) exists and contains `Dvinholm-Windows-x86.zip` (66,927,853 bytes), GitHub digest `sha256:27dc59af143206d7c1b8435cac69a82dcf53e398b38043a83b1764e2393597a4`.
- PR [#222](https://github.com/vorlocsev/dvinholm/pull/222) remains open and unmerged. Do not merge without explicit user approval.
- Physical J2534 adapter testing remains deferred to a future release.

## Next manual regression check
1. Confirm existing ECU profiles are still present and can be opened after replacement.
2. If possible, confirm obsolete files from the prior app build are removed and the updated app still closes/reopens cleanly.
3. Keep the PR open until the remaining update checks and review are complete.

---

## Follow-up — updater failure diagnostics and current CI label (2026-10-15)

- User reported the generic “Update preparation failed; the current app was left running” message and requested the Current label include the CI run number.
- Current build label now displays `CI #<run-number> · <7-char-SHA>` when CI metadata is embedded, matching the compact build identity.
- Update failures now write the full exception (including inner exception details) to `%LOCALAPPDATA%\\Dvinholm\\update.log` and show the sanitized error message in the app instead of hiding the cause behind a generic prompt. The current app remains running if preparation/replacement-helper startup fails.
- Next: run CI, then ask user to retry the update and report the exact status message or log if it fails again. Confirm the app was launched from an ordinary writable folder; replacement needs permission to modify that folder. Do not merge PR #222 without explicit approval. Physical J2534 testing remains deferred.

---

# Latest handoff update — 2026-10-15 (compact CI title and build ID)

## User request
- Show the current build's commit identifier using exactly the first 7 SHA characters.
- Reduce the app/window title to `Dvinholm CI #<run-number>`; remove “J2534 CAN Diagnostics” and the descriptive subtitle from the visible app header.

## Changes on branch `ui/compact-ci-title`
- Window caption is set at startup from embedded build metadata: `Dvinholm CI #<GitHub Actions run_number>`.
- Removed the in-content product heading and OS/WinUI/x86/read-only description; kept the update status row and update button.
- CI publish metadata now embeds both the full source SHA and workflow run number (`0.1.0+<sha>.ci<run_number>`).
- Current build display retains the semantic version and displays only the first 7 characters of the embedded commit SHA. Update availability continues comparing the full SHA.
- Updated metadata parsing to extract the full SHA and CI run number separately, preserving exact-commit comparisons.

## Validation status
- Pull-request CI passed on commit `cbc5ef8c6ae47f35820923770d954e2aec54d0e6` (run #1522); build, tests, publish, and artifact upload all succeeded. Manual Windows UI validation is still pending.
- Do not claim the title or current-build label verified on Windows until the new artifact is launched.
- Physical J2534/vehicle testing remains deferred; this UI change does not alter that scope.

## Next actions
1. Update this work log to reflect the successful CI run; this documentation-only commit triggers another CI run.
2. Once that CI run passes, ask the user to launch the artifact on Windows and confirm the caption is `Dvinholm CI #<number>`, the app header no longer contains the removed heading/description, and the current build SHA is shown as 7 characters.
3. Keep the updater targeting latest successful `main` CI until the later explicit switch to published Releases.

---

# Latest handoff update — 2026-10-15 (latest CI build updater in progress)

## User request
- Add an automatic/manual check for the latest successful CI build, show the current and available build versions, and provide an update button.
- Initial update channel must use the latest successful Windows x86 CI artifact from `main`; a later iteration should switch the source to the latest published GitHub Release.

## Changes currently on branch `feature/latest-build-updater`
- Added `LatestBuildUpdater.cs`: queries successful `main` workflow runs, locates the `Dvinholm-Windows-x86` artifact, downloads it, checks archive size/path safety, extracts it to a commit-specific directory under `%LOCALAPPDATA%\\Dvinholm\\Builds`, and launches the new executable without overwriting the running copy.
- Added current/available version display and an update button in the main window; startup check is queued asynchronously.
- Set project version metadata to `0.1.0` and updated CI publish to embed the source commit SHA in `AssemblyInformationalVersion`, so builds can compare exact commits.
- Documented update behavior, profile persistence, rollback folder retention, and the future switch to the Releases API in README.

## Validation status
- Implementation is not yet validated by CI or a Windows manual test. Do not mark this feature complete until the current branch-head CI passes and the download/update flow is manually checked on Windows.
- No release has been published for this feature. Physical J2534/vehicle testing remains deferred.

## Next actions
1. Run CI on the latest branch head; fix any build/test/workflow errors.
2. Verify a public-repository CI artifact can be downloaded by the app without authentication, and confirm the updater correctly handles no network, no artifact, and corrupt/oversized archive cases.
3. On Windows 11 IoT LTSC 26100.9550, verify current/available labels, same-build behavior, download/extraction, launching the new build, and preservation of ECU profiles.
4. Update this handoff and the v0.2.0 roadmap only after results are verified.
5. Later, switch the source from the latest successful `main` CI artifact to the latest published GitHub Release after that policy is explicitly implemented and tested.

---

# Latest handoff update — 2026-10-15 (v0.1.0 published)

## Verified release checkpoint
- User requested the first public release, version 0.1.0. Verified via GitHub API that tag `v0.1.0` exists and points directly to `b3a9a82e8bb26527b34551c4b93b3e3381a6f94d`.
- GitHub Release [Dvinholm 0.1.0 — Software MVP](https://github.com/vorlocsev/dvinholm/releases/tag/v0.1.0) is published (not draft/prerelease).
- Release asset `Dvinholm-Windows-x86.zip`: 66,752,893 bytes; GitHub digest `sha256:990bb1e762b46bc0dff2883558c0b7398924f1e16b6b59434cd51d40047017bb`.
- Exact release commit CI [run #38644431705](https://github.com/vorlocsev/dvinholm/actions/runs/38644431705) passed Restore, Build, Test, Publish, and Upload artifact.
- The user manually launched the immediately preceding app artifact from run #38640011679 on Windows 11 IoT LTSC 26100.9550 and confirmed the intended title and no-device status. The release commit after that test was documentation-only; do not claim the exact release ZIP was separately manually launched.
- Release notes and artifact are available at the published release URL above. The portable ZIP is not an installer.
- Physical DiCE/Mongoose adapter validation remains deferred to a future release. Remaining UI-only unverified paths are the no-registered-provider branch and UI recovery after read error/diagnostic cancellation; do not uninstall/disable drivers merely to test provider absence.

## Next actions
1. Align repository release notes and this handoff log with the published tag, exact commit, CI run, attached asset, and digest.
2. Run CI for that documentation-only follow-up PR and merge it if green.
3. After docs are synchronized, continue only with a concrete, non-duplicative software/UI regression gap or plan the next release scope; preserve virtual-adapter testing and defer real-hardware verification.

---

# Latest handoff update — 2026-10-15 (exact current main artifact manually launched)

## Verified checkpoint
- Current main checkpoint/artifact under manual test: commit `6a431be2ba7c7bb6451a13ceecadc63dcef83b01`, workflow [#38640011679](https://github.com/vorlocsev/dvinholm/actions/runs/38640011679), artifact `Dvinholm-Windows-x86` (ID `11930375485`).
- User confirmed the exact artifact launched successfully on Windows 11 IoT LTSC 26100.9550.
- User confirmed the window caption is `Dvinholm — J2534 CAN Diagnostics` and, with no physical adapter connected, the app correctly reports that no device is present.
- This closes the fresh manual launch/title/no-device smoke check for the exact artifact whose ZIP integrity, SHA-256, and x86 executable architecture were independently verified.
- Full CI for this exact main commit passed Restore, Build, Test, Publish, and Upload artifact. The artifact is portable/self-contained, not an installer.
- No release tag or public GitHub Release has been created. This remains a verified main build, not yet a frozen/versioned release candidate.
- Physical DiCE/Mongoose validation remains deferred to a future release cycle and is not a first software-MVP blocker.

## Next actions
1. Freeze the software-only MVP scope and choose a version number.
2. Keep the remaining unverified UI paths (no registered provider and UI-only read-error/cancellation recovery) explicitly documented; do not disable/uninstall drivers just to force the no-provider state.
3. Run full CI on the exact frozen release-candidate SHA and verify the uploaded artifact and hash against that candidate.
4. Align release notes, tag, commit, and artifact; publish only after explicit user approval.

---

# Latest handoff update — 2026-10-15 (current main CI and artifact independently verified)

## Verified current checkpoint
- Current `main` commit: `6a431be2ba7c7bb6451a13ceecadc63dcef83b01`, following merged [PR #215](https://github.com/vorlocsev/dvinholm/pull/215).
- Post-merge workflow [run #38640011679](https://github.com/vorlocsev/dvinholm/actions/runs/38640011679) completed successfully on that exact commit. Restore, Build, Test, Publish, and Upload artifact all passed.
- Artifact: `Dvinholm-Windows-x86`, ID `11930375485`, 66,753,007 bytes; GitHub digest: `sha256:793bb88455ca67f1517a7fc101447ba972a00d79bc49daf9556cd9a220c8c384`.
- Downloaded the exact artifact ZIP as `Dvinholm-Windows-x86-main-6a431be.zip`. Local SHA-256 matches GitHub metadata exactly: `793bb88455ca67f1517a7fc101447ba972a00d79bc49daf9556cd9a220c8c384`.
- ZIP integrity passed (`testzip() = None)); archive contains 476 entries, including `Dvinholm.App.exe` and `RestartAgent.exe`. Both PE headers identify machine `0x014c` (x86).
- This independently verifies the exact archive, digest, and executable architecture. It does not count as a fresh manual launch of this exact artifact; prior user-confirmed Windows smoke tests were on earlier artifacts.
- Artifact is a portable self-contained Windows x86 build, not an installer. No version tag or public GitHub Release has been created.
- Physical J2534 adapter validation remains deferred to a future release cycle.

## Next actions
1. If practical, run a fresh manual launch/close/relaunch smoke test on this exact artifact; otherwise keep that evidence gap explicit.
2. Decide MVP version and scope before calling this a release candidate.
3. Keep no-provider UI and read-error/cancellation UI recovery paths marked unverified where noted.
4. Align release notes, tag, commit, and artifact to one exact candidate SHA if a versioned release is approved.
5. Do not create a version tag or public release without explicit user approval.

---

# Latest handoff update — 2026-10-14 (current main CI and artifact independently verified)

## Verified current checkpoint
- Current `main` commit: `e85dee15d86a593c2d1fd18b6eddc0f66516fe93`, following merged [PR #214](https://github.com/vorlocsev/dvinholm/pull/214).
- Post-merge workflow [run #38614877115](https://github.com/vorlocsev/dvinholm/actions/runs/38614877115) completed successfully on that exact commit. Restore, Build, Test, Publish, and Upload artifact all passed.
- Artifact: `Dvinholm-Windows-x86`, ID `11919756001`, 66,752,717 bytes; GitHub digest: `sha256:6df5b6f48372ff0143d54a9344ece0edf8e5f880feea1c36e1fceb0574173104`.
- Downloaded the exact artifact ZIP locally as `Dvinholm-Windows-x86-main-38614877115.zip`. Local SHA-256 matched GitHub metadata exactly: `6df5b6f48372ff0143d54a9344ece0edf8e5f880feea1c36e1fceb0574173104`.
- ZIP integrity check passed (`testzip() = None`); archive contains 476 entries, including `Dvinholm.App.exe` and `RestartAgent.exe`. Both PE headers identify machine `0x014c` (x86).
- This independently verifies the current artifact archive, checksum, and executable architecture. It does not count as a fresh manual launch of this exact artifact; prior user-confirmed Windows smoke tests were on earlier artifacts.
- Artifact is a portable self-contained Windows x86 build, not an installer. No version tag or public GitHub Release has been created.
- Physical J2534 adapter validation remains deferred to a future release cycle.

## Next actions
1. Ask for/await a fresh manual smoke test of this exact artifact only if practical; otherwise explicitly retain that as an outstanding release-evidence item.
2. Decide MVP version and scope before calling this a release candidate.
3. Keep no-provider UI and read-error/cancellation UI recovery paths marked unverified where noted.
4. Align release notes, tag, commit, and artifact to one exact candidate SHA if a versioned release is approved.
5. Do not create a version tag or public release without explicit user approval.

---

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


## Latest main build — user-confirmed Windows launch — 2026-10-15

- The user confirmed that the latest Dvinholm Windows build launched successfully on Windows 11 IoT LTSC 26100.9550. This closes the outstanding manual launch check for the latest ZIP available to the user; the exact artifact ID/hash should be matched to the local file if a release candidate is later frozen.
- Physical J2534 adapter testing remains deferred to future releases; no physical adapter or vehicle compatibility is claimed.
- Main commit under CI review at the time of this entry: `15e82064de8214ff45d6b06e61475874398d620d`; post-merge workflow run [#38641403628](https://github.com/vorlocsev/dvinholm/actions/runs/38641403628) had Restore and Build completed successfully, with test-host setup still in progress. Do not call the run fully green until Test, Publish, and Upload artifact complete successfully.
- Next: confirm the complete post-merge CI result, then freeze the software-only MVP candidate. Public tag/release still requires explicit user approval.


## Follow-up — update from the newest successful CI build and replace old files (2026-10-15)

- User clarified that the in-app updater must select the latest successful CI artifact across all branches, not only `main`. The updater examines recent runs of the repository CI workflow in descending recency and accepts the first completed-success `push` or manual `workflow_dispatch` run with a non-expired `Dvinholm-Windows-x86` artifact, across all branches. Feature-branch builds are eligible and may contain unmerged work, so they are not necessarily release-stable. Pull-request validation runs are excluded because they can build a synthetic merge ref whose SHA differs from the branch commit. The UI displays the CI run number and seven-character SHA.
- Update installation downloads and verifies the selected artifact, extracts it to a staging directory, then starts a PowerShell helper. The helper waits for the current app process to exit and uses Robocopy `/MIR` to replace files in the **same directory from which Dvinholm was launched**, deleting obsolete files in that directory before restarting the app.
- After successful replacement, the helper removes old per-build cache directories except the active directory if it is inside `Builds`, and removes stale staging folders. ECU profiles remain separate at `%LOCALAPPDATA%\\Dvinholm\\ecu-profiles.json` and are not deleted by the mirror. Helper errors are recorded in `%LOCALAPPDATA%\\Dvinholm\\update.log` and opened in Notepad.
- Update detection compares both the full commit SHA and CI run number, so a newer CI artifact for the same source commit can still be offered.
- Validation status at handoff: changes pushed to PR #222; CI must pass on the latest PR head before treating the code as validated. Manual Windows test remains required: update from an older build, confirm in-place replacement/restart, confirm an obsolete file is removed from the app directory, and verify saved ECU profiles remain. Physical J2534 hardware testing remains deferred.

## Follow-up — fix anonymous updater downloads after HTTP 401 (2026-10-15)

- Root cause confirmed from `%LOCALAPPDATA%\\Dvinholm\\update.log`: the updater requested `/actions/artifacts/{id}/zip`, an authenticated GitHub Actions artifact-download endpoint. The public repository's workflow artifact metadata can be read anonymously, but the artifact ZIP endpoint returned HTTP 401.
- Changed the updater to resolve the matching public GitHub prerelease asset tagged `ci-<run_number>` and download its `browser_download_url` without credentials. It still verifies the downloaded size and GitHub asset SHA-256 digest when provided, then validates archive paths and the executable before staging.
- Changed `.github/workflows/ci.yml` to package the published Windows x86 output into `Dvinholm-Windows-x86.zip` and publish it as a public prerelease asset for successful non-PR builds. The normal Actions artifact remains available for manual testing. Pull-request validation runs do not publish releases.
- The workflow needs `contents: write` to create the automated prerelease. No personal access token is embedded in Dvinholm; the workflow uses its short-lived `github.token`.
- Validation pending: CI must pass on the newest PR head; confirm the workflow creates the `ci-<run_number>` prerelease and that its asset digest/size are present in the release API. Then manually test update on Windows and confirm profiles persist and obsolete files are removed.
- PR #222 remains open and unmerged. Physical J2534 adapter testing remains deferred.


## Updater failure-path test coverage — PR #224 (2026-10-15)

- Opened [PR #224](https://github.com/vorlocsev/dvinholm/pull/224) to make the updater's download/preparation path accept an injected `HttpClient` while keeping the production entry point unchanged.
- Added automated tests for HTTP 401/network-style request failure, artifact size mismatch, SHA-256 mismatch, corrupt ZIP, unsafe ZIP entry paths, and missing `Dvinholm.App.exe`. Failure tests assert that the per-build staging directory is not left behind.
- Tests use a local stub HTTP handler and generated ZIPs; they do not call GitHub or require a physical J2534 adapter.
- Validation pending: PR CI must compile and run the tests before considering these checks complete. The current CI run was queued at handoff.
- Follow-up after CI: fix any failures, then consider extracting release discovery parsing into a testable component and adding cancellation/oversized-download cleanup cases. Physical J2534 adapter and vehicle tests remain deferred.

## Updater failure-path tests — CI verified (2026-10-15)

- PR [#224](https://github.com/vorlocsev/dvinholm/pull/224) head `5f406b81487c8053042f4201a6c57352e22836d9` passed GitHub Actions: Restore, Build, Test, Publish, and artifact publication all succeeded.
- The test step reported **444 passed, 0 failed, 0 skipped**. This includes the six isolated updater failure-path tests; they use a stub HTTP handler and do not make live GitHub requests.
- PR #224 remains open and unmerged pending review/explicit merge authorization.
- PR #222 was subsequently merged into `main` as `4be4590832fba9e89dcf40973b877e02d4a07a30`; earlier handoff notes saying it remained open describe the state at their respective checkpoints and are historical.
- Next coverage candidates: cancellation/oversized-response cleanup and testable parsing of latest-build discovery metadata. Keep physical J2534 adapter and vehicle checks deferred to a future release.

## Additional updater failure tests — PR #224 (2026-10-15)

- Added two more isolated tests on the PR branch: a response declaring a download larger than the configured 500 MiB limit is rejected before streaming, and cancellation while awaiting the HTTP response propagates as cancellation.
- Both tests assert that no per-build staging directory is left behind. They use a stub handler and do not contact GitHub or require a physical adapter.
- The earlier PR head passed 444/444 tests. These two new tests are not yet validated; wait for CI on the latest head before claiming the updated test count is green.


---

# Automated updater metadata lookup tests — 2026-10-15

- Added an injectable `HttpClient` overload for `GetLatestSuccessfulBuildAsync`; the production entry point still uses the configured shared client.
- Added deterministic HTTP-stub tests for selecting a successful push/workflow-dispatch run while ignoring pull-request and failed runs, extracting the expected release asset metadata, rejecting a malformed workflow-runs response, and reporting when no eligible successful run exists.
- Tests do not access GitHub over the network and do not require a physical J2534 adapter.
- Validation for this addition is pending CI on the latest PR head; do not mark these new tests passed until that run completes.
- PR [#224](https://github.com/vorlocsev/dvinholm/pull/224) remains open and unmerged. Do not merge without explicit user approval.


---

# CI follow-up — deterministic CAN disposal test — 2026-10-15

- The new updater lookup tests compiled, but the full suite exposed an existing scheduler-sensitive failure in `CanBusDisposeQueuedSendTests.DisposePreventsQueuedSendFromEnteringAdapterAfterActiveWriteCompletes` (448 passed, 1 failed at that run).
- Root cause in the test: a fixed delay did not prove that `Dispose` had atomically marked the bus disposed before the active adapter write was released.
- Added an internal read-only disposal-state accessor and changed the test to wait for that state with a bounded timeout before releasing the active write. This makes the intended race ordering explicit rather than timing-based.
- Validation is pending CI on the latest head; do not mark the suite green until the rerun completes.
- PR [#224](https://github.com/vorlocsev/dvinholm/pull/224) remains open and unmerged. No physical J2534 adapter is required.


## Release metadata fallback coverage — PR #224 (2026-10-15)

- Latest CI for commit `74d2bf003fe931adc1898c91ab1daec76a65edef` passed both GitHub Actions checks; the full test suite reported **449 passed, 0 failed, 0 skipped**.
- Added two stubbed-HTTP lookup tests: a 404 for the newest CI prerelease falls back to an older published build, and a release lacking the expected `Dvinholm-Windows-x86.zip` asset also falls back instead of selecting invalid metadata.
- These tests are committed to PR #224 and still require CI on the new head. No live GitHub requests or physical J2534 hardware are used by the tests.
- PR #224 remains open and unmerged; wait for CI on the newest commit before marking these two tests validated.


## CI green and HTTPS metadata validation — PR #224 (2026-10-15)

- CI for `1d90ffd528154f5cffe7cd3a0ccd0f69cedd44b9` passed with **451 passed, 0 failed, 0 skipped**.
- Added a lookup test ensuring a release asset with an insecure `http://` download URL is rejected and discovery falls back to the previous valid HTTPS asset.
- The new test is isolated with a stub HTTP handler. CI validation is pending for the latest branch head.
- PR #224 remains open and unmerged; physical adapter testing remains deferred.


## Updater metadata hardening — port to current main (2026-10-16)

- The original PR [#225](https://github.com/vorlocsev/dvinholm/pull/225) was found to be based on an obsolete main commit and GitHub reports it as conflicting; it has not been merged.
- Preparing a fresh branch from current main to carry forward its updater metadata validation, trusted GitHub release URL allowlist, SHA-256 digest checks, malformed run-number fallback, and binary release-asset API download request.
- The current-main `IsUpdateAvailable` helper is preserved in the port.
- The original PR head had successful CI (workflow run [#1718](https://github.com/vorlocsev/dvinholm/actions/runs/38823643808)); the fresh port must pass its own CI before it can be considered validated.
- Hardware checks remain out of scope; physical J2534 adapter and vehicle verification stays deferred to a future release.


## PR #253 merged — rollback test cleanup fix (2026-10-16)

- PR [#253](https://github.com/vorlocsev/dvinholm/pull/253) passed latest PR-head CI [#38932272236](https://github.com/vorlocsev/dvinholm/actions/runs/38932272236) and was squash-merged to `main` as `db3e8607ec0c28057649f7f616056a71b9bc991e`.
- The runtime rollback test now disposes its PowerShell process before deleting the isolated temporary directory; stdout/stderr are drained concurrently. Rollback assertions were retained.
- Post-merge main CI [#38933065485](https://github.com/vorlocsev/dvinholm/actions/runs/38933065485) completed successfully; build, tests, publish, and artifact steps passed on merge commit `db3e8607ec0c28057649f7f616056a71b9bc991e`.
- Software-only UDS lifecycle audit reviewed MainWindow.Closed plus existing active/queued UDS cancellation, ISO-TP cancellation/recovery, and J2534 session lifecycle tests. The close handler cancels work and only schedules session disposal after tracked workers and the UDS gate are stopped.
- No distinct uncovered race was demonstrated in the testable layers, so no duplicate test was added. Directly unit-testing the WinUI Closed handler would require extracting a focused shutdown coordinator; defer that refactor until it provides a concrete regression benefit.
- Physical adapter and vehicle testing remain deferred.

## Manual Windows connection-retry check (2026-10-16)

- User confirmed success for the manual retry check after a failed J2534 connection attempt. The app remained usable and the retry path behaved as expected.
- This was a software/UI check only; no physical J2534 adapter or vehicle was used, so it does not establish hardware compatibility.
- Next: continue with software-only validation of connection-state controls and error recovery. Keep real adapter/vehicle checks deferred.

## Manual Windows verification — diagnostic controls gated without adapter (2026-10-16)

- User confirmed that with no physical J2534 device connected, Read VIN, Read DTC, and Start Monitor remain unavailable; connection failure is handled without hanging and the app remains usable.
- This verifies the expected UI safety gate only. It does not verify live CAN/UDS behavior or physical hardware compatibility.
- Next: continue software-only release checks and keep physical adapter/vehicle tests deferred.


## Manual Windows verification — profile persists across restart (2026-10-16)

- User confirmed that a previously saved ECU profile remains available after closing Dvinholm normally and launching it again.
- This verifies local profile persistence across an ordinary app restart; it does not validate ECU communication or physical adapter behavior.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: continue software-only release checks and record each confirmed manual result.


## Manual Windows verification — edited profile persists across restart (2026-10-16)

- User confirmed that changing a safe profile field (profile name), saving the profile, closing and relaunching Dvinholm preserves the updated name.
- The profile remains available and its edited value persists after restart; this is a local UI/storage check only.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: continue with a non-destructive software-only profile check; do not delete the user's only useful profile.


## Manual Windows verification — edited profile exports correctly (2026-10-16)

- User confirmed that exporting the edited ECU profile to JSON succeeds and the exported file contains the updated profile name with the expected intact structure/fields.
- This verifies profile export and local JSON serialization only; it does not validate ECU communication or physical adapter behavior.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: perform a safe import round-trip using a separate copy/file so the original profile remains untouched.


## Manual Windows verification — profile JSON import round-trip (2026-10-16)

- User confirmed that importing the exported ECU profile JSON succeeds and the imported profile retains the expected name and fields.
- The original profile remains available; this verifies the local export/import round-trip and profile data preservation only.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: check profile isolation by switching between profiles, but first ensure a second disposable/test profile exists; do not risk modifying or deleting the only useful profile.


## Manual Windows verification — profile isolation/switching (2026-10-16)

- User confirmed that the original ECU profile and a separate `TEST-ECU` profile are both available and can be opened independently.
- Profile names/data remain distinct while switching; no cross-profile mixing was observed.
- This verifies local profile selection and data isolation only. No physical J2534 adapter or vehicle was used; hardware validation remains deferred.
- Next: verify safe deletion behavior using only the disposable `TEST-ECU` profile, keeping the original useful profile untouched.


## Manual Windows verification — delete disposable profile safely (2026-10-16)

- User confirmed that deleting the disposable `TEST-ECU` profile removes it from the profile list, the original profile remains intact, and after restarting Dvinholm the deleted test profile does not reappear.
- This verifies local profile deletion/persistence and preservation of the remaining profile only; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: run a final software-only profile workflow check, then review outstanding release-gate items and PR/CI status before deciding whether to prepare release 0.2.0.


## Manual Windows verification — complete profile workflow (2026-10-16)

- User confirmed the complete profile workflow succeeded: opened the original profile, temporarily renamed and saved it, exported JSON, restarted Dvinholm and confirmed the changed name persisted, then restored and saved the original name.
- This consolidates the local profile edit/save/export/restart workflow; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: inspect the current roadmap, open PR #255 and current CI state; identify any remaining software-only release gates before recommending whether 0.2.0 is ready.


## Manual Windows verification — malformed profile JSON rejected safely (2026-10-16)

- User confirmed that importing a deliberately malformed JSON copy (removed closing brace) reports an import error, does not hang the app, and leaves the original saved profile unchanged.
- This verifies malformed-input handling for local profile import only; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: continue software-only release gates and review PR #255 mergeability/CI before recommending release 0.2.0.


## Manual Windows verification — valid JSON missing profile name (2026-10-16)

- User confirmed the import test succeeded for a syntactically valid profile JSON copy with the profile-name field removed.
- The test confirms the app handles this missing-required-field case acceptably; the original profile was kept separate from the test copy. The exact UI behavior (rejection versus safe fallback) was not specified, so no narrower behavior is claimed.
- This covers local profile-import validation only; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: test a syntactically valid profile with an invalid field value/type, using a disposable copy, and confirm safe handling without altering the original profile.


## Manual Windows verification — profile name has wrong JSON type (2026-10-16)

- User confirmed successful handling of a syntactically valid profile JSON copy where the profile-name value was changed from a string to a number.
- The test was performed on a copy; the original saved profile was preserved. Exact UI behavior was not specified, so no specific error text or fallback behavior is claimed.
- This verifies local profile-import handling for an unexpected field type only; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: test an unknown/extra JSON property in a disposable profile copy and confirm the app imports or rejects it safely without altering the original profile.


## Manual Windows verification — extra unknown JSON property (2026-10-16)

- User confirmed successful handling of a profile JSON copy containing an extra property (`test_extra_field`) while preserving the original profile.
- This verifies local profile-import behavior with an unknown/extra property only; it does not establish whether the importer persists or discards the extra property unless separately inspected.
- No ECU communication was involved. No physical J2534 adapter or vehicle was used; hardware validation remains deferred.
- Next: test importing the same valid profile JSON twice and confirm there is no unintended duplicate or corruption of the saved profile list.


## Manual Windows verification — repeated import of same profile JSON (2026-10-16)

- User confirmed importing the same profile JSON twice and restarting Dvinholm completed successfully; the saved profile list remained correct with no observed unintended duplicates or corruption.
- This verifies the manual repeated-import/persistence workflow for the tested profile only; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: verify that cancelling out of the profile-import file picker leaves the profile list unchanged and does not report a false import success.


## Manual Windows verification — cancel profile import (2026-10-16)

- User confirmed cancelling the profile-import file picker completed safely: no profile was selected, the profile list remained unchanged, and the app did not report a false successful import.
- This verifies cancellation handling for the local profile-import UI only; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: test cancelling profile export/save dialog (if the app exposes cancellation) and confirm no file is falsely reported as saved and the existing profile remains intact.


## Manual Windows verification — cancel profile export (2026-10-16)

- User confirmed cancelling the profile export/save dialog completed safely: no false successful-save indication occurred and the original profile remained unchanged.
- This verifies cancellation handling for local profile export only; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: export a profile to a new temporary filename, verify the file exists and contains valid JSON, then import it into a disposable test profile/workflow without overwriting the original.


## Manual Windows verification — profile export/import round trip (2026-10-16)

- User confirmed exporting a profile to a new temporary filename, verifying that the file exists and contains valid JSON, then importing it without overwriting the original profile succeeded.
- This confirms the tested local profile export/import round trip and preservation of the original; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: check profile validation and error feedback by importing a JSON object with a required profile field missing (using a disposable copy), confirming the original profile remains intact and the app stays responsive.


## Manual Windows verification — required profile field missing (2026-10-16)

- User confirmed successful handling of an import test using a syntactically valid JSON profile copy with another required field omitted.
- The original profile was kept separate. The exact field and UI behavior were not specified, so no particular validation message or fallback behavior is claimed.
- This covers local profile-import validation only; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: inspect the repository's documented profile JSON schema/validation rules before choosing any further field-specific tests; avoid guessing which fields are required.


## Manual Windows verification — import empty profile catalog (2026-10-16)

- User confirmed the empty JSON array (`[]`) import test succeeded after creating a backup of the existing profile catalog.
- This verifies the tested empty-catalog import/UI path only. Do not assume profiles were preserved in the active list; the user has not yet reported restoring the backup.
- No ECU communication was involved. No physical J2534 adapter or vehicle was used; hardware validation remains deferred.
- Next: ask the user to confirm the empty catalog is displayed correctly, then restore the saved catalog backup and verify the profiles return intact before continuing.


## Manual Windows verification — restore catalog after empty import (2026-10-16)

- User confirmed restoring the saved JSON catalog after importing the empty array (`[]`) succeeded; the original profiles returned, their names and main fields were intact, and the profiles persisted after restarting Dvinholm.
- This verifies recovery from the tested empty-catalog import workflow; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: test duplicate profile IDs in an otherwise valid JSON catalog using a disposable copy, confirm import is rejected safely and the current catalog remains intact.


## Manual Windows verification — duplicate profile IDs (2026-10-16)

- User confirmed that importing a JSON catalog with duplicate profile IDs was handled safely: the duplicate catalog was rejected/reported as an error, the app did not hang, and the existing profiles remained intact.
- No ECU communication was involved. No physical J2534 adapter or vehicle was used; hardware validation remains deferred.
- Next: test invalid CAN identifier input in a disposable profile/catalog and verify validation prevents invalid settings from being accepted while existing profiles remain intact.


## Manual Windows verification — invalid CAN identifiers (2026-10-16)

- User confirmed invalid CAN identifiers outside the selected format's range were rejected/not accepted on save; existing profiles remained intact.
- This was a UI/profile validation check only; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: verify that request and response CAN IDs cannot be configured identically, using a disposable profile and confirming existing profiles remain intact.


## Manual Windows verification — identical request/response CAN IDs (2026-10-16)

- User confirmed that configuring the same CAN ID for request and response was rejected/not accepted on save; other profiles remained intact.
- This was a profile validation check only; no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: verify that an invalid or malformed diagnostic identifier (DID) is rejected safely in a disposable profile, without modifying the existing catalog.


## Manual Windows verification — malformed DID input (2026-10-16)

- User confirmed the malformed DID input check was successful: the invalid value was handled safely, without a hang or damage to the existing profiles.
- No ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: verify duplicate DID handling in a disposable profile/catalog, if the UI supports editing DID entries; otherwise report that the UI does not expose DID editing and continue with the next available software-only check.


## Manual Windows verification — duplicate DID entries (2026-10-16)

- User confirmed the duplicate DID test succeeded: repeated DID entries were handled safely, without an app hang or damage to existing profiles.
- No ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: verify catalog export after the validation tests: export the restored original catalog to a new JSON filename and confirm the file is valid JSON with the expected profile names and fields; do not overwrite the only backup.


## Manual Windows verification — export catalog after validation tests (2026-10-16)

- User confirmed the catalog exported successfully to a new JSON file; the file was valid JSON, contained the expected original profile names and key fields, and had no unintended temporary test profiles.
- The sole backup was not overwritten and no ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: import this newly exported JSON into a separate temporary/test catalog or use the app's safe import workflow, then verify profile count, names, and key fields match the exported source. Keep the original backup intact.


## Manual Windows verification — catalog export/import round trip (2026-10-16)

- User confirmed re-importing the newly exported catalog succeeded; profile count, names, and key fields matched, no unintended duplicates or missing settings were observed, and the result persisted after restarting Dvinholm.
- The original backup was kept separately. No ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: perform a final Windows build/package smoke check on the current candidate: launch the app, verify the main window loads without an unhandled error, then close it normally. Do not treat this as release approval; exact-head CI and remaining roadmap gates still need verification.


## Manual Windows verification — candidate startup and normal exit (2026-10-16)

- User confirmed the current Windows candidate launches with the main window fully loaded, without an unhandled error or crash, and closes normally.
- This was a startup/shutdown smoke check only. No ECU communication was involved.
- No physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- Next: verify the current pull request head and CI/check status before deciding which release gates remain. Do not merge or publish a release without explicit user authorization.


## Manual Windows verification — startup without physical J2534 adapter (2026-10-16)

- User confirmed the current Windows candidate correctly reports that no physical J2534 device is present, does not hang, keeps diagnostic actions unavailable without a connection, and can be closed and relaunched normally.
- This is a no-device software behavior check only. No ECU communication was involved.
- Physical adapter and vehicle verification remains explicitly deferred to a future release.
- Next: inspect the latest CI run for the current PR head and record its final result before evaluating release readiness.


## UDS UI state audit — 2026-10-16

- Reviewed current-main `src/Dvinholm.App/MainWindow.xaml.cs` and the related test inventory.
- Confirmed by source inspection: Read VIN/DTC/DID all route through `RunUdsOperationAsync` and `TryGetUdsClient`; the client guard requires an active connection and a selected CAN-ID format with valid IDs. UDS and ECU discovery share `_udsGate`; the UI disables UDS controls during a request and restores them according to `_connected`.
- Confirmed close handling cancels discovery/UDS, waits with bounded timeouts, and only schedules session disposal when tracked tasks and the UDS gate have stopped. This is a source audit, not a new runtime test.
- Existing tests already cover active/queued UDS cancellation, ISO-TP cancellation/disposal, dispatcher/session disposal races, CAN ID parsing, and virtual-adapter UDS flows. No duplicate test was added.
- Next: manually verify disconnected/failed-connect button state (Start Monitor and Read VIN/DTC/DID remain disabled, Connect becomes available for retry) on the current Windows candidate. No physical adapter is needed; real adapter/vehicle tests remain deferred.


## Manual Windows UI verification — failed J2534 path and disconnected controls (2026-10-16)

- User confirmed the manual test passed: configuring a deliberately nonexistent J2534 DLL path produces a handled connection error; Connect is available for retry afterward; Start Monitor, Read VIN, Read DTC, and Read DID remain disabled while disconnected.
- No physical J2534 adapter or vehicle was used.
- This closes the current manual UI check for disconnected-state control availability after a failed connection.
- Next: continue the remaining pre-release checks with the virtual/no-adapter setup; keep physical adapter and vehicle verification deferred to a future release.


## Manual Windows UI verification — valid J2534 DLL path, adapter absent (2026-10-16)

- User confirmed that selecting a real/previously detected J2534 DLL path and pressing Connect is handled correctly when no physical adapter is connected: the app reports the missing device without hanging.
- Connect becomes available again after the attempt; Start Monitor, Read VIN, Read DTC, and Read DID remain disabled while disconnected.
- No physical adapter or vehicle was used; hardware verification remains deferred.
- Next: continue the pre-release virtual/no-adapter checks.


## Manual Windows UI verification — repeated no-adapter connection attempts (2026-10-16)

- User confirmed three consecutive Connect attempts with no physical J2534 adapter were handled correctly.
- The app remained responsive and open; each attempt completed without hanging or crashing, and Monitor/UDS actions remained unavailable while disconnected.
- No physical adapter or vehicle was used.
- Next: continue the remaining pre-release checks with the virtual/no-adapter setup.


## Manual Windows UI verification — disconnected state after restart (2026-10-16)

- User confirmed Dvinholm closes and relaunches normally after failed no-adapter connection attempts.
- After relaunch without a physical adapter, the application does not show an active connection; Start Monitor, Read VIN, Read DTC, and Read DID remain disabled.
- App closes normally. No physical adapter or vehicle was used.
- Next: continue the remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — switching ECU profiles (2026-10-16)

- User confirmed switching between the primary and another saved ECU profile displays each profile's own CAN request/response IDs, and returning to the primary profile restores its original values.
- No profile settings were edited and no physical J2534 adapter or vehicle was used.
- This verifies profile selection does not visibly mix CAN ID settings in the UI.
- Next: continue the remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — ECU profile persistence after restart (2026-10-16)

- User confirmed a non-primary saved ECU profile retains its name and CAN request/response IDs after closing and relaunching Dvinholm.
- No profile values were changed; no physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — cancel ECU profile selection (2026-10-16)

- User confirmed cancelling/closing the profile-selection action leaves the primary ECU profile selected and its CAN IDs unchanged.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks with the virtual/no-adapter setup.


## Manual Windows UI verification — invalid CAN ID rejected after profile switch (2026-10-16)

- User confirmed that after switching to a non-primary ECU profile, an out-of-range CAN ID is not accepted on save and the profile's original settings remain intact.
- This was performed without a physical J2534 adapter or vehicle.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — profile recovery after invalid CAN ID (2026-10-16)

- User confirmed that after an out-of-range CAN ID is rejected, the original CAN IDs remain intact and can be re-opened by switching away from and back to the profile.
- No false persistent error or app malfunction was observed.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — repeated profile switching and persistence (2026-10-16)

- User confirmed five switches between the primary and second ECU profiles, followed by an application restart, preserved both profile names and their respective CAN request/response IDs.
- No profile edits were made and no physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — selected profile consistency on startup (2026-10-16)

- User confirmed that after restarting Dvinholm, the currently selected ECU profile's displayed CAN IDs match that profile; switching between primary and second profiles also shows the correct corresponding values.
- The app's choice of which profile is selected by default after restart is not treated as a failure, provided the selected profile and displayed parameters are consistent.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — cancel unsaved ECU profile edit (2026-10-16)

- User confirmed changing a CAN ID in the second profile's editor and cancelling/closing without saving leaves the persisted CAN ID unchanged.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — save and restore ECU profile name (2026-10-16)

- User confirmed changing the second ECU profile name with the temporary `-TEST` suffix, saving, switching away/back, and then restoring the original name all worked.
- The profile's CAN IDs remained unchanged throughout.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — export primary ECU profile after name restoration (2026-10-16)

- User confirmed a primary ECU profile export to a separate temporary JSON file was valid and contained the expected original profile name and CAN IDs.
- The temporary `-TEST` suffix was absent; the backup file and source profile were not overwritten or edited.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — import exported ECU profile without overwriting original (2026-10-16)

- User confirmed importing the temporary JSON export was handled correctly and the original ECU profile remained unchanged.
- The imported profile's name and CAN IDs were checked; no original profile was overwritten.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — repeat JSON import (2026-10-16)

- User confirmed the repeat-import check passed: importing the same temporary JSON again did not damage the original ECU profile or its CAN IDs; no hang or false-success behavior was observed.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — import name collision (2026-10-16)

- User confirmed that importing a JSON profile whose name matches an existing profile was handled safely; cancelling the overwrite prompt left the original profile and CAN IDs unchanged, with no hang.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — dismiss overwrite dialog (2026-10-16)

- User confirmed the overwrite/duplicate-name dialog could be dismissed safely; the app remained responsive and the existing profile and CAN IDs were unchanged after restart.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — cancel import file picker (2026-10-16)

- User confirmed cancelling the import file picker leaves the profile list and selected profile unchanged and does not display a false import-success message.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — cancel profile export (2026-10-16)

- User confirmed cancelling the JSON export/save dialog does not show a false export-success message, does not change the source profile, and leaves the app responsive.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — export non-primary ECU profile (2026-10-16)

- User confirmed the non-primary profile displayed its own CAN IDs and its separate JSON export contained the matching profile name and CAN IDs.
- Existing source and backup files were not overwritten.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — profile switching after export and restart (2026-10-16)

- User confirmed both the primary and exported non-primary ECU profiles continue to show their own correct CAN IDs after switching between them and restarting the app.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — import malformed CAN ID (2026-10-16)

- User confirmed a copied profile JSON with a malformed CAN ID value (`ZZZZ`) was handled safely: no hang, and existing profiles and their original CAN IDs remained intact.
- The working JSON and backup were not edited.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — import empty profile name (2026-10-16)

- User confirmed a copied profile JSON with an empty profile name was handled safely; the app did not hang and existing profiles remained unchanged.
- The working JSON and backup were not edited.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — import JSON missing profile name field (2026-10-16)

- User confirmed a copied profile JSON with the profile-name field removed was handled safely; the app did not hang and existing profiles remained unchanged.
- The working JSON and backup were not edited.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — restart after import edge-case series (2026-10-16)

- User confirmed the app closed and relaunched normally after the series of import edge-case checks; original profiles, names, and CAN IDs remained correct, with no false errors.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — rename profile, restart, restore name (2026-10-16)

- User confirmed the primary ECU profile accepted a temporary `-TEST` rename, persisted it across restart with CAN IDs unchanged, and was restored to its original name successfully.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — CAN IDs after repeated profile switching (2026-10-16)

- User confirmed the primary profile's CAN IDs remain correct after switching to another saved profile and back repeatedly.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — discard unsaved CAN ID edit (2026-10-16)

- User confirmed an unsaved CAN ID edit was discarded when switching profiles; returning to the primary profile showed its original persisted value.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — save and restore CAN ID (2026-10-16)

- User confirmed a valid changed CAN ID could be saved, persisted across profile switching, and then restored to its original value successfully.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release checks using the virtual/no-adapter setup.


## Manual Windows UI verification — restart after CAN ID save/restore (2026-10-16)

- User confirmed the app restarted normally after the CAN ID save/restore test; the primary profile's restored name and CAN IDs remained correct, and switching to another profile and back showed the correct values.
- No physical J2534 adapter or vehicle was used.
- Next: finish remaining pre-release UI checks, then verify CI on the current branch head and review release readiness.


## Manual Windows UI verification — export profile and compare after restart (2026-10-16)

- User confirmed the primary ECU profile retained its original name and CAN IDs after restart, and the separately exported JSON contained matching values.
- Existing backup files were not overwritten.
- No physical J2534 adapter or vehicle was used.
- Next: finish remaining pre-release UI checks, then verify CI on the current branch head and review release readiness.


## Manual Windows UI verification — CAN Monitor unavailable without adapter (2026-10-16)

- User confirmed the app behaves correctly with no physical J2534 adapter: CAN Monitor cannot be started while disconnected, no false active-monitoring status is shown, and closing/relaunching the app works normally.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release UI checks, then verify CI on the current branch head and review release readiness.


## Manual Windows UI verification — UDS controls across ECU profiles while disconnected (2026-10-16)

- User confirmed Read VIN, Read DTC, and Read DID remained unavailable while disconnected when switching between the primary and another saved ECU profile; no false connected state appeared.
- The user returned to the primary profile and closed the app normally.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release UI checks, then verify CI on the current branch head and review release readiness.


## Manual Windows UI verification — repeated CAN Monitor attempts while disconnected (2026-10-16)

- User confirmed repeated attempts to start CAN Monitor without a physical J2534 adapter did not hang or crash the app and did not produce a false active-monitoring state.
- App closed and relaunched normally after the check.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release UI checks, then verify CI on the current branch head and review release readiness.


## Manual Windows UI verification — disconnected state after restart (2026-10-16)

- User confirmed after closing and relaunching without a physical J2534 adapter, the app correctly reports no active connection and Start Monitor, Read VIN, Read DTC, and Read DID remain unavailable.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release UI checks, then verify CI on the current branch head and review release readiness.


## Manual Windows UI verification — repeated ECU profile switching while disconnected (2026-10-16)

- User confirmed repeated switching between the primary and another saved ECU profile showed each profile's correct CAN IDs; Start Monitor, Read VIN, Read DTC, and Read DID remained unavailable without a physical adapter.
- UI remained responsive and the app closed normally.
- No physical J2534 adapter or vehicle was used.
- Next: finish the remaining pre-release checks, then verify CI for the current branch head and review release readiness.


## Manual Windows UI verification — failed connection on non-primary ECU profile (2026-10-16)

- User confirmed a connection attempt without a physical J2534 adapter is handled without hanging; the non-primary profile remains selected and its CAN IDs remain unchanged.
- Monitor and UDS actions remain unavailable while disconnected.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release UI checks; verify CI on the current branch head before assessing release readiness.


## Manual Windows UI verification — repeated Connect retries on secondary ECU profile (2026-10-16)

- User confirmed three connection attempts without a physical J2534 adapter are handled without hanging or crashing; the secondary profile and its CAN IDs remain unchanged and diagnostic actions remain unavailable.
- No physical J2534 adapter or vehicle was used.
- Next: continue remaining pre-release UI checks and verify CI on the current branch head before release-readiness assessment.


## Manual Windows UI verification — secondary profile survives failed connection (2026-10-16)

- User confirmed the selected secondary ECU profile and its request/response CAN IDs remained intact after a connection attempt without a physical J2534 adapter, closing Dvinholm, and relaunching it.
- The application did not report a false active connection; Start Monitor, Read VIN, Read DTC, and Read DID remained unavailable while disconnected.
- No physical J2534 adapter or vehicle was used.
- Next: verify that switching ECU profiles while disconnected keeps all Monitor/UDS actions unavailable and does not leave a stale enabled control.


## Manual Windows UI verification — disconnected profile switching (2026-10-16)

- User confirmed switching between the primary and secondary ECU profiles while disconnected leaves Start Monitor, Read VIN, Read DTC, and Read DID unavailable.
- Both profiles continued to show their own correct CAN IDs; the application remained responsive and did not report an active connection.
- No physical J2534 adapter or vehicle was used.
- Next: verify that closing and reopening the profile selector without choosing a different profile does not alter the selected profile, CAN IDs, or disconnected control state.


## Manual Windows UI verification — cancel profile selection (2026-10-16)

- User confirmed canceling/closing the profile selector without choosing another profile leaves the selected secondary ECU profile and its CAN IDs unchanged.
- The connection remains inactive and Start Monitor, Read VIN, Read DTC, and Read DID remain unavailable; no physical J2534 adapter or vehicle was used.
- Next: verify that changing a profile's CAN ID, then canceling/discarding the edit, restores the saved value and leaves the disconnected UI state unchanged.


## Manual Windows UI verification — discard unsaved CAN ID edit (2026-10-16)

- User confirmed canceling/discarding an unsaved CAN ID edit restores the saved value after returning to the profile.
- Other profile settings remained unchanged and Monitor/UDS actions stayed unavailable while disconnected.
- No physical J2534 adapter or vehicle was used.
- Next: verify a valid CAN ID edit can be saved, persists after switching away and back, and can then be restored to the original value.


## Manual Windows UI verification — save and restore CAN ID (2026-10-16)

- User confirmed a valid CAN ID edit on the secondary ECU profile saved successfully and persisted after switching to another profile and back.
- The primary profile was unaffected; the original CAN ID was restored, saved, and remained correct after application restart.
- No physical J2534 adapter or vehicle was used.
- Next: verify the current branch head and PR checks before continuing toward release 0.2.0.


## Manual Windows UI verification — export restored ECU profile (2026-10-16)

- User confirmed exporting the secondary ECU profile to a new JSON file produced the expected profile name and both restored CAN IDs.
- No temporary test name or prior test CAN ID was present; existing backup files were not overwritten.
- No physical J2534 adapter or vehicle was used.
- PR #255 CI for the then-current head was still running at last check; verify the latest run before release-readiness decisions.
- Next: inspect the exported JSON in Dvinholm by importing it as a separate profile, without overwriting the source profile, then verify the imported name and CAN IDs.


## Manual Windows UI verification — import export as separate profile (2026-10-16)

- User confirmed the exported JSON imports successfully as a separate ECU profile without overwriting the source profile.
- Imported profile name and both CAN IDs match the exported values; switching between source and imported profiles preserves each profile's own settings.
- No physical J2534 adapter or vehicle was used.
- PR #255 CI on the latest documentation head was queued at the last check; confirm its final result before release-readiness decisions.
- Next: verify that exporting the full profile catalog preserves the expected profile count, names, and CAN IDs without modifying the separate backup.


## Manual Windows UI verification — full profile catalog export (2026-10-16)

- User confirmed the full catalog exported to a new JSON file with the expected profile count, names, and CAN IDs for the primary, secondary, and imported profiles.
- No unexpected temporary test values appeared and the separate backup was left untouched.
- No physical J2534 adapter or vehicle was used.
- PR #255 CI for the latest head was queued at the last check; confirm the final result before release-readiness decisions.
- Next: verify the full catalog JSON can be imported as a round trip without unintended duplicates or missing/changed profiles, keeping a separate backup available.


## Manual Windows UI verification — profile catalog survives restart (2026-10-16)

- User confirmed that after a normal close and relaunch, the profile count matched the exported catalog and the primary, secondary, and imported profiles retained their expected names and CAN IDs.
- No profiles were missing or duplicated, and no false active connection was shown without a physical adapter.
- No physical J2534 adapter or vehicle was used.
- Next: verify the current PR #255 head and its CI status before deciding the remaining software-only release gates.


## Manual Windows UI verification — repeat full-catalog import (2026-10-16)

- User confirmed importing the previously exported profile catalog completed successfully without unexpected duplicate profiles or unexpected changes to existing profile settings.
- Profile names and CAN IDs remained correct; the separate backup was preserved.
- No physical J2534 adapter or vehicle was used.
- PR #255 CI for the latest head was queued at the last check; verify a terminal CI result before release-readiness decisions.
- Next: perform one final disconnected-state smoke test after the catalog import: restart the app, confirm no active connection and verify Monitor/UDS actions stay disabled.


## Manual Windows UI verification — post-import disconnected-state smoke test (2026-10-16)

- User confirmed the app closed and relaunched normally after full-catalog import.
- Profiles and CAN IDs remained correct after restart.
- With no physical J2534 adapter attached, the app did not report an active connection and Start Monitor / Read VIN / Read DTC / Read DID remained unavailable.
- App stayed responsive and closed without hanging or an error.
- Physical J2534 adapter and vehicle tests remain deferred to a future release.
- CI for the latest PR #255 head was queued at the last check; check its result and assess remaining software-only release 0.2.0 gates next.


## CI verification — PR #255 (run 2214, 2026-10-16)

- GitHub Actions CI run 2214 for commit `220dee77d3f59ba7b4dd3075c2f9b9ec237bd09b` completed successfully.
- Restore, Build, Test, Publish, and artifact upload steps all completed successfully. Optional public-asset packaging/publishing steps were skipped by workflow conditions.
- This result applies to the tested commit; documentation updates create a new head and require their own CI run.
- PR #255 remains open and unmerged. No release was created.
- Next: inspect PR review/status requirements and remaining software-only release 0.2.0 gates; keep physical adapter/vehicle checks deferred.


## CI verification — PR #255 latest documentation head (run 2218, 2026-10-16)

- GitHub Actions CI run 2218 completed successfully for documentation head `6ec1af6551842a21a338ceab0c5e7bbbf8ac665c`.
- Restore, Build, Test, Publish, and CI artifact upload succeeded; optional public-asset packaging/publishing steps were skipped by workflow conditions.
- PR #255 remains open and unmerged; its only changed files are `AI_WORKLOG.md` and `ROADMAP.md`.
- The passing CI validates this documentation head, not a future release merge commit. No release was created.
- Release readiness is not yet established: connected-session UI gating cannot be verified without an active J2534/CAN session, and exact release archive/release-note checks remain to be completed.


## CI verification — PR #255 documentation head (run 2222, 2026-10-16)

- GitHub Actions CI run 2222 completed successfully for head `8928a4dbef800ac19d32d6ec6b54cd8d439fa73a`.
- Restore, Build, Test, Publish, and CI artifact upload succeeded. Optional public updater-asset packaging/public-asset publishing steps were skipped by workflow conditions.
- PR #255 remains open and unmerged; its current changes are documentation-only. No release was created.
- This CI result does not establish release readiness: the enabled-controls transition requires a valid active J2534/CAN session, and release notes plus the exact release archive still need separate review.
- Physical J2534 adapter and vehicle validation remain deferred to a future release.
- Next: record this result in the roadmap, then re-check CI for the resulting documentation head before continuing the release-candidate review.


## CI verification — PR #255 updated documentation head (run 2225, 2026-10-16)

- GitHub Actions run 2225 completed successfully for head `be269fd3849588ce7fe777b9b997c2a9dd1eee88`.
- Both reported build checks completed successfully; the workflow includes restore, build, automated tests, publish, and CI artifact upload.
- PR #255 remains open, unmerged, and mergeable at this check. No release was created.
- Release-candidate gates remain: review release notes and the exact release archive separately from CI artifacts; active-session UDS/Monitor enablement cannot be verified in the current no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Next: re-check CI after this documentation update, then inspect the current release notes/assets without publishing or merging anything.


## CI verification — PR #255 documentation head (2026-10-16)

- GitHub Actions CI run **#2227** (push) and **#2228** (pull request) both completed successfully for head `f0239cb37d0c6b12eeeb9dbb3aa1cd372d51f8fe`.
- Both runs passed restore, build, automated tests, publish, updater-asset packaging, CI artifact upload, and public CI asset publishing.
- PR #255 remains open and unmerged; no stable release was created.
- Release gates still open: review release notes and verify the exact release archive separately from CI artifacts; verify Monitor/UDS controls become enabled only after a valid active J2534/CAN session.
- Physical J2534 adapter and vehicle validation remain deferred to a future release.
- Next: re-check CI on the updated documentation head after this record is committed.


## CI verification — PR #255 (2026-10-16)

- GitHub Actions PR CI run **#2232** completed successfully for `577c85223f11cc692ae1da8ea8c09b10929dbd83`.
- Build, automated tests, publish, updater asset packaging, CI artifact upload, and public CI asset steps passed.
- PR #255 remains open and unmerged; no stable release was created.
- Physical J2534 adapter and vehicle validation remain deferred to a future release.
- Remaining release gates: inspect CI prerelease publication behavior, verify the exact stable release archive, and validate Monitor/UDS enablement during an active J2534/CAN session when suitable hardware becomes available.


## CI workflow review — PR #256 (2026-10-16)

- CI run #2239 passed for `496af38ab82d4bfc7121b4f4484c642d6bb4d311`.
- Reviewed `.github/workflows/ci.yml`: updater ZIP packaging and public `ci-*` prerelease creation are now conditioned on pushes to `refs/heads/main`; branch pushes and PRs still run build/test and upload the normal Actions artifact.
- PR #256 is open and unmerged. Do not claim the behavior is active on `main` until the PR is reviewed and merged.
- This addresses avoiding public updater prereleases from documentation/feature branches. Stable release `0.2.0` remains unpublished.
- Physical J2534 adapter and vehicle validation remain deferred to a future release.


## CI failure follow-up — PR #257 (2026-10-16)
- CI #2243 for docs PR #255 failed in `LatestBuildUpdaterFailureTests.ReplacementScript_RestoresPreviousBuildWhenReplacementFailsAtRuntime`: 475 passed, 1 failed. The failure occurred during temporary-directory cleanup because PowerShell still held a handle; the isolated script also used synthetic PID `-1` without mocking `Wait-Process`.
- Opened PR #257 (`fix/rollback-test-mock-wait-process`) adding a no-op `Wait-Process` mock for the isolated runtime test. PR #257 remains open/unmerged.
- CI #2246 for PR #257 passed: restore, build, test, publish, and artifact upload succeeded; public updater/release steps were skipped as expected for a PR.
- Follow-up: rerun/check CI for PR #255 and record its outcome. Do not merge PRs or publish v0.2.0 without explicit authorization. Physical J2534 adapter/vehicle validation remains deferred to a future release.


## Post-merge CI verification — PRs #255–#257 (2026-10-16)

- PRs #256, #257, and #255 are merged into `main` in that order; latest reviewed main commit is `a47679bdb049fe6814751c319707e4119fa56c7c`.
- Main CI #2251 passed for commit `cc4b849b8689dae08548c61e529e04f293709a12`; the updater ZIP/public `ci-*` prerelease steps ran on a `main` push as intended.
- Main CI #2252 initially failed one rollback runtime test (475/476 passed) during temporary-directory cleanup. Retrying the failed job as attempt 2 succeeded: [CI #2252](https://github.com/vorlocsev/dvinholm/actions/runs/38975138132).
- Main CI #2253 passed for commit `a47679bdb049fe6814751c319707e4119fa56c7c`, including restore, build, tests, publish, package, artifact upload, and public CI asset publishing.
- These results establish that the merged main commits passed CI (including the successful retry); the initial transient cleanup failure remains worth monitoring if it recurs.
- Latest stable release remains `v0.1.1`; no stable `0.2.0` release was published.
- Remaining release checks: review release notes and the exact stable archive independently; active-session Monitor/UDS control enablement remains unverified without a valid active J2534/CAN session. Physical adapter/vehicle verification remains deferred to a future release.


## Post-PR #258 merge verification and release asset review (2026-10-16)

- [x] PR #258 merged; documentation records post-merge CI outcomes for PRs #255–#257.
- [x] Main CI #2258 passed on merge commit `5d70fe220fc73852a417c45bce730a1181b9dfbe`: https://github.com/vorlocsev/dvinholm/actions/runs/38977361452.
- [x] Reviewed stable v0.1.1 release metadata and release notes. GitHub reports asset `Dvinholm-Windows-x86.zip` (66,766,108 bytes) and SHA-256 `3822d13e5a137bd92bf12d35d2a5794053481ef28873d2a08657beb756f3a53a`.
- [ ] Independent archive download/content inspection is still incomplete: the GitHub release asset URL could not be fetched through the available browser/download path. Do not claim the archive was verified from its checksum metadata alone.
- [ ] Software-only release review remains open for active-session Monitor/UDS enablement; current no-adapter setup cannot confirm controls transition to enabled only after a valid active J2534/CAN session.
- Stable v0.2.0 remains unpublished and requires explicit user authorization. Physical adapter/vehicle validation remains deferred to a future release.


## Post-PR #259 main CI verification (2026-10-17)

- [x] PR #259 merged as commit `1935d56751453a5feeaa599f63302f6523717918`.
- [x] Main CI #2263 passed for that merge commit: https://github.com/vorlocsev/dvinholm/actions/runs/38978437672.
- [x] Reviewed current `MainWindow.xaml.cs`: UDS buttons are disabled before connection and enabled after successful J2534 connection; VIN/DTC/DID actions also pass through `TryGetUdsClient`, which checks connection state and validates CAN ID format/values. Disconnect disables Monitor and UDS controls before tearing down the session; session state is reset on disconnect failure.
- [ ] Exact v0.1.1 release ZIP still has not been independently downloaded and inspected; GitHub release metadata/checksum alone does not establish that the local archive contents were verified.
- [ ] Manual transition from disconnected to a valid active J2534/CAN session remains unverified because no physical adapter is available. Physical adapter/vehicle testing remains deferred to a future release.
- Stable v0.2.0 remains unpublished; explicit user authorization is required before publishing it.


## Post-PR #261 main CI verification (2026-10-17)

- PR [#261](https://github.com/vorlocsev/dvinholm/pull/261) merged as `6620ad2bf9b4e3daa3b6e27b2347181e9f80b6c2`.
- Main CI [#2273](https://github.com/vorlocsev/dvinholm/actions/runs/38980902038) completed successfully on that exact commit; build, tests, publish, and artifact packaging passed.
- Updated `ROADMAP.md` current-main checkpoint from the prior merge SHA to `6620ad2bf9b4e3daa3b6e27b2347181e9f80b6c2`.
- PR #228 (updater button state) remains open and unmerged. The work log records user confirmation that the identical-SHA false positive no longer appears on the tested Windows build; no merge was performed because explicit merge approval is required.
- Remaining release-review tasks: independently inspect the exact v0.1.1 release archive and keep active-session Monitor/UDS transition marked unverified until a valid J2534/CAN session is available.
- Physical adapter/vehicle validation remains deferred. Stable v0.2.0 must not be published without explicit user authorization.


## Published v0.1.1 release ZIP audit — 2026-10-17

- [x] Created and merged PR [#263](https://github.com/vorlocsev/dvinholm/pull/263) to add a read-only GitHub Actions audit of the exact published release asset.
- [x] Audit CI [run #2282](https://github.com/vorlocsev/dvinholm/actions/runs/38982113636) passed. It downloaded the published `Dvinholm-Windows-x86.zip`, verified SHA-256 `3822d13e5a137bd92bf12d35d2a5794053481ef28873d2a08657beb756f3a53a`, ran ZIP integrity validation, and confirmed a Dvinholm executable is present. This is actual archive-byte verification, not merely reading release metadata or checking a CI-generated artifact.
- [ ] Check the post-merge main CI run after PR #263.
- [ ] Active-session Monitor/UDS enablement remains unverified without a valid active J2534/CAN session; the user currently has no physical adapter.
- Stable v0.2.0 remains unpublished and requires explicit user authorization. Physical adapter/vehicle validation remains deferred.


## Post-merge documentation checkpoint — 2026-10-17

- PR [#264](https://github.com/vorlocsev/dvinholm/pull/264) merged as `1749b56d826523bf382f2d287e8f1a30a791286f`; its PR-head CI [#2290](https://github.com/vorlocsev/dvinholm/actions/runs/38983217022) passed.
- Main CI [#2286](https://github.com/vorlocsev/dvinholm/actions/runs/38983168717) passed on the preceding code merge `d113d6536cdd6d2c76bd5ecfbd4b1a858a955922`, including the published v0.1.1 release ZIP audit.
- Post-merge main CI [#2291](https://github.com/vorlocsev/dvinholm/actions/runs/38983741022) for `1749b56d826523bf382f2d287e8f1a30a791286f` was still running at the time of this update; do not mark it green until it completes.
- Exact release ZIP audit is complete. Active-session Monitor/UDS transition remains unverified without a valid J2534/CAN session. Stable v0.2.0 remains unpublished and requires explicit user authorization.


## PR #267 — non-blocking window shutdown cleanup (2026-10-17)

- PR [#267](https://github.com/vorlocsev/dvinholm/pull/267) merged by squash as `78c555df959129a7a743ac8c34156703ca584bce`.
- Final PR-head CI [run #38986409454](https://github.com/vorlocsev/dvinholm/actions/runs/38986409454) passed both the build/test job and audit of the exact published v0.1.1 ZIP.
- Post-merge main CI [run #38986840493](https://github.com/vorlocsev/dvinholm/actions/runs/38986840493) passed on the merge commit; build/tests and release ZIP audit succeeded.
- Change summary: window close requests cancellation and moves completion waits/UDS gate coordination to background cleanup; StopMonitor no longer synchronously waits on the UI thread; connection tracking uses the actual connect task and avoids reading session state after the window starts closing.
- Scope caveat: no dedicated WinUI close-handler test seam or manual close stress test was run. CI establishes compilation and the existing automated suite passes, not direct proof of the UI shutdown path at runtime.
- Physical J2534 adapter/vehicle testing remains deferred. PR #228 remains open and must not be merged without explicit approval. Stable v0.2.0 remains unpublished and requires explicit user authorization.


## Post-merge CI checkpoint — PR #268 (2026-10-17)

- [x] PR [#268](https://github.com/vorlocsev/dvinholm/pull/268) merged by squash as `08015d0f6f8808422fe3dc01c29c05c540445fe6` after PR-head CI [#38987327051](https://github.com/vorlocsev/dvinholm/actions/runs/38987327051) passed.
- [x] Post-merge main CI [#38987746187](https://github.com/vorlocsev/dvinholm/actions/runs/38987746187) passed on the exact merge commit. Both the build/test job and audit of the exact published v0.1.1 release ZIP succeeded.
- [x] Current roadmap checkpoint advanced to merge commit `08015d0f6f8808422fe3dc01c29c05c540445fe6`.
- PR #228 remains open, unmerged, and must not be merged without explicit user approval. GitHub currently reports it has conflicts with base; any future rebase/conflict resolution needs fresh CI and the required Windows updater manual verification.
- No dedicated WinUI window-close regression test or manual close stress test was run for PR #267; CI only confirms compilation and existing automated tests.
- Physical J2534 adapter/vehicle validation remains deferred. Stable v0.2.0 remains unpublished and requires explicit user authorization.


## Autonomous PR audit and updater regression checkpoint — 2026-10-18

- [x] PR [#286](https://github.com/vorlocsev/dvinholm/pull/286) merged as `f923d408e691aface6f67e925216f9b74fba8ae8`; CI [#39105288517](https://github.com/vorlocsev/dvinholm/actions/runs/39105288517) passed build, tests, publish, and artifact upload. The published v0.1.1 archive audit also passed.
- [x] Closed stale PR [#225](https://github.com/vorlocsev/dvinholm/pull/225) as superseded: current `main` already validates SHA-256 digest metadata and has malformed/non-string/empty digest fallback tests.
- [x] Closed stale PR [#228](https://github.com/vorlocsev/dvinholm/pull/228) as superseded: current `main` already starts with the idle “Check update” action and restores that action when no newer build is available. The old branch conflicted with current main.
- [x] Closed PR [#283](https://github.com/vorlocsev/dvinholm/pull/283) as redundant documentation: the disconnected-state manual check is already recorded by merged PR #282 and its supporting note.
- [x] Closed PR [#284](https://github.com/vorlocsev/dvinholm/pull/284) after its branch conflicted with current main; recreated the unique ZIP traversal escape-path and cleanup assertions on a fresh branch in [PR #287](https://github.com/vorlocsev/dvinholm/pull/287).
- [x] Closed PR [#285](https://github.com/vorlocsev/dvinholm/pull/285) as superseded by deterministic expanded-size tests in merged PR #286.
- [ ] PR #287 CI is running. Merge only after the current head's full build/test/publish pipeline and published v0.1.1 archive audit pass.
- Physical J2534 adapter and vehicle validation remain deferred; no hardware compatibility is claimed. Stable release publication remains gated on release readiness review.


## CI release audit target corrected — 2026-10-18

- User correctly pointed out that the workflow's hard-coded v0.1.1 archive audit was stale while the current stable release is v0.2.0.
- Reworked the audit job to resolve GitHub's latest non-prerelease release dynamically, then download its Windows ZIP and SHA256SUMS.txt, verify the checksum, validate ZIP integrity, and confirm the app executable is present.
- This avoids pinning the CI release audit to v0.1.1 and automatically follows future stable releases. CI pending; not yet verified.


### Checksum audit fixture correction — 2026-10-18

- First CI attempt correctly resolved and downloaded v0.2.0, but checksum matching failed because the published SHA256SUMS line uses the standard binary-mode filename prefix (*Dvinholm-Windows-x86.zip).
- Adjusted verification to extract the expected digest for either plain or *-prefixed filename and compare it directly with the computed ZIP SHA-256.
- New CI run is required to verify this correction.


### Updater first-install failure cleanup — 2026-10-18

- Review found that a failed first-time replacement could leave a partial destination directory because rollback only ran when an existing build was available.
- Added a separate cleanup path that removes an incomplete first-install destination and records this in the updater log; existing-install rollback remains unchanged.
- Added regression assertions for the generated replacement script. Validation is pending CI on the current main base; no release or physical adapter/vehicle test is included.


## Current-main updater verification — 2026-10-18

- Merged [PR #289](https://github.com/vorlocsev/dvinholm/pull/289) to replace the hard-coded v0.1.1 release archive audit with a dynamic latest-stable-release audit. Main CI run [#39109327293](https://github.com/vorlocsev/dvinholm/actions/runs/39109327293) passed after that merge, including build and release audit.
- Merged [PR #290](https://github.com/vorlocsev/dvinholm/pull/290) at `2ccb296e8e745201188371a55d9967994f73a467`. Its PR CI run [#39109404796](https://github.com/vorlocsev/dvinholm/actions/runs/39109404796) passed build/tests and release audit.
- Post-merge main CI is running and must be checked before calling the merge pipeline fully green.
- Updater fix: if a first-time installation fails after replacement starts and there was no previous destination, remove the incomplete destination and record the cleanup in the failure log. Existing-install rollback behavior is preserved. Regression assertions verify the generated PowerShell branch/message.
- Manual evidence still limited to the user's v0.2.0 update and no-adapter retry checks. No physical J2534 adapter or vehicle validation has been performed.


## Rebased warning cleanup — 2026-10-18

- Rechecked stale PR [#226](https://github.com/vorlocsev/dvinholm/pull/226) against current `main`. Most catch-variable cleanups from the old patch are already present in current main; only the profile-import save catch still declared an unused `saveException` variable.
- Removed that unused variable without changing exception handling or user-facing behavior. J2534Session already uses unnamed catch variables for cleanup-only catches on current main, so no redundant edit was made there.
- Historical PR #226 CI reported 452 passed, 0 failed, 0 skipped and zero warnings, but this rebased one-line delta must pass fresh CI before merge.
- No physical J2534 adapter or vehicle validation was performed; those checks remain deferred.


### PR #293 merged; post-merge CI in progress — 2026-10-18

- Merged [PR #293](https://github.com/vorlocsev/dvinholm/pull/293) as `ddec537cbc5e8affe251d0b66d518dfd9fd23f00` after both latest-head PR runs passed build/tests and the dynamic latest-stable-release audit: [run #39112176555](https://github.com/vorlocsev/dvinholm/actions/runs/39112176555) and [run #39112180900](https://github.com/vorlocsev/dvinholm/actions/runs/39112180900).
- Change is limited to removing the unused `saveException` catch variable; error handling and user-facing text are unchanged.
- Post-merge main run [#39113096094](https://github.com/vorlocsev/dvinholm/actions/runs/39113096094) has passed the latest-stable-release audit; build was still running at the time of this note.
- Continue software-only updater review after main CI completes. No stable release and no physical J2534 adapter/vehicle testing.


### Runtime regression coverage for failed first install — 2026-10-18

- Merged [PR #294](https://github.com/vorlocsev/dvinholm/pull/294), updating `ROADMAP.md` and this work log with PR #293's verified merge status.
- Merged [PR #295](https://github.com/vorlocsev/dvinholm/pull/295) as `6ebcd4f25c92c6052c52f049fb16511889fe05f9`. Added a Windows-only runtime test to execute the generated replacement PowerShell with a mocked copy command: simulate failure after creating a partial install destination, assert cleanup removes that destination, and confirm the updater log records the cleanup. No production code changed.
- PR #295 CI passed in [run #39113276689](https://github.com/vorlocsev/dvinholm/actions/runs/39113276689) and [run #39113283879](https://github.com/vorlocsev/dvinholm/actions/runs/39113283879).
- Main run after PR #293, [#39113096094](https://github.com/vorlocsev/dvinholm/actions/runs/39113096094), is now successful. Post-merge main runs for the docs and new test are still in progress.
- Continue software-only updater review. No new stable release; physical adapter and vehicle testing remain deferred.


### Updater rollback-copy failure coverage — 2026-10-18

- Extended the existing Windows runtime rollback test with a separate scenario where the updated executable exits during startup and the mocked backup-to-destination robocopy returns failure code 8.
- The scenario asserts the rollback failure is logged, the backup executable and extracted staging remain available for recovery, the temporary helper script is removed, and the destination remains in the failed updated state rather than falsely claiming restoration.
- Validation and merge are pending fresh CI. This is test-only; no production updater behavior or release changed. Physical adapter/vehicle checks remain deferred.


## Manual ECU test-profile validation — 2026-10-18

- [x] User confirmed the fresh CI build `ci-2715` contains the `Add test profiles` button.
- [x] Added both synthetic test profiles; the app reported that two profiles were added.
- [x] Verified Standard CAN profile values: request ID `7E0`, response ID `7E8`, 11-bit format, DID `F190`.
- [x] Verified Extended CAN profile values: request ID `18DA10F1`, response ID `18DAF110`, 29-bit format, DID `1234`.
- [x] Switching back to Standard CAN restored saved values. After manually changing response ID to `7E9`, switching to Extended and back restored `7E8`.
- [x] Both profiles remained present after multiple app restarts, and their respective parameters restored correctly after the final restart.
- [x] Clicking `Add test profiles` again reported the profiles already exist; no duplicates appeared.
- [x] Exported the profiles to JSON and imported the file successfully; both profiles remained available after import.
- [x] Imported intentionally invalid JSON; the app reported `profile import failed` and retained the existing profile catalog and values.
- [x] User confirmed profile metadata/status and synthetic-profile description were displayed as expected.
- No diagnostic request was sent. No physical J2534 adapter or vehicle was available or tested. Stable release remains v0.2.0; CI build `ci-2715` is a prerelease test build.


## PR #353 — updater error query-string redaction — 2026-10-18

- Merged [PR #353](https://github.com/vorlocsev/dvinholm/pull/353) as `dbcc0a5d2b4a3902fd262f531d23df7ebdb0dfeb` after the full PR CI passed.
- Added `DownloadFailure_DoesNotExposeSensitiveQueryStringInError`: an HTTP 401 from an asset URL containing a temporary query token must preserve the HTTP status and path while excluding both the token and query-string marker from the exception message. Production updater behavior was unchanged; this is regression coverage for existing redaction.
- PR CI [#39163653989](https://github.com/vorlocsev/dvinholm/actions/runs/39163653989) passed build, tests, publish, and stable-release archive audit.
- Post-merge main CI [#39164047182](https://github.com/vorlocsev/dvinholm/actions/runs/39164047182) passed build, tests, publish/package, public CI asset publishing, and stable-release archive SHA-256/ZIP audit.
- Closed superseded documentation PRs #347–#350 because the manual ECU profile validations were already consolidated by PR #352. No profile results or unique code changes were lost.
- Stable release remains v0.2.0; no stable release was published. Physical J2534 adapter and vehicle checks remain deferred.
- Next: continue the software-only updater reliability review. Do not change production code without a concrete defect; no further uncovered rollback issue was established by this review pass.


## PR #355 — cancellation before extraction — 2026-10-18

- Merged [PR #355](https://github.com/vorlocsev/dvinholm/pull/355) as `552f5ea2aa75f5f1af093e9f7d2be97f11b7c825` after green CI [#39165406043](https://github.com/vorlocsev/dvinholm/actions/runs/39165406043).
- Added `CancellationBeforeExtraction_CleansTemporaryArchiveAndStagingDirectory`. A synchronous progress callback cancels at the “Validating and extracting” boundary, after the archive download finishes but before the first extraction entry is processed. The test checks cancellation is propagated and both temporary archive and staging directory are cleaned.
- Build, tests, publish/package, and stable release archive SHA-256/ZIP audit passed. Production updater code unchanged; no stable release published (still v0.2.0).
- Physical J2534 adapter/vehicle validation remains deferred.
- Next: continue software-only review of cleanup and recovery failure paths, and add tests only for reproducible uncovered behavior.

## ECU editor field-validation UI — merged and CI verified (2026-10-20)

- [x] [PR #372](https://github.com/vorlocsev/dvinholm/pull/372) merged at `f36094cccda5d29f1b305be68879c3d1abe27cb2`. Its PR CI #2850 ([run #39332349835](https://github.com/vorlocsev/dvinholm/actions/runs/39332349835)) and main CI #2851 ([run #39333268386](https://github.com/vorlocsev/dvinholm/actions/runs/39333268386)) passed. Editor errors are outside the scrollable form and visible after a rejected Save.
- [x] [PR #373](https://github.com/vorlocsev/dvinholm/pull/373) merged at `347a83d655644d00aa00d798377df4570dec0211`. Added parameter-specific validation exceptions, field highlighting/focus and eight xUnit cases. PR CI #2853 ([run #39334351132](https://github.com/vorlocsev/dvinholm/actions/runs/39334351132)) and main CI #2854 ([run #39335101214](https://github.com/vorlocsev/dvinholm/actions/runs/39335101214)) both passed.
- [x] On the exact post-merge main CI #2854, build, test, publish/package, branded icon check, artifact upload, public CI asset publication, and stable release archive audit passed.
- [x] Old overlapping PR #371 closed as superseded by #372/#373.
- [ ] No manual WinUI interaction run of the *new* validation display/focus/highlight behavior has yet been confirmed. Follow [the eight-step Windows smoke checklist](docs/manual-ecu-profile-validation-ui-2026-10-20.md) using a build containing `347a83d`, with no J2534 adapter.
- Stable release remains v0.2.1; current changes are present on main/CI only. No physical adapter/vehicle tests performed.

## User-confirmed Windows ECU editor behavior — 2026-10-20

- Main post-documentation merge CI [#2857](https://github.com/vorlocsev/dvinholm/actions/runs/39336486699) completed successfully on `99727e1cd568e5d133626f520755183ec9ffafc6`.
- User confirmed visible, field-specific rejection of malformed Request CAN ID `ZZZZ`, matching Request/Response IDs `7E0`/`7E0`, malformed DID `ZZZZ`, duplicate DID `F190, F190`, empty required Source and case-insensitive duplicate Profile ID.
- User confirmed that correcting to request `7E0`, response `7E8`, candidate DID `F190`, and a synthetic Source permits Save, with an Unverified profile.
- User confirmed rename to `Test ECU Final` persisted after full restart, with CAN IDs and DIDs unchanged and unrelated profiles intact.
- Each checkpoint was recorded on [PR #374](https://github.com/vorlocsev/dvinholm/pull/374); confirmation of the final rename/restart check is also there.
- Evidence boundaries: exact installed CI number/short SHA was not supplied, immediate border-reset upon typing was not separately observed, and an explicit invalid-DID edit followed by Cancel was not tested in the final check. Leave those checklist subparts pending. Physical adapter and vehicle testing remain deferred. Stable v0.2.1 is unchanged.

## CAN Monitor software ID refilter and bounded history — 2026-10-20

- Audited the WinUI CAN Monitor path in `MainWindow.xaml.cs`: `AddFrame` discarded incoming rows that did not match the currently typed ID filter; `Filter_Changed` was a no-op. A later filter change could therefore not reveal previously hidden frames.
- [PR #376](https://github.com/vorlocsev/dvinholm/pull/376) merged as `2dec8831b63c0c5306694dd21209ce8b2a19e07d`. Created `CanMonitorRow` and `CanMonitorHistory` (bounded to 5000 raw received rows) and preserved historical RX rows independent of the selected filter.
- Refreshing the software filter now rebuilds the visible ListView projection from the bounded buffer. Count text distinguishes shown frames from total received, and Clear resets buffer and rate. CSV export still uses the filtered visible rows.
- Added seven no-hardware automated tests in `CanMonitorHistoryTests.cs`, including virtual J2534 receive injection and multi-ID filter replay. [PR CI #2862](https://github.com/vorlocsev/dvinholm/actions/runs/39342233407) passed Build, Test, Publish and the stable ZIP audit.
- Post-merge main [CI #2863](https://github.com/vorlocsev/dvinholm/actions/runs/39342680771) started; final result pending at this checkpoint.
- Not covered: manual WinUI monitoring and CSV export with injected demo frames, because `VirtualJ2534Adapter` exists only as a test fixture and disconnected Start Monitor remains disabled by design. A dedicated offline demo UI mode is a future enhancement, not a claim about current functionality.
- Physical J2534 and vehicle checks remain deferred; stable release still v0.2.1.

## Offline CAN Monitor demo — implementation checkpoint (2026-10-20)

- Merged [PR #378](https://github.com/vorlocsev/dvinholm/pull/378) as `8b8c89c6df18044dd10d3b0d4d85ba8ba46d31ff`. Added in-process `CanDemoFrameGenerator` (deterministic synthetic 11-bit and 29-bit CAN rows), a `DispatcherQueueTimer` UI producer and a visually labeled **DEMO** control. No real J2534/UDS/session APIs are invoked by the generator.
- Real Start monitor and UDS remain gated on a real connected session. Demo is only allowed while disconnected and idle; the app disables Connect while demo is active. Demo capture stops on window close. Stopped demo frames remain visible for CSV and refiltering until Clear or an attempted real connection.
- `CanMonitorCsv` adds a `source` column carrying `SYNTHETIC_DEMO` for demo exports; normal CAN monitor exports keep `timestamp,id,type,dlc,data` unchanged.
- Added eight in-process `CanDemoFrameGeneratorTests` covering deterministic samples, DLC/hex, 11/29-bit IDs, timestamp advancement, bounded history/filter integration and both CSV schemas.
- [CI #2868](https://github.com/vorlocsev/dvinholm/actions/runs/39344455511) passed build/tests/publish and stable release audit on the PR head. Post-merge [main CI #2869](https://github.com/vorlocsev/dvinholm/actions/runs/39345037152) pending at this checkpoint.
- **Manual WinUI demo behavior is not yet user-tested.** See [Windows smoke checklist](docs/manual-can-demo-monitor-2026-10-20.md), which requires no hardware. The virtual J2534 test fixture remains separate from the demo producer; neither validates real adapter/vehicle hardware. Stable release remains v0.2.1.

## Demo CAN Monitor — user-confirmed Windows smoke results (2026-10-20)

- Eight consecutive offline Windows UI scenarios were **user-confirmed** and recorded in the [PR #379 discussion](https://github.com/vorlocsev/dvinholm/pull/379): initial Start demo synthetic frame display; `7E8` filtering with advancing RX count and hidden-frame replay; zero CAN ID `0` matching `000`; invalid `ZZZZ` filter hiding rows without erasing history; actual save and inspection of filtered CSV with header `timestamp,id,type,dlc,data,source` and `SYNTHETIC_DEMO` row provenance; Clear while running with row/counter reset and resumed RX; Stop with no more RX, history preserved for refiltering, Connect re-enabled and demo marker cleared by Clear; and a full restart without an adapter leaving demo off, monitor empty, Start demo enabled and real Start monitor/VIN/DID/DTC disabled.
- This confirms the principal UI acceptance scenarios including Windows picker/file-system CSV behavior, which pure unit tests cannot prove. [Completed checklist](docs/manual-can-demo-monitor-2026-10-20.md).
- CI verification: [PR #378 CI #2868](https://github.com/vorlocsev/dvinholm/actions/runs/39344455511), post-merge [main #2869](https://github.com/vorlocsev/dvinholm/actions/runs/39345037152), docs [PR #379 CI #2871](https://github.com/vorlocsev/dvinholm/actions/runs/39345289595), and [main #2872](https://github.com/vorlocsev/dvinholm/actions/runs/39345796252) all completed successfully.
- **Boundaries:** exact installed CI number/short SHA not supplied in user confirmation; ordinary real-capture CSV schema is covered by automated tests but was not manually exported from an actual J2534 session. All successful UI tests used only synthetic demo data. Real J2534 hardware, ECU and vehicle compatibility remain untested/deferred. Stable v0.2.1 is unchanged.

## v0.2.2 stable Windows manual acceptance and update-source development (2026-10-20)

- Published stable Dvinholm v0.2.2, SHA `77b6899`, with portable Windows x86 release ZIP and SHA-256; main CI #2878 passed. The user confirmed the version label, identical CI SHA and repeated **Up to date** status after Check Update. The check did not offer **Replace files and restart**.
- The user confirmed offline Demo CAN Monitor frames, software filter, tagged CSV export and refiltering of buffered rows on the stable build.
- The user confirmed ECU Profiles New/Edit/persistence, required-field and identifier validation, DID uniqueness/hex rejection, JSON export and duplicate-safe reimport, ID/DID search, verified-only filtering, 11-/29-bit profile switching and two consecutive **Add test profiles** clicks without duplicates. The test profile was marked **Unverified** throughout. Evidence: consecutive [PR #380 comments](https://github.com/vorlocsev/dvinholm/pull/380).
- [PR #382](https://github.com/vorlocsev/dvinholm/pull/382) merged as `9a8966f`: update-source selector added (Development CI from `main` by default, plus stable releases); stable lookup resolves the immutable release Git tag commit and validates published portable ZIP size, URL and SHA-256. Simulated-HTTP tests and [PR CI #2880](https://github.com/vorlocsev/dvinholm/actions/runs/39453824756), [post-merge CI #2881](https://github.com/vorlocsev/dvinholm/actions/runs/39454617709) succeeded. Next UI polish renames the displayed choices **Development / Stable** without changing their behavior.
- Pending separate Windows validation: UI source switching and fresh stable/CI checks on a merged CI build. Real J2534 adapters and vehicles are not tested; v0.2.2 remains the shipped stable release.

- User requested the labels **Development / Stable** rather than **CI (main) / Stable releases**. Keep internals and release eligibility unchanged; Windows UI verification of both modes remains pending after merge.

## Development / Stable switching and ECU persistence — Windows acceptance (2026-10-20)

- Post-merge [main CI #2884](https://github.com/vorlocsev/dvinholm/actions/runs/39457307648) for [PR #383](https://github.com/vorlocsev/dvinholm/pull/383) completed successfully; public [ci-2884](https://github.com/vorlocsev/dvinholm/releases/tag/ci-2884) asset published for `651e68b`. CI #2883 on the PR also passed.
- User manually installed Development CI #2884 over stable v0.2.2 via **Check update → Replace files and restart** and saw **Development** (default) and **Stable** in the WinUI source selector.
- Development update check returned **Up to date** for CI #2884 (`651e68b`). Stable update check found release v0.2.2 (`77b6899`), correctly identifying that it differs from current Development build. Switching back to Development reset the old Stable candidate and returned the button to **Check update**, without installing a downgrade.
- User confirmed persisted synthetic `user.demo.v022.ecm` after CI update/restart: edited name, 11-bit `7E0/7E8`, DIDs `F187,F190`, source note and `isVerified=false` remained. Both built-in synthetic profiles remained available. The saved catalog was not overwritten by replacement of application files.
- Evidence recorded in successive [PR #383 comments](https://github.com/vorlocsev/dvinholm/pull/383) on the date above; [manual acceptance record](docs/manual-updater-channels-2026-10-20.md). No physical J2534 hardware or vehicle tests.
- Next proposed stage: prevent confusing replacement with an older stable release as a forward update; commit ancestry must be determined before calling an update a downgrade. Do not claim a safe downgrade flow until implemented and tested.

## Stable cancel and offline UDS example preview (2026-10-20)

- User confirmed Windows CI #2890 after [PR #385](https://github.com/vorlocsev/dvinholm/pull/385): distinct Stable v0.2.2 triggered **Switch to Stable?**; **Cancel** preserved the installed Development build and reset **Check update**. After restarting, the user confirmed CI #2890 and **Up to date**, plus saved `Edited Demo ECM v0.2.2` and both built-in test profiles. Results recorded in PR #385; no real downgrade was installed.
- CI #2890 initially cancelled its Windows job but attempt 2 completed successfully and published the public CI archive; stable v0.2.2 unchanged. J2534 hardware testing still deferred.
- In development: one-click **OFFLINE SYNTHETIC UDS SAMPLE** using fictional VIN/F187/DTC values via existing validated UDS display code; offline, clearly separated from real diagnostics and blocked while connected/demo active. Eight new unit tests and Windows acceptance remain to verify.

## Offline UDS preview — completed CI and 8 manual Windows checks (2026-10-21)

- [PR #386](https://github.com/vorlocsev/dvinholm/pull/386) merged as `c86abc5a6b4fc69f6c0cfb6dec4ca08220b74665`. PR CI #2892 and [post-merge main CI #2893](https://github.com/vorlocsev/dvinholm/actions/runs/39495359322) succeeded: Windows x86 build/test, packaging, public [ci-2893](https://github.com/vorlocsev/dvinholm/releases/tag/ci-2893) release, and stable asset audit.
- User installed CI #2893 and confirmed **Preview UDS example (offline)** without a J2534 session, explicitly labeled synthetic; real Read DID / Read VIN / Read DTC controls remain disabled.
- User confirmed all 8 Windows smoke scenarios: the example button and synthetic provenance; fictional DID F190 VIN `TEST0000000000ABC` with original bytes; F187 unverified ASCII preview `DEMO-ECM` and raw bytes; two fictional DTC records (mask FF; confirmed 0x08 and pending 0x04); scrollable/selectable long output without obscuring CAN Monitor/ECU Profiles; blocking preview during live synthetic CAN Demo and restoring after Stop; no demo/preview autorun and real read controls still disabled after full application restart. Final user confirmation: 2026-10-21. Sequential evidence in [PR #386 comments](https://github.com/vorlocsev/dvinholm/pull/386) and [manual checklist](docs/manual-uds-offline-preview-2026-10-20.md).
- Eight deterministic in-process unit tests passed within CI and are distinct from the manual Windows observations. No hardware, physical ECU, or vehicle compatibility was demonstrated. Published Stable v0.2.2 is unchanged.
- Next proposed (not yet implemented): optional explicit TXT export of the **synthetic-only** UDS preview using a user-selected save location and a preserved offline provenance disclaimer. Do not conflate this with export of a real VIN or live diagnostic data; respect privacy if a later live-export feature is planned.

## Synthetic-only UDS TXT report — next development PR (2026-10-21)

- New planned optional action **Export synthetic UDS TXT** uses the Windows save-file picker for explicit, user-chosen TXT output with a `SYNTHETIC` filename and report header. Content is reconstructed **only** from the deterministic offline fixture, not the shared status text or a live UDS session; no real VIN/DTC can be exported by this action.
- Block while an actual adapter is connected, active UDS request is in progress, or CAN Demo is running. Recheck after returning from the picker and treat Cancel as no write. Preserve existing CAN demo, UDS read gating and profile storage behavior.
- Eight pure tests added for report output; PR CI and actual Windows picker/export smoke tests still required. Stable v0.2.2 unchanged. No physical J2534/ECU/vehicle data involved.

## Synthetic-only UDS TXT export — Windows acceptance complete (2026-10-21)

- [PR #388](https://github.com/vorlocsev/dvinholm/pull/388) merged as `94ab300ae1c232fcd731d4951ec8f534c0529dfb`; post-merge main **CI #2899** passed, publishing `ci-2899` Windows x86 ZIP. Eight deterministic offline report unit tests passed in CI.
- User confirmed **all eight Windows TXT export acceptance checks** after updating to Development CI #2899: new export button with ECU profiles preserved and real Read VIN/DID/DTC disabled; picker proposing `uds-SYNTHETIC-example.txt`; Cancel creates no file; a real TXT was saved to a user-chosen folder and opened in Notepad with prominent synthetic provenance; fixed fictional F190 VIN, F187 preview, DTC statuses and raw bytes matched the in-app preview; export was refused during running CAN Demo and worked again after Stop; on full restart there was no auto-export, the existing chosen-location report persisted, real reads were disabled, and the picker still opened on demand.
- Evidence is in user-confirmed [PR #388 comments](https://github.com/vorlocsev/dvinholm/pull/388) (final check `6306527823`) and the [completed checklist](docs/manual-uds-synthetic-txt-export-2026-10-21.md). This is software-only Windows validation, not a claim of real J2534/ECU/vehicle support.
- Proposed next improvement: protect newly saved user reports from the application installation tree, which can be replaced during in-place updates. Scope and 6–8 checks should be implemented/tested in a separate PR. Stable v0.2.2 unchanged.

## Windows report-save empty file regression — user report and fix work (2026-10-21)

- User reported an **empty file** created while trying the export destination safeguard introduced by [PR #390](https://github.com/vorlocsev/dvinholm/pull/390) (post-merge main CI #2905 succeeded). This does **not** invalidate previously confirmed normal TXT saves outside the application folder, but the new blocked-location/no-file behavior has **not** passed Windows manual acceptance.
- Root cause: WinRT `Windows.Storage.Pickers.FileSavePicker.PickSaveFileAsync()` can materialize an empty `StorageFile` before the app checks if the destination lies inside the directory mirrored by the updater. Returning on rejection leaves a 0-byte placeholder, although no report content is written. Logged the user observation in PR #390 comments.
- Follow-up development: migrate TXT and CSV report Save dialog to Win32 path-only selection (`GetSaveFileNameW`, `OFN_NOTESTFILECREATE`) so the install-directory rejection occurs before any file is created. Write into a temporary file in the chosen folder and replace the final file only after a nonempty successful write. Eight automated tests plus a fresh manual Windows check required. Do not claim fixed on Windows without confirmation. Stable v0.2.2 unchanged, real J2534 tests deferred.
