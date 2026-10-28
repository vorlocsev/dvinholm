# CI test scope after Lite-only consolidation

Dvinholm has one automatic workflow for pull requests and pushes to `main`.
Both keep x86 compilation, the actual Stable feature-profile smoke tests,
Lite-only publish, ZIP extraction/preflight, and SHA256 integrity checks.

| Trigger | App.Tests scope | Rationale |
| --- | --- | --- |
| Pull request | Every test except classes explicitly marked `Category=ArchivedWorkflow` | Faster feedback for code currently shipped |
| Push to `main` | **All tests**, with no filter | Postmerge full-regression gate, followed by public Lite-only prerelease verification |
| Manual `workflow_dispatch` | **All tests**, with no filter | Full audit on demand |

The 15 deferred classes cover only historical 0.2.4 RC/staging/manual
acceptance and experimental unsigned MSIX workflow contracts. They remain
compiled, discoverable, and fully executed on `main` and manual runs.
No CAN, ISO-TP, UDS, ECU profile, CAN RX, Windows Lite runtime, updater,
or archive integrity regression has been excluded from pull requests.

The `CiTestScopeContractTests` guard checks the PR filter, complete
main/manual run, positive tags for historical cases, and absence of archive
tags on representative active safety suites. A green PR is **not** a
Stable release, proof of Windows GUI startup, or physical J2534 validation.
The already published Stable release assets are immutable.
