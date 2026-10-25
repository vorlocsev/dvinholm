## Development 0.2.4 passive UDS ReadDID minimum payload (2026-10-25)

- [x] Reject passive discovery candidates whose positive UDS ReadDataByIdentifier response `0x62` does not contain both bytes of the returned DID.
- [x] Keep valid `0x62` Single/First Frame responses, declared ISO-TP length despite CAN padding, other valid positive response types and strictly framed negative responses.
- [x] Add eight offline regressions, including a virtual J2534 receive-only scan proving invalid `0x62` responses cannot generate ECU candidates or transmit CAN frames.
- [ ] Verify PR branch CI, Stable staging, RC and MSIX. Merge only after green and confirm postmerge `main`. Stable v0.2.3 remains published and unchanged; real adapters and installed Windows GUI updater smoke remain deferred.

---

## Development 0.2.4 ECU profile search literal 0x handling (2026-10-25)

- [x] Preserve literal `0x` text in profile ID, name and source search. Bare `0x` must not become an empty wildcard; malformed/nonhex suffixes must not match unrelated metadata after prefix removal.
- [x] Strip an optional `0x`/`0X` prefix only for valid nonempty hexadecimal CAN ID/DID queries. Retain existing partial hexadecimal matching, case-insensitive metadata filtering, verified-only selection and sorted results.
- [x] Add eight offline regressions including bare prefix, literal text, invalid/nonhex suffixes, standard/extended CAN, DID, immutable catalog and verified selection.
- [x] PR #481 merged as `21ad88f` after CI #3432, staging #52, RC #53 and MSIX #121 succeeded. Main postmerge CI #3433, staging #53, RC #54 and MSIX #122 started; verify outcomes independently. Stable v0.2.3 unchanged; real J2534 and installed updater smoke remain deferred.

---

## Development 0.2.4 ECU profile JSON editor-compatible limits (2026-10-25)

- [x] Enforce the same metadata length limits on JSON imports as the ECU profile editor: ID up to 120, name up to 160 and source up to 2000 trimmed characters.
- [x] Reject more than 128 candidate DIDs in any imported profile, without changing the valid 128-DID boundary or existing duplicate DID checks.
- [x] Add eight deterministic offline tests for metadata boundaries, 128/129-DID boundary and preventing persistence when a later profile is invalid.
- [x] PR #480 merged as `298ab8b` after CI #3427, staging #50, RC #51 and MSIX #118 passed. Postmerge main CI #3428, staging #51, RC #52 and MSIX #119 started; confirm conclusions before closing integration. Stable v0.2.3 unchanged; real adapter and installed-app GUI updater testing remain deferred.

---

## Development 0.2.4 hexadecimal profile JSON import compatibility (2026-10-25)

- [x] Accept optional `0x`/`0X` prefix for imported CAN request/response IDs and candidate DIDs, consistent with the existing profile editor. Preserve canonical prefix-free export.
- [x] Keep 11-bit/29-bit arbitration limits, 16-bit DID bounds, malformed/multiple prefix rejection and numeric duplicate DID detection unchanged.
- [x] Add eight deterministic offline regression checks, including mixed-case prefixes, round-trip, malformed prefixes, out-of-range values, and no persistence on duplicate DID.
- [x] PR #479 merged as `80bb483`; postmerge main CI #3423, staging #49, RC #50 and MSIX #116 all passed. Stable v0.2.3 unchanged; physical J2534 and interactive updater tests deferred.

---

## Development 0.2.4 imported ECU candidate DID uniqueness (2026-10-25)

- [x] Reject repeated candidate DIDs in imported ECU JSON instead of silently deduplicating when converting to `IReadOnlySet<ushort>`. Compare parsed numeric DIDs so hexadecimal case and leading zeros cannot hide repetition.
- [x] Keep distinct DIDs, empty/null lists and the same DID in **different** ECU profiles valid; preserve normal JSON round-trips.
- [x] Add eight deterministic offline regressions, including rejection in a later profile and no persistence callback on invalid import.
- [x] PR #478 merged as `2f64b62`; postmerge main CI #3418, staging #47, RC #48 and MSIX #113 all passed. Stable v0.2.3 unchanged; real J2534 deferred.

---

## Development 0.2.4 ECU profile JSON duplicate-field protection (2026-10-25)

- [x] PR #476 merged as `13a4227`; postmerge main CI #3408, Stable staging #43, RC #44 and experimental MSIX #107 succeeded.
- [x] Reject duplicate JSON property names within each ECU profile object, including case variants, before deserializing. Avoid silent replacement of CAN request/response IDs, addressing type, DID list or author-marked verification.
- [x] Preserve forward-compatible distinct extra fields, existing valid catalogs, validation errors and the no-persistence-on-invalid-import boundary.
- [x] Add eight offline checks: repeated addressing, case-variant field names, verification, DID list, second-profile import atomicity and valid round-trip. No hardware involved.
- [x] PR #477 merged as `a2801bb`; postmerge main CI #3413, staging #45, RC #46 and MSIX #110 all passed. Stable v0.2.3 remains unchanged; physical J2534 and installed GUI updater checks stay deferred.

---

## Development 0.2.4 UDS cross-service malformed NRC correlation (2026-10-25)

- [x] PR #475 merged as `034721c`; postmerge main CI #3403, Stable staging #41, RC #42 and MSIX #104 succeeded.
- [x] Tighten UDS cross-service correlation: ignore only complete three-byte `7F SID NRC` messages for another service. Do not suppress malformed two-byte or padded negatives and replace a useful framing exception with a timeout.
- [x] Preserve valid other-service NRC/pending filtering, same-service NRC parsing and unrelated positive-response filtering.
- [x] Add eight deterministic offline regressions, including malformed cross-service NRC then successful next DID read over virtual J2534.
- [x] PR #476 merged as `13a4227`; main CI #3408, staging #43, RC #44 and MSIX #107 succeeded. Stable v0.2.3 release remains untouched; real adapter and installed-app updater GUI smoke remain deferred.

---

## Development 0.2.4 passive UDS discovery NRC framing (2026-10-25)

- [x] Postmerge PR #474 checks completed successfully: main CI #3398, Stable staging #39, RC #40 and experimental MSIX #101 on `601ecab`.
- [x] Accept passive ECU candidates only for exact three-byte ISO-TP Single Frame UDS negative responses (`7F SID NRC`). Reject truncated, overlong and First Frame negative prefixes rather than misidentifying ECU responders.
- [x] Preserve valid NRC 0x78/0x31 handling, legal Classic CAN padding outside the declared Single Frame payload and normal positive-response discovery.
- [x] Add eight read-only offline tests, including a virtual-J2534 passive scan verifying malformed NRC traffic is ignored and no CAN transmissions occur.
- [x] PR #475 merged as `034721c`; main CI #3403, staging #41, RC #42 and MSIX #104 passed. Stable v0.2.3 unchanged; physical J2534 and installed-app GUI updater smoke stay deferred.

---

## Development 0.2.4 UDS negative-response framing (2026-10-25)

- [x] Main CI #3393, Stable staging #37, RC #38 and MSIX #98 succeeded after CAN Monitor PR #473 merged as `c292b6b`.
- [x] Require exactly three bytes for UDS negative responses (`7F SID NRC`); padded/malformed `responsePending` (`0x78`) must fail visibly instead of being silently treated as an interim response.
- [x] Retain correct handling of valid 3-byte `0x78`, other-service filtering, and structured negative-response error codes.
- [x] Add eight offline UDS regression checks, including malformed response followed by successful DID read over virtual J2534 with no real adapter.
- [x] PR #474 merged as `601ecab`; main CI #3398, Stable staging #39, RC #40 and MSIX #101 passed. Stable v0.2.3 remains published; physical J2534 and interactive Windows updater tests remain pending.

---

## Development 0.2.4 CAN Monitor arbitration ID boundary (2026-10-25)

- [x] Main CI #3387, Stable staging #35, RC #36 and MSIX #95 succeeded after PR #472.
- [x] Fix the software CAN-ID filter to reject hexadecimal identifiers outside the 29-bit arbitration range (`0x00000000` through `0x1FFFFFFF`), whether entered alone or mixed with valid comma-separated IDs.
- [x] Apply the same numeric validation when matching buffered CAN rows and preserve historical numeric 11/29-bit matching and empty-query wildcard behavior.
- [x] Add eight offline checks, including both range boundaries, invalid multi-ID fail-closed handling, unchanged buffered history, and virtual-J2534 receive-only operation.
- [x] PR #473 merged as `c292b6b`; main CI #3393, Stable staging #37, RC #38 and MSIX #98 passed. Stable v0.2.3 unchanged; physical J2534 deferred.

---

## Next Development milestone: 0.2.4 (unreleased)

- [x] Main CI #3377, Stable staging #33 and public Stable network audit #6 passed at `4923c81` after PR #471.
- [x] Set the Development project and CI Portable/Lite binary versions to **0.2.4** without changing the published **Stable v0.2.3** release or immutable tag.
- [x] Update the Windows first-launch Russian guide, README and changelog to distinguish published Stable from the 0.2.4 Development stream.
- [x] Add eight offline regression checks for version identity, release safeguards and correct public download guidance.
- [ ] Confirm branch CI, merge after green and verify the resulting 0.2.4 Development artifact on `main`.
- [ ] Next 0.2.4 work stages: updater/UI reliability, CAN Monitor stability, ISO-TP/UDS correctness, profiles and versioned update behavior, with 6–8 checks per stage. No 0.2.4 Stable tag/release without explicit approval. Physical J2534 tests remain deferred.

---

## Live GitHub Stable lookup and download audit (2026-10-25)

- [x] PR #470 merged as `8b5978c`; main CI #3372, staging #31 and published-artifact audit #4 all passed.
- [x] Add an opt-in Windows GitHub Actions network test using the **production** `GetLatestStableReleaseAsync` and `DownloadAndPrepareAsync` against the actual public v0.2.3 GitHub release.
- [x] Eight checks cover immutable tag commit, public v0.2.3, asset SHA-256 and size, download endpoint, streamed binary/extraction, version 0.2.3, no acceptance-only receipt and removal of temporary staging.
- [ ] Confirm live CI test and all PR checks before merging. No GUI, actual installed-app update or physical J2534 tests are implied by successful automation.

---

## Published Stable 0.2.3 production-updater helper regression (2026-10-24)

- [x] PR #469 merged as `80b0563`: main CI #3367, staging #29, read-only published ZIP audit #2 succeeded.
- [x] Add a dedicated Windows x86 integration fixture that extracts **published** v0.2.2 and v0.2.3 ZIPs and runs the **actual generated replacement helper** on a disposable v0.2.2 tree, both for new-app startup success and forced startup failure/rollback.
- [x] Cover eight observable checks: separate public inputs, original version identity, baseline hashes, isolated user data, forward-byte identity, rollback-byte identity, stale-file cleanup/restoration, unchanged ECU profile and CAN speed.
- [x] Stub only GUI/process starts, independent guard startup and HKCU Run key manipulation. Keep original backup/Robocopy/recovery logic active. No real app installation is touched.
- [x] PR #470 CI #3371, staging #30 and 2/2 production helper tests passed; merged as `8b5978c`. Main CI #3372, staging #31 and audit #4 succeeded. Interactive installed-app updater **remains unverified**; physical J2534 testing remains postponed.

---

## Stable v0.2.3 public-to-public archive upgrade audit (2026-10-24)

- [x] Main CI #3361 and staging #27 passed after post-release documentation PR #468 merged as `9a45f76`.
- [x] Add a separate **read-only, automatically triggered Windows GitHub Actions audit** which downloads the actual published v0.2.2 and v0.2.3 Portable ZIPs with strict checksum manifests.
- [x] Pin the approved published v0.2.3 release target `91ecc34`, exact 62,245,250-byte ZIP and SHA-256 `fae05958bf1ca4ef92f915508b66da5acb85b4c975c6eb84a950cb8da707ed44`.
- [x] Reuse eight disposable Windows mirror/backup/rollback gates and add eight workflow contract tests. No app installations, GUI runs or release modifications.
- [x] PR #469 CI #3366 and staging #28 passed; merged as `80b0563`. Main CI #3367, staging #29 and the published ZIP audit #2 succeeded.
- [ ] Still requires **actual installed v0.2.2 -> published v0.2.3** updater smoke on Windows 11 LTSC. Offline mirror checks cannot verify the interactive updater, physical J2534 or vehicle.

---

## Stable v0.2.3 published and next steps (2026-10-24)

- [x] User confirmed actual Windows 11 LTSC offline application operation for the isolated Stable 0.2.3 candidate, built at `91ecc343608c931eb033523e3db6012a6c5dbd58`.
- [x] GitHub published v0.2.3 release ID `422666589` via the one-time authorized recovery workflow after PR #467 merged as `da135af`; the exact candidate ZIP and SHA256SUMS.txt were independently checked.
- [x] Public release/tag confirmed: `v0.2.3` -> `91ecc34`. Portable ZIP is 62,245,250 bytes with SHA-256 `fae05958bf1ca4ef92f915508b66da5acb85b4c975c6eb84a950cb8da707ed44`.
- [x] Stable 0.2.3 excludes synthetic/demo commands; Development retains them. Production tests and offline update/rollback checks passed.
- [ ] Perform actual **installed** 0.2.2 -> published 0.2.3 updater smoke on Windows and verify catalog/speed persistence, unchanged user data, clear status and rollback behavior when applicable.
- [ ] Prepare a later milestone with 6-8 checks per stage. Physical J2534/vehicle tests remain postponed; never infer adapter compatibility from CI.
- [x] Postrelease README, CHANGELOG and RU guide references corrected to public 0.2.3 in PR #468. No new Stable release without explicit approval.

---

## Stable 0.2.3 interrupted draft recovery (2026-10-24)

- [x] Release dispatch #19 on user-approved commit `91ecc34` completed all 1111 application / eight Stable / eight package safety and rollback validations but failed in `publish` after creating the draft.
- [x] Verified existing GitHub draft ID `422666589`, `draft=true`, target `91ecc343608c931eb033523e3db6012a6c5dbd58`, and exactly two GitHub SHA-256-addressed uploaded assets. Public release/tag URL is unavailable until publication.
- [x] Add one-time `resume-draft` flow to verify original successful CI/staging, fetch and SHA-256-verify both real draft uploads, run full 0.2.3 binary/commit checks, and publish only that approved existing draft. No duplicate creation, no copy replacement.
- [x] PR #467 merged to main as `da135af`; user-authorized one-time recovery published the existing draft successfully, verified the exact assets and preserved target SHA `91ecc34`.
- [x] v0.2.3 public release and tag are live; GitHub Release is no longer a draft and contains exactly the two SHA-verified assets.

---

## Stable v0.2.3 isolated acceptance app-data protection (2026-10-24)

- [x] Postmerge PR #465 main CI #3344 and staged Stable #16 passed on f45064d. One-click no-GUI launcher dry-run succeeded.
- [x] Identify a missed test-copy risk: profile catalog, CAN rate and J2534 DLL settings normally share %LOCALAPPDATA%\Dvinholm with installed v0.2.2.
- [x] Add verified-receipt-based app-data isolation for an extracted acceptance copy, including direct EXE relaunch; block installed-update and hardware connect commands during offline acceptance.
- [x] Add eight fail-closed regression checks for production path, override, direct restart, missing receipt, normal-user-path misuse, arbitrary path, relative path and whitespace path.
- [ ] Confirm all branch and main CI/staging checks. Manual Windows 11 LTSC interactive acceptance still required before requesting publication authorization.

---

## Stable v0.2.3 one-click Windows LTSC test kit (2026-10-24)

- [x] Main PR #464 commit `72dac8b` passed CI #3339, Stable staging #13 and RC #20; actual 0.2.2-to-0.2.3 ZIP rehearsal 8/8.
- [x] Bundle a double-click START-ACCEPTANCE.cmd and workflow-generated exact-commit BUILD_COMMIT.txt alongside the existing read-only offline verification scripts.
- [x] Runner adds a no-GUI --prepare-only test with the real staged ZIP, proving the new entry point verifies and extracts safely before humans start the Windows app.
- [ ] Pass new PR branch/staging checks and postmerge CI on main; kit must remain separate from updater/release targets.
- [ ] Actual Windows 11 LTSC human GUI verification remains required before asking the user for release approval. Installed Stable updater to v0.2.3 is a post-publication smoke test.

---

## Stable v0.2.3 legacy v0.2.2 debug-file compatibility (2026-10-24)

- [x] CI #3333/#3334, RC #17 and MSIX #75 passed on PR #464; staging #10 found that already-published v0.2.2 contains `Dvinholm.App.pdb` and `createdump.exe`.
- [x] With user permission, allow precisely those two pre-existing legacy debug entries in the read-only published-v0.2.2 archive comparison. Continue full SHA-256, layout, x86 runtime and version validation. Keep v0.2.3 staging/production ZIP debug exclusions unchanged by default.
- [x] PR #464 CI #3337/#3338, staging #12, RC #19 and MSIX #77 all passed; merged as 72dac8b. Main CI #3339, staging #13 and RC #20 all passed.

---

## Stable 0.2.3 real-archive upgrade rehearsal (2026-10-24)

- [x] Postmerge PR #463 main CI #3332 and automatic Stable staging #9 passed at 7ab2e1f. All eight acceptance-kit negative/positive gates passed.
- [x] Download actual published v0.2.2 Portable ZIP and SHA256SUMS from the tagged GitHub release, and compare against actual staged v0.2.3, both validated against exact SHA256 and version metadata.
- [x] Use disposable Windows directories and Robocopy mirrors to rehearse v0.2.2 payload backup, full v0.2.3 replacement, removal of obsolete files, unchanged external user data and byte-exact rollback; eight script gates.
- [ ] Confirm branch CI and 0.2.3 Stable staging PR tests, then postmerge main CI and staging. This is not an interactive WinUI launch or a true updater-network test.
- [ ] Manual Windows 11 LTSC acceptance and explicit publication approval are still required. Live 0.2.2 to 0.2.3 Stable-channel update is a postpublication smoke test. Physical J2534 remains deferred.

---

## Stable v0.2.3 acceptance kit safety regressions (2026-10-24)

- [x] PR #462 merged as `12de888`, main CI #3329, Stable staging #7 and RC #15 all succeeded.
- [x] Add eight Windows PowerShell 5.1 fail-closed scenarios using the real staged Stable 0.2.3 ZIP: valid extraction, existing target, invalid SHA, different valid SHA, corrupted checksum, missing checksum, unexpected extra file, and missing destination parent.
- [x] Keep tests in the **nonpublishing staging** job, with a contract test requiring the safety runner before any manual publication job.
- [x] PR #463 CI #3330/#3331, Stable staging #8, RC #16 and MSIX #74 passed; merged as 7ab2e1f. Main CI #3332 and staging #9 passed.
- [ ] User Windows 11 LTSC interactive startup and offline profiles/CSV/speed acceptance remain pending. Only then request approval to publish; live 0.2.2-to-0.2.3 updater check follows public release.

---

## Stable v0.2.3 Windows LTSC acceptance kit (2026-10-24)

- [x] Postmerge PR #461 main CI #3326, Stable staging #5 and Portable RC #13 succeeded on fa919f9.
- [x] Add a Windows PowerShell 5.1 acceptance kit that checks real staged ZIP and exact main SHA, refuses to overwrite existing installations, extracts to an isolated folder, and writes eight manual offline GUI checks.
- [x] Package the read-only verifier and guide separately from the executable ZIP; CI checks an actual isolated extraction and receipt of its report without GUI launch.
- [x] PR #462 CI #3327/#3328, Stable staging #6, RC #14, MSIX #73 succeeded. Merged as 12de888; main CI #3329, Stable staging #7 and RC #15 also succeeded.
- [ ] Confirm real Windows 11 LTSC launch/restart and offline profile, CSV and CAN speed behavior; do not claim GUI acceptance from Windows runner extraction.
- [ ] Perform real v0.2.2 to 0.2.3 Stable update smoke after publication. Offline updater replacement/rollback already tested in CI; physical J2534 deferred.

---

## Stable 0.2.3 automatically staged verification (2026-10-24)

- [x] PR #460 merged as `49379fe`, postmerge main CI #3323 and RC #11 both passed. Development 1103/1103 plus production-only 8/8 x86 smoke tests are independently covered.
- [x] Stage verified Stable v0.2.3 x86 Portable ZIP automatically for relevant `main` pushes and PRs, alongside the manual validation mode. Require strict SHA-256 and downloaded GitHub Actions artifact roundtrip.
- [x] Confine any actual release creation to manual `workflow_dispatch` with exact main SHA, explicit approval, downloaded draft audit and restricted write permission.
- [x] PR #461 CI #3324/#3325, staging #4, RC #12 and MSIX #72 passed. Main CI #3326, staging #5 and RC #13 passed after merge.
- [ ] Interactive Windows 11 LTSC GUI, persisted profiles, 0.2.2→0.2.3 updater acceptance, and explicit publish authorization remain pending; physical J2534 postponed.

---

## Stable 0.2.3 headless production smoke (2026-10-24)

- [x] Postmerge PR #459 at main `2d39303`: CI #3320 and Portable RC #9 both succeeded; latest published Stable remains v0.2.2.
- [x] Add an independent Windows x86 xUnit smoke project compiled with `DvinholmDevelopmentFeatures=false`, with eight production-only assertions for missing demo types/handlers, retained real VIN/DID/DTC/CAN/profile UI, regular CSV and multi-ID software filter.
- [x] Execute the production-mode test project in ordinary CI and 0.2.3 staged Stable validation, without WinUI launch or physical J2534.
- [x] PR #460 CI #3321/#3322, Stable validation #3, RC #10, MSIX #71 passed; merged as 49379fe. Main CI #3323 and RC #11 passed with production smoke checks.
- [ ] Windows 11 LTSC interactive start/restart and updater acceptance remain unverified. Stable v0.2.3 must not be published without explicit authorization.

---

## Stable v0.2.3 manual release pipeline preparation (2026-10-24)

- [x] Prepare a workflow with validation-only PR and default dry-run behavior. No release on push, PR or branch creation.
- [x] Gate actual Stable publication on explicit manual dispatch, current main SHA and authorization; validate binary, DRAFT assets and downloaded SHA-256 before public release.
- [x] Require real production DLL to exclude synthetic handlers/generators and retain VIN/DID/DTC. No trimming/AOT or physical J2534.
- [x] PR #459 CI #3318/#3319, Stable validation #2, Portable RC #8 and MSIX #70 succeeded. Squash merged as 2d39303; main CI #3320 and RC #9 subsequently succeeded.
- [ ] Require Windows 11 LTSC manual app startup, multi-ID filter/CSV, profile persistence and v0.2.2 updater acceptance, plus explicit user approval, before dispatching publish mode.

---

## Stable / Development isolation and v0.2.3 Portable RC (2026-10-24)

- [x] Merge resolution preserves PR #458 production UI safeguards while replacing hidden demo XAML placeholders and inert Release handlers with compile-time feature separation. Release defaults to `DvinholmDevelopmentFeatures=false`; Development CI and virtual regression tests opt in explicitly.
- [x] Stable Release omits Add test profiles, Start demo, Preview UDS example, Export synthetic UDS TXT, generator classes and synthetic CSV branch; existing user profiles and real VIN/DID/DTC, CAN Monitor and CSV remain.
- [x] Build a dedicated nonpublishing 0.2.3 Windows x86 Portable candidate workflow with eight checks: exact ZIP names, size, strict SHA-256, safe ZIP layout, extraction/x86, absence of synthetic DLL metadata, 0.2.3 assembly version, and exact commit informational version. Re-download the Actions artifact and revalidate unchanged bytes.
- [x] PR #457 checks passed, merged as 3ee0459; postmerge CI #3315, RC #6 and MSIX #68 succeeded. Later CI #3320 and RC #9 succeeded after PR #459.
- [ ] Manual Windows 11 LTSC Stable startup/filter/CSV, saved profiles and updater from 0.2.2 acceptance deferred; physical J2534/ECU deferred to later releases. Stable v0.2.2 remains published. No 0.2.3 Stable tag, release or signing until separately approved.

---

## CAN Monitor multi-ID software filter (Development, 2026-10-24)

- [x] Support a bounded list of up to 16 exact hexadecimal CAN IDs in the existing software-only monitor filter, including 0x prefixes, case-insensitive hex and optional surrounding whitespace. Keep single-ID and blank-filter behavior.
- [x] Fail closed on invalid/empty members or oversized queries; preserve chronological rows, buffered hidden frames and RX totals. Parse once per view refresh to avoid repeated per-row query parsing.
- [x] Keep J2534 hardware filters single-ID-only; provide a clear UI message for comma-separated requests, and show 7E0,7E8 in the software filter placeholder. Update RU user guide.
- [x] Add eight no-hardware Windows x86 tests covering legacy syntax, request/response order, mixed 11-/29-bit IDs, duplicates, malformed members, blank segments, query limit and virtual J2534 receive without TX.
- [ ] Require Windows CI 1079/1079, Portable/Lite ZIP SHA-256, Stable audit, experimental unsigned MSIX and postmerge main CI before considering the stage complete.
- [ ] Physical J2534, WinUI manual acceptance, new Stable release, updater, trimming/AOT remain deferred/unchanged.

---

## Safe CAN Monitor CSV field encoding (Development, 2026-10-24)

- [x] Escape delimiter commas, embedded double quotes, and CR/LF inside any display text field while preserving the historical five-column CAN capture and six-column SYNTHETIC_DEMO schemas.
- [x] Neutralize spreadsheet formula operators in unexpectedly supplied text, including leading whitespace, without altering valid CAN hex/11-bit/29-bit values. Render typed timestamp and DLC in invariant decimal.
- [x] Add eight synthetic Windows x86 tests: real capture, demo marker, comma, quotes, multiline, formula ID, whitespace-prefixed formula, empty/null inputs. No hardware or UI automation.
- [x] PR #455 CI #3292/#3293 passed 1071/1071, Portable/Lite ZIP SHA-256, Stable audit and experimental MSIX #49/#50. Merged as 206d136; postmerge main CI #3294 and MSIX #51 both succeeded.
- [ ] Manual Windows UI and real J2534 vehicle tests remain deferred. Stable v0.2.2, updater and trimming/AOT unchanged.

---

## Uploaded experimental MSIX artifact roundtrip integrity (Development, 2026-10-24)

- [x] Download the just-uploaded unsigned MSIX test artifact from GitHub Actions into an isolated folder and verify it against the original CI-built package.
- [x] Reject anything other than exactly Dvinholm-Lite-x86-UNSIGNED.msix and matching .msix.sha256 at the artifact root; no extra files or nested directories; hash comparison to original output and strict sidecar verification.
- [x] Eight Windows x86 synthetic fixture scenarios: valid, empty, missing MSIX, missing checksum, extra file, nested folder, modified package, modified sidecar. Use Windows PowerShell 5.1 and .NET SHA-256 without Get-FileHash.
- [x] PR #454 passed CI #3290 1063/1063, Portable/Lite SHA-256, Stable audit, experimental MSIX #47 roundtrip, and merged as 848be14; main CI #3291 and MSIX #48 both passed.
- [ ] No trusted signing, AppX install or runtime provisioning, LTSC WinUI launch or physical J2534 performed. Stable v0.2.2, Portable updater, trimming/AOT unchanged.

---

## Experimental unsigned MSIX SHA-256 manifest (Development, 2026-10-24)

- [x] Add a deterministic single-entry lowercase SHA-256 manifest bound to Dvinholm-Lite-x86-UNSIGNED.msix in experimental CI, generated and verified after package validation and before artifact upload.
- [x] Upload .msix and .msix.sha256 together. Reject missing files, tampered package bytes, invalid or uppercase hash, wrong filename and additional entries.
- [x] Eight Windows x86 tests cover valid, tampered, missing package, missing manifest, substituted name, uppercase digest, extra line and malformed digest. CI #3282/#3283 exposed that Windows x86 PowerShell does not provide Get-FileHash; use streaming .NET SHA256 instead, preserving the same manifest format and all eight checks.
- [x] PR #453 CI #3287 passed 1055/1055, Portable/Lite SHA-256, Stable audit and unsigned MSIX #44 with MakeAppx and sidecar verification. Merged as 810803d; postmerge main CI pending independent confirmation.
- [ ] Digest is integrity-only, NOT signed or trusted distribution. LTSC installation, GUI launch and physical J2534 remain untested; Stable v0.2.2, Portable updater and trimming/AOT unchanged.

---

## Windows SDK MakeAppx extracted-package checks (Development, 2026-10-24)

- [x] Add a read-only Windows SDK MakeAppx unpack gate on the **actual freshly built unsigned x86 MSIX** inside experimental CI, with guaranteed disposable-temp cleanup.
- [x] Eight checks: locate Windows SDK, unpack cleanly, root Lite x86 identity/publisher, binary PE x86, .NET 10 runtimeconfig, five branded assets, no bundled runtimes/debug payload and no signing payload.
- [x] Keep static checks plus synthetic host-inventory gate. Never sign/install/register certificates/packages, provision runtimes, launch WinUI or access physical J2534.
- [x] PR #452 CI #3280 passed 1047/1047, Portable/Lite SHA-256 and Stable audit; experimental MSIX #38 passed all eight Windows SDK MakeAppx checks. Merged as a47131d; independent main CI to confirm.
- [ ] Trusted signing, Store-less Windows LTSC dependency provisioning/installation, WinUI startup and adapter tests still unverified. Stable v0.2.2, Portable updater, trimming/AOT unchanged.

---

## Public MSIX certificate crypto-profile checks (Development, 2026-10-24)

- [x] Restrict pre-sign public certificate to RSA >=2048-bit public keys and SHA-256/384/512 with RSA PKCS#1 certificate signature. Fail closed for SHA-1 and untested ECDSA variants.
- [x] Add eight Windows x86 tests: RSA 1024 rejection, RSA 3072/4096 success, SHA-1 rejection, SHA-384/512 success and ECDSA P-256/P-384 rejection. The SHA-1 case uses a deliberately synthetic DER OID replacement because .NET 10 cannot issue SHA-1 certificates; this tests pre-sign metadata only.
- [x] Keep private-key material in memory in synthetic tests; only ephemeral public DER files go to isolated test temp directories; no CI signing or local trust modification.
- [x] PR #451 final CI #3277 passed 1047/1047, Portable/Lite ZIP SHA-256 and Stable audit; unsigned experimental MSIX #35 passed. Merged as 60f9489. Independent main CI to confirm.
- [ ] Do not treat read-only metadata validation as a signed or trusted MSIX. Stable v0.2.2, Portable updates, trimming/AOT, real LTSC install and physical J2534 unchanged.

---

## Lite MSIX public DER certificate strictness (Development, 2026-10-24)

- [x] Require exact raw public DER .cer byte equality against the X.509 certificate encoding; reject PEM, wrapped certificates, extra trailing data and non-.cer extensions before claiming signing metadata suitability.
- [x] Add eight Windows x86 test fixtures: PEM encoding, DER trailing bytes, wrong extension, missing EKU, multiple EKUs, missing KeyUsage, missing BasicConstraints and wrong MSIX package identity.
- [x] Keep offline pre-sign inspection read-only, never create PFX/private-key files, trust store mutation, signed installer, real LTSC launch or real J2534 access.
- [x] PR #450 CI #3272 passed 1039/1039, actual unsigned MSIX #31 passed, Portable/Lite ZIP and SHA-256 plus Stable audit succeeded. Merged as 5492877; independent main CI to verify.
- [ ] Trusted signing, official offline dependencies and real LTSC package installation remain unverified; Stable v0.2.2 and trimming/AOT unchanged.

---

## Public MSIX code-signing certificate preflight (Development, 2026-10-24)

- [x] Add read-only Windows PowerShell 5.1 public DER .cer preflight. Require certificate subject exactly matching the built Lite MSIX Publisher, code-signing-only EKU, DigitalSignature KeyUsage, non-CA certificate and current dates. Never request/import/export private keys or change trusted stores.
- [x] Add eight Windows x86 xUnit fixtures with disposable in-memory ephemeral RSA certificates (one valid, seven invalid certificate cases); publicly exported .cer only and no trust-store mutation. Keep development MSIX unsigned.
- [x] Enforce Windows PowerShell 5.1 syntax parse in experimental MSIX workflow, without creating signing secrets, issuing test certs in CI packaging or installing the package.
- [x] PR #449 CI #3269 passed 1031/1031, MSIX #28 succeeded, Portable/Lite ZIP SHA256 and Stable audit passed; merged as 204aa75. Postmerge main CI requires independent verification.
- [ ] Real trusted signing, offline dependency provisioning, LTSC installation, WinUI GUI startup and physical J2534 remain unverified; Stable v0.2.2, Portable updater and trimming/AOT unchanged.

---

## Recovery subprocess CI checkpoint stability (Development, 2026-10-23)

- [x] Investigate CI #3263 failure for PR #447: 1/1023 failed when a Windows PowerShell recovery subprocess did not reach the mandatory after-lock signal within the test-only 20-second deadline; same SHA passed full CI #3262 with 1023/1023 and MSIX #24 green.
- [x] Keep all **eight** double-crash matrix combinations and original file-integrity, mutex ownership, rollback, marker, idempotence and exact-once launch assertions. No test skipped and no production updater changes.
- [x] Permit 60 seconds for first/second crash checkpoint creation, 30 seconds for killed subprocess reaping and 120 seconds for real recovery/Robocopy subprocess completion; report PID, phase, exit state and bound rather than a bare cancellation on a missing checkpoint.
- [x] PR #448 CI #3266 passed Windows x86 1023/1023, Portable/Lite ZIP checksums and Stable audit; experimental MSIX #26 passed. Merged as 441434c; independent postmerge CI #3267 was running at the time of this update.
- [ ] Live Windows UI/LTSC deployment, signed MSIX and physical J2534 tests remain deferred. Stable v0.2.2, runtime packaging, Portable updater, trimming and AOT unchanged.

---
## Lite MSIX offline multi-framework .NET readiness accuracy (Development, 2026-10-23)

- [x] Parse both runtimeOptions.framework and runtimeOptions.frameworks, rejecting unknown, duplicate and invalid .NET 10 shared frameworks rather than silently dropping them.
- [x] Honor independent minimum x86 Desktop/Core versions and per-reference rollForward/applyPatches; only conservatively certified Minor with patch servicing is accepted.
- [x] Add eight Windows PowerShell 5.1 simulated host regressions: dual valid, Desktop/Core patch floors, unknown, duplicate, missing Core, per-reference Disable and applyPatches=false.
- [x] PR #447 CI #3262 passed 1023/1023, unsigned MSIX #24 passed, Portable/Lite ZIP checksums and Stable audit passed. Same SHA CI #3263 first attempt had 1 unrelated 20-second recovery checkpoint timeout; rerun requested. PR #447 merged as 96a0d43; independent main CI to confirm.
- [ ] No signed MSIX deployment, real LTSC host installation, GUI startup or physical J2534 tested. Stable v0.2.2, Portable updater, trimming/AOT unchanged.

---

## Lite MSIX actual-package offline CI gate (Development, 2026-10-23)

- [x] Run read-only simulated x86 host dependency inspection against the **actual unsigned MSIX built in CI** after the 20 existing archive checks, before artifact upload.
- [x] Use disposable PowerShell 5.1 synthetic fixture inventory in an isolated temporary directory with guaranteed cleanup. Explicit SIMULATED/NOT INSTALL-READY messaging; never install, sign, register, download dependencies or launch WinUI.
- [x] Extend Windows x86 regression coverage by eight invalid package/runtimeconfig scenarios: wrong identity, x64 package, SDK publisher, old SDK minimum, .NET 9, Major roll-forward, missing runtimeconfig and insufficient runtime patch.
- [x] PR #446 CI #3260 passed 1015/1015, actual unsigned MSIX #21 passed real-package simulated-host gate, Portable/Lite ZIP checksums and Stable audit passed; merged as 9fbb5bb. Independent postmerge main CI remains to verify.
- [ ] Signed MSIX trust chain and actual offline LTSC install/runtime provision/GUI startup still untested. Stable v0.2.2, trimming/AOT and physical J2534 test deferral unchanged.

---

## Store-less Windows 11 LTSC Lite MSIX dependency inventory (Development, 2026-10-23)

- [x] Add read-only offline MSIX prerequisite inspection: parse the built package for declared .NET 10 and Microsoft.WindowsAppRuntime.2 dependency, compare with host x86 .NET Desktop/Core and current-user registered x86 Windows App SDK Framework.
- [x] Keep fixtures explicit (both text files or neither), no user secrets, no package downloads, no certificate changes, no AppX installs, no WinUI launch and no physical J2534 interaction.
- [x] Add eight Windows PowerShell 5.1 integration fixtures and enforce syntax parse in the experimental MSIX CI.
- [x] PR #445 CI #3257 passed 1007/1007; experimental MSIX #18, Portable/Lite ZIP extraction/checksums and Stable audit passed; merged as b41698c. Independent main CI #3258 passed.
- [ ] Signed trusted MSIX and actual Store-less Windows 11 LTSC installation/startup tests are unverified. Stable v0.2.2, Portable updater and trimming/AOT remain unchanged.

---

## Lite MSIX Framework dependency and .NET 10 contract (Development, 2026-10-23)

- [x] Inspect actual unsigned CI MSIX #9, not only the source manifest: it declares Microsoft.WindowsAppRuntime.2 min 2.5.1.0 with Microsoft publisher, and has root .NET 10 runtimeconfig; no AppxSignature.p7x.
- [x] Add checks 15–20 to fail closed if the generated package loses or changes Framework dependency identity/version/publisher, the root .NET runtimeconfig or the unsigned-only designation.
- [x] Add eight synthetic Windows PowerShell 5.1 archive tests covering valid package and seven wrong-dependency/runtime/signature variants; no installation, signing or GUI launch.
- [x] PR #444 CI #3254 passed 999/999, actual MSIX #15 passed, Portable/Lite ZIP checks and Stable audit passed; merged as 38ce8db. Independent main CI #3255 succeeded.
- [ ] For Windows LTSC without Store: trusted MSIX signing, official offline Windows App SDK dependency provisioning, .NET 10 x86 and real first-run test are still required. Physical J2534 testing deferred; Stable v0.2.2 and trimming/AOT unchanged.

---

## Lite MSIX Windows 11 LTSC manifest hardening (Development, 2026-10-23)

- [x] Extend static MSIX validator with six fail-closed identity/host contracts (checks 9–14): expected unsigned Development publisher, exactly one Windows.Desktop target, Windows minimum in supported range through 26100, runFullTrust, one full-trust App entry, root EXE.
- [x] Add eight PowerShell 5.1 Windows x86 archive fixtures: one valid package and seven invalid publisher/Windows target/full-trust/entrypoint/package-path cases; no signing, install, network or GUI launch.
- [x] PR #443 CI #3247 passed 991/991; actual unsigned MSIX #9 passed, Portable/Lite ZIP checks and Stable audit passed. PR #443 merged as b1addca; independent postmerge main CI remains to confirm.
- [ ] Actual signing, dependency installation and GUI start on Windows 11 LTSC without Microsoft Store remain untested; physical J2534 is deferred. No trimming/AOT or Stable release update.

---

## Lite MSIX rejection-regression coverage (Development, 2026-10-23)

- [x] Add eight Windows PowerShell 5.1 regression fixtures to prove the existing MSIX validator rejects every invalid-package contract: missing manifest, x64 identity, wrong package name, wrong executable, missing executable, bundled runtime, missing logo and debug payload.
- [x] Generate only synthetic ZIP/MSIX archives in isolated temp folders; do not install, sign or launch WinUI.
- [x] PR #442 CI #3241 passed 983/983, Portable/Lite ZIP and Stable audit; merged as 54d5493; postmerge main CI #3242 passed.
- [ ] Actual unsigned MSIX CI stays separate and experimental; trusted signing and real installation on Store-less LTSC are not validated. Physical J2534 testing remains deferred, trimming/AOT off.

---

## Tiny framework-dependent Lite MSIX (2026-10-23)

- [x] Add opt-in x86 MSIX mode with no bundled .NET 10 or WinAppSDK runtime. Existing Portable and Lite ZIP builds unchanged.
- [x] Generate five tiny branded PNG assets from Dvinholm.ico at CI build time; no added checked-in binary logos.
- [x] Add eight package smoke checks: manifest, x86, package identity, app entry, EXE, missing bundled runtimes, visual assets, and debug artifact exclusion.
- [x] Disable Portable in-place update UI for MSIX mode.
- [x] Experimental MSIX CI #6 passed actual x86 unsigned package build, eight MSIX smoke checks, artifact upload; **11,316,543 bytes**. This remains unsigned test-only, not an installable Stable release.
- [ ] Validate runtime dependency acquisition on real Windows, especially Store-less LTSC. MSIX cannot guarantee .NET auto-download; no real J2534 testing claimed.
- [ ] Stable v0.2.2, Portable updater and trimming/AOT unaffected.

---

## Lite default .NET Minor roll-forward correctness (Development, 2026-10-23)

- [x] Fix the default `Minor` policy: if any runtime from the requested minor line is installed, require a qualifying patch in that minor. Do not accept a different, newer minor merely because the present patch is too old.
- [x] Preserve fallback to a newer minor when the requested minor is absent, along with existing `LatestMinor`, `LatestPatch`, `Disable` behavior and fail-closed unsupported policies.
- [x] Extend the Windows PowerShell 5.1 simulated-host roll-forward test matrix by eight cases (16 total): equal patch, later patch, absent requested minor, old patch plus newer minor, old patch alone, compatible and newer minors, LatestMinor exception, LatestPatch non-exception.
- [ ] Verify PR CI expected **975/975** on Windows x86, Portable/Lite ZIP extraction, Development digest audit where applicable and Stable audit. Merge only on full green; verify independent main CI.
- [ ] Real Lite GUI launch and physical J2534 testing remain deferred; Portable updater, Stable v0.2.2 and trimming/AOT untouched.

---

## Lite user-facing onboarding and release-channel clarity (2026-10-23)

- [x] Explain Lite's experimental framework-dependent requirements, bundled read-only one-click prerequisite checker and independent extraction directory in the README.
- [x] Distinguish ready-to-extract Development prerelease ZIPs from outer GitHub Actions artifact wrappers; include matching SHA256SUMS.txt and updater limitations.
- [x] Record successful PR #438 postmerge CI #3224 in ROADMAP; do not claim real WinUI launch or physical J2534 verification.
- [x] README-only PR #439 merged as `ef4f0d0`; independent main CI #3227 passed **967/967** with Portable/Lite extracted ZIP checks, Stable audit and post-upload Development digest audit. Stable v0.2.2, updater, trimming/AOT and binaries unchanged.

---

## Lite self-check CMD safety and incomplete-folder handling (Development, 2026-10-23)

- [x] Pin the bundled CMD launcher to Windows-owned System32 PowerShell; do not execute a same-name binary through PATH or the extraction/current directory.
- [x] Reject missing Lite EXE, runtimeconfig or bundled inspector before invoking the checker; preserve distinct nonzero diagnostics, inspector exit code, and read-only behavior.
- [x] Add opt-in `DVINHOLM_LITE_NO_PAUSE=1` for scripted use, keeping the interactive prompt by default.
- [x] Add eight Windows CMD integration fixtures: paths with spaces, other working directory, three missing payload components, propagated inspector error, PowerShell PATH hijack attempt, and scripted mode.
- [x] PR CI #3221 failed **962/967** due to an actual CMD quoting problem: `-LiteDirectory "%~dp0"` ends with a backslash, which can escape a closing quote when passed to PowerShell and swallow the following SDK argument. Missing-file assertions passed.
- [x] Use `-LiteDirectory "%~dp0."` so the argument ends with a dot, and normalize the directory path in the regression assertions.
- [x] PR #438 merged as `2dfaf0b`; independent main CI #3224 passed with Windows x86 build, Lite bundle ZIP contract, Portable/Lite extraction/checksums, Stable audit and post-upload Development release verification.
- [ ] Real Lite WinUI GUI launch and physical J2534 remain deferred; Stable v0.2.2, Portable updater and trimming/AOT unchanged.

---

## Lite standalone prerequisite self-check in ZIP (Development, 2026-10-23)

- [x] Bundle source-matched PowerShell checker and read-only one-click Windows CMD launcher in Lite ZIP root. Portable ZIP remains free of Lite helpers.
- [x] Enforce exact checker content and correct ZIP placement using eight Windows PowerShell 5.1 regression fixtures and a packaging gate.
- [x] Original PR #436 CI #3211 and #3212 passed **951/951**; merge with the already-integrated PR #437 preserves both changes in the same branch.
- [x] Integrated PR #436 merged as `a53dd20` after 959/959; independent main CI #3219 passed **959/959**, final Portable/Lite ZIP extraction, SHA-256 checks, Stable audit and downloaded public `ci-3219` release assets.
- [ ] Real Lite WinUI interactive startup and physical J2534 tests remain deferred; Stable v0.2.2 and trimming/AOT unchanged.

---

## Exact verified Development ZIPs as GitHub Actions artifacts (2026-10-23)

- [x] Upload the already-packaged and SHA-256-verified final Portable and Lite ZIPs, not the uncompressed `app/` and `app-lite/` publish directories.
- [x] Require all three upload paths (Portable ZIP, Lite ZIP, SHA256SUMS.txt) to exist, fail closed on missing files, avoid re-compressing finalized ZIPs using upload-artifact `compression-level: 0`.
- [x] Add eight deterministic CI contract regressions covering correct files, raw folder fallback, invalid checksum path, skipped missing-file guard, redundant compression, missing packaging gate and release-asset drift.
- [x] Public prerelease assets and Portable updater are unchanged; GitHub Actions users extract the outer Actions download once to obtain the inner final ZIP.
- [x] PR #437 CI #3214 and #3215 passed **951/951**, Windows x86, ZIP extraction/checksums, Stable audit; merged. Independent main CI #3216 passed **951/951** and uploaded final Portable/Lite ZIPs and checksum manifest as Actions artifacts.
- [ ] No live Lite WinUI launch or physical J2534 tests; Stable v0.2.2 and trimming/AOT untouched.

---

## GitHub asset SHA-256 digest cross-check (Development, 2026-10-23)

- [x] Compare all three downloaded public Development assets against GitHub Releases API `assets[].digest` (`sha256:...`), independently of the separately published `SHA256SUMS.txt`.
- [x] Fail closed for missing/unsupported/malformed GitHub digests, not-uploaded asset state, incorrect ZIP/manifest API digests, and coherent replacement of a ZIP plus a regenerated manifest.
- [x] Add eight deterministic Windows PowerShell 5.1 regressions, retaining the previous eight public release metadata scenarios (total 16).
- [x] Preserve stream-based .NET SHA256 hashing, public asset checks, Stable v0.2.2 and Portable updater; no trimming/AOT.
- [x] PR CI #3207 passed **943/943**, Portable/Lite extracted ZIP, SHA-256 manifest and Stable audit. Concurrent CI #3206 failed **942/943** on the pre-existing Windows recovery test `ServiceFileFailure_PreservesRecoveryAndAllowsSafeRetry(false, damaged-resume)`: a PowerShell/Robocopy process exceeded the 35-second test-only timeout under runner contention.
- [x] Retain the real recovery assertions and increase the recovery PowerShell subprocess's bounded test deadline to 120 seconds, without changing production scripts.
- [x] PR #435 corrected CI #3208 and #3209 passed **943/943**, both Portable/Lite extracted ZIPs and Stable audit; merged as `ab9a8df`. Independent main CI #3210 passed **943/943**, public release post-upload audit verified all three files, SHA256SUMS and GitHub API asset digests.
- [ ] No real WinUI Lite start or physical J2534 test claimed.

---

## Development public release post-upload verification (2026-10-23)

- [x] Verify GitHub Releases downloaded Portable ZIP, Lite ZIP and SHA256SUMS.txt after publication on main; require expected prerelease tag, exact main SHA, exactly three named assets, and matching published/downloaded byte sizes.
- [x] Verify the downloaded ZIPs against downloaded SHA256SUMS.txt using the existing strict PowerShell 5.1 verifier. This checks bytes after upload, not just local packaging.
- [x] Eight synthetic metadata/asset tests: valid, wrong tag, wrong commit, stable instead of prerelease, missing asset, duplicate asset, size mismatch and same-size payload corruption.
- [x] PR #434 CI #3203 and #3204 each passed **935/935**, Windows x86, Portable/Lite smoke, checksum ZIP verification and Stable audit. Merged as `9b107f6`; independent main CI #3205 passed **935/935** and the public re-download audit verified all three assets, including tag/commit/size/SHA256SUMS.
- [ ] Real Lite GUI launch and physical J2534 tests remain deferred. Stable v0.2.2, Portable updater and trimming/AOT unchanged.

---

## Development Portable/Lite SHA-256 distribution manifest (Development, 2026-10-23)

- [x] Generate the exact two-file `SHA256SUMS.txt` only after packaging/ZIP extraction; independently verify hashes in CI; ship it with both public Development prerelease ZIPs and as a CI artifact.
- [x] Add eight synthetic Windows PowerShell 5.1 checks: valid manifest, missing Lite ZIP, mutated Portable/Lite, missing manifest, duplicate entry, invalid digest and path traversal.
- [x] Document manual checksum verification and distinguish integrity checks from digital-signature authenticity. No application runtime or update selection changes.
- [x] PR CI #3197 and #3198 both failed **919/927** because `Get-FileHash` was unavailable in the Windows PowerShell 5.1 test host; all eight new checksum cases were blocked. Replace hashing with built-in .NET SHA256 over read-only file streams without external modules.
- [x] PR #433 CI #3201 passed **927/927**, Windows x86 build, Portable/Lite extracted ZIP, SHA-256 verification and Stable audit; merged as `59d06f5`. Main CI #3202 independently passed **927/927** with Portable **62,247,606 bytes**, Lite **10,358,559 bytes**; verified public prerelease `ci-3202` has all three expected assets.
- [ ] Actual Lite WinUI startup and physical J2534 tests deferred; Stable v0.2.2 and trimming/AOT unchanged.

---

## Lite Windows App SDK Singleton minimum-version verification (Development, 2026-10-23)

- [x] Require a compatible registered Windows App SDK Singleton version, not just its package name and host architecture. Singleton's MSIX major encodes `8000 + SDK major`, so SDK 2.5 requires version >= `8002.5.0.0` and later backward-compatible Singleton versions are accepted.
- [x] Add eight synthetic Windows PowerShell 5.1 prerequisite cases: valid 8002.5 and newer 8003; reject missing, old SDK minor, old SDK major, wrong CPU architecture, malformed package version and preview-only package naming.
- [x] Document required package-version scheme and limitations: fixture results are SIMULATED, not a real GUI launch or SDK bootstrap.
- [x] PR #432 CI #3194 and #3195 passed **919/919**, Windows x86, both extracted ZIPs, simulated Lite preflight and Stable audit; merged as `5e009fe`. Independent main CI #3196 passed **919/919**, Portable **62,247,574 bytes**, Lite **10,358,545 bytes**, and Stable audit.
- [ ] Real interactive WinUI Lite launch and physical J2534 tests deferred; Stable v0.2.2, Portable updater and trimming/AOT unchanged.

---

## Lite .NET roll-forward readiness accuracy (Development, 2026-10-22)

- [x] Fix false positives when Lite runtimeconfig uses Disable or LatestPatch: inspect runtimeOptions/framework rollForward before accepting installed .NET 10 x86 runtimes.
- [x] Support default/Minor, LatestMinor, LatestPatch and Disable for .NET 10; reject Major/LatestMajor and applyPatches=false conservatively until explicitly verified.
- [x] Add eight Windows PowerShell 5.1 simulated host tests for default patch readiness, Minor, LatestPatch (success and rejection), Disable (success and rejection), invalid policy and applyPatches=false.
- [x] Document conservative behavior and distinction between simulated readiness and interactive GUI launch. Stable v0.2.2, Portable updater and trimming/AOT remain unchanged.
- [x] PR CI #3190 passed **911/911**, Windows x86 build, Portable/Lite extracted ZIP and Stable audit on `cc4e1c8`. Concurrent push CI #3189 failed **910/911**, solely because the PowerShell subprocess of `WindowsLiteRuntimeConfigContractTests` exceeded its 20-second fixture timeout. No product assertion failed.
- [x] Raise Windows PowerShell subprocess timeouts for nine distribution/Lite fixture classes from 20/25 seconds to 60 seconds; keep negative assertions and actual application behavior unchanged.
- [x] PR #431 CI #3191 and #3192 independently passed **911/911** with Windows x86 build, Portable/Lite extracted ZIP gates and Stable audit; merged as `0805d4f`. Main CI #3193 independently passed **911/911**, Portable **62,247,637 bytes**, Lite **10,358,566 bytes**, and Stable audit.
- [ ] Real Lite GUI startup and physical J2534 tests remain deferred.

---

## Windows fixture cleanup stability across Lite and packaging tests (Development, 2026-10-22)

- [x] Centralize bounded, best-effort Windows test-directory cleanup (60 attempts, 200 ms delay), retrying sharing-related IOException/UnauthorizedAccessException. Log a warning if a locked disposable directory cannot be removed; never turn a passing production assertion into a teardown-only failure.
- [x] Replace duplicated teardown loops in eight Windows distribution/Lite integration test classes and the updater's existing manifest test. Preserve all preflight/WinUI/J2534 test assertions.
- [x] Add eight deterministic regressions: missing directory, empty/nested contents, transient IO and access-denied locks, persistent IO and access-denied locks, unexpected exception propagation.
- [x] PR #430 CI #3184 passed **903/903**, Windows x86, Portable/Lite extracted ZIP, Lite simulated-host validation and Stable audit; merged as `040c1eb`.
- [x] Main CI #3185 independently passed **903/903** with Portable **62,247,567 bytes**, Lite **10,358,528 bytes**, and Stable audit.
- [ ] Lite interactive Windows startup and real J2534 deferred. Portable updater behavior and Stable v0.2.2 unchanged; trimming/AOT disabled.

---

## Strict Lite runtimeconfig framework contract (Development, 2026-10-22)

- [x] Validate every shared-framework declaration before any host checks; reject unsupported framework names, duplicate names, missing/malformed versions and .NET major other than 10.
- [x] Preserve valid .NET 10 Core-only and Core+Desktop manifests and the implicit x86 Desktop prerequisite where it is not explicitly referenced.
- [x] Add eight synthetic Windows PowerShell 5.1 regressions: valid Core-only, valid Core+Desktop, unsupported extra framework, Desktop .NET 9, duplicate Core, duplicate across framework/frameworks, malformed version and missing version.
- [x] Continue CI integration against actual extracted Lite ZIP via explicitly simulated host. No runtime installation or WinUI startup.
- [x] PR #429 CI #3181 passed **895/895**, Windows x86 publish, Portable/Lite extracted archives and Stable audit, merged as `321ab89`.
- [ ] Main CI #3182 failed **894/895**, solely due to teardown IOException in `WindowsLiteAppSdkInventoryTests` after Windows locked a disposable fixture directory; other 894 test assertions passed. Address in next CI reliability PR.
- [ ] Real interactive Lite launch and physical J2534 testing deferred. Portable updater/Stable v0.2.2 unchanged, trimming/AOT disabled.

---

## Guard against Windows App SDK version drift in simulated Lite CI (Development, 2026-10-22)

- [x] Read the actual project's pinned Microsoft.WindowsAppSDK PackageReference before evaluating Lite's simulated host prerequisites; detect absent, duplicate, malformed or unsupported SDK versions.
- [x] Allow patch updates within the modeled 2.5.x SDK line, but fail closed on 2.4/2.6/3.x so the simulation cannot silently certify an unmodeled Windows App SDK runtime. Pass the validated project-derived SDK line to the prerequisite checker.
- [x] Add eight isolated Windows PowerShell 5.1 integration tests: SDK 2.5.1 and 2.5.2 pass, 2.4.0/2.6.0/3.0.0 blocked, and missing/duplicate/malformed SDK references rejected.
- [x] CI's existing real Lite ZIP extraction gate uses the actual repo project by default. Tests may supply a synthetic project via SdkProjectFile only with explicit SimulatedLiteHost; no system packages or GUI processes touched.
- [x] Repair PowerShell 5.1 syntax after PR CI #3171 exposed six failures (881/887 passed). Add an early Windows PowerShell 5.1 parser gate for Lite prerequisite and ZIP distribution scripts, before restore/build/test.
- [x] PR #428 CI #3178 passed **887/887**, Windows x86 build, syntax, ZIP checks and Stable audit; merged as `919b108`. Main CI #3179 independently passed **887/887**, Portable **62,247,610 bytes**, Lite **10,358,563 bytes** and Stable audit.
- [ ] Real interactive Lite GUI startup and physical J2534 tests deferred. Trimming/AOT disabled, Portable updater and Stable v0.2.2 unchanged.

---

## Extracted Lite ZIP simulated-host prerequisite integration (Development, 2026-10-22)

- [x] Add an explicit Lite-only SimulatedLiteHost option to the real ZIP extraction gate, exercising the Lite prerequisite inspector on the extracted runtimeconfig with temporary x64-host synthetic fixtures (x86 .NET 10 Desktop, Framework/DDLM, Visual C++ x86 and x64 Main/Singleton).
- [x] Wire this mode into CI after Lite packaging/extraction. Mark results SIMULATED INPUT; it does not query host packages, install runtimes or launch WinUI.
- [x] Add eight synthetic archive integration cases: valid Core and Desktop Lite manifests; invalid .NET 9, missing framework, embedded CoreCLR, x64 executable, malformed manifest, and invalid Portable simulated mode.
- [x] Document reproduction instructions and distinction between simulated host verification and real host readiness.
- [x] PR #427 CI #3168 passed **879/879**, Windows x86 build, both ZIP extraction checks, simulated Lite host preflight and Stable audit; merged as `7a20b80`. Post-merge main CI #3169 independently passed **879/879** (Portable **62,247,592 bytes**, Lite **10,358,535 bytes**) and Stable audit.
- [ ] Interactive Windows Lite GUI launch and physical J2534 tests still deferred. Portable updater, Stable v0.2.2 and trimming/AOT unchanged.

---

## Lite Windows App SDK complete runtime inventory (Development, 2026-10-22)

- [x] Fix stable 2.5.1 framework naming: use Microsoft.WindowsAppRuntime.2 instead of the incorrect Microsoft.WindowsAppRuntime.2.5.
- [x] Check Framework x86, Main and Singleton for host OS architecture, and matching DDLM x86. Check 2.5+ version where applicable using current-user registered AppX packages.
- [x] Add eight PowerShell 5.1 inventory tests: valid x64/x86 hosts, missing Framework/Main/Singleton/DDLM, wrong DDLM architecture, outdated Main. Upgrade all existing 16 mock scenarios to complete and realistic AppX package fixtures.
- [x] Document package naming and architecture differences. Inventory check does not install components or prove GUI startup.
- [x] PR #426 CI #3165 passed 871/871, Windows x86 build, Portable/Lite extraction and Stable audit; merged as b47ba18. Main CI #3166 independently passed 871/871, Portable 62,247,665 bytes, Lite 10,358,618 bytes and Stable audit.
- [ ] Real interactive Lite WinUI startup and physical J2534 testing remain deferred. Stable v0.2.2, Portable updater and trimming/AOT unchanged.

---

## Lite Visual C++ x86 runtime diagnostics (Development, 2026-10-22)

- [x] Extend the existing read-only Lite prerequisite checker to inspect the x86 Microsoft Visual C++ Redistributable registration (VS 2015+ family), across both Windows registry views, and reject missing, disabled or inconsistent registrations. Never confuse an x64-only runtime with x86.
- [x] Require a **third explicit x86 VC fixture** when synthetic .NET/Windows App SDK inputs are used, so no test silently combines fake packages with the actual host registry.
- [x] Add eight PowerShell 5.1 fixture regressions: two valid VC 14.x records, no x86 registration, Installed=0, older VC 12, malformed Major, missing Version and contradictory Version/Major.
- [x] Keep this as non-mutating diagnostics, not installation, full SDK bootstrap testing or a real GUI launch.
- [x] PR #425 CI #3161 passed **863/863**, Windows x86 build, both ZIP/extraction gates and Stable audit; merged as `8d7ed93`. Post-merge main CI #3162 remained pending when this stage started.
- [ ] Install prerequisites and verify actual Lite WinUI startup on a Windows machine; physical J2534 hardware stays deferred.
- [x] Preserve Portable updater behavior, published Stable v0.2.2 and PublishTrimmed/PublishAot disabled.

---

## Lite host prerequisite diagnostics (Development, 2026-10-22)

- [x] Add a read-only Windows PowerShell 5.1 check for .NET 10 x86 Desktop/shared frameworks and matching current-user x86 Windows App SDK framework package using the extracted Lite runtimeconfig; never install packages or launch WinUI.
- [x] Eight synthetic regression scenarios: valid host metadata; missing and malformed manifest; bundled CoreCLR; .NET 9 only; absent x86 Desktop runtime; wrong SDK release line; x64-only SDK framework package.
- [x] Document the real-host inspection command and explicit SIMULATED INPUT fixture marker; no claim of real UI, bootstrapper, Visual C++ or J2534 coverage.
- [x] PR #424 CI #3156 passed **855/855**, Windows x86 build, Portable + Lite ZIP/extraction checks and Stable audit; merged as `aff4e59`. Post-merge main CI #3157 independently passed **855/855**, Windows x86 build/package (Portable **62,247,592 bytes**, Lite **10,358,569 bytes**) and Stable audit.
- [ ] Manually launch Lite in an interactive Windows session with .NET 10 Desktop Runtime x86, Windows App SDK runtime and Visual C++ x86 installed; record outcome separately.
- [x] Preserve Portable updater behavior, disabled trimming/AOT and unchanged published Stable v0.2.2.

---

## Extracted Windows x86 distribution smoke (Development, 2026-10-22)

- [x] Validate **after actual ZIP extraction into a disposable directory** that Portable and Lite each contain the WinUI application executable, managed app DLL, dependency/runtime manifests and icon, and that the Windows executable is a genuine **x86 PE image (machine 0x014C)** rather than an x64 apphost renamed as x86.
- [x] Confirm Portable extraction retains its bundled CoreCLR/hostfxr and Lite extraction requires .NET 10 in `runtimeconfig.json` but carries no bundled .NET runtime. Run the existing safe-ZIP preflight *before* extracting.
- [x] Add eight real Windows PowerShell smoke regressions: valid Portable, valid Lite, x64 Portable, x64 Lite, corrupted PE signature, missing app DLL, missing `.deps.json`, and missing icon. All use synthetic disposable archives; no WinUI UI or J2534 is launched.
- [x] Make extraction preflight mandatory for both distributions in CI and for future Portable Stable release packaging **before checksum and publication**. Keep Stable v0.2.2 unchanged; do not claim a full GUI smoke test.
- [ ] Confirm all PR CI Windows x86 tests/build/Portable+Lite packaging and Stable audit, merge only after green, then verify post-merge main CI. Actual Lite GUI launch on clean Windows with x86 .NET 10 Desktop Runtime and compatible Windows App SDK remains deferred.
- [x] Prior PR #422 CI #3144 passed **839/839** and merged as `9364750`. Main CI #3145 also passed **839/839**, Windows x86 build, both ZIPs (Portable 62,247,631 bytes; Lite 10,358,585 bytes) and Stable audit.

---

## Portable/Lite ZIP security and runtime contract regression tests (Development, 2026-10-22)

- [x] Harden the common Stable/CI distribution ZIP gate against path traversal, absolute paths, dot segments, control characters and case-insensitive path collisions before extraction. These are important on Windows even if a ZIP tool produced the entries on another platform.
- [x] Validate Lite's `runtimeconfig.json` contains a framework-dependent .NET 10 runtime reference in addition to requiring that the Lite ZIP does **not** bundle CoreCLR/hostfxr. Keep Portable bundling both native runtime components and preserve existing updater asset naming.
- [x] Add eight Windows PowerShell integration cases that run the actual distribution ZIP preflight against isolated harmless archives: valid Portable, valid Lite, traversal, case-colliding duplicate path, invalid Lite runtime reference, leaked PDB, leaked debugger and missing Portable hostfxr. Required safety tests run alongside the normal application suite in CI.
- [ ] Confirm complete PR CI Windows x86 tests/build/Portable+Lite packaging and Stable audit, then merge on green and confirm independent post-merge main CI. First PR CI #3138 passed 837/839: two positive PowerShell 5.1 ZIP tests exposed a missing `System.IO.Compression.FileSystem` assembly import; fixed before rerun. This is archive validation, **not** a clean-Windows WinUI runtime/desktop smoke test or physical J2534 validation.
- [x] Prior PR #421 CI #3133 passed **831/831** with Portable **62,247,587 bytes** and Lite **10,358,538 bytes**, merged as `66d8255`. Post-merge main CI #3134 passed 831/831, built both ZIPs (Portable **62,247,551 bytes**, Lite **10,358,516 bytes**) and passed Stable audit.

---

## Portable ZIP optimization and experimental Lite distribution (Development, 2026-10-22)

- [x] Preserve the Windows x86 **Portable self-contained** distribution and existing updater asset name. Optimize its ZIP using strong Deflate compression when 7-Zip is available; fall back to .NET ZIP otherwise.
- [x] Exclude PDBs, crash dumps, trace/coverage outputs and named debug utilities from the **distribution ZIP**. Keep necessary .NET/WinUI native binaries and XAML resources; do not blindly strip dependencies.
- [x] Publish a separate Windows x86 **Lite framework-dependent** variant that requires preinstalled .NET 10 Desktop Runtime x86 plus Windows App SDK runtime. Upload separately from Portable and keep the in-app updater bound to Portable. Do not claim Lite is usable without prerequisite installation and a Windows launch smoke test.
- [x] Explicitly keep `PublishTrimmed=false`, `PublishAot=false` for both. Trimming needs dedicated WinUI 3/XAML/COM compatibility evaluation; no trimming in main.
- [x] Packaging gates validate executable, icon, runtimeconfig/deps files, no debugging byproducts or tools, Portable self-contained .NET runtime, Lite framework-dependency, and a strictly smaller Lite ZIP. CI prints actual sizes to its summary.
- [x] Stable release workflow applies the same clean ZIP gate **before computing SHA-256 and uploading**; debug tools are excluded from future Stable archives. Historical Stable v0.2.2 is unchanged.
- [x] PR #421 CI #3133 passed 831/831 with Portable (62,247,587 bytes) and Lite (10,358,538 bytes), and merged as `66d8255`. Post-merge main CI #3134 is tracked in the next stage. **Lite GUI launch on clean Windows with required runtimes** and real J2534 remain deferred.

---

## Full-backup SHA-256 manifest for new recovery checkpoints (Development, 2026-10-22)

- [x] Write `backup.sha256` **outside** the mirrored backup using a canonical SHA-256 digest of every file's relative path and bytes plus the directory paths (including empty directories). Create it from the actual old-build backup **before** starting a destructive replacement; verify the manifest immediately when it is written.
- [x] Mark new *previous-build* recovery checkpoints as `pending-v2`, requiring complete backup-tree digest verification **before** the first Robocopy rollback, in either independent guard/login recovery or updater exception rollback. Preserve backup, destination and checkpoint when missing/altered data is detected. Recognize legacy `pending` checkpoints from earlier releases without requiring a manifest; these legacy checkpoints do not gain a retroactive hash guarantee.
- [x] Eight Windows x86 end-to-end tests: changed previous executable, changed binary settings, deleted file, added file, deleted empty directory, extra directory, deleted manifest and malformed manifest. Create the trusted digest through the actual generated PowerShell functions, verify non-destructive refusal, repair data/manifest and verify byte-exact recovery using real Robocopy.
- [ ] Confirm PR CI build/823+8 tests/Windows x86 package/Stable audit, merge only after all green, and independently validate post-merge main CI. The digest is **not a signature**: it protects against accidental corruption, not an adversary able to rewrite both backup and manifest. Real reboot, WinUI and physical J2534 remain deferred.
- [x] Prior PR #419 merged as `2ee8b50`, main CI #3114 passed **823/823** tests, Windows x86 packaging and Stable audit.

---

## Backup executable preflight and repairable recovery (Development, 2026-10-22)

- [x] Reject missing/non-file/zero-byte `Dvinholm.App.exe` in a pending previous-build backup **before** Robocopy can replace or remove any installed files. Keep the pending checkpoint and backup available for inspection or repair. Also reject an invalid backup executable during initial updater backup creation, before arming a checkpoint or replacing any files.
- [x] Add eight isolated Windows PowerShell regression checks: absent backup directory, missing backup executable with remaining settings, backup executable path replaced by a directory, and empty (zero-byte) executable, each with flat or nested partial destination. Assert no mutation on failure, retained checkpoint and user data, then repair backup and verify exact restored bytes, obsolete-file removal and checkpoint cleanup.
- [x] PR #419 CI #3113 passed 823/823 and merged as `2ee8b50`; main CI #3114 also passed 823/823, Windows x86 packaging and Stable audit. Nonempty executables alone do not verify other bytes; the following full-tree digest stage addresses accidental corruption. Physical J2534, actual reboot and manual WinUI checks remain deferred.
- [x] Previous PR #418 merged as `ce05ae8`; post-merge main CI #3106 passed 815/815, Windows x86 packaging and Stable audit.

---

## Recovery service-file resilience (Development, 2026-10-22)

- [x] Treat an existing malformed, truncated, non-file or invalid `helper.identity` as unsafe during sign-in recovery. Preserve the pending checkpoint, backup and installed tree instead of assuming a live helper has stopped. A *missing* identity remains acceptable for recovery after a reboot.
- [x] Verify the copied `resume.ps1` against the original updater helper using SHA-256 **before** registering sign-in recovery or arming a pending checkpoint. If the durable script copy differs, abort the update before touching the installation.
- [x] Add eight Windows x86 PowerShell integration cases: previous-build rollback vs incomplete first install, each with missing/corrupt `helper.identity` and missing/corrupt `resume.ps1`. For damaged identity, require a fail-closed sign-in, then repair and retry. For unavailable durable script, demonstrate that sign-in cannot execute it and the independent live `-Watch` guard can finish recovery from its intact helper script. Preserve unrelated files and verify idempotence.
- [x] PR #418 CI #3105 passed 815/815, merged as `ce05ae8`; post-merge main CI #3106 also passed 815/815 with Windows x86 package and Stable audit. **A missing/corrupt `resume.ps1` without a surviving watcher still requires manual repair.** Physical J2534 and manual UI tests remain deferred.
- [x] PR #417 merged as `4a0f0a9`; post-merge main CI #3098 passed 807/807, Windows x86 build/package and Stable audit.

---

## Fail-closed recovery checkpoint validation (Development, 2026-10-22)

- [x] Validate mutually exclusive previous-build and first-install checkpoint markers **under the recovery mutex** before any destructive action. Require a regular file containing exactly `pending` after whitespace trimming. Corrupt, empty, binary, or conflicting markers fail closed, log the error and preserve backup, installed files, and checkpoints for inspection.
- [x] Add eight Windows x86 real PowerShell `-RecoverOnLogon` integration cases: four damaged-control states (garbage, empty, binary, both markers) for both previous-build rollback and incomplete first install. Verify no modification on failure, repair checkpoint, then verify correct byte-exact rollback or cleanup and no user data loss.
- [x] PR #417 CI #3097 passed 807/807, merged as `4a0f0a9`; post-merge main CI #3098 passed 807/807 with Windows x86 packaging and Stable audit. Real Windows reboot, installed app startup, manual UI and physical J2534 remain deferred.
- [x] Previous PR #416 merged as `7e72277`; post-merge main CI #3093 passed 799/799, Windows x86 packaging and Stable audit.

---

## Repeated recovery crashes and durable checkpoints (Development, 2026-10-22)

- [x] Add eight isolated x86 Windows PowerShell scenarios spanning first-install vs existing install, flat vs nested binary assets, and watcher vs sign-in recovery after a second crash.
- [x] Force-kill the first owner after the real named mutex has been acquired and before any file mutation. Start another independent PowerShell process, allow real Robocopy/cleanup to execute, and force-kill it *after* restoring/deleting files but before the checkpoint is disarmed. Confirm marker and backup survive both crashes.
- [x] Start a third independent process: it must finish the recovery safely, restore byte-for-byte old build or remove partial first install, clean backup/checkpoint, preserve unrelated data, and launch the mocked recovered executable no more than once. Repeat recovery to prove idempotence.
- [x] PR #416 passed CI #3092 (799/799), merged as `7e72277`; post-merge main CI #3093 also passed 799/799 with Windows x86 package and Stable audit. Earlier test-instrumentation failures (#3086/#3088/#3090) were fixed before merge. Real reboot/login, real app startup, UI and physical J2534 remain deferred.
- [x] Previous PR #415 merged as `14b1038`. Post-merge main CI #3083 passed Windows x86 build/package, Stable audit and **791/791** automated tests.

---

## Recovery after a killed Windows mutex owner (Development, 2026-10-22)

- [x] Add eight isolated Windows x86 crash simulations across previous-build rollback / incomplete first install, flat / nested binary assets, and independent `-Watch` / `-RecoverOnLogon` retries.
- [x] Start an actual Windows PowerShell process with the production named recovery mutex, pause after lock acquisition but before changing any checkpoint, then forcibly terminate that process. Verify the checkpoint and backup remain intact, the next process takes the abandoned mutex and safely completes Robocopy + SHA-256 recovery, and a later sign-in is idempotent with at most one recovered app launch.
- [x] PR #415 CI #3082 passed 791/791 and merged as `14b1038`. Post-merge main CI #3083 outcome is tracked in the stage above; no real reboot, installed app launch, UI or physical J2534/ECU test is claimed.
- [x] PR #414 merged as `a3accbf`; main CI #3078 passed 783/783 tests, Windows x86 build/package and Stable audit.

---

## Recovery isolation across independent installs (Development, 2026-10-22)

- [x] Generate a stable 24-hex-character SHA-256 identity from the normalized, case-insensitive **full backup path**, replacing the old backup-folder basename as both the Windows named-mutex suffix and HKCU Run recovery-entry suffix. This prevents collisions between distinct transactions that happen to share a backup directory name, without breaking locking between the guard and sign-in recovery for the same backup.
- [x] Add eight Windows x86 real cross-process integration scenarios for different installations with identical backup leaf names: first-install vs previous-build checkpoint in each installation, with plain or apostrophe-containing paths. Hold installation A's real mutex after acquisition, verify B completes independently, then release A and verify byte-exact restores/cleanup, one launch per restored old build and preserved unrelated user files.
- [x] Preserve case-insensitive identity stability in the generator and update the old script-identity assertion.
- [x] PR #414 CI #3077 passed 783/783, merged as `a3accbf`, post-merge main CI #3078 passed 783/783 with Windows x86 packaging and Stable audit. Physical J2534, actual reboot/login and installed GUI remain deferred.
- [x] Previous PR #413 CI #3070 passed 775/775 and merged as `70b6939`. Post-merge main CI #3071 passed 775/775 including Windows x86 packaging and Stable audit.

---

## Concurrent guard and sign-in recovery mutex (Development, 2026-10-22)

- [x] Add eight genuine **cross-process PowerShell** contention scenarios: first-install checkpoint versus previous-build rollback, flat versus nested assets, second concurrent sign-in versus independent `-Watch` guard. A test-only gate pauses the first process **after** acquiring the real production named mutex and **before** any destructive recovery action.
- [x] Require the concurrent contender to exit without changing the installed files or pending checkpoint. Release the owning process and assert successful cleanup, byte-exact recovery where applicable, no duplicate executable launch, untouched unrelated user data and idempotent later sign-in.
- [x] PR #413 CI #3070 passed 775/775, merged as `70b6939`; main CI #3071 passed 775/775 with Windows x86 packaging and Stable audit. Real reboot, UI, J2534 and ECU remain deferred.
- [x] Previous PR #412 post-merge main CI #3066 passed 767/767 with Windows x86 build and Stable audit.

---

## Retry recovery after an invalid previous build (Development, 2026-10-22)

- [x] Add eight isolated Windows x86 scenarios across nested assets, matching file metadata and obsolete destination directories. The initial sign-in recovery receives a pending `previous.pending` checkpoint and an invalid backup with no executable; it must preserve backup, checkpoint and the still-installed files, recording the failure.
- [x] Repair the backup executable and retry the **same** generated PowerShell recovery script. Require real Robocopy, SHA-256 and directory-manifest validation to restore exactly the repaired snapshot and delete partial-update files, then remove the checkpoint and backup.
- [x] Invoke the recovery script a third time after success; verify it is idempotent, with restored bytes and unrelated user data unchanged.
- [x] PR #412 CI #3065 passed 767/767, merged as `3cc8643`, and post-merge main CI #3066 passed 767/767; Windows x86 build, artifact and Stable audit succeeded. No real reboot/sign-in, physical J2534 or manual Windows UI test was performed.

---

## Exact updater directory manifest validation (Development, 2026-10-22)

- [x] Verify relative directory paths, including empty nested folders, in the generated PowerShell `EnsureMirroredBytes` function. A complete file count and matching SHA-256 alone do not prove the destination directory tree matches the staged build.
- [x] Add eight isolated Windows x86 cases: empty trees, matching files, same-size stale bytes, absent files, extra file, absent empty directory, unexpected empty directory, and apostrophe-containing nested binary paths. Run the actual generated PowerShell verification functions, never the installed app.
- [x] PR #411 CI #3061 passed 759/759 Windows x86 tests with build/artifact and Stable audit, merged as `cb789ca`. Post-merge main CI #3062 also passed 759/759 with packaging and Stable audit. These checks do not simulate a real reboot, installation lock, ECU or J2534 adapter.

---

## Full replacement and rollback integration (Development, 2026-10-22)

- [x] Add eight Windows x86 scenarios in `UpdateFullReplacementIntegrationTests`: first install vs existing install, startup accepted vs rejected, flat vs nested binary assets. Use real generated PowerShell and real Robocopy for all backup, forward replacement and rollback operations in disposable directories; substitute only app process launch, watchdog acknowledgement and HKCU registration.
- [x] Snapshot both builds by relative filenames and complete bytes; assert exact new-tree commit, exact old-tree rollback, removal of obsolete files, removal of failed first installation and preservation of unrelated user data. Check checkpoint/backup cleanup and number of mocked executable launches.
- [x] Fix direct replacement mirror with Robocopy `/IS /IT` so same-size/same-timestamp files are not incorrectly skipped. Previous rollback mirror already uses these options. CI #3040 found that metadata-based mirroring can still leave stale bytes; verify each staged file with SHA-256 after the mirror, repair mismatches explicitly, then reject unexpected files before starting the new app. CI #3040 reported 748/751 passing because same-metadata files retained old bytes. CI #3046 and #3048 exposed PowerShell test-host cmdlet availability (`Get-FileHash` missing); compute SHA-256 through `System.Security.Cryptography.SHA256` directly for portability. CI #3052 passed 750/751, exposing a byte-mismatched executable after rollback. Apply the same SHA-256 repair/verification to both exception-based rollback and independent sign-in/guard recovery before starting the old build. Next CI still requires full validation.
- [x] PR #410 CI #3056: 751/751 passed, Windows x86 build and artifact succeeded, Stable audit green. Merged as `c11a030`; post-merge main CI #3057 passed 751/751 with build/package and Stable audit. Full replacement tests still mock app launch and HKCU registration.
- [ ] No real app launch, Windows reboot/logon, physical J2534/ECU or manual WinUI checks are claimed. Stable remains v0.2.2 until a release is explicitly published and validated.

---

## Byte-exact rollback mirror integration (2026-10-22)

- [x] Add eight Windows x86 PowerShell recovery test variants that execute real Robocopy `/MIR` from a disposable old-build backup, mock only recovered executable launch, and assert exact relative file names and bytes against the pre-recovery snapshot.
- [x] Exercise binary bytes, nested assets and obsolete directories in every combination; assert recovery checkpoint/backup cleanup and unrelated user-file preservation.
- [ ] Confirm PR CI Windows build, tests, package, stable audit, then merge and verify main CI. No actual OS reboot/login or real installed app launch is simulated. Physical J2534 remains deferred.

---

## Updater recovery integration: real file replacement without adapter (planned 2026-10-22)

- [ ] Introduce a disposable-directory integration harness for the generated PowerShell updater, preserving the production recovery script and using only a mock executable launcher. Do not touch the installed Dvinholm directory, real HKCU Run entries, or production update backups.
- [ ] Verify eight end-to-end cases: (1) healthy update and checkpoint cleanup; (2) replacement copy failure and old-build rollback; (3) interrupted helper after partial copy with independent watcher; (4) missing watcher readiness blocks destructive copy; (5) first-install interruption cleans incomplete destination; (6) bad old-build backup is preserved and not launched; (7) simultaneous watcher and logon-recovery ownership is serialized; (8) retry after recovery restores a complete new build.
- [ ] Verify byte-for-byte directory contents and absence of obsolete files after both successful replacement and rollback; fail tests on leaked registration, lingering processes or unexpected backup deletion.
- [ ] Run Windows x86 PR CI with all automated tests, package and Stable audit. Merge only after a green run, then verify post-merge main CI. Record exact counts and run links.
- [ ] Keep actual Windows reboot/logon, manual WinUI and physical J2534/ECU/vehicle tests explicitly deferred. **Do not claim reboot safety based on mock-launcher tests.** Stable remains v0.2.2 until release verification.

---

## Resume interrupted updater recovery at next Windows sign-in — Development (2026-10-22)

- [x] Audit: the PR #404 independent `-Watch` process can restore a build after helper termination only while Windows remains running. After reboot or simultaneous process termination, `previous.pending` / `first-install.pending` could remain stranded with no watcher to handle it.
- [x] Extend the generated PowerShell helper with a durable `-RecoverOnLogon` mode and a per-update **HKCU Run** value. Copy the recovery script to `UpdateBackups/<transaction>.state/resume.ps1` outside the installation directory, register it **before** writing a pending checkpoint or running destructive Robocopy, and abort before replacement on registration failure. Keep old build and checkpoint until confirmed recovery or confirmed successful startup.
- [x] Prevent rollback over an update helper still running after a secondary sign-in using a PID-plus-process-start-time identity. Serialize independent guard and sign-in recovery with a per-transaction named mutex; acquire abandoned mutex after a prior crash. Clear the persistent Run entry before removing durable recovery data; failed deregistration retains the script for cleanup at another sign-in.
- [x] Add **eight** no-adapter Windows x86 tests: durable registration ordering, unique recovery entry and abandoned-mutex behavior, interrupted first install, no-pending cleanup, unusable old-build backup preservation, complete old-build restore (mocking only executable launch), live-helper preservation and quoted apostrophe paths. Legacy full-updater mock scripts explicitly suppress real HKCU registration.
- [x] [PR #406](https://github.com/vorlocsev/dvinholm/pull/406) merged as `62bc80a` after successful [PR Windows CI #3021](https://github.com/vorlocsev/dvinholm/actions/runs/39680082224) with **727/727 passing tests**. Post-merge [main CI #3022](https://github.com/vorlocsev/dvinholm/actions/runs/39680830580) independently passed **727/727**, x86 build/package and Stable release audit. Earlier CI #3011 failed C# PowerShell quoting and #3017 reported two outdated test fixture assertions; both issues were fixed and retested before merge. Offline tests **do not** simulate a real OS reboot, actual login or a production install. HKCU Run resumes at **user sign-in**, not before login. A damaged user profile/registry, lost backup or disk failure can still prevent recovery; real-life validation remains future scope.
- [ ] Stable release remains v0.2.2; no physical J2534/ECU/vehicle or manual WinUI checks claimed.

---

## Independent updater recovery guard for abruptly terminated helper — Development (2026-10-21)

- [x] Root cause: the updater previously created an old-build backup and used PowerShell `catch` to roll back, but termination of that **same helper process** bypasses `catch` and could strand partially mirrored application files.
- [x] Add an independent `-Watch` PowerShell process that monitors the updater helper's PID. The helper must wait for the guard's `guard.ready` acknowledgement before creating a pending checkpoint and before invoking the destructive Robocopy mirror.
- [x] Keep `previous.pending` or `first-install.pending` checkpoint files in a separate `.state` directory adjacent to the old-build backup and **outside** the application installation tree. For an unexpected helper exit with a pending checkpoint, attempt to restore the validated old build and restart it; for a first install, remove the incomplete destination.
- [x] Disarm the pending checkpoint only after the new application stays running or after an exception rollback has successfully restarted the prior build. Preserve the previous backup and marker if the independent guard cannot restore the app; never mistake a cleanup failure for recovery.
- [x] Add **eight** offline Windows x86 tests for PowerShell parsing, guard-readiness ordering, backup-validation ordering, disarm ordering, absence of checkpoint, incomplete first install, invalid recovery backup and apostrophe-containing Windows paths.
- [x] [PR #404](https://github.com/vorlocsev/dvinholm/pull/404) merged as `ed813d5`. Initial [CI #2995](https://github.com/vorlocsev/dvinholm/actions/runs/39645470165) reported **709 passed / 10 failed** because pre-existing mocked PowerShell replacement tests did not spawn the new independent watcher; a **test-fixture-only** readiness shim fixed those cases without weakening production requirements. Rerun [CI #2997](https://github.com/vorlocsev/dvinholm/actions/runs/39646392672) passed **719/719**, then post-merge [main CI #2998](https://github.com/vorlocsev/dvinholm/actions/runs/39647172973) passed **719/719** with Windows x86 build/package and Stable audit. These tests verify isolated watch mode and scripted invariants; they do **not** simulate abrupt termination of the running updater while replacing a real installed app.
- [ ] **Remaining scope:** power loss, OS restart and simultaneous helper/guard termination can still leave a pending checkpoint; an independently restartable recovery bootstrapper would be needed for a complete reboot-safe update guarantee. No manual WinUI testing or real J2534/ECU/vehicle testing is required or claimed. Stable v0.2.2 remains unchanged.

---

## Verify ECU catalog backup bytes before promotion — 2026-10-21

- [x] Audited the existing best-effort `ecu-profiles.json.bak` snapshot. A backup copy that **returned successfully but wrote only a prefix or wrong bytes** was promoted unconditionally over the last valid backup; these partial-success scenarios were not covered by prior recovery tests.
- [x] Compare complete staged backup bytes with the *committed primary* before atomically replacing `.bak`. A missing, empty, truncated or modified staged file is treated as a backup-stage failure, leaving the previous backup untouched and never misreporting a committed primary save as failed.
- [x] Add a test-only backup copy delegate, independent of the existing primary staging delegate. Production uses unchanged `File.Copy`; tests inject deterministic copy interruptions without filesystem permission changes.
- [x] Add **eight** offline regression tests: first backup throws, first backup silently missing, truncated second backup, empty second backup, same-length corruption, interrupted second backup, access-denied backup copy, and successful retry plus recovery after primary damage.
- [x] [PR #402](https://github.com/vorlocsev/dvinholm/pull/402) passed [Windows x86 PR CI #2983](https://github.com/vorlocsev/dvinholm/actions/runs/39640925892) and merged as `0cae282`; post-merge [main CI #2985](https://github.com/vorlocsev/dvinholm/actions/runs/39641711895) also passed build, all automated tests, public artifact publishing and Stable release audit. No manual WinUI or real J2534/ECU/vehicle testing is claimed. The updater's **abrupt process termination during in-place replacement** remains a separate risk; exception-based rollback alone does not establish crash recovery.
- [ ] Stable v0.2.2 remains unchanged unless a new release is independently verified.

---

## Backup-assisted recovery of damaged local ECU profile JSON — 2026-10-21

- [x] Audit: startup `LoadSavedProfileCatalog` previously failed for malformed/truncated `ecu-profiles.json`, with no backup recovery. Subsequent profile saves could overwrite the corrupted original, potentially destroying data a user might want to recover manually.
- [x] Snapshot successful catalog saves to `ecu-profiles.json.bak` via temporary-file rename, preserving the prior backup if snapshot creation fails. Primary saves remain atomic; backup is best-effort and does not turn a committed save into a false failure.
- [x] Introduce `LoadWithRecovery`: prefer a valid primary, otherwise load a valid local backup **without modifying** either file, and notify the Windows UI. If both copies are unusable, fail without deleting either. A missing primary remains empty rather than silently restoring an orphan backup.
- [x] Before any later save replaces a malformed existing primary, archive its actual bytes under a unique `.corrupt-*.json` name. If archiving fails, refuse the save rather than risk deleting the sole damaged original.
- [x] Add **eight** isolated offline tests: first backup, backup refresh, malformed recovery, truncated recovery, primary precedence over bad backup, both invalid, missing backup, recovery followed by safe archived save.
- [ ] Confirm Windows x86 PR CI build/tests/package/audit and post-merge main CI. Manual WinUI tests are deliberately deferred and **not** claimed. Real J2534 adapter, ECU and vehicle testing remains deferred; no new Stable release is implied.

---

## Atomic Import JSON — no partial ECU profile activation (2026-10-21)

- [x] Identified a UI state inconsistency: `ImportProfiles_Click` called `SetProfileCatalog(imported)` **before** saving the local JSON. When persistence failed, the imported catalog was visible in memory but would disappear after restart. This also changed the currently selected profile/CAN IDs despite the failed save.
- [x] Add `EcuProfileCatalogImporter.ImportAndPersist` that validates **the complete JSON** and then persists via the existing atomic `EcuProfileCatalogStore`, returning the replacement catalog only after successful save. Move `SetProfileCatalog` after this commit; on errors, keep existing profiles, selection and active UDS fields untouched. Distinguish validation errors from storage failures in status messages.
- [x] Add **eight** no-adapter xUnit cases: success with commit-before-return; malformed JSON; invalid CAN ID; case-insensitive duplicate IDs; invalid DID in the second profile; interrupted storage write preserving prior disk and in-memory data; failed first save leaves no file; successful retry after save failure.
- [x] [PR #399](https://github.com/vorlocsev/dvinholm/pull/399) passed [Windows x86 PR CI #2966](https://github.com/vorlocsev/dvinholm/actions/runs/39624626893) and merged as `6a885d6`; post-merge [main CI #2967](https://github.com/vorlocsev/dvinholm/actions/runs/39625478007) passed the full automated test suite, Windows x86 build/package and stable-release audit. **No manual Windows or physical J2534/ECU/vehicle tests are claimed.** Latest Stable stays at v0.2.2 until a separately verified release.

---

## ECU profile save failure and retry safety — software-only (2026-10-21)

- [x] Audited `EcuProfileCatalogStore`: it used a same-directory temporary file and rename already, but had no deterministic injection point for partially written files or a staging callback that returned without producing a valid file. A failed edit was kept out of the UI by ordering, but the save/commit boundary was not directly unit-tested.
- [x] Guard against staging callbacks that return without a file or leave a zero-byte file, before replacing the existing JSON. Provide an **internal-only staging writer** for deterministic failure simulation; production still writes using `File.WriteAllText` and cleans temporary files in `finally`.
- [x] Add `EcuProfileEditor.SaveAndPersist`: validate a separate replacement catalog, persist it, and return it for publication only after the store succeeds. Existing validation, JSON schema, `isVerified` semantics and CAN/UDS behavior remain unchanged.
- [x] Eight deterministic automated regressions: partial stage failure with/without existing file; missing/empty staged file; rename failure retaining target directory; malformed edit skips persistence; storage error preserves current memory and disk; successful retry after failure.
- [x] [PR #397](https://github.com/vorlocsev/dvinholm/pull/397) merged as `4758931` after successful [Windows PR CI #2955](https://github.com/vorlocsev/dvinholm/actions/runs/39619738972); post-merge [main CI #2956](https://github.com/vorlocsev/dvinholm/actions/runs/39620490799) also passed Windows x86 build, all automated tests, publish and Stable audit. **No manual WinUI validation was performed or claimed.** Physical J2534, ECU and vehicle testing stays deferred. Stable v0.2.2 unchanged.

---

## Stabilize ISO-TP channel-gate cancellation CI regression (2026-10-21)

- [x] Post-merge [main CI #2942](https://github.com/vorlocsev/dvinholm/actions/runs/39543337851) for [PR #394](https://github.com/vorlocsev/dvinholm/pull/394) failed **1 of 679** xUnit tests. Test `Request_CancellationWhileWaitingForChannelGateIsHonored` received the expected cancellation exception but exceeded an additional 1-second stopwatch bound on the loaded Windows runner. Build and stable release audit passed; no evidence connects this failure to the JSON export implementation.
- [x] Strengthened the test's deterministic protocol invariants: cancellation must raise `OperationCanceledException`, the blocked first request must still own the gate, and the virtual adapter must have exactly one CAN write. Remove the brittle wall-clock assertion without suppressing actual deadline failures.
- [x] [PR #395](https://github.com/vorlocsev/dvinholm/pull/395) passed [Windows x86 PR CI #2945](https://github.com/vorlocsev/dvinholm/actions/runs/39543923380) and merged as `f2009b2`; post-merge [main CI #2946](https://github.com/vorlocsev/dvinholm/actions/runs/39544351586) also passed build, **all automated tests**, package and stable release audit. Test-only stabilization; no protocol behavior or real hardware test claimed.

---

## Guarded atomic ECU profile JSON export — Development (2026-10-21)

- [x] Source audit: the ECU profile **Export JSON** handler still used WinRT `FileSavePicker` (which can materialize a 0-byte placeholder) and `File.WriteAllTextAsync` directly on the target, unlike the already hardened synthetic TXT/CAN CSV exports. An interrupted or rejected save could leave an empty or truncated JSON backup.
- [x] Replace JSON saving with shared Win32 path-only Save As selection, ordinary application-directory guard, and same-directory temporary UTF-8 write + completed-file rename. Keep cancellation a no-write operation, prevent export of an empty catalog, retain existing JSON schema and self-reported verification flags. Never send a CAN/UDS request.
- [x] Add **eight** deterministic software-only tests for complete JSON round-trip, UTF-8 BOM-free output, overwrite, empty catalog, blocked install root, blocked nested directory with existing file preserved, allowed sibling directory, and invalid destination write failure with no residual temporary file.
- [x] [PR #394](https://github.com/vorlocsev/dvinholm/pull/394) passed [Windows PR CI #2941](https://github.com/vorlocsev/dvinholm/actions/runs/39542866180) and merged as `cb79f27`. First post-merge main CI #2942 exposed the unrelated scheduler-sensitive ISO-TP test; this was corrected in PR #395, and [main CI #2946](https://github.com/vorlocsev/dvinholm/actions/runs/39544351586) completed successfully. Eight new JSON export tests run automatically. **No manual WinUI checks were requested/performed**; interactive Save As UI remains unverified by these unit tests.
- [x] No new Stable release published: latest confirmed Stable remains `v0.2.2`. Physical J2534 adapter/ECU/vehicle tests remain deferred; manual WinUI checks are not a gate for this software-only stage.

---

## Create and edit ECU profiles in WinUI — 2026-10-19

- [x] Added a local profile editor for new and existing ECU entries with name, stable ID, 11/29-bit CAN addresses, candidate DID list and required source note.
- [x] Validates duplicate IDs, hexadecimal CAN ranges, identical request/response IDs, invalid/duplicate DIDs and required metadata before attempting a disk write.
- [x] Every edited or newly created profile is marked unverified; old author-verified flag never silently carries over to modified data.
- [x] Saves to the existing JSON catalog atomically *before* replacing the live UI catalog. Cancel, invalid input and failed writes cannot silently overwrite valid saved profiles.
- [x] Editor clears search/verification filters and selects the saved profile. UI changes are local, with no J2534 commands sent.
- [x] Added software-only regression coverage for creation, replacement, invariants, 11/29-bit ranges, DID input, JSON roundtrip and preserving unrelated profiles.
- [x] [PR #369](https://github.com/vorlocsev/dvinholm/pull/369) passed Windows CI [#39323572877](https://github.com/vorlocsev/dvinholm/actions/runs/39323572877) with **587/587 passing tests**, and merged as `2989b0bb52515123ef27976b6cc83dc5df7df847`. Post-merge [main CI #39324283802](https://github.com/vorlocsev/dvinholm/actions/runs/39324283802) is checked separately.
- [ ] Optional manual dialog validation on Windows remains **deferred at user request**: [ten-step checklist](docs/manual-ecu-profile-editor-2026-10-19.md) is retained for future reference, but is **not** required to proceed with automated development. No manual passes are claimed. Real adapter/vehicle tests remain deferred.

---
## ECU profile catalog search and provenance — 2026-10-19

- [x] Added search by profile name/id, source, CAN Request/Response ID and candidate DID with case-insensitive matching and optional `0x` prefix.
- [x] Added an explicitly **author-marked verified** filter; retained JSON format and avoided inventing Volvo P2 addresses or diagnostic capabilities.
- [x] Replaced truncated metadata with a bounded, scrollable, selectable profile description showing CAN IDs, source and DID list, including a warning that the profile is not hardware-certified.
- [x] Filter changes retain the active profile and manually edited UDS values if it remains visible, and clear stale UDS fields when the selected profile becomes hidden.
- [x] Added hardware-independent unit tests for filter criteria, empty and verified-only results, catalog immutability, source and verification details.
- [ ] Manual Windows layout, search/selection and no-adapter verification: [nine-step checklist](docs/manual-ecu-profile-search-2026-10-19.md). Physical J2534 validation remains deferred.
- [x] [PR #367](https://github.com/vorlocsev/dvinholm/pull/367) passed Windows CI [#39297846535](https://github.com/vorlocsev/dvinholm/actions/runs/39297846535) (564/564 tests) and merged as `15f43e16fa9a697a857bdfa46e4bbd8e71e4e38c`. Post-merge `main` CI [#39298463839](https://github.com/vorlocsev/dvinholm/actions/runs/39298463839) also passed Windows build/tests, ZIP publishing, branded-icon packaging and stable-release audit.

---

## Russian user guide and accurate update-channel wording — 2026-10-19

- [x] Added a user-oriented Russian guide for installation, disconnected UI, J2534 connection, CAN Monitor, passive ECU discovery, ECU profiles, read-only UDS, updates and troubleshooting.
- [x] Corrected the WinUI Check update label: the updater searches successful **main-branch** CI assets, not all branches; existing code tests cover branch filtering.
- [x] Linked the guide from the GitHub README.
- [x] [PR #366](https://github.com/vorlocsev/dvinholm/pull/366) passed Windows CI [#39291963115](https://github.com/vorlocsev/dvinholm/actions/runs/39291963115) and merged as `9b42f31fcdf046cc6c006398ae0e1d80c90b0acd`; post-merge `main` CI [#39292558370](https://github.com/vorlocsev/dvinholm/actions/runs/39292558370) passed. Real J2534/vehicle validation remains deferred.

---

## Readable UDS DTC results — 2026-10-19

- [x] Added a read-only DTC formatter with result count and requested status mask, raw 24-bit code and ISO 14229 status flags displayed on separate lines.
- [x] UI `Read DTC` shows human-readable individual entries in the existing bounded/selectable status panel; no guesses about Volvo-specific fault names or causes.
- [x] Added deterministic unit tests for empty, single/multiple, duplicate, zero-status, zero-mask and manufacturer-specific records.
- [x] [PR #365](https://github.com/vorlocsev/dvinholm/pull/365) passed Windows CI [#39290526032](https://github.com/vorlocsev/dvinholm/actions/runs/39290526032), then merged as `fae165f57aedf9067883311eee71d466ed65237b`. Post-merge main CI is tracked separately. Stable release `v0.2.1` remains unchanged; physical adapter testing is deferred.

---

## Readable UDS DID values — 2026-10-19

- [x] New safe formatter displays the requested DID, the exact raw response bytes and validated VIN for `F190`.
- [x] For unfamiliar DIDs, only printable ASCII previews are suggested, explicitly marked unverified; physical units and Volvo-specific interpretations are not invented.
- [x] Wired the formatter into the existing read-only `Read DID` button, made the status panel scrollable/selectable without pushing CAN Monitor out of view, and added no-adapter regression tests for VIN, binary, malformed, empty and ASCII-preview responses.
- [x] [PR #364](https://github.com/vorlocsev/dvinholm/pull/364) passed Windows CI [#39287171823](https://github.com/vorlocsev/dvinholm/actions/runs/39287171823) and merged as `ba192e1ff97ccd6923d08b43130967dab9fc96fb`. Post-merge `main` CI [#39290076530](https://github.com/vorlocsev/dvinholm/actions/runs/39290076530) passed. Hardware/vehicle testing remains deferred; stable release remains `v0.2.1`.

---

## Passive ECU discovery — preserve CAN losses on cancellation (2026-10-19)

- [x] Updated cancelled passive scans to retain the receive-queue dropped-frame count alongside previously observed ECU candidates.
- [x] WinUI cancellation status now calls out the number of dropped frames, and the detailed status message warns that partial observations may be missing responders.
- [x] Added deterministic unit coverage for cancellation messages with and without dropped frames; retained the existing virtual-adapter cancellation coverage.
- [x] [PR #363](https://github.com/vorlocsev/dvinholm/pull/363) passed Windows CI [#39281948860](https://github.com/vorlocsev/dvinholm/actions/runs/39281948860) and was merged as `fde996e7ca7555391519e2ab8c10f440f148534c`. No physical J2534 adapter was required; stable release remains `v0.2.1`.

---

## Stable release v0.2.1 — 2026-10-19

- [x] Prepared Windows x86 v0.2.1 packaging, release notes, changelog, updated README links and in-app version metadata.
- [x] Release workflow configured to verify branded icon, publish portable ZIP plus SHA-256 file, then re-download and validate the published assets.
- [x] [PR #360](https://github.com/vorlocsev/dvinholm/pull/360) merged as `eb51b998cee3557b661b5cfa9626126ddaea6be4` after green Windows PR CI [#39183661356](https://github.com/vorlocsev/dvinholm/actions/runs/39183661356); post-merge `main` CI [#39184066818](https://github.com/vorlocsev/dvinholm/actions/runs/39184066818) also passed.
- [x] Created `release/v0.2.1` from `eb51b99`; [stable release CI #39184108681](https://github.com/vorlocsev/dvinholm/actions/runs/39184108681) passed build/tests/publish/icon checks and re-download/SHA-256 verification. [v0.2.1](https://github.com/vorlocsev/dvinholm/releases/tag/v0.2.1) is published as the latest stable release: ZIP 67,042,782 bytes, SHA-256 `5245d7691482da6434262b5767a0b23ed757df9136fb38b9aed196523a47bf09`.
- [x] User manually confirmed that the branded icon is visible in Dvinholm v0.2.1 on Windows (2026-10-19). Per-surface rendering in the titlebar, taskbar and Explorer was not separately specified in this confirmation; physical adapter/vehicle testing stays deferred.

---

## Dvinholm repository branding and Windows icons — 2026-10-19

- [x] Prepared approved silver-arrow / northern-river logo as a 7-size Windows `.ico` (16–256 px), a README PNG icon, and a wide README WebP banner.
- [x] Updated `README.md` presentation, quick-start, stable/CI links and corrected updater-channel wording; archived older development plans to `docs/DEVELOPMENT_HISTORY.md`.
- [x] Wired `ApplicationIcon` into the project, copied the ICO to published output and applied the same icon to the WinUI window at startup.
- [x] Added an automated Windows CI packaging check for the icon file and ICO directory.
- [x] [PR #358](https://github.com/vorlocsev/dvinholm/pull/358) merged as `780768061ca5bc8972ddc22924f3c124f63f1cc3` after successful PR CI [#39181656596](https://github.com/vorlocsev/dvinholm/actions/runs/39181656596); post-merge main CI [#39182025601](https://github.com/vorlocsev/dvinholm/actions/runs/39182025601) also passed build, tests, icon package verification and stable-release audit.
- [x] Windows visual smoke check: user confirmed the branded icon is visible in v0.2.1 on 2026-10-19, in addition to the earlier CI packaging checks. Specific Windows display surfaces were not individually reported.
- Stable release `v0.2.0` remains unchanged; J2534 hardware/vehicle testing remains deferred.

---

## Updater startup-launch exception rollback — 2026-10-19

- [x] Added a Windows PowerShell regression case to [PR #357](https://github.com/vorlocsev/dvinholm/pull/357): simulated `Start-Process` exception before the updated executable starts.
- [x] The test asserts that the previous build and its marker are restored, the previous executable is restarted, and the original launch error is logged without misreporting a successful rollback as failed.
- [x] PR [#357](https://github.com/vorlocsev/dvinholm/pull/357) merged as `90c42b96c4e2b87ea74d5dca8006a7f75ffff97f`; PR CI [#39178639657](https://github.com/vorlocsev/dvinholm/actions/runs/39178639657) and post-merge main CI [#39178995450](https://github.com/vorlocsev/dvinholm/actions/runs/39178995450) passed build, tests, packaging, and stable-release audit.
- Stable release stays `v0.2.0`; real J2534 adapter and vehicle testing remain deferred.

---

## Synthetic ECU test profiles — 2026-10-18

- [x] Added an **Add test profiles** action that safely appends two clearly labeled, unverified synthetic profiles to the existing local catalog without replacing user profiles.
- [x] Standard 11-bit profile uses request/response IDs `7E0/7E8`; extended 29-bit profile uses `18DA10F1/18DAF110`. Their DIDs differ to make field refresh easy to observe.
- [x] Test profiles are persisted in the user's existing catalog and are explicitly marked as UI-test-only, not vehicle-verified.
- [x] Windows manual validation completed: both profiles were added, their IDs/DIDs restored correctly when switching, values persisted across restarts, duplicate creation was prevented, JSON export/import worked, and invalid JSON was rejected without losing existing profiles.
- [x] Post-merge main CI [#39162799935](https://github.com/vorlocsev/dvinholm/actions/runs/39162799935) passed build, tests, publish/package, and the latest-stable-release audit.
- Stable release remains `0.2.0`; physical J2534 adapter/vehicle verification remains deferred.

---

## ECU profile selection refresh — 2026-10-18

- [x] Fixed profile-selection refresh by applying the final ComboBox selection on the UI dispatcher queue and reporting the applied request/response IDs in status text.
- [x] User manually verified profile A → profile B → profile A after editing the response ID; the saved value was restored correctly.
- [x] User confirmed both profiles and parameters remained correct after updating and restarting the application.
- [x] Post-merge main CI [#39162799935](https://github.com/vorlocsev/dvinholm/actions/runs/39162799935) passed build, tests, publish/package, and the latest-stable-release audit.
- Stable release remains `0.2.0`; no physical J2534 adapter or vehicle is available/required for this UI-only fix.

---

## CAN baud-rate persistence — 2026-10-18

- [x] Reproduced the source-level gap: the CAN speed ComboBox had a default selection but no persistence/load handler.
- [x] Merged PR [#343](https://github.com/vorlocsev/dvinholm/pull/343) as `2787b85d77c2a6249598182775a5a9c320092c08`; supported rates (500000, 250000, 125000) now persist to the per-user settings file and invalid/missing values safely fall back to 500000.
- [x] Added unit tests for missing settings, supported-rate round trips, invalid values, unsupported rates, and blank paths.
- [x] PR CI [#39150451313](https://github.com/vorlocsev/dvinholm/actions/runs/39150451313) and post-merge main CI [#39150903328](https://github.com/vorlocsev/dvinholm/actions/runs/39150903328) passed build/tests, publish, and stable-release audit.
- [x] Manual Windows verification passed after full application restarts for 250 kbit/s, 125 kbit/s, and 500 kbit/s.
- [x] Stable release remains `0.2.0`; no physical J2534 adapter/vehicle test was needed for this fix.

---

## Rollback-copy failure-path regression — 2026-10-18

- [x] PR [#336](https://github.com/vorlocsev/dvinholm/pull/336) merged as `7fb38e25936ca718b4701f4ec0f8141940048b22`; regression covers robocopy exit code 8, verifies rollback failure is logged, recovery artifacts are preserved, and the temporary helper script is removed. Main CI [#39140253444](https://github.com/vorlocsev/dvinholm/actions/runs/39140253444) passed.
- [x] PR [#337](https://github.com/vorlocsev/dvinholm/pull/337) merged as `369c3d38c20d86bec827914a42b38a5b67771dc5`; regression covers robocopy reporting success while the restored `Dvinholm.App.exe` is missing. PR CI [#39140363953](https://github.com/vorlocsev/dvinholm/actions/runs/39140363953) passed.
- [x] Post-merge main CI [#39140786191](https://github.com/vorlocsev/dvinholm/actions/runs/39140786191), [#39141242021](https://github.com/vorlocsev/dvinholm/actions/runs/39141242021), and [#39142078521](https://github.com/vorlocsev/dvinholm/actions/runs/39142078521) passed build/tests and the latest-stable-release audit.
- [x] Main CI #39142078521 published the Windows x86 workflow artifact `Dvinholm-Windows-x86` (66,770,176 bytes; SHA-256 `0c2a02c876fd8ef1fbf8d1d787defa642224457caf329a2cb36705e9b6491a09`).
- [x] Stable release remains `0.2.0`; no release change and no physical J2534 adapter/vehicle testing.
- [ ] Next: finish the bounded software-only updater audit and validate the current CI artifact with the remaining Windows UI smoke scenarios. Hardware testing remains deferred.

---

## Successful updater replacement cleanup regression — 2026-10-18

- [x] PR [#334](https://github.com/vorlocsev/dvinholm/pull/334) merged as `ff3f8eaeae3634f265718376a7b2ef8e6eefdecc`.
- [x] Added a Windows-only runtime test for successful replacement: new executable and files are installed, the updated executable is launched, and backup/staging directories are removed after startup succeeds.
- [x] Both PR CI runs passed: [#2656](https://github.com/vorlocsev/dvinholm/actions/runs/39137524253) and [#2657](https://github.com/vorlocsev/dvinholm/actions/runs/39137530241).
- [x] Preceding main CI passed in [run #2655](https://github.com/vorlocsev/dvinholm/actions/runs/39137390573).
- [ ] Post-merge main CI [run #2658](https://github.com/vorlocsev/dvinholm/actions/runs/39138576615) is running; confirm before marking complete.
- [ ] Continue software-only updater failure-path audit. Physical J2534 adapter/vehicle testing remains deferred.

---

## PowerShell updater path-escaping regression — 2026-10-18

- [x] PR [#332](https://github.com/vorlocsev/dvinholm/pull/332) merged as `56fac7e6a9a9b88e5c60782931709384ca7f326b`.
- [x] Added a regression test confirming updater-generated PowerShell literals escape apostrophes in staging, destination, helper-script, and backup paths.
- [x] PR CI passed in [run #39135634503](https://github.com/vorlocsev/dvinholm/actions/runs/39135634503) and [run #39135642946](https://github.com/vorlocsev/dvinholm/actions/runs/39135642946), including build/tests and stable-release audit.
- [ ] Post-merge main CI [run #39136947275](https://github.com/vorlocsev/dvinholm/actions/runs/39136947275) is running; confirm before marking complete.
- [ ] Continue software-only updater failure-path review. Physical J2534 adapter/vehicle testing remains deferred.

---

## Updater runtime-failure cleanup and recovery preservation — 2026-10-18

- [x] PR [#328](https://github.com/vorlocsev/dvinholm/pull/328) merged as `ac05588e26f4b9e255181d7a23cd6e76a04776c0`. Runtime failure paths now best-effort remove the staging directory when rollback did not fail, and remove the temporary PowerShell helper script; cleanup errors are appended to the update log. Recovery files are retained if rollback fails.
- [x] PR-head build/tests and stable-release audit passed in [run #39129093156](https://github.com/vorlocsev/dvinholm/actions/runs/39129093156); post-merge main CI [run #39133270837](https://github.com/vorlocsev/dvinholm/actions/runs/39133270837) passed build/tests, publish, and stable-release audit.
- [x] PR [#329](https://github.com/vorlocsev/dvinholm/pull/329) merged as `2404addf2af1b71688e04b5fe55474da39f3b81b`. Strengthened the Windows PowerShell runtime test: when the restored app exits during startup and rollback is declared failed, staging data must remain available for recovery while the helper script is still removed. PR CI passed in [run #39133330458](https://github.com/vorlocsev/dvinholm/actions/runs/39133330458).
- [x] Post-merge main CI for PR #329 passed in [run #39133643286](https://github.com/vorlocsev/dvinholm/actions/runs/39133643286), including build/tests, publish, and stable-release audit.
- [ ] Continue software-only updater failure-path review. Stable release `0.2.0` remains unchanged; physical J2534 adapter/vehicle testing remains deferred.

---

## Updater process-exit wait hardening — 2026-10-18

- [x] PR [#325](https://github.com/vorlocsev/dvinholm/pull/325) merged as `cc2cca05a2c66838ed3705c8a82b2daffbd07978`. The PowerShell replacement helper now limits waiting for the previous app process to 60 seconds and aborts before file replacement if that process remains stuck.
- [x] Added a Windows PowerShell runtime regression test confirming a simulated wait timeout leaves the installed executable untouched and does not create a rollback backup. PR CI [#39127243905](https://github.com/vorlocsev/dvinholm/actions/runs/39127243905) passed; post-merge main CI [#39127517071](https://github.com/vorlocsev/dvinholm/actions/runs/39127517071) passed build/tests, publish, and stable-release audit.
- [x] PR [#326](https://github.com/vorlocsev/dvinholm/pull/326) merged as `7662476e99595a41beb5dc0fb47a675efda26817`. It handles the race where the old process exits between the initial process check and `Wait-Process`: proceed only if a second check confirms that the process is gone; preserve the error if it remains.
- [x] Added a Windows PowerShell runtime regression test for the process-exit race. PR CI [#39127650770](https://github.com/vorlocsev/dvinholm/actions/runs/39127650770) and push CI [#39127638650](https://github.com/vorlocsev/dvinholm/actions/runs/39127638650) passed.
- [x] Post-merge main CI for #326 passed in [run #39127972826](https://github.com/vorlocsev/dvinholm/actions/runs/39127972826), including build/tests and stable-release audit.
- [ ] Continue software-only updater failure-path review. Stable release `0.2.0` remains unchanged; physical J2534 adapter/vehicle testing remains deferred.

---

## Cleanup after rollback-backup creation fails — 2026-10-18

- [x] PR [#323](https://github.com/vorlocsev/dvinholm/pull/323) merged as `38d2133e69f646a49c99717cb80f1acb894bc3a9`.
- [x] Fixed a failure path where copying the installed build into its rollback backup could fail after creating a partial backup directory. Since replacement has not started and the installed build is untouched, the helper now removes the incomplete backup; if cleanup fails, it appends that diagnostic to the update log.
- [x] Added a Windows PowerShell runtime regression test simulating backup-copy failure, asserting the original executable/marker remain unchanged, the partial backup is removed, and the app is not launched.
- [x] PR-head CI passed in [run #39125629739](https://github.com/vorlocsev/dvinholm/actions/runs/39125629739); the push run [#39125615157](https://github.com/vorlocsev/dvinholm/actions/runs/39125615157) also passed build/tests and the stable-release audit. Post-merge main CI [#39125940869](https://github.com/vorlocsev/dvinholm/actions/runs/39125940869) passed build/tests, publish, and stable-release audit.
- [x] PR [#322](https://github.com/vorlocsev/dvinholm/pull/322) documented PR #321's rollback-backup retention test; its post-merge main CI [#39125736986](https://github.com/vorlocsev/dvinholm/actions/runs/39125736986) passed.
- [ ] Continue software-only updater failure-path review. Stable release `0.2.0` remains unchanged; physical J2534 adapter/vehicle testing remains deferred.

---

## Rollback backup retention when restored startup fails — 2026-10-18

- [x] PR [#321](https://github.com/vorlocsev/dvinholm/pull/321) merged as `2f3446d5362cac3d122873007b88dfd9f3cbde9b`; PR CI [#39124778330](https://github.com/vorlocsev/dvinholm/actions/runs/39124778330) passed build/tests and stable-release audit.
- [x] Expanded the Windows PowerShell runtime regression test to cover both cases: a restored app that survives startup permits backup cleanup; a restored app that exits causes a rollback-failure diagnostic and retains the backup executable.
- [x] Post-merge main CI [#39125088281](https://github.com/vorlocsev/dvinholm/actions/runs/39125088281) passed build/tests, publish, and the stable-release audit.
- [ ] Continue software-only updater failure-path review. Stable release `0.2.0` remains unchanged; physical J2534 adapter/vehicle validation remains deferred.

---

## Updater release-asset URL validation — 2026-10-18

- [x] PR [#316](https://github.com/vorlocsev/dvinholm/pull/316) merged as `fb9bc552e6854419f6e0a281320b0970f8b67b76`. PR CI [#39122775092](https://github.com/vorlocsev/dvinholm/actions/runs/39122775092) and post-merge main CI [#39123078698](https://github.com/vorlocsev/dvinholm/actions/runs/39123078698) passed build/tests and the stable-release audit.
- [x] Updater lookup requires the browser download URL to match the exact expected `ci-{runNumber}/Dvinholm-Windows-x86.zip` release path, preventing mismatched run/tag/asset metadata.
- [x] Regression coverage verifies a newer run pointing to an older release tag is rejected and discovery falls back to an older valid published build.
- [ ] Continue software-only updater failure-path review. Stable release `0.2.0` remains unchanged; physical J2534 adapter/vehicle testing remains deferred.

---

## Verify updater process startup before discarding rollback copy — 2026-10-18

- [x] The replacement helper waits five seconds after launching the updated app and checks whether the process exited. An early exit is treated as an update failure, triggering the existing rollback path when an older installation exists.
- [x] Rollback startup is checked too. If the restored app exits during its startup window, the helper records rollback failure instead of claiming it was successfully restarted; the backup is not deleted before this check.
- [x] Added a Windows PowerShell runtime regression test that simulates the new executable exiting immediately and verifies that the previous executable is restored and relaunched. CI validation is pending.
- [x] PR [#318](https://github.com/vorlocsev/dvinholm/pull/318) merged as `00d49cb75fdefe2f3ee90811c1fbf5cbdd619ce4`; PR CI [#39123184595](https://github.com/vorlocsev/dvinholm/actions/runs/39123184595) and post-merge main CI [#39124043911](https://github.com/vorlocsev/dvinholm/actions/runs/39124043911) passed build/tests and stable-release audit. No stable release or physical J2534 adapter/vehicle validation is included.

---

## Updater helper-setup cleanup — 2026-10-18

- [x] PR [#314](https://github.com/vorlocsev/dvinholm/pull/314) merged as `9ae0ab54dd9d0846974cb47f86e7a980fb4f00a9` after [PR CI #39121712511](https://github.com/vorlocsev/dvinholm/actions/runs/39121712511) passed build/tests and the stable-release audit.
- [x] `StartReplacement` now guards destination normalization, local app-data directory creation, replacement-script construction, script writing, and process launch with the same failure cleanup path. If setup fails before a helper-script path exists, the extracted staging directory is still removed best-effort.
- [x] Added a regression test using a NUL-containing destination path to verify setup failure throws `ArgumentException` and removes the staging directory.
- [x] Post-merge main CI [run #39121998264](https://github.com/vorlocsev/dvinholm/actions/runs/39121998264) completed successfully, including build/tests and stable-release audit.
- [ ] Continue software-only updater failure-path review. Stable release `0.2.0` remains unchanged; physical J2534 adapter/vehicle testing remains deferred.

---

## Updater API error handling and ZIP expansion limits — 2026-10-18

- [x] PR [#307](https://github.com/vorlocsev/dvinholm/pull/307) merged as `f75d8fb9d7050aff0cda0d74601570ae23ea2ddf`. Added boundary tests for ZIP expansion accounting: exact configured limit is accepted, negative entry sizes are rejected, and over-limit/near-overflow values are rejected.
- [x] PR [#308](https://github.com/vorlocsev/dvinholm/pull/308) merged as `0a7bcd2df9b6a4d3650fc6da7735501503aad476`. Updater discovery now treats HTTP 404 on a CI release tag as an expected missing asset and falls back to older builds; other HTTP errors (including 403/429 and server errors) stop the scan via `EnsureSuccessStatusCode` rather than making more requests and obscuring the real failure.
- [x] PR-head CI passed for #307 ([run #39119334834](https://github.com/vorlocsev/dvinholm/actions/runs/39119334834)) and #308 ([run #39119376885](https://github.com/vorlocsev/dvinholm/actions/runs/39119376885)); both also passed their push CI runs.
- [x] Main CI after the prior docs checkpoint passed ([run #39119254855](https://github.com/vorlocsev/dvinholm/actions/runs/39119254855)).
- [x] Post-merge main CI passed after PR #307 ([run #39119672274](https://github.com/vorlocsev/dvinholm/actions/runs/39119672274)) and PR #308 ([run #39119700082](https://github.com/vorlocsev/dvinholm/actions/runs/39119700082)).
- [x] Documentation checkpoint PR [#311](https://github.com/vorlocsev/dvinholm/pull/311) merged; its post-merge main CI passed in [run #39120748971](https://github.com/vorlocsev/dvinholm/actions/runs/39120748971).
- [ ] Continue software-only updater failure-path audit. No stable release is being published; physical J2534 adapter/vehicle verification remains deferred.

---

## Post-merge verification — first-install cleanup diagnostics — 2026-10-18

- [x] Main CI [run #39118506041](https://github.com/vorlocsev/dvinholm/actions/runs/39118506041) completed successfully on merge commit `369da5ead23b3299b7f4f53a1a4ead3c2c9c3e8a`; build/tests and latest-stable-release audit passed.
- [x] Documentation PR [#305](https://github.com/vorlocsev/dvinholm/pull/305) merged after its current-head CI [run #39118570583](https://github.com/vorlocsev/dvinholm/actions/runs/39118570583) passed.
- [ ] Continue software-only updater review. No new stable release and no physical J2534 adapter/vehicle validation.

---

## First-install cleanup reporting hardened — 2026-10-18

- [x] PR [#304](https://github.com/vorlocsev/dvinholm/pull/304) merged as `369da5ead23b3299b7f4f53a1a4ead3c2c9c3e8a`.
- [x] The updater no longer unconditionally claims that a partial first-install destination was removed. It checks the result and records a distinct error if deletion fails or the destination remains.
- [x] Added Windows PowerShell runtime coverage simulating access denied during cleanup, while asserting that the failure is logged and the app is not launched. Current-head PR CI passed build/tests and latest-stable-release audit ([PR run #39118180207](https://github.com/vorlocsev/dvinholm/actions/runs/39118180207), [push run #39118177031](https://github.com/vorlocsev/dvinholm/actions/runs/39118177031)).
- [ ] Post-merge main CI [run #39118506041](https://github.com/vorlocsev/dvinholm/actions/runs/39118506041) is still running; verify before marking this merge checkpoint green.
- [ ] Continue software-only updater failure-path review. No stable release or physical J2534 adapter/vehicle validation is included.

---

## Updater discovery restricted to published main builds — 2026-10-18

- [x] PR [#301](https://github.com/vorlocsev/dvinholm/pull/301) merged as `8afeba1a27f326f97ef87fde2f78072216e77c7d`. Lookup now considers only completed successful `push` runs for `main`, matching the workflow that publishes public updater assets.
- [x] Added regression coverage proving feature-branch pushes and `workflow_dispatch` runs are skipped without release lookups. PR-head build/tests and latest-stable-release audit passed ([run #39116058181](https://github.com/vorlocsev/dvinholm/actions/runs/39116058181), [run #39116048252](https://github.com/vorlocsev/dvinholm/actions/runs/39116048252)).
- [x] PR [#300](https://github.com/vorlocsev/dvinholm/pull/300) merged as `052e9d2a26ad5d030cdb6dd35934c887ff11c294`, recording PR #299 cleanup hardening.
- [x] Post-merge main CI passed for both merges: [run #39116463864](https://github.com/vorlocsev/dvinholm/actions/runs/39116463864) passed build/tests and stable-release audit on PR #301's merge commit; [run #39116480546](https://github.com/vorlocsev/dvinholm/actions/runs/39116480546) passed build/tests and stable-release audit on PR #300's merge commit.
- [ ] Continue software-only updater failure-path review. Do not publish a new stable release until release gates are satisfied. Physical J2534 adapter/vehicle testing remains deferred.

---

## Updater cleanup error handling — 2026-10-18

- [x] PR [#299](https://github.com/vorlocsev/dvinholm/pull/299) merged as `a0f7162e2c984acb90970778795b004336aafd9e` after both PR CI runs passed build/tests and stable-release audit.
- [x] Best-effort cleanup after failed downloads/extraction now catches both `IOException` and `UnauthorizedAccessException`, so expected cleanup failures do not mask the original failure.
- [x] Added regression coverage for both cleanup exception types.
- [ ] Verify post-merge `main` CI before declaring the merge checkpoint fully green. Continue reviewing updater failure-path coverage and avoid publishing a new stable release until software validation is complete. Physical J2534 adapter/vehicle tests remain deferred.

---

## Updater helper-start cleanup merged — 2026-10-18

- [x] PR [#297](https://github.com/vorlocsev/dvinholm/pull/297) merged as `c8466f49e53ebb2332e9f40733f159af41729e7b` after the latest PR CI run [#39114708239](https://github.com/vorlocsev/dvinholm/actions/runs/39114708239) passed both build/tests and stable-release audit.
- [x] Failed update-helper startup now triggers best-effort cleanup of the temporary PowerShell script and staging directory; script-write failures are covered by the same cleanup path. Cleanup exceptions do not replace the original startup/write error.
- [x] Added regression coverage for removing the script and staging directory when helper startup fails.
- [x] Increased the rollback runtime integration test timeout from 20 to 60 seconds after CI hit a timeout while waiting for the PowerShell test process. The updated PR CI passed; this is a test-timeout adjustment, not evidence of a production rollback defect.
- [ ] Await post-merge `main` CI before marking the merge checkpoint fully green. Continue software-only updater review; physical J2534 adapter/vehicle testing remains deferred. No stable release was published.

---

## Updater expanded-size guard — arithmetic overflow edge case (2026-10-18)

- [x] Strengthened the production guard to compare each entry length against the remaining 1,048,576,000-byte budget before adding it, preventing aggregate arithmetic overflow from escaping as `OverflowException`.
- [x] Added a regression test for `[1, long.MaxValue]`, expecting the consistent `InvalidDataException` rejection path.
- [x] CI passed on head `8b960c192ffc3b356429232814cc5408d5afa08d`: build/tests, publish, artifact upload, and v0.1.1 release ZIP audit succeeded ([run #39103682783](https://github.com/vorlocsev/dvinholm/actions/runs/39103682783)).
- [ ] Continue software-only updater review; PR [#286](https://github.com/vorlocsev/dvinholm/pull/286) remains open pending explicit approval.
- PR remains open pending explicit approval. No release publication or physical J2534 adapter/vehicle testing.

---

## Updater expanded-archive size-limit test — fixture approach revised (2026-10-18)

- [x] CI repeatedly showed that editing ZIP size metadata in the synthetic archive did not trigger the expected exception (483 other tests passed). The test fixture was unreliable; no conclusion about a production defect follows from that failure.
- [x] Confirmed the production limit is `1_000L * 1024 * 1024` = 1,048,576,000 bytes (1,000 MiB). Isolated the existing aggregate expanded-size guard in `LatestBuildUpdater.ValidateExpandedSize`, called by the production extraction path.
- [x] Replaced ZIP-metadata manipulation with direct boundary tests: aggregate above 1,048,576,000 bytes rejected, exactly 1,048,576,000 bytes accepted, and negative entry size rejected.
- [x] CI passed on head `aa2a816e214e9f34ca5387595962fd8b4c1ed055`: all 486 tests passed and the published v0.1.1 archive audit succeeded ([run #39102771531](https://github.com/vorlocsev/dvinholm/actions/runs/39102771531)).
- [x] Confirmed the new size-guard tests are now aligned with the production cap and pass in CI.
- [ ] Continue software-only updater review. PR #286 remains open; do not merge or publish a release without explicit approval. No physical adapter/vehicle testing.

---

## Updater expanded-archive size-limit regression test — CI failure investigated (2026-10-18)

- [x] PR #285 CI exposed a real test-fixture flaw: changing only the ZIP central-directory size did not make the .NET ZIP reader report the oversized entry; 483 tests passed and this new test failed because no exception was thrown.
- [x] Created follow-up branch `fix/updater-expanded-size-test` from PR #285 and updated the fixture to set the uncompressed-size field consistently in both the local file header (offset 22) and central-directory header (offset 24), without creating a huge payload.
- [ ] Run CI on the follow-up branch and confirm the oversized entry is rejected before extraction and both staging/temporary ZIP files are cleaned up.
- [ ] Do not mark this regression test verified until CI passes. No production-code changes, release publication, or physical adapter/vehicle testing.

---

## Updater expanded-archive size-limit regression test — 2026-10-18

- [x] Added a focused regression test that alters a small ZIP's central-directory metadata to declare an entry larger than the 1,000,000,000-byte extraction limit, without allocating a huge test payload.
- [x] The test expects rejection before extraction and verifies staging-directory and temporary-archive cleanup.
- [ ] Await CI on PR #285 before marking the test verified.
- [ ] Continue software-only updater review. No production-code changes, release publication, or physical adapter/vehicle testing.

---

## Manual Windows verification — disconnected startup state (2026-10-18)

- [x] After relaunch, user confirmed the saved test DLL path remains visible and the app reports **not connected**.
- [x] This confirms the app does not auto-connect on startup. The Connect action was not used; no physical adapter was available or tested.
- [x] Continue with software-only release checks; do not claim physical J2534/vehicle compatibility.

---

## J2534 DLL path persistence — manual verification passed (2026-10-18)

- [x] User tested the Windows x86 CI artifact on Windows 11 LTSC IoT 26100.9550: entered `C:\\Dvinholm-Test\\missing-j2534.dll`, fully closed the app, reopened it, and confirmed the path was still present.
- [x] This confirms the saved DLL-path setting survives an application restart. No connection attempt was made; this was a persistence-only check.
- [x] Fix is merged to `main` in commit `696aad80be40deac5acb6632d20b74e4471a2f9e` via [PR #281](https://github.com/vorlocsev/dvinholm/pull/281). Branch CI passed: [run #39077395043](https://github.com/vorlocsev/dvinholm/actions/runs/39077395043).
- [ ] Do not publish a new stable release until the remaining software-only checks and release gates are reviewed. Physical J2534 adapter/vehicle testing remains deferred.

---

## Dvinholm v0.2.0 stable release — Windows launch confirmed — 2026-10-18

- [x] User manually confirmed that the exact published v0.2.0 Windows archive opens on Windows 11 LTSC IoT 26100.9550.
- [x] Stable release remains available at [v0.2.0](https://github.com/vorlocsev/dvinholm/releases/tag/v0.2.0). Asset SHA-256: `c80e56a8cf594485cfeb82a1003907cff22c52716abf2e60e518d290b4f3deec`.
- [x] Automated release pipeline passed 479 tests; latest main CI on `f889482e99565ae4093de2da3cc3c4bbe75308a2` passed: [run #39048794190](https://github.com/vorlocsev/dvinholm/actions/runs/39048794190).
- [ ] Continue manual UI verification: updater button/state and retry flows; inspect disconnected/no-adapter behavior and monitor/UDS controls as applicable. The app launch alone does not verify all UI flows.
- [ ] Physical J2534 adapter and vehicle testing remain deferred to a future release; do not claim hardware validation.

---

## Post-PR #275 main CI passed — 2026-10-17

- [x] Documentation checkpoint PR [#275](https://github.com/vorlocsev/dvinholm/pull/275) is merged as `9d8dcbf9d7797b5a65d7fd9461c6472ba34ff931`; the complete existing `ROADMAP.md` and `AI_WORKLOG.md` content was preserved.
- [x] Post-merge [CI run #39045906011](https://github.com/vorlocsev/dvinholm/actions/runs/39045906011) passed on the exact `main` SHA. Both `build` and `audit-v0-1-1-release` jobs completed successfully.
- [x] The published stable v0.1.1 release ZIP audit remains green; this CI result does not constitute manual verification of updater behavior in the installed Windows UI.
- [ ] Windows manual updater/button-state verification remains outstanding. Physical J2534 adapter/vehicle testing remains deferred.
- [ ] Stable v0.2.0 remains unpublished and requires explicit user authorization. Do not merge PR #228 or PR #225 without explicit approval; the digest metadata fix from #225 is already present on `main`.

---

## PR #274 merged; updater CI run pagination — 2026-10-17

- [x] PR [#274](https://github.com/vorlocsev/dvinholm/pull/274) merged into `main` as `53d32922ac4d198d80efc7a4c2d2a6b2d2266c82`.
- [x] Both pre-merge CI runs on head `01d20a0a79c4ef5530b9df92dd6f6967591769f4` passed: [push run #39044585178](https://github.com/vorlocsev/dvinholm/actions/runs/39044585178) and [PR run #39044588419](https://github.com/vorlocsev/dvinholm/actions/runs/39044588419).
- [x] Post-merge [CI run #39044984373](https://github.com/vorlocsev/dvinholm/actions/runs/39044984373) passed on the exact merge SHA; build/tests and audit of the published v0.1.1 ZIP succeeded.
- [x] `LatestBuildUpdater.GetLatestSuccessfulBuildAsync` now paginates through GitHub Actions workflow runs (100 per page) until an eligible published build is found or a short page indicates there are no more runs. Pull-request events remain excluded because their SHA can identify a synthetic merge commit.
- [x] Added `LookupContinuesToSecondPageWhenFirstPageHasNoEligibleBuild`, covering a full page of PR runs followed by an eligible push build on page 2. The test asserts page progression and expected build metadata.
- [ ] Windows manual updater/button verification remains outstanding; no physical J2534 adapter/vehicle was available or needed for this API lookup change.
- [ ] Stable v0.2.0 remains unpublished and requires explicit user authorization. PR #228 remains open and must not be merged without explicit user approval.

## Post-PR #272 main CI and UDS source-audit checkpoint — 2026-10-17

- [x] PR [#272](https://github.com/vorlocsev/dvinholm/pull/272) merged as `8eb69feabc652984c4deaa6236efff249a70bd47`.
- [x] Post-merge [CI run #38997313752](https://github.com/vorlocsev/dvinholm/actions/runs/38997313752) passed on the exact merge SHA; build/tests and audit of the published v0.1.1 ZIP both succeeded.
- [x] Re-audited `MainWindow.xaml.cs`: UDS operations are serialized through `_udsGate`, cancel tokens are signalled on window close, and session disposal is gated on tracked task completion and gate availability. This is a source review, not a new WinUI runtime test.
- [x] Reviewed the test inventory; existing tests already cover UDS cancellation, late-response correlation, ISO-TP cancellation/recovery, and virtual J2534 flows. No duplicate test was added.
- [ ] Manual Windows updater/button-state confirmation remains outstanding. User has no physical adapter; active-session CAN/UDS UI behavior and hardware/vehicle validation remain deferred.
- PR [#228](https://github.com/vorlocsev/dvinholm/pull/228) and stale updater PR [#225](https://github.com/vorlocsev/dvinholm/pull/225) remain unmerged; do not merge either without explicit approval. Stable v0.2.0 remains unauthorized.

---

## Current main checkpoint — PR #271 merged; post-merge CI passed (2026-10-17)

- [x] PR [#271](https://github.com/vorlocsev/dvinholm/pull/271) was squash-merged as `bffd39ded30af567089dacab1cbdf95f1347912a`; it records the updater forward-port and its CI results in this roadmap and the AI work log.
- [x] Post-merge CI [run #38990733864](https://github.com/vorlocsev/dvinholm/actions/runs/38990733864) passed on this exact `main` commit: build, tests, publish pipeline, and audit of the published v0.1.1 ZIP all succeeded.
- [x] Current `main` includes the updater hardening for malformed run metadata, validated GitHub release asset URLs/digests, binary release-asset download, and user-initiated update discovery with SHA-based update-state handling.
- [ ] Manual Windows runtime confirmation of updater startup/button behavior remains outstanding; CI does not substitute for this check.
- PR [#228](https://github.com/vorlocsev/dvinholm/pull/228) remains open and unmerged. Do not merge it without separate explicit approval.
- Physical J2534 adapter/vehicle testing remains deferred. Stable v0.2.0 remains unpublished and requires explicit user authorization.

---

## Updater-state forward-port merged — post-merge CI passed (2026-10-17)

- [x] PR [#270](https://github.com/vorlocsev/dvinholm/pull/270) was squash-merged into `main` as `b2a4661aee05251472ee93f78e1b0ea815dcd33f`.
- [x] Post-merge CI [run #38989890059](https://github.com/vorlocsev/dvinholm/actions/runs/38989890059) passed on the exact merge commit. The build/test/publish pipeline and audit of the exact published v0.1.1 ZIP both succeeded.
- [x] The forward-port preserves SHA-based update comparison already on main, makes update discovery user-initiated instead of automatic at startup, normalizes the button to **Check update**, adds same-SHA/different-SHA regression coverage, and skips CI for worklog-only pushes.
- [ ] User should manually confirm the updater startup/button behavior in the Windows app when convenient; CI is not a substitute for runtime UI verification.
- PR [#228](https://github.com/vorlocsev/dvinholm/pull/228) remains open and unmerged. Do not merge it without separate explicit approval.
- Physical J2534 adapter/vehicle testing remains deferred. Stable v0.2.0 remains unpublished and requires explicit user authorization.

---

## Latest CI checkpoint — main CI #2296 retry passed (2026-10-17)

- Main commit `024addce1dbe68a6e573f7e67748e1946f0d4128` has a successful second attempt of [CI #2296](https://github.com/vorlocsev/dvinholm/actions/runs/38984726872): build/tests and v0.1.1 release archive audit passed.
- The first attempt had 475/476 tests passing; `LatestBuildUpdaterFailureTests.ReplacementScript_RestoresPreviousBuildWhenReplacementFailsAtRuntime` failed while deleting its temporary directory because Windows reported it was still in use. The rerun passed, so this is currently non-reproducible and should not be described as a confirmed updater defect or a proven fix.
- Continue investigating only if the cleanup failure recurs; do not weaken rollback assertions or hide cleanup failures. Hardware validation remains deferred; v0.2.0 requires explicit user authorization.

# Dvinholm development roadmap

Updated: 2026-10-17  
Current main checkpoint: `8eb69feabc652984c4deaa6236efff249a70bd47`  
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


## Manual UI verification — safe profile deletion (2026-10-16)

- [x] User confirmed deleting disposable `TEST-ECU` removes it from the list.
- [x] Original useful profile remains intact and available.
- [x] After app restart, deleted `TEST-ECU` does not reappear.
- This verifies local profile deletion/persistence only; physical J2534 adapter and vehicle testing remains deferred.
- [ ] Finish final software-only profile workflow check, then review release-gate items and current PR/CI state before deciding on release 0.2.0.


## Manual UI verification — complete profile workflow (2026-10-16)

- [x] Opened the original profile, changed its name temporarily, saved, exported JSON, restarted the app and confirmed the edited name persisted.
- [x] Restored the original profile name and saved successfully.
- This confirms the local edit/save/export/restart workflow only; physical J2534 adapter and vehicle testing remains deferred.
- [ ] Review remaining software-only release gates plus current PR #255 and CI state before deciding whether Dvinholm 0.2.0 is ready.


## Manual UI verification — malformed profile JSON rejected safely (2026-10-16)

- [x] User confirmed importing a deliberately malformed JSON copy (missing closing brace) reports an error without hanging the app.
- [x] Existing original profile remains available and unchanged.
- This verifies local malformed-input handling only; physical J2534 adapter and vehicle testing remains deferred.
- [ ] Continue software-only release gates and confirm PR #255 CI/mergeability before recommending release 0.2.0.


## Manual UI verification — valid JSON missing profile name (2026-10-16)

- [x] Confirmed safe handling when importing a syntactically valid profile JSON copy with the profile-name field omitted.
- Exact UI behavior (rejection versus safe fallback) was not specified; record only the successful test outcome, without assuming implementation details.
- This is local profile-import validation only. No physical J2534 adapter or vehicle was used; hardware validation remains deferred.
- [ ] Next: test an invalid field value/type in a disposable, syntactically valid profile JSON copy and confirm safe handling without changing the original profile.


## Manual UI verification — profile name has wrong JSON type (2026-10-16)

- [x] Confirmed successful handling of a syntactically valid profile JSON copy whose profile-name value was changed from a string to a number.
- The test used a copy and the original saved profile was preserved. Exact UI behavior was not specified, so no particular error message or fallback is claimed.
- This covers local profile-import handling only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: test an unknown/extra JSON property in a disposable profile copy and confirm safe handling without altering the original profile.


## Manual UI verification — extra unknown JSON property (2026-10-16)

- [x] Confirmed successful handling of a valid profile JSON copy containing an extra property (`test_extra_field`); the original profile remained preserved.
- This confirms safe import handling for an unknown/extra property, but does not claim whether that property is persisted or discarded.
- Local profile-import check only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: import the same valid profile JSON twice and check for unintended duplicate profiles or corruption of the saved profile list.


## Manual UI verification — repeated import of same profile JSON (2026-10-16)

- [x] Confirmed that importing the same profile JSON twice and restarting Dvinholm left the saved profile list correct, with no observed unintended duplicates or corruption.
- This verifies the repeated-import/persistence workflow for the tested profile only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: cancel the profile-import file picker and verify that the profile list remains unchanged and no false import-success message appears.


## Manual UI verification — cancel profile import (2026-10-16)

- [x] Confirmed cancelling the profile-import file picker leaves the profile list unchanged and does not report a false successful import.
- Local profile-import UI check only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: if export/save offers a file dialog, cancel it and verify no file is falsely reported as saved and the existing profile remains intact.


## Manual UI verification — cancel profile export (2026-10-16)

- [x] Confirmed cancelling the profile export/save dialog does not show a false successful-save result and leaves the original profile unchanged.
- Local profile-export UI check only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: export to a new temporary filename, verify the file exists and is valid JSON, then exercise the export/import round trip without overwriting the original profile.


## Manual UI verification — profile export/import round trip (2026-10-16)

- [x] Confirmed export to a new temporary filename produced a file containing valid JSON, and importing it without overwriting the original profile succeeded.
- This validates the tested local profile export/import round trip only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: repeat required-field validation with a disposable JSON copy missing a required profile field, confirming the original remains intact and the app stays responsive.


## Manual UI verification — required profile field missing (2026-10-16)

- [x] User confirmed successful handling of a syntactically valid profile JSON copy with another required field omitted.
- Exact field and UI behavior were not specified; no specific error message or fallback is claimed. The original profile was kept separate.
- Local profile-import validation only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: inspect the repository's profile JSON schema and validation rules before choosing further field-specific tests; do not guess required fields.


## Manual UI verification — required profile field missing (2026-10-16)

- [x] User confirmed successful handling of a syntactically valid profile JSON copy with another required profile field omitted.
- The exact field and UI behavior were not specified; no particular error message or fallback is claimed. The original profile was kept separate.
- Local profile-import validation only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: inspect the profile JSON schema/validation rules before further field-specific tests; then check empty-catalog import behavior using a backed-up catalog.


## Manual UI verification — import empty profile catalog (2026-10-16)

- [x] User confirmed the empty JSON array (`[]`) import test succeeded after backing up the existing profile catalog.
- This records the empty-catalog import path only; do not assume the original profiles have been restored or remained in the active list. Restore from the backup and verify integrity before further destructive-state tests.
- Local profile UI check only; no physical J2534 adapter or vehicle was used. Hardware validation remains deferred.
- [ ] Next: confirm the empty catalog displays correctly, restore the saved catalog backup, and verify the original profiles return intact.


## Manual UI verification — restore catalog after empty import (2026-10-16)

- [x] Confirmed that restoring the saved JSON catalog after importing `[]` returned the original profiles, preserved names and main fields, and persisted after app restart.
- This closes the empty-catalog recovery workflow. No physical J2534 adapter or vehicle was used; hardware validation remains deferred.
- [ ] Next: test a catalog containing duplicate profile IDs in a disposable JSON copy; verify rejection is safe and the current catalog remains intact.


## Manual UI verification — duplicate profile IDs (2026-10-16)

- [x] Confirmed that importing a catalog with duplicate profile IDs is rejected/handled as an error without hanging or damaging the existing profiles.
- No ECU communication or physical J2534 adapter/vehicle was used; hardware validation remains deferred.
- [ ] Next: verify invalid CAN identifier validation using a disposable profile/catalog; confirm invalid values are not accepted and existing profiles remain intact.


## Manual UI verification — invalid CAN identifiers (2026-10-16)

- [x] Confirmed that out-of-range CAN IDs for the selected identifier format are rejected/not accepted on save and existing profiles remain intact.
- This was a profile validation check only; no ECU communication or physical adapter/vehicle was used. Hardware validation remains deferred.
- [ ] Next: verify request and response CAN IDs cannot be identical in a disposable profile; confirm existing profiles remain intact.


## Manual UI verification — identical request/response CAN IDs (2026-10-16)

- [x] Confirmed that a profile with identical request and response CAN IDs is rejected/not accepted on save and other profiles remain intact.
- This was a profile validation check only; no ECU communication or physical adapter/vehicle was used. Hardware validation remains deferred.
- [ ] Next: verify invalid/malformed diagnostic identifier (DID) validation using a disposable profile, without modifying the existing catalog.


## Manual UI verification — malformed DID input (2026-10-16)

- [x] User confirmed malformed DID input was handled safely without hanging or damaging existing profiles.
- No ECU communication or physical J2534 adapter/vehicle was used; hardware validation remains deferred.
- [ ] Next: test duplicate DID handling in a disposable profile/catalog if the UI supports DID editing. If not, record the UI limitation and proceed to another software-only check.


## Manual UI verification — duplicate DID entries (2026-10-16)

- [x] User confirmed duplicate DID entries were handled safely without an app hang or damage to existing profiles.
- No ECU communication or physical J2534 adapter/vehicle was used; hardware validation remains deferred.
- [ ] Next: export the restored original catalog to a new JSON filename and verify valid JSON, expected profile names, and key fields without overwriting the only backup.


## Manual UI verification — export catalog after validation tests (2026-10-16)

- [x] Confirmed export to a new JSON file succeeds; JSON is valid and contains expected original profile names/key fields with no unintended temporary test profiles.
- The original backup was preserved; no ECU communication or physical adapter/vehicle was used. Hardware validation remains deferred.
- [ ] Next: import the newly exported JSON through the safe test workflow and compare profile count, names, and key fields with the exported source, keeping the original backup intact.


## Manual UI verification — catalog export/import round trip (2026-10-16)

- [x] Confirmed re-import of the exported catalog preserves profile count, names, and key fields; no unintended duplicates or missing settings; data persists after restart.
- Original backup kept separately. No ECU communication or physical adapter/vehicle was used; hardware validation remains deferred.
- [ ] Next: launch the current Windows candidate, confirm the main window loads without an unhandled error, and close normally. This is not release approval; exact-head CI and all remaining release gates must still be checked.


## Manual Windows verification — candidate startup and normal exit (2026-10-16)

- [x] User confirmed the current Windows candidate starts with the main window fully loaded, without an unhandled error/crash, and closes normally.
- Startup/shutdown smoke check only; no ECU communication or physical adapter/vehicle was used. Hardware validation remains deferred.
- [ ] Next: inspect the current PR head and CI/check status to identify remaining release gates. Do not merge or publish a release without explicit user authorization.


## Manual Windows verification — startup without physical J2534 adapter (2026-10-16)

- [x] Confirmed current candidate reports the missing physical J2534 device, does not hang, keeps diagnostics unavailable while disconnected, and closes/relaunches normally.
- This is a no-device software behavior check only. Physical adapter/vehicle verification remains deferred to a future release.
- [ ] Next: inspect the latest CI result for the current PR head before evaluating release readiness.


## UDS UI state and cancellation audit — 2026-10-16

- [x] Source-audited UDS UI gating: diagnostic commands require an active connection and valid selected CAN-ID format/IDs; UDS requests and discovery are serialized; controls are disabled during requests and restored based on connection state.
- [x] Reviewed close/disposal ordering and existing tests; current coverage already includes active/queued UDS cancellation, ISO-TP disposal/cancellation, dispatcher/session disposal races, CAN ID parsing, and virtual-adapter read-only UDS flows. No redundant test added.
- [ ] Manually verify after a failed/no-device connection that Start Monitor and Read VIN/DTC/DID remain disabled and Connect is re-enabled for retry.
- [ ] Review any concrete uncovered close/disconnect race before adding a new deterministic virtual-adapter test. Physical J2534 adapter and vehicle testing remain deferred.


## Manual Windows UI verification — failed J2534 path (2026-10-16)

- [x] A nonexistent J2534 DLL path produces a handled connection error.
- [x] Connect is available for retry after the failure.
- [x] Start Monitor, Read VIN, Read DTC, and Read DID remain disabled while disconnected.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — valid J2534 DLL path, no device (2026-10-16)

- [x] A valid/detected J2534 DLL path with no physical adapter produces a handled missing-device result without hanging.
- [x] Connect is available again after the attempt.
- [x] Start Monitor, Read VIN, Read DTC, and Read DID remain disabled while disconnected.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical adapter and vehicle checks remain deferred to a future release.


## Manual Windows UI verification — repeated no-adapter attempts (2026-10-16)

- [x] Three consecutive Connect attempts with no physical J2534 adapter complete without a hang or crash.
- [x] The app remains responsive and Monitor/UDS actions remain disabled while disconnected.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — disconnected state after restart (2026-10-16)

- [x] App closes and relaunches normally after failed no-adapter connection attempts.
- [x] Relaunch does not incorrectly report an active connection; Monitor and UDS actions remain disabled without an adapter.
- [x] App closes normally after the check.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — ECU profile switching (2026-10-16)

- [x] Switching between saved ECU profiles shows the corresponding CAN request/response IDs.
- [x] Returning to the primary profile restores its original values; no profile edits were made.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — ECU profile persistence (2026-10-16)

- [x] A non-primary saved ECU profile retains its name and CAN request/response IDs after app restart.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — cancel ECU profile selection (2026-10-16)

- [x] Cancelling/closing profile selection leaves the current ECU profile and CAN IDs unchanged.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — CAN ID validation after profile switch (2026-10-16)

- [x] An out-of-range CAN ID is rejected when saving a non-primary ECU profile.
- [x] The profile's original settings remain intact after rejection.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — recovery after invalid CAN ID (2026-10-16)

- [x] After rejecting an out-of-range CAN ID, the original profile values remain intact and are displayed again after switching away and back.
- [x] No false persistent error or app malfunction observed.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — repeated ECU profile switching (2026-10-16)

- [x] Five switches between two saved ECU profiles do not mix their names or CAN request/response IDs.
- [x] Both profiles retain their respective values after restarting the application.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — startup profile consistency (2026-10-16)

- [x] After restart, the selected ECU profile's displayed CAN IDs match that profile.
- [x] Switching between the primary and second profiles displays the corresponding CAN IDs correctly.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — cancel unsaved ECU profile edit (2026-10-16)

- [x] Cancelling/closing an ECU profile editor after changing a CAN ID without saving preserves the original persisted value.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — ECU profile rename persistence (2026-10-16)

- [x] A temporary profile-name change saves and persists after switching away and back.
- [x] CAN IDs remain unchanged, and the original profile name was restored and saved.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — export profile after name restoration (2026-10-16)

- [x] Export to a separate temporary JSON file is valid and contains the expected original profile name and CAN IDs.
- [x] Temporary `-TEST` suffix is absent; backup and source profile were not overwritten.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — import exported profile safely (2026-10-16)

- [x] Import of the exported ECU profile JSON was handled correctly.
- [x] Imported profile details were checked and the original profile was not overwritten.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — repeat JSON import (2026-10-16)

- [x] Re-import the same temporary ECU-profile JSON; original profile and CAN IDs remain unchanged, with no hang or false success.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — import name collision (2026-10-16)

- [x] Import JSON using an existing profile name and cancel overwrite; original profile and CAN IDs remain unchanged, and the app stays responsive.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — dismiss overwrite dialog (2026-10-16)

- [x] Dismiss the profile overwrite dialog; app stays responsive and the existing profile/CAN IDs remain unchanged after restart.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — cancel import file picker (2026-10-16)

- [x] Cancel the import file picker; profile list and selection stay unchanged, with no false success notification.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — cancel profile export (2026-10-16)

- [x] Cancel the JSON export/save dialog; no false success message, source profile unchanged, app remains responsive.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — export non-primary ECU profile (2026-10-16)

- [x] Export a non-primary ECU profile and verify the JSON contains its own name and CAN IDs; source and backup files remain untouched.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — profile switching after export and restart (2026-10-16)

- [x] Confirm primary and non-primary profiles retain their own CAN IDs across switching and app restart.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — import malformed CAN ID (2026-10-16)

- [x] Import a copied profile JSON with a malformed CAN ID; app remains responsive and existing profiles/CAN IDs are preserved.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — import empty profile name (2026-10-16)

- [x] Import copied profile JSON with an empty name; app remains responsive and existing profiles stay unchanged.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — import JSON missing profile name field (2026-10-16)

- [x] Import copied profile JSON without a profile-name field; app remains responsive and existing profiles remain unchanged.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — restart after import edge-case series (2026-10-16)

- [x] Restart after import edge-case checks; original profiles, names, and CAN IDs remain correct, and the app closes normally.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — rename profile, restart, restore name (2026-10-16)

- [x] Verify profile rename persists across restart without changing CAN IDs, then restore and save the original name.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — CAN IDs after repeated profile switching (2026-10-16)

- [x] Verify primary profile CAN IDs remain correct after repeated switching to another saved profile and back.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — discard unsaved CAN ID edit (2026-10-16)

- [x] Verify an unsaved CAN ID edit is discarded on profile switch and the persisted value is restored when returning.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — save and restore CAN ID (2026-10-16)

- [x] Verify a valid CAN ID change persists after profile switching and the original value can be restored and saved.
- [ ] Continue remaining pre-release checks using the virtual/no-adapter setup.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.


## Manual Windows UI verification — restart after CAN ID save/restore (2026-10-16)

- [x] Restart after CAN ID save/restore; primary profile name and CAN IDs are correct, and switching profiles displays each profile's own values.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — export profile and compare after restart (2026-10-16)

- [x] Export the primary profile, restart the app, and verify the profile name and CAN IDs match the exported JSON.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — CAN Monitor unavailable without adapter (2026-10-16)

- [x] Confirmed CAN Monitor remains unavailable without a physical J2534 adapter and no false active-monitoring status is shown.
- [x] Confirmed the app closes and relaunches normally after this check.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — UDS controls across ECU profiles while disconnected (2026-10-16)

- [x] Read VIN, Read DTC, and Read DID stay unavailable when switching between saved ECU profiles without an adapter.
- [x] No false connected state appears; returning to the primary profile and closing the app work normally.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — repeated CAN Monitor attempts while disconnected (2026-10-16)

- [x] Repeated attempts to start CAN Monitor without an adapter do not hang or crash the app and do not show false active monitoring.
- [x] App closes and relaunches normally after the check.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — disconnected state after restart (2026-10-16)

- [x] After restart without a physical J2534 adapter, no active connection is reported.
- [x] Start Monitor, Read VIN, Read DTC, and Read DID remain unavailable while disconnected.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — repeated ECU profile switching while disconnected (2026-10-16)

- [x] Repeated profile switching displays the correct CAN IDs for each saved ECU profile.
- [x] Monitor and UDS actions remain unavailable without an adapter; UI stays responsive and closes normally.
- [ ] Finish remaining pre-release checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — failed connection on non-primary ECU profile (2026-10-16)

- [x] Failed connection without an adapter is handled without hanging; selected secondary profile and its CAN IDs are preserved.
- [x] Monitor and UDS actions remain unavailable while disconnected.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — repeated Connect retries on secondary ECU profile (2026-10-16)

- [x] Three connection attempts without an adapter are handled without hanging or crashing.
- [x] The secondary ECU profile and CAN IDs remain unchanged; Monitor and UDS actions remain unavailable while disconnected.
- [ ] Finish remaining pre-release UI checks.
- [ ] Verify CI status for the current branch head and review release readiness.
- Physical J2534 adapter and vehicle verification remain deferred to a future release.
- Do not merge the PR or publish release 0.2.0 without explicit user authorization.


## Manual Windows UI verification — secondary profile survives failed connection (2026-10-16)

- [x] Confirmed secondary ECU profile and CAN IDs persist after a failed connection attempt without a physical J2534 adapter and an application restart.
- [x] Confirmed no false connected state; Start Monitor, Read VIN, Read DTC, and Read DID stay unavailable while disconnected.
- [ ] Continue disconnected-state UI checks when switching ECU profiles.
- [ ] Verify CI for the current branch head and review release readiness before considering release 0.2.0.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — disconnected profile switching (2026-10-16)

- [x] Confirmed switching between primary and secondary ECU profiles while disconnected preserves each profile's CAN IDs and keeps Start Monitor, Read VIN, Read DTC, and Read DID unavailable.
- [x] Confirmed the application remains responsive and does not show a false active connection.
- [ ] Verify canceling/closing the profile selector leaves the current profile and disconnected control state unchanged.
- [ ] Verify CI for the current branch head and review release readiness before considering release 0.2.0.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — cancel profile selection (2026-10-16)

- [x] Confirmed canceling/closing the profile selector without choosing another profile preserves the selected ECU profile and CAN IDs.
- [x] Confirmed disconnected state remains accurate and Monitor/UDS controls stay unavailable.
- [ ] Verify canceling/discarding an unsaved CAN ID edit restores the persisted value.
- [ ] Verify CI for the current branch head and review release readiness before considering release 0.2.0.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — discard unsaved CAN ID edit (2026-10-16)

- [x] Confirmed discarding an unsaved CAN ID edit restores the persisted value; other settings remain intact and Monitor/UDS controls stay unavailable while disconnected.
- [ ] Verify a valid CAN ID edit persists across profile switching and can be restored to its original value.
- [ ] Verify CI for the current branch head and review release readiness before considering release 0.2.0.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — save and restore CAN ID (2026-10-16)

- [x] Confirmed a valid CAN ID edit persists across profile switching; the primary profile remains unchanged.
- [x] Confirmed restoring the original CAN ID and restarting the application preserves the restored value.
- [ ] Verify CI and required checks for the current branch head / PR #255.
- [ ] Review remaining release 0.2.0 gates after checking current CI results.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — export restored ECU profile (2026-10-16)

- [x] Confirmed exported secondary-profile JSON contains the expected profile name and restored request/response CAN IDs.
- [x] Confirmed no temporary test name/test CAN ID appears and backup files remain untouched.
- [ ] Import the exported JSON as a separate profile and verify its name and CAN IDs without overwriting the source profile.
- [ ] Verify CI for the current branch head and review remaining release 0.2.0 gates.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — import export as separate profile (2026-10-16)

- [x] Confirmed exported JSON imports as a separate profile without overwriting the source profile.
- [x] Confirmed imported profile name/CAN IDs match the JSON and source/imported settings remain independent after switching.
- [ ] Export the full profile catalog and verify expected profile count, names, and CAN IDs; keep the backup untouched.
- [ ] Verify CI for the current PR #255 head and review remaining release 0.2.0 gates.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — full profile catalog export (2026-10-16)

- [x] Confirmed the full catalog JSON has the expected profile count, names, and CAN IDs for primary, secondary, and imported profiles.
- [x] Confirmed no unexpected test values and no backup overwrite.
- [ ] Verify full-catalog import round trip without unintended duplicates or missing/changed profiles; preserve a separate backup.
- [ ] Verify CI for the current PR #255 head and review remaining release 0.2.0 gates.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — profile catalog survives restart (2026-10-16)

- [x] Confirmed after normal close/relaunch that the profile count, names, and CAN IDs match the exported catalog.
- [x] Confirmed no profiles were missing or duplicated and no false active connection was shown without an adapter.
- [ ] Verify CI and required checks for the latest PR #255 head, then review remaining software-only release 0.2.0 gates.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — repeat full-catalog import (2026-10-16)

- [x] Confirmed re-importing the exported profile catalog did not create unexpected duplicates or alter existing profile settings unexpectedly.
- [x] Confirmed profile names/CAN IDs remain correct and the separate backup is preserved.
- [ ] Final disconnected-state smoke test after catalog import: restart, confirm no active connection, and verify Monitor/UDS controls remain disabled.
- [ ] Verify terminal CI result for latest PR #255 head and review software-only release 0.2.0 gates.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## Manual Windows UI verification — post-import disconnected-state smoke test (2026-10-16)

- [x] After catalog import, restarted the app and confirmed profiles and CAN IDs remain correct.
- [x] Without a physical J2534 adapter, confirmed no false active connection and Monitor / VIN / DTC / DID actions remain unavailable.
- [x] Confirmed the app stays responsive and closes normally.
- [ ] Verify CI for the latest PR #255 head and review remaining software-only release 0.2.0 gates.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## CI verification — PR #255 (run 2214, 2026-10-16)

- [x] CI run 2214 passed for commit `220dee77d3f59ba7b4dd3075c2f9b9ec237bd09b`: restore, build, tests, publish, and artifact upload succeeded.
- [ ] Re-check CI for the current head after these documentation updates.
- [ ] Inspect PR #255 review requirements and remaining software-only release 0.2.0 gates before making any release-readiness decision.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## CI verification — PR #255 latest documentation head (run 2218, 2026-10-16)

- [x] CI run 2218 passed for head `6ec1af6551842a21a338ceab0c5e7bbbf8ac665c`: restore, build, automated tests, publish, and CI artifact upload succeeded.
- [x] Confirmed PR #255 is documentation-only (`AI_WORKLOG.md`, `ROADMAP.md`) and remains open/unmerged.
- [ ] Verify UDS/Monitor controls transition to enabled only after a valid active J2534/CAN session; this requires a suitable adapter/session and is not validated by the disconnected-state smoke test.
- [ ] Complete the release-candidate checklist: review release notes and verify the exact release archive separately from CI artifacts.
- [ ] Re-check CI on the eventual merge commit before any release.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish a release without explicit user authorization.


## CI verification — PR #255 documentation head (run 2222, 2026-10-16)

- [x] CI run 2222 passed for head `8928a4dbef800ac19d32d6ec6b54cd8d439fa73a`: restore, build, automated tests, publish, and CI artifact upload succeeded.
- [ ] Re-check CI on the new documentation head after recording this result.
- [ ] Review release notes and verify the exact release archive separately from CI artifacts.
- [ ] Verify Monitor/UDS controls become enabled only after a valid active J2534/CAN session; this is not verifiable in the current no-adapter setup.
- [ ] Re-check CI on the eventual merge commit before any release.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish release 0.2.0 without explicit user authorization.


## CI verification — PR #255 updated documentation head (run 2225, 2026-10-16)

- [x] CI run 2225 passed for head `be269fd3849588ce7fe777b9b997c2a9dd1eee88`.
- [ ] Re-check CI on the new documentation head after recording this result.
- [ ] Review release notes and verify the exact release archive separately from CI artifacts.
- [ ] Verify Monitor/UDS controls become enabled only after a valid active J2534/CAN session; this remains unverified without a suitable active session.
- [ ] Re-check CI on the eventual merge commit before any release.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish release 0.2.0 without explicit user authorization.


## CI verification — PR #255 documentation head (2026-10-16)

- [x] CI run #2227 (push) passed for `f0239cb37d0c6b12eeeb9dbb3aa1cd372d51f8fe`.
- [x] CI run #2228 (pull request) passed for the same head, including build, automated tests, publish and CI asset steps.
- [ ] Re-check CI on the new documentation head after recording this result.
- [ ] Review release notes and verify the exact release archive separately from CI artifacts.
- [ ] Verify Monitor/UDS controls become enabled only after a valid active J2534/CAN session; remains unverified without a suitable active session.
- [ ] Re-check CI on the eventual merge commit before any release.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish release 0.2.0 without explicit user authorization.


## CI verification — PR #255 (2026-10-16)

- [x] PR CI run #2232 passed for `577c85223f11cc692ae1da8ea8c09b10929dbd83`.
- [ ] Inspect CI workflow behavior for automatically published updater prereleases on documentation-only pushes.
- [ ] Verify the exact stable release archive separately from CI artifacts.
- [ ] Validate Monitor/UDS controls with an active J2534/CAN session when suitable hardware is available.
- [ ] Re-check CI on the eventual merge commit before any release.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not merge PR #255 or publish release 0.2.0 without explicit user authorization.


## CI updater-prerelease publication guard (2026-10-16)

- [x] PR #256 workflow change passed CI run #2239.
- [x] Confirmed workflow still builds/tests PRs and branch pushes, while public updater ZIP/prerelease steps are restricted to pushes to `main`.
- [ ] Review and merge PR #256; the fix is not active on `main` before merge.
- [ ] After merge, confirm main CI and ensure no public `ci-*` prerelease is created for documentation/feature-branch pushes.
- [ ] Verify stable release archive and release notes before considering release `0.2.0`.
- Physical J2534 adapter and vehicle verification remain deferred. Do not merge PRs or publish release `0.2.0` without explicit user authorization.


### Rollback test CI follow-up (2026-10-16)
- [x] Diagnose CI #2243: one rollback runtime test failed while cleaning up a temp directory after PowerShell used a synthetic PID without a `Wait-Process` mock.
- [x] Open PR #257 to mock `Wait-Process` in the isolated test.
- [x] CI #2246 for PR #257 passed, including all tests and publish/artifact steps.
- [ ] Confirm CI on docs PR #255 after the latest work-log/roadmap update.
- [ ] Keep PRs unmerged until explicitly authorized; verify main-branch CI and stable release archive before v0.2.0. Physical adapter/vehicle testing remains deferred.


## Post-merge CI checkpoint — 2026-10-16

- [x] PR #256 merged; main CI #2251 passed, including the main-only updater prerelease packaging/publication path.
- [x] PR #257 merged; main CI #2252 passed on retry (attempt 2). Initial attempt had 475/476 tests pass and failed during temporary-directory cleanup in the rollback runtime test; monitor for recurrence rather than treating the first run as green.
- [x] PR #255 merged; main CI #2253 passed on commit `a47679bdb049fe6814751c319707e4119fa56c7c`.
- [x] Current stable release remains `v0.1.1`; `0.2.0` is not published.
- [ ] Review release notes and inspect the exact stable release archive separately from CI artifacts.
- [ ] Validate Monitor/UDS controls becoming enabled only after a valid active J2534/CAN session; not verifiable with the current no-adapter setup.
- Physical J2534 adapter and vehicle validation remain deferred to a future release. Do not publish `0.2.0` until release gates are reviewed and the user authorizes publication.


## Post-PR #258 merge checkpoint — 2026-10-16

- [x] PR #258 merged; `AI_WORKLOG.md` and `ROADMAP.md` now record the CI results for PRs #255–#257.
- [x] Main CI #2258 passed on merge commit `5d70fe220fc73852a417c45bce730a1181b9dfbe` ([run #2258](https://github.com/vorlocsev/dvinholm/actions/runs/38977361452)).
- [x] Reviewed v0.1.1 release notes and asset metadata: `Dvinholm-Windows-x86.zip`, 66,766,108 bytes; published SHA-256 `3822d13e5a137bd92bf12d35d2a5794053481ef28873d2a08657beb756f3a53a`.
- [ ] Exact stable archive is not independently inspected yet; direct download attempt was blocked by the available access path. Metadata alone is not a binary integrity verification.
- [ ] Confirm Monitor/UDS controls become enabled only after a valid active J2534/CAN session; current no-adapter setup cannot validate this state transition.
- Stable `0.2.0` is not published; do not publish without explicit user authorization. Physical J2534 adapter and vehicle checks remain deferred to a future release.


## Post-PR #259 main CI and UI gating review — 2026-10-17

- [x] PR #259 merged; main CI #2263 passed on merge commit `1935d56751453a5feeaa599f63302f6523717918` ([run #2263](https://github.com/vorlocsev/dvinholm/actions/runs/38978437672)).
- [x] Source review confirms UDS controls are initially disabled, enabled after a successful connection, and guarded by connection/CAN-ID validation when commands execute. Disconnect disables Monitor/UDS controls before session teardown and resets the state if disconnect fails.
- [ ] Confirm the UI state transition against a valid active J2534/CAN session when suitable hardware is available; this remains unverified in the current no-adapter setup.
- [ ] Independently download and inspect the exact v0.1.1 release archive; published metadata and SHA-256 are recorded but the archive bytes have not been inspected independently.
- Physical J2534 adapter and vehicle verification remain deferred to a future release. Do not publish stable `0.2.0` without explicit user authorization.


## Post-PR #260 main CI verification — 2026-10-17

- [x] PR [#260](https://github.com/vorlocsev/dvinholm/pull/260) merged as `23e12e0f188178d469112df44fc1138e26100cab`.
- [x] Main CI [#2268](https://github.com/vorlocsev/dvinholm/actions/runs/38979739897) completed successfully on that exact merge commit; the build job passed with no failed steps.
- [x] Current main checkpoint updated to the exact merge SHA.
- [ ] Independently inspect the exact v0.1.1 release archive. GitHub release metadata lists the published SHA-256, but the archive bytes have not yet been independently downloaded/inspected; metadata alone is not a binary verification.
- [ ] Active-session UI transition for Monitor/UDS remains unverified because no physical J2534 adapter is available. Disconnected-state gating and source guards have been reviewed; no live CAN/UDS session was tested.
- Stable v0.2.0 remains unpublished and requires explicit user authorization. Physical J2534 adapter and vehicle checks remain deferred to a future hardware-validation cycle.


## Post-PR #261 main CI verification — 2026-10-17

- [x] PR [#261](https://github.com/vorlocsev/dvinholm/pull/261) merged as `6620ad2bf9b4e3daa3b6e27b2347181e9f80b6c2`.
- [x] Main CI [#2273](https://github.com/vorlocsev/dvinholm/actions/runs/38980902038) completed successfully on that exact commit; build, test, publish, and artifact packaging passed.
- [x] Updated the roadmap header to the exact current `main` SHA.
- [x] Exact published v0.1.1 release ZIP independently audited by GitHub Actions; see the audit checkpoint below.
- [ ] Active-session Monitor/UDS enablement still cannot be manually verified without a J2534 adapter. Physical adapter/vehicle validation remains deferred.
- Stable v0.2.0 remains unpublished and requires explicit user authorization.


## Published v0.1.1 release ZIP audit — 2026-10-17

- [x] PR [#263](https://github.com/vorlocsev/dvinholm/pull/263) added a read-only GitHub Actions audit for the exact published asset [Dvinholm-Windows-x86.zip](https://github.com/vorlocsev/dvinholm/releases/download/v0.1.1/Dvinholm-Windows-x86.zip).
- [x] Audit run [#2282](https://github.com/vorlocsev/dvinholm/actions/runs/38982113636) passed: GitHub Actions downloaded the published ZIP, verified SHA-256 `3822d13e5a137bd92bf12d35d2a5794053481ef28873d2a08657beb756f3a53a`, validated ZIP integrity, and confirmed a Dvinholm executable is present. The archive file inventory is published in the Actions job summary.
- [x] PR #263 merged as `d113d6536cdd6d2c76bd5ecfbd4b1a858a955922`. The audit checks the actual release asset, not a newly generated CI artifact.
- [x] Post-merge main CI [#2286](https://github.com/vorlocsev/dvinholm/actions/runs/38983168717) passed on `d113d6536cdd6d2c76bd5ecfbd4b1a858a955922`, including the published ZIP audit and build/test/publish pipeline.
- [ ] Post-merge main CI for documentation checkpoint `1749b56d826523bf382f2d287e8f1a30a791286f` is still running: [#2291](https://github.com/vorlocsev/dvinholm/actions/runs/38983741022).
- [ ] Active-session Monitor/UDS control transition remains unverified because no physical J2534 adapter is available.
- Physical adapter/vehicle validation remains deferred. Stable v0.2.0 remains unpublished and must not be published without explicit user authorization.


## Window shutdown lifecycle fix — PR #267 (2026-10-17)

- [x] PR [#267](https://github.com/vorlocsev/dvinholm/pull/267) merged as `78c555df959129a7a743ac8c34156703ca584bce`.
- [x] Final PR-head CI [run #38986409454](https://github.com/vorlocsev/dvinholm/actions/runs/38986409454) passed: build/tests and the published v0.1.1 ZIP audit succeeded.
- [x] Post-merge main CI [run #38986840493](https://github.com/vorlocsev/dvinholm/actions/runs/38986840493) passed on the exact merge commit; build/tests and published release ZIP audit both succeeded.
- [x] Shutdown cleanup no longer synchronously waits on tracked operations or the UDS gate on the UI thread. Monitor stopping requests cancellation without synchronously waiting; connect completion is tracked as the actual connect task, and the continuation avoids session access after window closing.
- [ ] A dedicated WinUI window-close regression test/manual close stress test has not been run. CI confirms compilation and existing test suite only; do not claim direct runtime verification of the window-close behavior.
- Physical J2534 adapter and vehicle checks remain deferred. Stable v0.2.0 remains unpublished and requires explicit user authorization.


## Post-merge CI checkpoint — PR #268 (2026-10-17)

- [x] PR [#268](https://github.com/vorlocsev/dvinholm/pull/268) merged by squash as `08015d0f6f8808422fe3dc01c29c05c540445fe6` after its PR-head CI [#38987327051](https://github.com/vorlocsev/dvinholm/actions/runs/38987327051) passed build/tests and the exact published v0.1.1 ZIP audit.
- [x] Post-merge main CI [#38987746187](https://github.com/vorlocsev/dvinholm/actions/runs/38987746187) passed on the exact merge commit; build/tests and published v0.1.1 ZIP audit succeeded.
- [x] Roadmap and AI worklog now record PR #267's non-blocking shutdown cleanup, its CI verification, and the limitation that no dedicated WinUI close-handler regression test/manual close stress test was run.
- [ ] PR #228 (updater button state) remains open and must not be merged without explicit user approval. It is now behind current main and GitHub reports merge conflicts; rebase/conflict resolution and fresh CI would be required before considering it further.
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


## CI release audit follows latest stable release — 2026-10-18

- [x] Replace the stale hard-coded v0.1.1 archive audit with a dynamic lookup of GitHub's latest stable (non-prerelease) release.
- [x] Verify the current release ZIP against its published SHA256SUMS.txt, test ZIP integrity, and confirm the executable exists.
- [ ] Await CI on the workflow change before marking verified or merging.


- [x] Corrected checksum parsing for both plain and binary-mode (*filename) SHA256SUMS formats after the first dynamic-audit run exposed the filename-prefix mismatch.
- [ ] Re-run CI and verify the latest stable release audit succeeds.


## Updater first-install failure cleanup — 2026-10-18

- [x] Added cleanup of a partially created destination if first-time installation fails after replacement starts; existing-build rollback remains unchanged.
- [x] Added regression assertions for the generated PowerShell replacement script.
- [ ] Await current-main PR CI before marking this change verified or merging it.
- No stable release or physical J2534/vehicle test is included.


## Current-main updater verification — 2026-10-18

- [x] PR [#289](https://github.com/vorlocsev/dvinholm/pull/289) merged: release audit resolves the latest stable GitHub release dynamically, verifies the published SHA-256, validates ZIP integrity, and checks that the application executable is present.
- [x] Main CI at the PR #289 merge commit passed build and latest-stable-release audit: [run #39109327293](https://github.com/vorlocsev/dvinholm/actions/runs/39109327293).
- [x] PR [#290](https://github.com/vorlocsev/dvinholm/pull/290) merged as `2ccb296e8e745201188371a55d9967994f73a467`. Its current-head CI passed build/tests and latest-stable-release audit: [run #39109404796](https://github.com/vorlocsev/dvinholm/actions/runs/39109404796).
- [ ] Verify post-merge main CI for commit `2ccb296e8e745201188371a55d9967994f73a467` before treating the merge pipeline as fully green.
- [x] Re-recorded the v0.2.0 manual checks: the updater was exercised, repeated no-adapter connection failures return the Connect control to an active state, and disconnected-only actions remain disabled. This does not validate a physical adapter or vehicle.
- [ ] Continue software-only updater review: validate rollback/cleanup paths and their tests; defer real J2534 adapter and vehicle checks.


## Warning cleanup rebased onto current main — 2026-10-18

- [x] Audited stale PR #226 against current main; all but one of its source cleanups were already present.
- [x] Removed the remaining unused `saveException` variable in the profile-import save failure handler. No exception behavior or user-facing message changed.
- [ ] Fresh CI must pass before merging the rebased cleanup.
- Physical J2534 adapter/vehicle checks remain deferred.


## Current-main warning cleanup verified — 2026-10-18

- [x] PR [#293](https://github.com/vorlocsev/dvinholm/pull/293) merged as `ddec537cbc5e8affe251d0b66d518dfd9fd23f00` after current-head build/tests and latest-stable-release audit passed in [run #39112180900](https://github.com/vorlocsev/dvinholm/actions/runs/39112180900).
- [x] Main pipeline for the merge commit started; release audit passed in [run #39113096094](https://github.com/vorlocsev/dvinholm/actions/runs/39113096094). Build is still running, so post-merge main CI is not yet fully green.
- [x] Removed the final unused exception variable in the profile-import save handler without changing behavior.
- [ ] Continue software-only updater reliability review after the post-merge main build completes.
- Physical J2534 adapter and vehicle testing remain deferred; no new stable release is authorized by this cleanup.


## Updater first-install cleanup runtime regression test — 2026-10-18

- [x] PR [#294](https://github.com/vorlocsev/dvinholm/pull/294) merged; documentation records PR #293 cleanup and CI results.
- [x] PR [#295](https://github.com/vorlocsev/dvinholm/pull/295) merged as `6ebcd4f25c92c6052c52f049fb16511889fe05f9`. Added a Windows runtime regression test that simulates a partial first-install destination and a failing copy operation, then asserts the partial destination is removed and the error is logged.
- [x] PR #295 CI passed: [push run #39113276689](https://github.com/vorlocsev/dvinholm/actions/runs/39113276689) and [PR run #39113283879](https://github.com/vorlocsev/dvinholm/actions/runs/39113283879).
- [x] Main run for PR #293 merge [#39113096094](https://github.com/vorlocsev/dvinholm/actions/runs/39113096094) completed successfully.
- [ ] Post-merge main runs for documentation and regression test are still running; do not treat main CI as fully green until those complete.
- [ ] Continue software-only updater reliability review, including rollback and staging cleanup. No stable release planned from these maintenance changes.
- Physical J2534 adapter and vehicle tests remain deferred.


## Updater rollback-copy failure regression — 2026-10-18

- [x] Extended the Windows runtime rollback test to simulate failure of the backup-to-destination robocopy operation after the updated application exits during startup.
- [x] Assert that rollback failure is logged, the recovery backup and staging directory are preserved, and the helper script is still cleaned up; the destination is explicitly expected to remain on the failed new build when restore copying itself fails.
- [ ] Await PR and post-merge main CI before marking the test verified.
- No production code or stable release changed. Physical J2534 adapter and vehicle testing remain deferred.


## ECU test profiles — manual validation — 2026-10-18

- [x] Merged PR [#345](https://github.com/vorlocsev/dvinholm/pull/345) and verified the post-merge CI build [#39156713010](https://github.com/vorlocsev/dvinholm/actions/runs/39156713010) passed.
- [x] User confirmed the `Add test profiles` button appears in CI build `ci-2715`.
- [x] User verified Standard CAN values: request `7E0`, response `7E8`, 11-bit, DID `F190`; Extended CAN values: request `18DA10F1`, response `18DAF110`, 29-bit, DID `1234`.
- [x] Switching between profiles restored the correct saved values, including after a manual response-ID edit.
- [x] Both profiles persisted across app restarts, including the final restart-and-reselect check.
- [x] Repeated profile creation did not create duplicates.
- [x] JSON export and re-import succeeded; importing invalid JSON failed safely and preserved the existing profiles and values.
- [x] User confirmed profile metadata/status and the synthetic-test-profile description were correct.
- [ ] Future release: verify behavior with a real J2534 adapter and vehicle; do not send diagnostic requests using synthetic test profiles.
- Stable release remains v0.2.0; no stable release was published for this work.


## Post-merge updater error-redaction test — 2026-10-18

- [x] Merged [PR #353](https://github.com/vorlocsev/dvinholm/pull/353) as `dbcc0a5d2b4a3902fd262f531d23df7ebdb0dfeb`. Added a regression test proving HTTP download errors retain the status and URL path but do not expose a sensitive query token.
- [x] PR CI [#39163653989](https://github.com/vorlocsev/dvinholm/actions/runs/39163653989) passed build, tests, publish, and the latest-stable-release archive audit.
- [x] Post-merge main CI [#39164047182](https://github.com/vorlocsev/dvinholm/actions/runs/39164047182) passed build, tests, publish/package, public CI asset publishing, and latest-stable-release SHA-256/ZIP validation.
- [x] Closed superseded documentation PRs #347–#350; their manual ECU profile validation details are already consolidated in merged PR #352 and the current roadmap/work log.
- No production updater behavior or stable release changed. Stable release remains v0.2.0; physical J2534 adapter/vehicle testing remains deferred.
- [ ] Continue software-only updater reliability review; only open a production fix or test PR when a concrete uncovered defect or regression gap is identified.


## Updater cancellation boundary coverage — 2026-10-18

- [x] Merged [PR #355](https://github.com/vorlocsev/dvinholm/pull/355) after successful CI [#39165406043](https://github.com/vorlocsev/dvinholm/actions/runs/39165406043).
- [x] Added a regression test cancelling at the validation/extraction progress boundary after download completes; it asserts cancellation propagates and both the staging directory and temporary archive are cleaned.
- [x] Build, test suite, publish/package steps, and latest stable release SHA-256/ZIP audit passed on the PR.
- No production code or stable release changed. Stable remains v0.2.0; physical J2534 adapter/vehicle testing remains deferred.
- [ ] Continue software-only updater review, focusing on cleanup/recovery failure paths where tests can reproduce behavior without real hardware.

## ECU editor validation feedback — confirmed CI (2026-10-20)

- [x] [PR #372](https://github.com/vorlocsev/dvinholm/pull/372): move profile validation errors above the scrollable editor. [PR CI #2850](https://github.com/vorlocsev/dvinholm/actions/runs/39332349835) and [post-merge main CI #2851](https://github.com/vorlocsev/dvinholm/actions/runs/39333268386) passed.
- [x] [PR #373](https://github.com/vorlocsev/dvinholm/pull/373): link validation failures to individual editor fields, highlight and focus invalid entries, and add eight parameter-name regression cases. [PR CI #2853](https://github.com/vorlocsev/dvinholm/actions/runs/39334351132) and [post-merge main CI #2854](https://github.com/vorlocsev/dvinholm/actions/runs/39335101214) passed on `347a83d655644d00aa00d798377df4570dec0211`.
- [x] Close stale duplicate PR #371 as superseded by merged PRs #372 and #373.
- [x] Main CI #2854 built and tested Windows x86, uploaded its artifact and published the public CI updater asset; the stable-release archive audit succeeded.
- [ ] Complete the **eight manual Windows UI checks** in [the validation-feedback checklist](docs/manual-ecu-profile-validation-ui-2026-10-20.md) on a current CI build. **Do not mark manual UI checks passed from CI alone.**
- [ ] Physical J2534 adapter/vehicle testing remains deferred to later releases; no ECU compatibility claim. Stable release stays v0.2.1 pending a separate release decision.

## ECU profile editor — user-confirmed Windows results (2026-10-20)

- [x] On a Windows CI build incorporating PRs #372/#373, user confirmed correct visible error and field highlight for malformed Request CAN ID, identical Request/Response CAN IDs, malformed DID, duplicate DID, empty required Source, and case-insensitive duplicate Profile ID.
- [x] Valid synthetic profile Save succeeded with `7E0`/`7E8`, DID `F190`, synthetic Source and Unverified status.
- [x] Valid rename to `Test ECU Final` and full restart preserved the edited profile's name, CAN IDs and DIDs while unrelated profiles were unchanged.
- [x] Post-merge [main CI #2857](https://github.com/vorlocsev/dvinholm/actions/runs/39336486699) finished successfully.
- [ ] A distinct invalid-DID edit followed by Cancel, exact local Current CI label verification, and independent immediate border-reset observation remain to be confirmed. Do not claim 8/8 complete checklist rows without these substeps.
- [ ] Physical J2534 adapter/vehicle testing is deferred; stable release remains v0.2.1.
- Detailed evidence: [Windows validation checklist and results](docs/manual-ecu-profile-validation-ui-2026-10-20.md), [PR #374](https://github.com/vorlocsev/dvinholm/pull/374).

## CAN Monitor: bounded RX history and live software refilter — 2026-10-20

- [x] [PR #376](https://github.com/vorlocsev/dvinholm/pull/376) merged as `2dec8831b63c0c5306694dd21209ce8b2a19e07d`. Before this fix, software CAN ID filtering discarded nonmatching frames from the UI collection and the filter-change handler did not rebuild the list.
- [x] Preserve the last **5000 received CAN frames**, independently of the selected software ID filter, and rebuild the displayed list from that history when the filter text changes.
- [x] Preserve oldest-first ordering and evict rows from the visible list when their underlying history entries expire; count all received frames separately from shown rows. Clear resets captured history, view, count and rate.
- [x] Added seven offline regression tests including receive injection through the existing `VirtualJ2534Adapter`, filter changes, malformed and zero ID queries, bounded eviction, clear, and invalid-capacity guard.
- [x] [PR CI #2862](https://github.com/vorlocsev/dvinholm/actions/runs/39342233407) completed successfully (Windows build/test/publish and latest stable release archive audit).
- [x] Post-merge [main CI #2863](https://github.com/vorlocsev/dvinholm/actions/runs/39342680771) completed successfully.
- [x] Validated offline **Demo CAN Monitor** frame display, filtering and CSV export manually in Windows without a physical adapter. This checks synthetic UI data only; it does not establish real-bus monitoring or J2534 compatibility.
- [x] Added a separately labeled, in-process **Demo CAN Monitor** feed in merged PR #378, isolated from actual ECU/UDS and J2534 connections.
- Physical adapter/vehicle validation remains postponed; stable release remains v0.2.1.

## Offline Demo CAN Monitor — 2026-10-20

- [x] [PR #378](https://github.com/vorlocsev/dvinholm/pull/378) merged as `8b8c89c6df18044dd10d3b0d4d85ba8ba46d31ff`, adding **Start demo / Stop demo** for safe offline WinUI CAN Monitor checks. No J2534 adapter or real CAN bus is used by the demo.
- [x] Deterministic synthetic 11-bit/29-bit rows feed the existing bounded 5000-frame monitor history and UI table on a dispatcher timer. Filter change, shown/RX counters, clear and CSV paths are shared with monitor display.
- [x] Demo entry requires a disconnected/idle session. Real J2534 connection is disabled while demo runs. On stopping, synthetic history is retained for filter/CSV; initiating genuine J2534 connection clears demo history. The demo timer stops on window close.
- [x] Demo CSV filenames include `SYNTHETIC` and all rows contain `SYNTHETIC_DEMO` in an explicit source column; real-capture CSV retains its legacy five-column schema.
- [x] Eight offline unit tests cover deterministic input, 11/29-bit row representation, payload length/hex encoding, time progression, monitor history and software ID filters, demo CSV marking and real CSV backwards compatibility.
- [x] [PR CI #2868](https://github.com/vorlocsev/dvinholm/actions/runs/39344455511) passed Windows build, tests, packaged app and latest-stable ZIP audit.
- [x] Post-merge [main CI #2869](https://github.com/vorlocsev/dvinholm/actions/runs/39345037152) completed successfully. Documentation PR #379 CI #2871 and main CI #2872 also succeeded.
- [x] **All eight main Windows Demo CAN Monitor smoke scenarios user-confirmed on 2026-10-20**, including startup, software filters `7E8`/`0`/invalid `ZZZZ`, history replay, tagged filtered CSV via the file picker, Clear, Stop, and default-off after full restart. See [the completed checklist](docs/manual-can-demo-monitor-2026-10-20.md). The exact locally installed CI/SHA and an actual real-bus/CSV capture were not separately verified.
- [ ] Physical J2534 device and vehicle validation stays deferred. Stable release remains v0.2.1; the demo is a main/CI-only feature for now.

## Demo CAN Monitor — Windows acceptance completed (2026-10-20)

- [x] User manually confirmed eight offline demo UI checks with no J2534 adapter connected; evidence recorded in consecutive [PR #379 conversation comments](https://github.com/vorlocsev/dvinholm/pull/379).
- [x] [Main CI #2872](https://github.com/vorlocsev/dvinholm/actions/runs/39345796252) passed on `11d112d7b8fbf4353d606a614154574dbf869f96`. Prior PR #378 CI #2868, main CI #2869, and docs CI #2871 also passed.
- [ ] Remaining scope: exact installed UI Current build label, independent manual confirmation of real-capture CSV schema (automated unit test passed), and real adapter/vehicle behavior. No compatibility claim. Stable release remains v0.2.1; demo is available in newer main CI builds.

## v0.2.2 — Windows stable smoke and opt-in stable update source (2026-10-20)

- [x] Published stable **v0.2.2** (`77b6899`) with verified x86 package and SHA-256; [main CI #2878](https://github.com/vorlocsev/dvinholm/actions) succeeded. The running Windows app reported `Current: 0.2.2 77b6899` and `Up to date (latest: CI #2878 77b6899)`; a second check did not offer replacement.
- [x] User confirmed offline Demo CAN Monitor startup, `7E8` filtering, tagged CSV and hidden-frame replay on the published stable build, without an adapter.
- [x] User confirmed stable WinUI profile editor required-field checks, test profile creation/edit, cross-restart persistence, case-insensitive duplicate ID rejection, standard CAN ID bounds/collision validation, duplicate/malformed DID rejection, JSON export/reimport without duplicate entries, ID and DID search, verified-only filtering, and standard/extended synthetic profile switching. Repeated **Add test profiles** did not add duplicates; evidence in [PR #380 comments](https://github.com/vorlocsev/dvinholm/pull/380).
- [x] [PR #382](https://github.com/vorlocsev/dvinholm/pull/382) merged as `9a8966f` and [CI #2880](https://github.com/vorlocsev/dvinholm/actions/runs/39453824756) plus post-merge [main CI #2881](https://github.com/vorlocsev/dvinholm/actions/runs/39454617709) passed. Added separate development-CI and stable-release update channels with tag-to-commit resolution, archive metadata/digest validation, source-change offer invalidation, and offline automated tests; no changes to the published v0.2.2 archive. This follow-up renames their visible choices to **Development / Stable**.
- [x] **Windows source-switch acceptance user-confirmed on published CI #2884** (`651e68b`): updated from v0.2.2 using Development → Replace files and restart; both labels **Development / Stable** appeared, with Development selected by default. Development Check update reported **Up to date** on `CI #2884 · 651e68b`, Stable found previous release `v0.2.2 · 77b6899`, and switching back to Development cleared the stale Stable **Replace files and restart** offer. No downgrade was installed. See [manual record](docs/manual-updater-channels-2026-10-20.md) and [PR #383 conversation](https://github.com/vorlocsev/dvinholm/pull/383).
- [ ] Physical J2534/ECU/vehicle tests remain deferred; no vehicle compatibility claim. Stable version remains v0.2.2 until separately released.

## Development / Stable updater — Windows manual acceptance (2026-10-20)

- [x] [PR #383](https://github.com/vorlocsev/dvinholm/pull/383) merged as `651e68b`, renaming update sources to **Development / Stable** without changing channel discovery. [PR CI #2883](https://github.com/vorlocsev/dvinholm/actions/runs/39456524973) and [post-merge CI #2884](https://github.com/vorlocsev/dvinholm/actions/runs/39457307648) completed successfully (Windows build, automated tests, release audit and CI ZIP publication).
- [x] User confirmed Development updater downloaded and replaced v0.2.2 with CI #2884 and relaunched successfully; new labels present with Development selected by default.
- [x] User confirmed both channel lookups and that switching from an offered older Stable release back to Development invalidates the prior update offer; no downgrade initiated.
- [x] User confirmed saved unverified ECU profile `user.demo.v022.ecm` with Name `Edited Demo ECM v0.2.2`, `7E0/7E8`, DID `F187/F190`, source `Revised synthetic test only`, and both built-in test profiles all persisted after replacing files and restarting.
- [ ] Future update UX: visually distinguish a *downgrade* from an update when Stable resolves to a commit older than the installed Development build, and require explicit user confirmation before replacing files with an older stable release. An SHA mismatch alone does not establish commit ancestry; do not classify direction by run number or SHA lexicographic order.
- [ ] Verify safe downgrade UX via automated tests and Windows UI without actually downgrading the current installed build; no real J2534 adapter or vehicle validation. Stable remains v0.2.2.

## Stable-replacement Cancel — completed Windows acceptance (2026-10-20)

- [x] [PR #385](https://github.com/vorlocsev/dvinholm/pull/385) merged as `9d0e1f4`. CI #2890 attempt 1 was cancelled, but [attempt 2](https://github.com/vorlocsev/dvinholm/actions/runs/39463465646) successfully completed Windows x86 build, automated tests, publish and stable release audit; public `ci-2890` archive published.
- [x] User installed Development CI #2890, selected Stable v0.2.2 (`77b6899`) and clicked **Replace files and restart**. The separate **Switch to Stable?** confirmation appeared; **Cancel** closed it without download/replacement and reset **Check update**. No rollback installed.
- [x] User closed/restarted Dvinholm, verified installed CI #2890 (`9d0e1f4`), saw **Development → Up to date**, and confirmed `Edited Demo ECM v0.2.2` and both standard/extended synthetic profiles persisted. Evidence: consecutive [PR #385 comments](https://github.com/vorlocsev/dvinholm/pull/385).
- [ ] No actual Stable downgrade/recovery was tested. Future enhancement: assess commit ancestry rather than assuming SHA mismatch means a downgrade.

## Offline UDS example preview — next software-only feature (2026-10-20)

- [x] [PR #386](https://github.com/vorlocsev/dvinholm/pull/386) merged as `c86abc5` after successful [PR CI #2892](https://github.com/vorlocsev/dvinholm/actions/runs/39494817764); [post-merge CI #2893](https://github.com/vorlocsev/dvinholm/actions/runs/39495359322) passed and published Windows x86 `ci-2893`.
- [x] **Preview UDS example (offline)** is separated from live diagnostics and CAN Demo; it is available without hardware when disconnected and idle. No J2534 or live UDS operation is triggered.
- [x] Deterministic fictional F190 VIN `TEST0000000000ABC`, F187 DID `DEMO-ECM` and DTC `0x123456` / `0x00C0DE` use existing read-only formatters with exact raw bytes, a prominent synthetic disclaimer and no vehicle-specific claims.
- [x] Eight no-adapter unit tests cover provenance, deterministic data, VIN/raw bytes, unverified DID ASCII/raw bytes, DTC status and avoiding unsupported diagnostic claims; PR and post-merge CI succeeded.
- [x] **All 8 Windows manual checks user-confirmed on CI #2893** (final full-restart test 2026-10-21): offline access and real-read disabling, synthetic provenance, fictional VIN/raw bytes, F187 preview/raw bytes, DTC statuses, scroll/select with layout preserved, CAN Demo mutual exclusion and post-restart manual-only preview. See [completed checklist](docs/manual-uds-offline-preview-2026-10-20.md) and [PR #386 evidence](https://github.com/vorlocsev/dvinholm/pull/386).
- [ ] Physical J2534 adapter/vehicle tests remain deferred. Stable v0.2.2 is unchanged and the UDS preview is currently Development-only.

## Synthetic-only UDS TXT export — completed Windows acceptance (2026-10-21)

- [x] Implement **Export synthetic UDS TXT** as an explicit user action. Its payload must come **only** from the fixed `UdsOfflineSample` fixture, never live `StatusText`, VIN, DTC or ECU profile state. Save via a user-selected Windows TXT file picker; never write automatically.
- [x] Require the same disconnected/idle conditions as UDS preview; recheck after returning from file picker, reject active CAN Demo/UDS and preserve Cancel as a no-write action.
- [x] Eight offline deterministic unit tests for synthetic provenance, disclaimer, sample integrity, fixed filename, fictional F190/F187/DTC fields and no unsupported diagnostic conclusions. [PR #388](https://github.com/vorlocsev/dvinholm/pull/388) merged as `94ab300`; [main CI #2899](https://github.com/vorlocsev/dvinholm/actions) passed and published Development `ci-2899` Windows x86 ZIP.
- [x] **All 8 Windows manual TXT export checks user-confirmed** on installed Development CI #2899 (`94ab300`) on **2026-10-21**: button visibility/persisted ECU profiles; native picker and synthetic filename; Cancel/no write; actual selected-folder TXT save; report header/disclaimer; fictional F190/F187/DTC content/raw bytes; block during CAN Demo and resume after Stop; safe default-off restart with saved file preserved and live reads disabled. Evidence: [PR #388 manual checkpoints](https://github.com/vorlocsev/dvinholm/pull/388), [completed checklist](docs/manual-uds-synthetic-txt-export-2026-10-21.md).
- [ ] Keep genuine vehicle export out of scope and preserve a privacy review if real-data export is ever added. Stable v0.2.2 unchanged; no J2534/vehicle tests.


## Next software-only milestone — protect diagnostic exports from the app update directory (2026-10-21)

- [x] [PR #390](https://github.com/vorlocsev/dvinholm/pull/390) implemented protection against writing TXT/CSV report **contents** into the app installation tree, with main CI #2905 success. However, user subsequently reported an empty file created by the WinRT Save picker before a blocked selection could be validated; the earlier path-only guard is not sufficient to prevent placeholders.
- [x] Eight offline path tests check ordinary Windows folder comparisons and boundaries. **WinRT picker placeholder defect remained**, so a follow-up is required before declaring blocked save/no-new-file acceptance.
- [x] **Windows manual acceptance completed 2026-10-21:** user confirmed blocked TXT saves in the app root and a subfolder (no zero-byte files), blocked demo CSV in the app root, valid nonempty TXT/CSV files in Documents, Cancel without truncation or new file, and persistence on restart. See [8/8 checklist](docs/manual-report-export-destination-2026-10-21.md). The exact installed Current label was not pasted.
- [ ] Do not conflate safe synthetic export with live ECU diagnostics. Adapter, ECU and vehicle validation remains explicitly deferred; stable v0.2.2 unchanged.

## Fix zero-byte Save picker placeholders (2026-10-21)

- [x] User reported creation of an **empty file** in the report save flow after [PR #390](https://github.com/vorlocsev/dvinholm/pull/390). Microsoft documents that `Windows.Storage.Pickers.FileSavePicker.PickSaveFileAsync` creates a new empty `StorageFile` before app code executes; rejecting a directory afterward left that file in place. Logged in the PR #390 discussion. Do not consider this a successful no-file rejection.
- [x] [PR #391](https://github.com/vorlocsev/dvinholm/pull/391) replaced the WinRT picker with Win32 Save As **path-only** selection (`OFN_NOTESTFILECREATE`) for synthetic UDS TXT and CAN Monitor CSV. Destination/session checks run before file creation; reports are written to a same-folder temporary file and renamed over the target only when complete. Merged as `ff6e075` on 2026-10-21.
- [x] Eight deterministic Windows-compatible automated tests cover nonempty UTF-8 TXT, atomic overwrite, synthetic CSV, simulated failed/partial write rollback, empty-input rejection and filename validation. PR [CI #2907](https://github.com/vorlocsev/dvinholm/actions/runs/39536715346) and post-merge main [CI #2908](https://github.com/vorlocsev/dvinholm/actions/runs/39537200998) **passed**, including Windows x86 build, tests, publish/package and latest-stable-release audit.
- [x] **8/8 Windows manual acceptance checks user-confirmed on 2026-10-21:** no zero-byte placeholders for blocked TXT/CSV targets, successful Documents TXT/CSV, safe Cancel, and preserved profiles/reports/default-off demo after restart. User confirmed the installed Current field looked correct but did not transcribe the literal version/CI ID; record that evidence limit. See [completed Windows checklist](docs/manual-report-export-destination-2026-10-21.md). Physical J2534/ECU/vehicle tests are still deferred; stable v0.2.2 unchanged.
