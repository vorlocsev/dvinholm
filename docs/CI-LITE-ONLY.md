# Lite-only Development CI policy

Starting after PR #569, active Development CI uses one Windows x86 workflow
(README and frozen Stable release archives retain their historical provenance).

- One solution build, one full App.Tests regression run and one Stable-profile
  smoke test run. Reuse prior cases instead of adding repetitive eight-test
  batches; retain 6–8 meaningful acceptance gates per feature stage.
- One Lite x86 framework-dependent ZIP; no Portable or MSIX created by active CI.
- Before upload: verify Lite archive integrity, extracted x86 PE, prerequisite
  scripts and SHA256 manifest. On main only: publish the verified Lite ZIP
  and manifest as the public Development CI prerelease.
- Previous RC, Stable staging, and MSIX workflows run only by manual dispatch.
  Historical Stable assets must not be modified by this migration.

Lite requires .NET 10 Desktop Runtime x86, Windows App SDK 2.5 and Microsoft
Visual C++ Redistributable x86 on Windows. Existing older clients seeking
Portable CI ZIPs require one manual Lite migration after a prerequisite
check; do not rename Lite data to Portable or bypass prerequisites.

No Stable release is authorized or published by this policy.
