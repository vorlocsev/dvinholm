# Windows distributions

Dvinholm is a WinUI 3 Windows **x86** application. The main distribution remains a no-install Portable ZIP. The optional **Lite** ZIP is a framework-dependent alternative for machines whose runtimes are already installed.

| Variant | ZIP | Runtime dependencies | Automatic updater |
|---|---|---|---|
| Portable (default) | `Dvinholm-Windows-x86.zip` | Bundles .NET and Windows App SDK runtime | Supported; existing updater selects this exact asset name |
| Lite (experimental CI) | `Dvinholm-Windows-x86-Lite.zip` | Requires .NET **10 Desktop Runtime x86** and the **Windows App SDK runtime** compatible with the build | **Not supported** until separately validated |

Lite is **not** an independent stable release just because CI publishes a prerelease artifact. Install prerequisites before launching Lite on Windows. It uses the same application and J2534 backend, but its Windows App SDK bootstrap/runtime dependencies still need a real Windows launch smoke test. Never mix Portable and Lite DLLs in the same installation folder.

## Minimal Lite MSIX (experimental)

A dedicated x86 MSIX build excludes both **.NET 10 Desktop Runtime** and
**Windows App SDK runtime** using `SelfContained=false` and
`WindowsAppSDKSelfContained=false`. It has no trimming or AOT.
The validator also confirms that the **generated** MSIX (not just the source
manifest) declares exactly one `Microsoft.WindowsAppRuntime.2` Framework
package dependency, with minimum version `2.5.1.0` and the expected Microsoft
publisher. It checks the package-root `Dvinholm.App.runtimeconfig.json`
references the .NET 10 shared runtime, and rejects unexpected MSIX signature
payloads while this CI artifact is labeled unsigned. These values are tied to
the pinned Windows App SDK 2.5.1; upgrade them intentionally together.

Official packaged-app architecture and offline installation guidance:
https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deployment-architecture
https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview

The installed Windows host must provide .NET 10 Desktop Runtime **x86**;
Windows App SDK framework packages may be resolved by the Store on supported
Windows, but MSIX does **not** guarantee automatic installation of .NET.
Windows LTSC IoT without Store may need all prerequisites installed manually.

The separate `lite-msix.yml` CI validates 20 MSIX archive contracts and
refuses packages that bundle .NET or WinUI runtimes. Metadata checks also
require the expected development publisher, exactly one Windows.Desktop target
family with a minimum Windows version compatible with Windows 11 LTSC 26100,
`runFullTrust`, a single full-trust application registration, and the
Dvinholm executable at the MSIX root. Eight Windows x86 fixture scenarios
cover these checks (including a valid synthetic package). This is a static
archive inspection, not proof of real Windows installation, Store access,
correct Windows App SDK provisioning, or launch readiness. The development artifact
is **UNSIGNED and test-only**, not a one-click installer. It cannot be deployed
normally until signed with a trusted publisher certificate. The Portable file
replacement updater is disabled in MSIX builds; Windows handles package updates
through a separately configured signed distribution channel.

## Store-less LTSC MSIX read-only dependency inspection (Development)

From a repository checkout, inspect the **actual unsigned Development MSIX** on
the intended Windows machine, without downloading, installing, registering, or
running any package:

    powershell.exe -NoProfile -File .\eng\Test-LiteMsixOfflinePrerequisites.ps1 -PackagePath 'C:\Downloads\Dvinholm-Lite-x86-UNSIGNED.msix'

This checks only the declared package Framework dependency
(`Microsoft.WindowsAppRuntime.2`, minimum 2.5.1.0, expected Microsoft
publisher), the root .NET 10 runtimeconfig, installed **x86**
`Microsoft.NETCore.App` plus `Microsoft.WindowsDesktop.App` runtimes, and
the current user's registered x86 Windows App SDK Framework package.
It uses the x86 `dotnet.exe` host and does not mistake x64-only or older
frameworks for suitable dependencies. Run without test fixture parameters on
a real Windows host.

For CI, both `-RuntimeListFile` and `-FrameworkPackageListFile` must be
provided together; the checker prints **SIMULATED INPUT**, so successful
fixtures never claim verification of an actual computer. Eight simulated
Windows x86 cases cover valid inputs, absent/old .NET, missing Desktop,
absent/wrong-architecture/old Windows App SDK, and partial fixture rejection.

The offline inspector now reads **every** shared framework reference in both
`runtimeOptions.framework` and `runtimeOptions.frameworks`. It rejects
unknown/duplicate frameworks and unsupported per-framework roll-forward or
`applyPatches=false`, and applies independent minimum x86 versions for
`Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App`. If the
runtimeconfig declares only Core, the Desktop check uses the Core minimum.
Eight synthetic Windows x86 regression cases protect these rules. This
remains read-only and is not proof of package installability.

The experimental MSIX workflow now runs
`eng/Test-LiteMsixSimulatedHost.ps1` on the **real freshly built x86 MSIX**.
The gate creates disposable synthetic host-inventory files (x86 .NET
10.0.2 Desktop/Core and Windows App SDK Framework 2.5.1), invokes the
read-only inspector, and removes the inventory even on failure. A subsequent
SDK/runtime-version change will fail the gate until its requirements are
reviewed; this is not an installed-machine test. Additional negative
Windows tests cover wrong MSIX identity/architecture/publisher/version
and missing or incompatible .NET runtimeconfig.

**Passing this read-only inspection does not mean an unsigned MSIX can
be installed.** Signing with a certificate trusted by Windows, offline
dependency availability and provisioning, possible VCLibs requirements,
successful package registration, and real interactive WinUI startup remain
separate, unverified gates. Never bypass Windows trust controls or treat a
synthetic fixture as a machine readiness test. Physical J2534 testing is
deferred. Official guidance:
https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview
https://learn.microsoft.com/en-us/windows/msix/msix-troubleshooting-guide

## Lite host prerequisite inspection (Development)

Extract Lite into its **own directory** and double-click
`Check-Lite-Prerequisites.cmd` next to `Dvinholm.App.exe`.
The launcher invokes its bundled, read-only PowerShell checker. It reports
whether the x86 .NET 10 Desktop Runtime, Windows App SDK 2.5 packages,
and Visual C++ x86 prerequisites appear installed. It does not download,
install or change software, nor start WinUI. The launcher invokes Windows PowerShell from the Windows System32 directory,
not from the extraction folder or PATH. It first verifies that the Lite EXE,
runtimeconfig and checker script are present together. The command uses a
process-local PowerShell execution-policy override, preserves the inspector's
nonzero failure exit code and provides distinct errors for missing files.
For scripted use, set `DVINHOLM_LITE_NO_PAUSE=1` to suppress the final
key-press prompt; double-click launches still pause by default.

Developers can still use the source script from a repository checkout
(including synthetic Windows host fixtures):

    .\eng\Test-LitePrerequisites.ps1 -LiteDirectory 'C:\Dvinholm-Lite' -WindowsAppSdkLine '2.5'

The read-only tool validates **every** declared Lite runtimeconfig .NET shared
framework before querying the x86 dotnet installation for .NET 10
Desktop/shared runtimes. Unknown, duplicate, malformed or non-10.x .NET
framework declarations fail closed. .NET host roll-forward rules are
honored conservatively: default/Minor, LatestMinor, LatestPatch, and
Disable require compatible installed x86 versions. For the default `Minor`
policy, if the requested minor line is already installed, its available patch
must meet the requested minimum: even a newer installed minor cannot silently
substitute for an insufficient patch in the requested line. If the requested
minor is absent, moving to a higher minor within .NET 10 is supported.
`LatestMinor` can choose a newer minor, `LatestPatch` never crosses minor
versions, and `Disable` requires an exact version. Major/LatestMajor and
applyPatches=false fail closed until separately supported, preventing an
incorrect ready-to-launch verdict. This preflight does not override
DOTNET_ROLL_FORWARD environment settings or actually launch the .NET host.
It also checks for
all four current-user Windows App SDK runtime packages: Framework x86
(Microsoft.WindowsAppRuntime.2), Main (MicrosoftCorporationII.WinAppRuntime.Main.2),
Singleton (MicrosoftCorporationII.WinAppRuntime.Singleton), and DDLM x86
(Microsoft.WinAppRuntime.DDLM.2.5.*-x8). Stable 2.x uses the major version
in Framework/Main names, not the SDK minor version. On x64 Windows,
Main/Singleton are x64 while this app needs x86 Framework/DDLM.
The Singleton has a special version scheme: its MSIX version major is
**8000 + the SDK major** (for SDK 2.5, minimum `8002.5.0.0`).
The preflight rejects absent, obsolete, invalid, or wrong-architecture
Singleton registrations; newer Singleton releases remain accepted because
one Singleton is shared across SDK releases. This is a registration check,
not a proof that the package can initialize successfully.

The check compares versions with the SDK 2.5 requirement where relevant,
and **Microsoft Visual C++ Redistributable x86** (VS 2015+ family, registry
key under HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86).
It inspects **both Windows registry views** for the x86 key and does not
mistake an x64-only redist for an x86 installation. If missing, install the
official Microsoft VC_redist.x86.exe. It rejects missing Main/Singleton/DDLM and incorrectly architected or old packages. Three optional fixture files
(runtimes, SDK package list and x86 VC state) must be provided **together**
for regressions; test mode is always marked SIMULATED INPUT.

CI also runs a Lite ZIP integration gate against the **actual extracted**
Lite distribution, using synthetic x64 host metadata (x86 .NET 10 Desktop,
Windows App SDK Framework/DDLM and Visual C++ Redistributable; x64
Main/Singleton). The invocation is explicitly marked SIMULATED INPUT. It
does not examine an actual computer or start the GUI. Reproduce the CI gate:

    .\eng\Test-WindowsDistributionExtraction.ps1 -Flavor Lite -ZipPath 'artifacts\Dvinholm-Windows-x86-Lite.zip' -SimulatedLiteHost

This gate checks the project's exact Microsoft.WindowsAppSDK PackageReference
before creating 2.5 fixtures. A future SDK minor/major bump intentionally
fails CI until the fixture inventory is updated to match. A patched 2.5.x
NuGet version remains compatible with the modeled SDK 2.5 line.
For isolated regression tests, -SdkProjectFile can specify a synthetic
project file, but only together with -SimulatedLiteHost.
The flag is rejected for Portable. For a real Windows host, use the normal
Test-LitePrerequisites.ps1 without any fixture arguments.

The tool does not install packages or launch WinUI. Passing the inspection
cannot confirm package health, runtime bootstrapping or whether the GUI opens. Install official prerequisites,
then test the extracted Lite Dvinholm.App.exe interactively on Windows.
Never combine Portable and Lite files in one folder.

Microsoft guidance:
https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps
https://github.com/microsoft/WindowsAppSDK/blob/main/specs/Deployment/MSIXPackages.md
https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist

## Development ZIP SHA-256 verification

Development CI generates `SHA256SUMS.txt` only **after** both final Portable
and Lite archives have passed packaging/extraction checks. The file contains
two exact SHA-256 entries and is published alongside both ZIPs on the public
`ci-N` prerelease. It is also uploaded as a separate GitHub Actions artifact.
The updater remains Portable-only and does not change.
After each successful `main` release upload, CI downloads the **published**
three assets again and verifies release tag, exact commit, prerelease status,
three asset names/sizes and both SHA-256 hashes. The post-upload audit also
checks each asset's GitHub API `state=uploaded` and server-generated
`digest=sha256:...` for all three files, including the checksum manifest.
That catches coherent replacement of a ZIP plus a rewritten SHA256SUMS.txt,
which a manifest-only check cannot detect. This checks the actual
GitHub Releases uploads rather than just the local packaging directory.
It does not establish publisher authenticity or enable the Lite updater.

To verify both downloaded archives without launching WinUI, put
`SHA256SUMS.txt`, `Dvinholm-Windows-x86.zip` and
`Dvinholm-Windows-x86-Lite.zip` in one directory, then run:

    .\eng\Test-WindowsDistributionChecksums.ps1 -Directory 'C:\Dvinholm-Downloads'

The checker rejects missing archives, altered bytes, malformed or duplicate
entries, unknown names and directory traversal. Regenerate checksums only in
a trusted packaging workflow using `-Generate`. SHA-256 detects corruption
relative to the manifest; it is **not** a digital signature and cannot prove
publisher authenticity if an attacker replaces both archives and manifest.

## Distribution rules

The application is published for `win-x86` without Trimming or Native AOT. The self-contained Portable output stays complete, including native WinUI resources and .NET runtime files. Never delete arbitrary DLLs, JSON, PRI, MUI, WinMD, or locale folders to reduce the ZIP size.

The GitHub Actions artifacts for Development now contain the **same final,
verified ZIP files** as the public `ci-N` prerelease, rather than a separate
archive of uncompressed publish folders. GitHub Actions wraps artifacts in its
own download ZIP, so extract that wrapper once to obtain
`Dvinholm-Windows-x86.zip` or `Dvinholm-Windows-x86-Lite.zip`.
The standalone `Dvinholm-Distribution-Checksums` artifact contains
`SHA256SUMS.txt`; the hashes apply to the **inner final ZIPs**. Missing files
fail the workflow and the already-compressed ZIPs are uploaded with compression
disabled to avoid wasted CI time. The public prerelease files need no extra
wrapper extraction.

Both downloads are packaged by `eng/Package-WindowsDistribution.ps1`. It omits only debugging byproducts (PDB, dumps, tracing/coverage files) and explicit debugger utilities. High ZIP Deflate compression is used when 7-Zip is available, with `Compress-Archive -CompressionLevel Optimal` as a compatible fallback. These are ordinary ZIP archives compatible with the built-in Dvinholm updater.

`eng/Test-WindowsDistribution.ps1` is a required packaging gate. It checks executable and icon, runtime manifests, no debugger payload, and verifies Portable has bundled .NET and Lite does **not**. CI also requires Lite ZIP to be smaller than Portable ZIP and writes the size comparison to the job summary. These tests do not replace a Windows launch test, especially for Lite.

## Stable releases

**Before any future Stable release is uploaded**, verify its final ZIP with `Test-WindowsDistribution.ps1`. Debugging symbols and tools are excluded from the Stable download, while CI retains test diagnostics. Release checksum (`SHA256SUMS.txt`) must be calculated **after** cleaning and compressing the final ZIP, so the checksum identifies exactly the published artifact.

The historical Stable release v0.2.2 is unchanged. New distribution changes and experimental Lite artifacts are CI outputs until a later stable release is explicitly prepared, tested and published.

Official Windows App SDK deployment guidance: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview
