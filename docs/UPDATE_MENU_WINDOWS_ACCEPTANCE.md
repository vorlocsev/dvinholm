# Update menu | Isolated Windows acceptance (Development after Stable 0.2.4)

**Scope:** verify the new `Check update` flyout, saved Development/Stable
selection and clean Stable version labels **without running an updater or
touching the installed Stable v0.2.4**. This is a manual UI checklist, **not**
proof of completed Windows testing, adapter support or an approved release.

## Prepare an exact-SHA, isolated GUI copy

Use the **same successful run** of `Dvinholm Stable 0.2.4 (staging only, no
publish)` on the intended **post-v0.2.4 Development commit**. This is a
*read-only staging artifact*, not the already-published Stable v0.2.4. The
published release tag always points to frozen `a9e6188`; do not replace it.

Download both GitHub Actions artifacts from the same run:

- `Dvinholm-v0.2.4-STAGED-NOT-PUBLISHED`: two ZIPs and their original two-line `SHA256SUMS.txt`. Extract into `candidate`.
- `Dvinholm-v0.2.4-STABLE-WINDOWS-ACCEPTANCE-KIT`: receipt, scripts and this document. Extract into the sibling `kit` directory, preserving subfolders.

The acceptance kit's `BUILD_COMMIT_024_STABLE.txt` must exactly match the
chosen run's source commit. Run `kit/START-STABLE-ACCEPTANCE-024.cmd
--prepare-only` from a command prompt. It validates both downloaded ZIPs and
extracts a new `Dvinholm-024-stable-acceptance` directory. Do not copy raw
candidate files into an existing installation. Start only the prepared
`Dvinholm-024-stable-acceptance/Dvinholm.App.exe` on Windows 11 LTSC.

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
