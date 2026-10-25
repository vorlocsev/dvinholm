# Stable 0.2.4 | Windows manual acceptance and updater release boundary

**Record date:** 2026-10-26  
**Candidate source commit:** `a9e61883a413d4afaebb86fb468987c904250092`  
**Candidate type:** `0.2.4+<exact SHA>.stable` from **unpublished staging**  
**Evidence:** [Successful Stable staging #16](https://github.com/vorlocsev/dvinholm/actions/runs/40240287577) and [user-confirmed acceptance issue #498](https://github.com/vorlocsev/dvinholm/issues/498)  
**Published Stable remains:** v0.2.3, commit `91ecc343608c931eb033523e3db6012a6c5dbd58`

## User-confirmed interactive acceptance: 8/8 successful

On 2026-10-26, the tester replied **"без ошибок"** when asked whether all eight items from the isolated Windows 11 LTSC v0.2.4 acceptance checklist passed. This is **user-reported manual confirmation**, not machine telemetry, screen recording, or a fresh GitHub CI test.

| # | Human-check action | Reported result |
|---|---|---|
| 1 | Start and relaunch the prepared isolated Windows 11 LTSC Portable copy | Passed (user reported) |
| 2 | Window title, version, branding and icon | Passed (user reported) |
| 3 | No demo/test/preview UDS controls in production | Passed (user reported) |
| 4 | No connected J2534: disconnected state and safe disabled live actions | Passed (user reported) |
| 5 | CAN speed selection saved and loaded after restart | Passed (user reported) |
| 6 | ECU JSON import/export/edit/restart persistence using isolated data | Passed (user reported) |
| 7 | CAN Monitor layout/filter and offline CSV export | Passed (user reported) |
| 8 | Update UI, source selection and cancellation/error handling without replacing an installed build | Passed (user reported) |

Only the verified `a9e6188` candidate is covered. A fresh source commit, even a docs-only merge, changes the CI artifact's embedded informational SHA and requires a distinct verification/acceptance decision. **Do not assume that CI builds made after this record inherit human GUI acceptance.**

## Real installed updater: separate, still pending

The original user's eight-item isolated GUI acceptance did **not** replace the actual installed v0.2.3 via the built-in updater. The existing automated file replacement/rollback test confirms disposable archive behavior, **not** the installed updater, desktop restart or actual user's stored settings.

The Stable updater discovers only a **published, non-prerelease** GitHub Stable release (not an Actions staging artifact, RC ZIP or Development prerelease). Until a separately approved public v0.2.4 release exists, Stable must continue to resolve **v0.2.3**. Therefore a real v0.2.3 → v0.2.4 update cannot be tested in the Stable channel **before** the public release without substituting a different workflow. Never disguise a Development update as a Stable update.

### Safe prepublication status check (no update)

1. In the **already installed v0.2.3**, preserve existing settings: export ECU profiles and make an independent backup of `%LOCALAPPDATA%\Dvinholm` before any planned update.
2. In the installed app, choose **Stable** and **Check update**. It must report the latest published Stable **v0.2.3**, not offer the unpublished v0.2.4.
3. Do **not** click `Replace files and restart` for a downgrade or an unexpected build. Leave the prepared isolated v0.2.4 copy untouched.
4. Verify the ordinary installed v0.2.3 copy and data remain unchanged.

This is a **manual action plan, not yet a recorded outcome**. A non-updating Stable-channel lookup cannot prove the future replacement/restart path.

### Eight checks required for a real v0.2.3 → v0.2.4 installed-updater smoke after approval/publication

- [ ] Confirm the installed baseline is v0.2.3 and back up original binaries, ECU profiles and private settings independently.
- [ ] After explicit public v0.2.4 release authorization, verify that Stable `Check update` offers the **published v0.2.4 commit**, not an Actions artifact or prerelease.
- [ ] Verify the replacement confirmation and downloaded Stable ZIP integrity before installation; abort on any wrong SHA, version or unexpected downgrade.
- [ ] Trigger `Replace files and restart` on the installed app and confirm the new process starts successfully on Windows 11 LTSC.
- [ ] Confirm the installed version and embedded commit match the published v0.2.4 GitHub tag.
- [ ] Confirm ECU profiles, CAN speed and other actual persisted settings are unchanged from backup.
- [ ] Confirm obsolete binaries/debug/demo files are absent and the buttons revert to `Check update` / `Up to date`.
- [ ] Capture update errors or recovery if they occur; check the documented rollback behavior on failure without intentionally damaging the working installation.

**These eight installed-updater steps have not been performed.** They are tracked separately from the eight user-confirmed isolated GUI checks above. Because v0.2.4 is not yet published, this last gate cannot be certified as a live Stable-channel replacement today.

## Release decision

**HOLD for public Stable v0.2.4.** The verified candidate `a9e6188` and both associated [staging #16 artifacts](https://github.com/vorlocsev/dvinholm/actions/runs/40240287577) are ready for review, but no release authorization has been granted. Leave published Stable v0.2.3 untouched. Physical J2534/vehicle tests remain deferred to a future version; no such test is claimed.

**Repository maintenance:** This documentation update is intentionally kept in a **draft PR**, not merged into `main` before the release decision. Merging would change the exact candidate SHA after the user acceptance.
