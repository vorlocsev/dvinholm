# Update menu | Isolated Windows acceptance (Development after Stable 0.2.4)

**Scope:** verify the new `Check update` flyout, saved Development/Stable
selection and clean Stable version labels **without running an updater or
touching the installed Stable v0.2.4**. This is a manual UI checklist, **not**
proof of completed Windows testing, adapter support or an approved release.

## Prepare an exact-SHA, isolated GUI copy

Choose the newest **successful** run of `Dvinholm Stable 0.2.4 (staging
only, no publish)` at the intended **post-v0.2.4 Development commit**.

**Recommended: one download.** On that run's **Artifacts** section, download
`Dvinholm-UPDATE-MENU-WINDOWS-ONE-DOWNLOAD`. Extract the entire artifact
into a **new** folder; it contains `candidate/`, `kit/` and
`START-UPDATE-MENU-ACCEPTANCE.cmd`. Double-click
`START-UPDATE-MENU-ACCEPTANCE.cmd` to verify both archives against the
original SHA256SUMS and exact source commit, prepare an isolated copy,
then launch it. The verifier refuses to overwrite a previously prepared
destination. For an unattended verification **without starting WinUI**, run
`START-UPDATE-MENU-ACCEPTANCE.cmd --prepare-only` from Command Prompt.

**Alternative: legacy two-artifact setup.** Download both artifacts
from the **same** staging workflow run:

- `Dvinholm-v0.2.4-STAGED-NOT-PUBLISHED`: both original ZIPs and the
  original `SHA256SUMS.txt`. Extract into `candidate/`.
- `Dvinholm-v0.2.4-STABLE-WINDOWS-ACCEPTANCE-KIT`: exact-SHA receipt
  and scripts. Extract into sibling `kit/`, preserving its subfolders.

Run `kit/START-STABLE-ACCEPTANCE-024.cmd` to verify and launch, or use
`kit/START-STABLE-ACCEPTANCE-024.cmd --prepare-only` for headless
verification. **Never mix artifacts from different CI runs.**

Both paths create `Dvinholm-024-stable-acceptance/` beside `kit/`
and `candidate/`, containing a verified `Dvinholm.App.exe`.
Use the prepared EXE only, never the raw ZIP or the existing Stable
installation folder. This *staging-only* binary is **not** the
published Stable v0.2.4; the public release tag is frozen at `a9e6188`.
If the destination already exists, restart its verified EXE directly
rather than overwriting the folder.

The receipt keeps application data under
`Dvinholm-024-stable-acceptance/acceptance-user-data`, separate from the
installed `%LOCALAPPDATA%/Dvinholm` settings. It disables update checking,
file replacement, and physical adapter connection. The menu remains visible
to inspect, and the channel choice may be saved **only to isolated data**.
Never delete or move the acceptance receipt when relaunching the EXE.

## Eight manual checkpoints (not automatically completed)

- [ ] **1.** Start prepared EXE on Windows 11 LTSC; expected Dvinholm window, no crash.
- [ ] **2.** The main window has one `Check update` button, not an inline Development/Stable selector; clicking opens an `Updates` flyout.
- [ ] **3.** Flyout displays the selected source (`Development` or `Stable`), current application version and a separate **disabled** installation/check button.
- [ ] **4.** Switching from Development to Stable (or back) changes the selector without opening a new window, downloading bytes or starting an update.
- [ ] **5.** Close Dvinholm and relaunch **the same prepared EXE**. Previously selected channel is restored; the saved preference is under the private `acceptance-user-data/update-source.txt`.
- [ ] **6.** For this staged Stable-channel production assembly, `Current` shows only `0.2.4` (not a CI number, SHA or `.stable` suffix). `Available` is not populated offline because network checking is blocked.
- [ ] **7.** The flyout warns that offline update checking/installation are disabled, including after a channel switch or flyout reopen; its action button cannot trigger a download or replacement.
- [ ] **8.** `Connect` remains unavailable in the isolated copy and the installed `%LOCALAPPDATA%/Dvinholm/update-source.txt` remains unchanged. No physical J2534 is required.

Keep all eight boxes unchecked until **actually observed on Windows**.
A passing GitHub Actions build or a successful prepare-only extraction does
not satisfy manual GUI acceptance.

## What this does not prove

The installed application's real GitHub lookups, UI download progress,
version switching across live releases, updater restart/rollback and
installed-profile migration must be tested separately **outside the isolated
copy** after another explicit test plan. Do not equate the read-only staging
build with published Stable v0.2.4 or announce Stable v0.2.5.
