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

The workflow also runs the eight production-only x86 smoke tests, uploads the ZIP/manifest as a GitHub Actions artifact, downloads them into a new location, re-runs the eight ZIP gates and dry-runs the one-click acceptance launcher against the downloaded ZIP using `--prepare-only` without opening WinUI. A success is **not** a GUI or physical-adapter acceptance.

## Additional Lite RC validation (not a release)

The optional framework-dependent Windows x86 **Lite** variant is now prepared by a **separate, read-only** workflow: `release-candidate-lite-v0.2.4.yml`. It uploads `Dvinholm-v0.2.4-LITE-RC-TEST-ONLY`, with `Dvinholm-Windows-x86-Lite.zip` and its **Lite-only** strict `SHA256SUMS.txt`.

Its eight checks verify exact commit and filename set, 1–30 MiB Lite ZIP bounds, SHA-256, safe layout without bundled `.NET` or debug tools, bundled read-only prerequisite checker byte parity, extracted x86 apphost with an **explicitly simulated** .NET 10/Windows App SDK 2.5 host, exclusion of demo/preview code, compiled version 0.2.4, and matching product SHA/channel. GitHub Actions uploads the ZIP and re-downloads it for the same checks, without regenerating the downloaded checksum.

Lite requires a real host to already have compatible **.NET 10 Desktop Runtime x86**, Windows App SDK x86 and VC++ Redistributable x86. The simulated check does **not** certify an actual Windows installation and never launches WinUI. Run the bundled `Check-Lite-Prerequisites.cmd` on a real Windows host *before* considering manual Lite use. Lite is experimental, not a replacement for Portable in the self-updater. Keep the acceptance kit below paired with the **Portable RC** from its own workflow; the separate Lite ZIP is not accepted by `START-ACCEPTANCE-024.cmd`.

No 0.2.4 Stable tag, release or update target has been created.

## Unified Stable 0.2.4 staging (not published)

The separate `stable-staging-v0.2.4.yml` workflow creates **one validation-only staging artifact** containing three files: `Dvinholm-Windows-x86.zip` (Portable), `Dvinholm-Windows-x86-Lite.zip` (Lite), and a canonical two-entry `SHA256SUMS.txt`. Both x86 assemblies have development/demo flags off, debug files excluded, trimming disabled, product version `0.2.4+<exact-commit>.stable` and are validated against the **same commit**.

The workflow performs eight ZIP/binary gates independently for each flavor, checks Lite is smaller, checks the combined checksums against the two independently verified single-flavor sidecars, uploads the three files to Actions, downloads the actual stored artifact and **repeats both eight-gate audits without recalculating the downloaded SHA-256**. Its only permission is `contents: read`; it cannot create or modify a GitHub Release, tag, install or update the published v0.2.3.

A green staging workflow **does not** approve 0.2.4 for publication and is **not** evidence of Windows GUI compatibility, a real installed Lite host or a physical J2534. The eight interactive Windows LTSC checkpoints below remain pending. Before any future Stable 0.2.4 publication, a separate version-specific user-data-isolated acceptance, update migration review and explicit user authorization are required.

## Published Stable 0.2.3 to staged 0.2.4 offline upgrade rehearsal

Before contemplating a 0.2.4 release, the read-only dual-flavor staging workflow now also downloads the **actual published v0.2.3 Portable**. It checks GitHub's release metadata, requires the original committed release identity `91ecc343608c931eb033523e3db6012a6c5dbd58`, verifies the published ZIP/manifest independently and compares against the downloaded 0.2.4 Stable-channel candidate.

The `eng/Test-RealStableUpgrade024.ps1` integration test runs eight gates: exact release/candidate file sets, published 0.2.3 checksum and product identity, both staged ZIP hashes, staged 0.2.4 exact-commit production identity, old/new compiled assembly versions, byte-exact old-install backup, forward replacement with obsolete-file cleanup and preserved synthetic settings, and byte-exact rollback to the previous installation with settings still unchanged. All mirroring happens under a new disposable temporary directory using extracted real ZIP bytes. It **never** runs the app, accesses an installed Dvinholm directory, calls J2534 or writes a GitHub release.

This verifies a **file replacement/rollback simulation only**. It does not demonstrate that the real in-app updater works, that a user's actual profile migrations behave correctly, or that WinUI launches. Those manual checks remain separate requirements before release approval.

## Stable 0.2.4 isolated Windows human acceptance (not published)

The read-only dual-flavor staging workflow produces the separate `Dvinholm-v0.2.4-STABLE-WINDOWS-ACCEPTANCE-KIT` artifact **after** rechecking the actual staged ZIPs. This kit is **not interchangeable** with the earlier Portable `rc`-channel kit. It validates the `stable` channel and both ZIPs using the exact commit; it creates no GitHub release.

Download **both artifacts from the same successful 0.2.4 Stable staging workflow run**. Extract the three-file `Dvinholm-v0.2.4-STAGED-NOT-PUBLISHED` artifact into a `candidate` directory, and the acceptance kit into a sibling `kit` directory. The kit now contains **`START-STABLE-ACCEPTANCE-024.cmd`**. After extracting the two artifacts into sibling `kit` and `candidate` directories, double-click the CMD file inside `kit`. It strictly validates the **entire** SHA receipt, audits both downloaded ZIPs and their shared checksum, creates a new private Portable copy, checks its receipt and user-data directory, then attempts to start **only that verified isolated EXE**. A launch attempt is not a completed Windows GUI test.

For verification and extraction **without** launching WinUI, run this from the `kit` directory:

```cmd
START-STABLE-ACCEPTANCE-024.cmd --prepare-only
```

The complete kit also includes `src/Dvinholm.App/Dvinholm.App.csproj`, retained solely so the Lite **simulated-host** preflight can validate the exact pinned Windows App SDK version. Keep this file in its original relative path beneath `kit` when extracting the downloaded Actions artifact; moving only the `.cmd` or `eng` folder makes the independent Lite audit fail closed.

The launcher rejects unknown arguments, tampered/multiline build receipts, missing ZIPs and any previously existing `Dvinholm-024-stable-acceptance` destination. Running it again intentionally refuses to overwrite the folder. After a successful prepare-only run, launch `Dvinholm-024-stable-acceptance\Dvinholm.App.exe` directly for manual testing; its acceptance receipt keeps settings isolated.

Alternatively, with Windows PowerShell in the common parent, the underlying preparation can still be run explicitly:

```powershell
$commit = (Get-Content -LiteralPath '.\kit\BUILD_COMMIT_024_STABLE.txt' -Raw).Trim()
powershell.exe -NoProfile -ExecutionPolicy Bypass -File '.\kit\eng\Prepare-StableAcceptance024.ps1' -ArchiveDirectory '.\candidate' -ExpectedCommit $commit -DestinationDirectory '.\Dvinholm-024-stable-acceptance'
```

The preparation script first enforces the exact lowercase 40-character SHA, an exact three-file staging set and the **original two-line checksum manifest**. It verifies both Portable and Lite against their original checksum entries and eight binary/archive gates each, including the 0.2.4 compiled assembly and `0.2.4+<SHA>.stable` product identity. Then it extracts **only Portable** into a new directory with `ACCEPTANCE-0.2.4.txt` and `acceptance-user-data`. The directory is atomically renamed into place. Existing destinations are refused; aborted preparations are cleaned up.

The preparation does **not** start the application. Once completed, manually start `Dvinholm-024-stable-acceptance\Dvinholm.App.exe` and follow the receipt checklist. The receipt keeps profile, baud-rate and J2534-path settings private even after a direct EXE restart and disables acceptance-mode hardware/update actions. Never run an unprepared candidate outside this isolated folder on a computer with an existing installation.

**A green CI preparation rehearsal is not successful human acceptance.** Windows 11 LTSC GUI, installed updater and real hardware testing remain unperformed; explicit authorization is still required for public 0.2.4 Stable publication.

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
2. `Dvinholm-v0.2.4-WINDOWS-ACCEPTANCE-KIT` with `START-ACCEPTANCE-024.cmd`, `BUILD_COMMIT_024.txt`, verification scripts and this guide, extracted into a sibling `kit` folder.

The kit also includes **`START-ACCEPTANCE-024.cmd`**. Its `BUILD_COMMIT_024.txt` must contain exactly a 40-character **lowercase hexadecimal SHA** followed by at most one final LF/CRLF. Malformed, multi-line, uppercase, extra-whitespace or shell-metacharacter values are refused *before* the launcher passes any input into another CMD or PowerShell invocation. Eight negative Windows CI checks exercise the real launcher against invalid provenance without a GUI or vehicle adapter.

Once the two artifacts are extracted into sibling `kit` and `candidate` folders, double-click the file in `kit`. It verifies the exact commit and SHA-256, extracts to a fresh `Dvinholm-024-acceptance` folder, checks the receipt and private data folder, then **attempts to launch only that isolated EXE** and opens its checklist in Notepad. No installer is run; a successful launch command does **not** prove that the GUI works.

To verify and extract without launching WinUI, run the launcher in the `kit` folder with `--prepare-only`:

```cmd
START-ACCEPTANCE-024.cmd --prepare-only
```

The preparation verifies the strict checksum, exact embedded commit and eight production-only ZIP/binary gates, then extracts into a previously nonexistent directory. The isolated copy receives `ACCEPTANCE-0.2.4.txt` and a private `acceptance-user-data` directory. An existing destination or invalid ZIP is rejected without overwriting an installation. **Running the launcher a second time deliberately fails** rather than overwriting the previous test copy; use the existing isolated executable for a manual relaunch.

Alternatively, after successful preparation, manually run `Dvinholm-024-acceptance\Dvinholm.App.exe` from that directory. The app recognizes the **0.2.4 receipt** on ordinary EXE relaunch, uses only that copy's private settings/catalog, and disables hardware connection and in-app updates. The earlier v0.2.3 receipt remains supported for older isolated test copies.

**Safety:** Never launch the raw RC executable outside this prepared folder alongside an installed copy: without an acceptance receipt it can use the normal `%LOCALAPPDATA%\Dvinholm` data directory. Do not relocate the EXE without its receipt. These safety measures are checked headlessly by CI, not by a Windows user yet.

## Release boundary

The current published Stable remains **v0.2.3**. Final 0.2.4 stable publishing needs its own reviewed staging/acceptance path and the user's explicit authorization after checks. Portable trimming remains disabled; real J2534 and vehicle tests remain deferred.
