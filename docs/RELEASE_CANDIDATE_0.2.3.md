# Dvinholm 0.2.3 Portable release candidate

Current published Stable remains v0.2.2. The release candidate is an
Actions test artifact, not an update target, installer, or GitHub release.

| Gate | Required evidence | Status |
| --- | --- | --- |
| 1 | Exact-commit Windows x86 CI green; all tests and Stable audit | Pass on main CI #3320 |
| 2 | Candidate-only Portable produced from 0.2.3 version override | Pass on main RC #9 |
| 3 | Eight RC checks: exact files, bounded ZIP size, SHA256, ZIP safety, isolated x86 extraction, actual DLL, assembly version, informational commit SHA | Pass on main RC #9 |
| 4 | GitHub Actions download of candidate and revalidation without modifying SHA manifest | Pass on main RC #9 |
| 5 | Development Portable/Lite ZIP checks and disabled trimming/AOT | Pass on main CI #3320 |
| 6 | Manual Stable-profile Windows 11 LTSC startup/close/relaunch, correct disconnected status, all test/demo buttons absent | Pending |
| 7 | Manual Stable CAN multi-ID filter 7E0,7E8, ordinary CSV and profile persistence (offline demo separately checked in Development) | Pending |
| 8 | Manual installed-0.2.2 updater/rollback behavior, finalized notes, authorized publish and published-asset audit | Pending |

Strictly no branch named release/v0.2.3, tag v0.2.3, Stable release
publication, MSIX signing/trust mutation, or physical J2534 testing during
this preparation. Never mix a Lite runtime tree into a Portable installation.

## Stable v0.2.3 manual release gate (prepared, NOT APPROVED)

- `release-v0.2.3.yml` validates relevant PRs and automatically stages **nonpublished** production ZIPs after relevant `main` pushes. A manual `dry-run` is also staging-only. Neither event can publish.
- Publication is enabled only by a manual dispatch on `main`, exact current 40-character SHA and explicit `APPROVE-DVINHOLM-V0.2.3` authorization. Do not start publish mode until manual LTSC and updater acceptance is confirmed and the user authorizes the version.
- Windows x86 development test profile executes tests. The packaged Stable app is built separately with `DvinholmDevelopmentFeatures=false`, trimming/AOT disabled and exactly `0.2.3+SHA.stable` metadata.
- Eight ZIP/DLL audit gates run before upload and after Actions roundtrip. For an explicitly approved publish run, create a GitHub DRAFT release, download and revalidate assets, and only then make it public.
- This workflow has not been dispatched for publication. Existing v0.2.2 must stay available; no MSIX signing or physical J2534 tests are involved.

- CI repair: normalize Windows CRLF in workflow tests, restore win-x86 runtime dependencies after switching SDK architecture, and keep release-dispatch inputs in environment variables rather than interpolating into PowerShell source. Refuse release if remote main moves after authorization.

## Automated production-mode acceptance (next PR)

The separate `tests/Dvinholm.Stable.Tests` project runs eight Windows x86 smoke tests on the actual `DvinholmDevelopmentFeatures=false` compilation. Both default CI and Stable staging execute it, independent of Development-only fixture tests. It verifies removal of synthetic types and handlers while retaining production controls, real read-only UDS methods, CSV and multi-ID filter logic. This is a headless regression check, not evidence of a WinUI launch, the installed 0.2.2 updater, real LTSC OS or J2534 hardware.

## Continuous Stable staging without release (PR #461)

- On relevant application, distribution-script or test changes in `main`, the protected, read-only validation job compiles real Release x86 with developer features disabled, runs eight production smoke checks and existing offline regressions, then stages Portable ZIP with SHA-256 and confirms artifact download integrity.
- `publish` remains conditioned solely on an explicitly approved `workflow_dispatch` on `main` with the exact SHA. A push or pull request cannot create a tag or release, including a draft.
- Staged GitHub Actions artifacts are **TEST-ONLY**, not public GitHub Releases or updater targets. Never claim the manual Windows 11 LTSC GUI/installed-update acceptance gates have passed due to a successful stage.
