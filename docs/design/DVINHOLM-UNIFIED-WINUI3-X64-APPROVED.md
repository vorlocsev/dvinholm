# Dvinholm Aurora: approved single-app WinUI 3 x64 design and migration

**User decision: 2026-11-02.** This file supersedes the **WPF / Windows x86 implementation choice** in `DVINHOLM-UNIFIED-WPF-SIX-PAGES-APPROVED.md`. The visual design, layout, and removal of Settings remain approved.

## Final target
- **One user-facing Dvinholm.exe**, WinUI 3, .NET 10, **Windows x64**, **self-contained .NET and Windows App SDK**.
- Do not require separately installing the .NET 10 Desktop Runtime or Windows App SDK runtime. Self-contained **does not imply a single physical PE file**: the app ZIP may include native runtime DLLs and resources.
- Hardware J2534 vendor DLLs and their OS drivers remain external; the app must never falsely claim they're bundled.
- **Exactly six top-level sidebar destinations:** Подключение / Connection, Обновление / Updates, CEM PIN, Конфигурация CEM / CEM Configuration, Ошибки (DTC) / Faults, Блоки (ECU) / Modules. **No Settings page.**
- Maintain original approved dark navy / cyan / violet Aurora glass cards, narrow sidebar with northern lights, icon #9 gear/gull/arrow, live top bar, RU/EN switch and context-relevant controls.
- Any vehicle/VIN/CEM and reported driver state is real evidence only. Mocked illustrated vehicle identifiers, PIN percentages, ECU topology, software versions and DTC values are not real app data.

## J2534 bitness is a hard gate
- A 64-bit process **cannot load a 32-bit J2534 PassThru DLL**. Driver enumeration must read the **64-bit registry view for x64**, and 32-bit view only for x86. Do not offer a 32-bit-only driver as a connectable x64 adapter.
- Specifically verify **Volvo DiCE** and **Mongoose JLR Pro** real supplied DLL architectures before accepting hardware support. A driver that only ships x86 is **incompatible with direct x64 in-process loading**.
- Do **not** silently introduce a second x86 helper or cross-bitness IPC bridge. If needed, document it as a separately reviewed architecture change, rather than pretending that x64 directly supports legacy x86 adapters.
- Driver absence or mismatched bitness must keep Connect disabled with a specific explanation. Virtual adapter offline testing does not prove physical adapter compatibility.

## Unified updater
- Channel selector Stable/Development and persisted choice live inside the **Updates** page.
- Reuse verified SHA-256, trusted provenance, transaction staging, clean restart, recovery/rollback, and fail-closed states from existing updater code.
- Running process self-replacement requires **short-lived helper after the app exits**, not a separate visible Launcher app.
- The x64 updater must only select **verified win-x64** assets and must never install legacy win-x86 archives.
- No auto-install until explicit user confirmation; upgrades and rollback must be tested with interruption/restart and an offline fixture.

## Migration boundaries and safety
1. **Phase 1: x64 build and compatibility** (this PR). Create a stand-alone x64 WinUI 3 self-contained artifact on Windows CI, validate architecture and bundled runtime contents; retain x86 production CI only as transitional compatibility. The x64 artifact is for **PR review only**, not a release.
2. **Phase 2: six-page native WinUI Aurora shell**. Reuse actual CEM, DTC, ECU, J2534 code from existing WinUI 3 project, and reorganize UI into six pages; remove Home, Settings, diagnostic demo routes from top-level nav without deleting underlying read-only functionality.
3. **Phase 3: integrated update controls**. Wire channel, check, verified download, install and rollback into Updates page; keep installer actions disabled until tested and explicit user approval.
4. **Phase 4: J2534 x64 adapter discovery and connection**. Verify 64-bit native PassThru DLL and real hardware; explicit errors when incompatible.
5. **Phase 5: release acceptance**. Verify DPI 100/125/150, RU/EN, navigation, actual Windows x64 boot, unchanged data migration, safe offline mode, clean restart, authenticated upgrade, no prototype or demo Stable leakage.

For each stage use **6–8 substantive checks** instead of long chains of micro-fixes. Keep Stable and independent Virtual Car untouched until actual production acceptance and release. **Do not merge the superseded WPF PR #701 or WinForms Launcher PR #699.**

## Stage 7: trusted publisher and rollback dry run (no installation)

- `UnifiedX64PublisherTrust` defines a **detached ECDSA P-256 SHA-256 signature** for the exact canonical release identity: tag + `Dvinholm-Windows-x64.zip` + lowercase 64-character ZIP digest. This is **not yet an active trust path**: the compiled production public key is intentionally empty, so `Verify()` always refuses. A key must be independently verified, embedded through a reviewable release-security change, and supported by a signer/key-rotation policy. The public key must **never** be loaded from the untrusted release being checked.
- A `SHA256SUMS.txt` file downloaded from the **same release** cannot independently prove publisher authenticity, even when the package hash matches. An unsigned archive remains a non-installable candidate.
- Staging defaults to `%LOCALAPPDATA%\Dvinholm-UpdateCache\staging` and future backups to `%LOCALAPPDATA%\Dvinholm-Backups`. These are **separate** from `%LOCALAPPDATA%\Dvinholm`, so an application installed inside its normal user-data folder does not accidentally encompass staging and recovery assets. The preflight still rejects overlapping, reparse or otherwise unsafe paths.
- `UnifiedX64RecoveryPreflight.Inspect` is a **read-only** backup planning audit: it requires a real AMD64 installed EXE; an isolated `pending-{GUID}` staging folder whose ZIP still matches the retained hash and byte count; disjoint installation, staging and backup paths; no filesystem reparse points; a bounded, safe installed file inventory; and a proposed randomized backup directory without creating it.
- The Updates page may run that audit only after the user chooses **Download and verify ZIP** and the archive passes integrity inspection. **It does not make a backup, copy, remove or replace the live EXE, run a helper, modify the startup configuration or restart.** The result is displayed as a tentative inventory, not a rollback guarantee.
- `CanInstall` remains **hardcoded false** until independently verified publisher provenance, a fully implemented transaction journal, atomic backup/extraction, Windows close-and-handoff, crash recovery, rollback and real hardware acceptance have passed.
- Eight isolated tests cover valid dry-run inventory; previously installed x86; overlapping backup/install; overlapping backup/stage; staging-path tampering; changed ZIP bytes; unsigned public release rejection; and cryptographic verification of fixture signatures including wrong key, tag and digest.
- All acceptance tests remain **offline, no J2534 hardware**. Physical adapter testing and Windows DPI/GUI acceptance are separate follow-up gates.

## Stage 8: isolated backup snapshot and journal (no install)

- `UnifiedX64BackupSnapshot.Create` is an **offline snapshot-preparation primitive only**. Given a successful stage-7 preflight, it re-evaluates the install/staging safety conditions; checks that installed file count and bytes have not changed; and creates a unique `.snapshot-{GUID}` staging directory in an **external** backup root.
- It copies live application files as read-only inputs into `payload/`, hashes every copied file with SHA-256, re-hashes the source to reject modified-in-flight data, and writes `snapshot-journal.json` with **schema 1** and state `BackupVerified_NoInstall`. The journal is flushed before the temporary folder is renamed to the proposed `backup-{GUID}` name. A backup with the same target name is never replaced or deleted.
- `UnifiedX64BackupSnapshot.Verify` checks journal schema/state, unique safe relative paths, exact tracked file inventory, file lengths and per-file SHA-256. It rejects unexpected backup files and root entries. This is local data-integrity evidence, **not** a publisher signature or permission to replace/restart the running EXE.
- Eight isolated tests cover successful snapshot, unchanged independent backup after source modification, file tampering, journal tampering, unexpected files, stale preflight, existing-backup refusal and staged ZIP tampering.
- Neither snapshot creation nor a rollback application command is wired to the visible Updates page yet. **No installation, startup helper, running EXE replacement, automatic recovery or user-data migrations are activated.** An interrupted `.snapshot-*` directory must not be mistaken for a completed backup.
- Pending later gates: independently vetted signing key, **publisher-provenance validation for the actual downloaded release**, explicit user approval, reliable on-disk transaction log, safe post-exit replacement, startup recovery, restored-file verification and Windows x64 physical GUI/J2534 acceptance.

## Stage 9: interruption and replay harness (strictly offline)

- New `UnifiedX64RecoverySandbox` exercises the **journal state machine and recovery algorithm on a purposely named, marked, direct child of the system temporary directory only**. It is **not** a production installer and is deliberately **not wired to the WinUI Updates page**.
- Uses real verified `UnifiedX64BackupSnapshot` receipt data from stage 8, inventories the original and candidate fixture files and durably records `BackupVerified → Applying → AppliedFixtureOnly` or, upon simulated interruption, `Applying → Recovering → RolledBack`.
- The fixture journal is `fixture-transaction.json` and is written to a new temporary file, flushed, then renamed; a state transition to `Applying` is recorded **before** changing the first fixture file. A crash in either replacement or rollback leaves a recoverable state. Recovery validates the original backup SHA-256 before writing any fixture destination.
- A replay in `Recovering` is idempotent. The test harness verifies all restored original file hashes before recording `RolledBack`. Damaged backups, journals, changed candidates or unsafe sandbox directories refuse operation.
- Eight fixture-only tests cover journal prepare without mutation, full apply fixture, interruption and successful restoration, interruption during restoration with replay, backup tampering, journal tampering, candidate tampering, and refusal outside the marked temp fixture.
- **Still NOT implemented:** actual live-program update, trustworthy publisher root/signing automation, authoritative installation transaction and restart handoff, recovery of a real installed version, or actual Windows hardware/DPI acceptance. Do not infer these from a passing sandbox fault-injection test.

## Stage 10: process-exit handoff rehearsal (offline scratch installation only)

- `UnifiedX64PostExitHandoff.WaitAndApplyFixtureAsync` waits for a **specific open Process handle to exit** before starting any synthetic file replacement. It rejects waiting on its own process, requires the fixture's verified transaction state to be `BackupVerified` both before and after waiting, and bounds the wait to 30 seconds with explicit cancellation.
- Windows offline acceptance tests start real short-lived child processes. They verify that the synthetic original remains intact **while the child is alive**, then check the fixture journal advances only after exit. Cancellation and timeouts must leave all original fixture bytes unchanged; malformed journals, absent fixture markers and modified candidates fail closed.
- The harness is **not an updater helper distributed with Dvinholm**. It cannot target the real Dvinholm installation because `UnifiedX64RecoverySandbox` accepts only a marked GUID-named direct child of the system temporary directory. No actual executable or user file replacement is enabled.
- Eight process-exit handoff checks accompany the previous stage's eight interruption/rollback tests. CI compilation and full offline regression are required, with no claims of real Windows end-user handoff until a separately reviewed helper and trusted publisher signature exist.
- Before production: explicitly approved signing trust root, signed x64 release manifest, independently verified staging and backup, a **short-lived helper launched before the app exits**, protection against PID reuse and other running app instances, fsynced transaction checkpoints, recovery at next startup, and Windows GUI/DPI/J2534 acceptance.

## Stage 11: separate x64 post-exit helper contract (non-shipping)

- Added a small **self-contained Windows x64 console executable** project `Dvinholm.UpdateHandoffProbe` to test the post-exit architecture independently from the WinUI process. **The project is not in the application solution/publish tree and is never copied to the user-facing `Dvinholm-Windows-x64.zip`.** It is a test-only helper, not a replacement for `Dvinholm.exe` and not a new visible launcher.
- The only recognized CLI is `--fixture-probe PID UTC_START_TICKS TIMEOUT_MS FIXTURE_ROOT`. Its argument validation rejects an arbitrary directory, PID without matching process creation time, and timeout outside 1–30 seconds. It keeps an opened OS process handle while waiting, rather than polling a PID that may be reused.
- A successful probe writes **one exclusive receipt only** in a GUID-named, explicitly marked direct child of the OS temporary directory. It performs **no file extraction, install, restart, rollback, executable patch or child-process launch**. The real WinUI app cannot invoke it, and its receipt is not an installation-authorization token.
- The separate Windows x64 pilot workflow publishes the helper in an isolated CI artifact directory and runs **eight process-isolation checks**: already-exited handle refusal/confirmation without false success, no receipt before active child exit, wrong start timestamp, nonfixture path, invalid marker, timeout, nonexistent PID and no receipt overwrite. A copy of the helper is **not uploaded to users**. A failing test must fail the PR pilot job.
- This stage validates process waiting and directory refusal only. Before a real updater exists: independently approved and pinned signing key, signed release provenance, backup creation and independent verification, single-instance coordination, transaction replay after forced termination, safe extraction to a separately verified staging directory, and a **reviewed** narrow privileged replacement helper with recovery/rollback are required.
- Full regression CI and the x64 pilot CI must both pass on the same reviewed branch head. Stable and independent Virtual Car must not be modified.

## Stage 12: unified installation-readiness audit (read-only)

- `UnifiedX64InstallReadinessGate.Inspect` brings previously separate checks into one **read-only, fail-closed decision**: official x64 candidate metadata, exact staged ZIP identity, fresh ZIP SHA-256/AMD64/runtimes and file count, journalled backup under a dedicated non-overlapping backup root, current backup receipt/inventory and pinned publisher attestation.
- No single successful subsystem result is approval for an install. `UnifiedX64InstallReadiness.CanInstall` remains **unconditionally false** and `PRODUCTION_INSTALL_HANDOFF_UNAVAILABLE` is always reported, since no audited live helper or crash-recovery transaction has been accepted.
- The publisher public key is deliberately **not provisioned**. `PUBLISHER_KEY_NOT_PROVISIONED` and `SIGNED_RELEASE_NOT_VERIFIED` must be returned even for a valid local ZIP and backup. Publishing `SHA256SUMS.txt` next to the ZIP does **not** prove publisher provenance.
- Eight offline tests cover: valid local archive/backup still refuses install; absent or x86 metadata; absent stage; altered ZIP; absent/tampered snapshot; foreign backup root; **same-size modified installed files**. The last case rehashes every installed file against the snapshot journal, so identical count and length alone never imply a valid rollback baseline. These use synthetic temporary files and do not replace installed Dvinholm.
- The CI packaging script and the ZIP verifier now explicitly reject a bundled `Dvinholm.UpdateHandoffProbe.exe`. The probe remains a non-shipping CI-only helper; the approved user-facing package contains only the WinUI app and its runtime dependencies.
- This gate does **not** create a backup, attach a signer, enable a user-facing Install button, relaunch the app or provide automatic rollback. Final review still requires an independently verified signing key and protected signing process, signed x64 release assets, a production post-exit helper with full transactional recovery, and manual Windows DPI/J2534 acceptance.

## Stage 13: manual detached release signing contract (install remains disabled)

- Artifact trio for a **future** signed x64 release: `Dvinholm-Windows-x64.zip`, `SHA256SUMS.txt`, `Dvinholm-Windows-x64.sig`. The signature is exactly one RFC 4648 Base64 line (88 ASCII characters plus LF) encoding a **64-byte ECDSA P-256 / IEEE P1363** signature, not DER and not PEM.
- The exact ASCII bytes signed are `Dvinholm x64 release attestation v1\\nTag: <tag>\\nAsset: Dvinholm-Windows-x64.zip\\nSHA256: <lowercase-sha256>\\n` (the notation `\\n` represents an actual LF). Valid tags: Stable `vX.Y.Z`; Development `ci-N`. This binds one specific package digest and release tag and prevents same-bytes cross-channel confusion.
- `UnifiedX64SignatureArtifact` strictly parses canonical Base64, rejects BOM, CRLF, padding anomalies and appended bytes; verifies with an explicitly supplied public P-256 key **for offline tests only**; and computes the public key fingerprint as SHA-256 of the DER-encoded SubjectPublicKeyInfo (SPKI). The production `VerifyPinned` delegates solely to the intentionally unprovisioned pinned key and therefore **always refuses** today.
- `eng/Sign-UnifiedX64Release.ps1` is a deliberate, **manual operator-only** tool. It refuses GitHub Actions; demands an existing valid P-256 code-signing certificate from `Cert:\\CurrentUser\\My` with a private key; verifies ZIP hash against a unique matching `SHA256SUMS.txt` record; signs the canonical payload; self-verifies; and writes `Dvinholm-Windows-x64.sig` using create-new semantics. The private key is never exported, uploaded or committed. CI **only parses** this script and does not execute it.
- **Required human security review before any real signing trust:** independently identify the authorized publisher and validate the public key (SPKI SHA-256) using an out-of-band trusted channel, approve certificate/key custody, document revocation and key rotation, review a separate PR that pins the exact public key, verify signed released assets and ensure no unattended CI signing with an unapproved test key.
- A signed file produced by a development or arbitrary local certificate is **not** production trust. The current source contains no production public key, the app has no signature download path yet, and the readiness gate always includes `PRODUCTION_INSTALL_HANDOFF_UNAVAILABLE`. No installation, executable replacement, auto-restart or rollback is activated.
- Eight offline P-256 signing-format tests exercise canonical round-trip, missing production key, wrong key/tag/digest, incorrect lengths/trailing data, CRLF/BOM/whitespace, alphabet/padding, deterministic SPKI fingerprint and invalid public keys. These tests generate ephemeral keys in memory, never release signing credentials.

## Release policy
- No x64 Stable release until CI and physical Windows acceptance. The old x86 updater feed must remain functional for installations already in the field while the new unified x64 feed is developed.
- Backward compatibility must be explicit; an x64 package cannot silently update an x86 install with a different driver architecture or an incompatible payload.
