# Dvinholm 0.2.4 | Release Candidate verification (not a release)

This is a **validation-only** Windows x86 Portable candidate. It does not create a GitHub Release or tag, alter published Stable v0.2.3, install/launch WinUI, send CAN traffic, or test real J2534 hardware.

## Automated gates

The standalone `eng/Test-ReleaseCandidate024.ps1` verifier checks eight requirements against the **actual packaged** Portable ZIP:

1. Exact lowercase 40-character commit SHA and flat, expected candidate files only.
2. Self-contained Portable ZIP within bounded size.
3. SHA-256 digest matches the strict single-entry manifest.
4. Safe archive paths, root app/icon/runtime, no debug files.
5. Successful disposable extraction with an actual Windows x86 apphost.
6. Stable/production assembly contains no demo or synthetic UI/fixture code and retains VIN/DID/DTC handlers.
7. Compiled assembly version is 0.2.4.
8. Product informational version contains the exact candidate commit SHA and the `rc` channel.

The workflow also runs the eight production-only x86 smoke tests, uploads the ZIP/manifest as a GitHub Actions artifact, downloads them into a new location, re-runs the eight ZIP gates and rehearses isolated extraction with a versioned receipt without opening WinUI. A success is **not** a GUI or physical-adapter acceptance.

## Remaining eight manual Windows acceptance checkpoints

These **have not been performed** on 0.2.4. Do not mark complete from CI.

- [ ] Launch a candidate on Windows 11 LTSC in an isolated test environment, without changing the installed application's data.
- [ ] Confirm actual window title/version and branded icon.
- [ ] Confirm no demo/test/preview UDS controls in production.
- [ ] Check J2534 device discovery while disconnected; live actions remain disabled.
- [ ] Verify CAN speed selection, save, restart and reload.
- [ ] Verify user profile JSON import/export/edit/search and restart persistence.
- [ ] Verify CAN Monitor layout, filtering and nonempty CSV export without actual adapter.
- [ ] Test update channel UI, cancel/error handling, and unchanged user data; never perform an unapproved downgrade.

## Prepare a verified isolated copy for human acceptance

Download the **two artifacts from the SAME successful RC 0.2.4 workflow run**:

1. `Dvinholm-v0.2.4-RC-TEST-ONLY` with `Dvinholm-Windows-x86.zip` and `SHA256SUMS.txt`, extracted into a `candidate` folder.
2. `Dvinholm-v0.2.4-WINDOWS-ACCEPTANCE-KIT` with `BUILD_COMMIT_024.txt`, verification scripts and this guide, extracted into a sibling `kit` folder.

From Windows PowerShell in their common parent directory, run:

```powershell
$commit = (Get-Content -LiteralPath '.\kit\BUILD_COMMIT_024.txt' -Raw).Trim()
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\kit\eng\Prepare-RcAcceptance024.ps1' -ArchiveDirectory '.\candidate' -ExpectedCommit $commit -DestinationDirectory '.\Dvinholm-024-acceptance'
```

This step **does not launch WinUI**. It verifies the strict checksum, exact embedded commit and eight production-only ZIP/binary gates, then extracts into a previously nonexistent directory. The isolated copy receives `ACCEPTANCE-0.2.4.txt` and a private `acceptance-user-data` directory. An existing destination or invalid ZIP is rejected without overwriting an installation.

After successful preparation, manually run `Dvinholm-024-acceptance\Dvinholm.App.exe` from that directory. The app recognizes the **0.2.4 receipt** on ordinary EXE relaunch, uses only that copy's private settings/catalog, and disables hardware connection and in-app updates. The earlier v0.2.3 receipt remains supported for older isolated test copies.

**Safety:** Never launch the raw RC executable outside this prepared folder alongside an installed copy: without an acceptance receipt it can use the normal `%LOCALAPPDATA%\Dvinholm` data directory. Do not relocate the EXE without its receipt. These safety measures are checked headlessly by CI, not by a Windows user yet.

## Release boundary

The current published Stable remains **v0.2.3**. Final 0.2.4 stable publishing needs its own reviewed staging/acceptance path and the user's explicit authorization after checks. Portable trimming remains disabled; real J2534 and vehicle tests remain deferred.
