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
