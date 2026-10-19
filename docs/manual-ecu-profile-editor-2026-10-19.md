# ECU profile editor — Windows manual smoke checklist

**Feature:** [PR #369](https://github.com/vorlocsev/dvinholm/pull/369), merged in `main` as `2989b0bb52515123ef27976b6cc83dc5df7df847`.

**Version:** a successful **`main` CI build newer than v0.2.1**; stable release v0.2.1 does **not** include the editor.

**Hardware:** **no J2534 adapter required**. Leave Dvinholm disconnected. This is a **pending** manual checklist, not a record that tests have passed.

| # | Action | Expected result | Result |
|---|---|---|---|
| 1 | Start current `main` CI build. | **New profile** is visible; **Edit selected** is disabled when no profile is selected. The CAN Monitor remains visible. | ☐ |
| 2 | Click **New profile**. Try saving with empty ID/name/source. | Editor stays open and displays a validation message. Existing profiles remain unchanged. | ☐ |
| 3 | Create a test-only draft with ID `user.demo.ecm`, name `My demo ECU`, 11-bit request/response `7E0`/`7E8`, DID `F190`, source `Synthetic test, not validated on a vehicle`. | Save succeeds; the new profile is selected, `isVerified` is false, UDS fields match the profile. No diagnostic request is sent. | ☐ |
| 4 | Open **New profile** again and enter the same ID with different letter case. | Duplicate ID is rejected; dialog remains open. Cancel it. | ☐ |
| 5 | Try request ID `800` in 11-bit mode or identical request/response IDs. | Both are rejected; no overwrite of the original profile. | ☐ |
| 6 | Try DID list `F190, 0xF190` and then `XYZ`. | Duplicate and malformed DID inputs are rejected with messages. | ☐ |
| 7 | Select `My demo ECU`, click **Edit selected**; verify ID is read-only, change name to `Edited demo ECU` and source to `Revised synthetic test only`; save. | Edited data persists, selected profile is visible after resetting filters, `isVerified` remains false. | ☐ |
| 8 | Edit again but click **Cancel**. | Previous name/source stay unchanged. No diagnostic request is sent. | ☐ |
| 9 | Export JSON, restart app, select `user.demo.ecm`. | Edited name, CAN IDs, source and DID persist; exported JSON remains compatible with previous import format. | ☐ |
| 10 | Activate **Marked verified only** with only unverified test profiles. | Test profiles are hidden and their active UDS inputs are cleared; disabling the filter restores the saved catalog. | ☐ |

**To report:** Windows version, `Current: CI #... · SHA` from the app, and which steps passed/failed. For screen captures, redact any real VIN, private paths, credentials or adapter IDs.

**Important:** Creating a profile does *not* verify CAN addresses, diagnostics or compatibility with Volvo. Validation of real J2534 devices and vehicles remains deferred.
