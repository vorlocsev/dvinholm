# Dvinholm 0.2.3 Portable release candidate

Current published Stable remains v0.2.2. The release candidate is an
Actions test artifact, not an update target, installer, or GitHub release.

| Gate | Required evidence | Status |
| --- | --- | --- |
| 1 | Exact-commit Windows x86 CI green; all tests and Stable audit | Pending final candidate CI |
| 2 | Candidate-only Portable produced from 0.2.3 version override | Pending actual RC workflow |
| 3 | Eight RC checks: exact files, bounded ZIP size, SHA256, ZIP safety, isolated x86 extraction, actual DLL, assembly version, informational commit SHA | Pending actual RC run |
| 4 | GitHub Actions download of candidate and revalidation without modifying SHA manifest | Pending roundtrip |
| 5 | Development Portable/Lite ZIP checks and disabled trimming/AOT | Pending exact-head CI |
| 6 | Manual Windows 11 LTSC x86 Portable startup, close/relaunch, disconnected status | Pending |
| 7 | Manual offline demo CAN, multi-ID filter 7E0,7E8, filtered CSV and ECU profile persistence | Pending |
| 8 | Manual installed-0.2.2 updater/rollback behavior, finalized notes, authorized publish and published-asset audit | Pending |

Strictly no branch named release/v0.2.3, tag v0.2.3, Stable release
publication, MSIX signing/trust mutation, or physical J2534 testing during
this preparation. Never mix a Lite runtime tree into a Portable installation.
