# ECU profile validation feedback — Windows smoke checklist

**Checkpoint:** `main` commit `347a83d655644d00aa00d798377df4570dec0211`; PRs [#372](https://github.com/vorlocsev/dvinholm/pull/372) and [#373](https://github.com/vorlocsev/dvinholm/pull/373) are merged.

**Automated validation:** [PR #373 CI #2853](https://github.com/vorlocsev/dvinholm/actions/runs/39334351132) and [post-merge main CI #2854](https://github.com/vorlocsev/dvinholm/actions/runs/39335101214) both completed successfully. Main CI included Windows x86 build, automated tests, packaged icon check, CI archive upload and public CI asset publication, and latest-stable-release archive audit.

**Manual validation is pending.** Perform these checks on a **successful CI #2854 Windows x86 build or later build containing commit `347a83d`**. Stable v0.2.1 predates this change. No physical adapter or vehicle is needed; keep Dvinholm disconnected. Use synthetic profiles only; do not send diagnostic requests.

| # | Action | Expected result | Manual result |
|---|---|---|---|
| 1 | Update from an older CI build using **Check Update**, or extract the CI #2854 Windows ZIP. Restart and inspect Current version. | The running application shows CI #2854 / `347a83d` or a newer commit containing these changes. Existing saved ECU profiles remain present. | ☐ |
| 2 | Open **New profile**. Enter a unique synthetic ID, a name, source note, request ID `ZZZZ`, response `7E8` (11-bit), and DID `F190`. Click Save. | The dialog stays open; error appears **above** the scrollable fields without scrolling. **Request CAN ID** receives focus and error border. Catalog remains unchanged. | ☐ |
| 3 | Replace request `ZZZZ` with `7E0`, then set response to `7E0`; click Save. | Request border resets after editing it. Response field receives error border/focus because request and response IDs must differ. | ☐ |
| 4 | Set response `7E8`, then DID `ZZZZ`; click Save. Repeat with `F190, F190`. | DID field receives error border/focus in both invalid and duplicate cases. The error remains visible above the scroll area. | ☐ |
| 5 | Fix DID to `F190`; clear the required source note, then try Save. | Source note is highlighted and focused. No invalid catalog is saved. | ☐ |
| 6 | Supply a synthetic source note and save the valid profile. | Save succeeds, dialog closes, selected profile shows correct IDs/DIDs and remains **unverified**. No diagnostic requests are sent. | ☐ |
| 7 | Attempt to create a second profile whose ID differs from the saved ID only by case. | Duplicate is rejected, **Profile ID** field receives error border/focus, existing profile is unchanged. Cancel. | ☐ |
| 8 | Edit the saved profile, introduce an invalid DID, then Cancel; reopen, change the name and save; restart the app. | Cancel preserves previous fields; valid edit persists after restart; unrelated profiles remain unchanged. | ☐ |

**Report:** Windows version, the exact `Current` CI number and 7-character SHA, each check's pass/fail result, and optionally a cropped screenshot of the error area. Redact vehicle identifiers, private file paths and adapter details.

**Scope:** A passing software CI run is not a manual WinUI interaction test and cannot establish physical J2534 adapter or vehicle compatibility.

## User-confirmed manual results — 2026-10-20

**Confirmed on Windows UI by the user:**

- [x] Invalid Request CAN ID `ZZZZ` is rejected; the validation message is visible without scrolling and the Request CAN ID input is highlighted/focused.
- [x] Matching Request/Response CAN IDs (`7E0`/`7E0`) are rejected with the Response CAN ID input highlighted.
- [x] Malformed DID `ZZZZ` and duplicate DID `F190, F190` are rejected, with DID highlighting and without saving invalid changes.
- [x] A blank required Source/verification note is rejected and Source is highlighted.
- [x] Correcting values to Request `7E0`, Response `7E8`, DID `F190` and a synthetic source note allows Save; the profile is present in the catalog and remains Unverified.
- [x] An existing Profile ID entered with different letter case is rejected, with no duplicate or modifications to the existing profile.
- [x] After a valid edit renaming the profile to `Test ECU Final`, a full application restart retains the name, CAN IDs and DIDs, and unrelated profiles remain unchanged.

**Not yet explicitly confirmed:** the exact installed `Current: CI #... · SHA` displayed during the UI checks; a separate edit-invalid-DID-then-Cancel scenario (the first half of checklist step 8); the Request border reset immediately upon typing, independently of subsequent validation. These are not marked as passed. The original checklist above is retained as a complete reproducible protocol rather than rewriting unverified sub-steps as successful.

**Evidence:** consecutive user confirmations are recorded in the conversation of [PR #374](https://github.com/vorlocsev/dvinholm/pull/374). Main CI #2857 ([run #39336486699](https://github.com/vorlocsev/dvinholm/actions/runs/39336486699)) passed on `99727e1cd568e5d133626f520755183ec9ffafc6`. No physical J2534 adapter/vehicle validation was conducted.
