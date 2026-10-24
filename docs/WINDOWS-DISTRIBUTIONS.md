# Windows distributions

Dvinholm is a WinUI 3 Windows **x86** application. The main distribution remains a no-install Portable ZIP. The optional **Lite** ZIP is a framework-dependent alternative for machines whose runtimes are already installed.

| Variant | ZIP | Runtime dependencies | Automatic updater |
|---|---|---|---|
| Portable (default) | `Dvinholm-Windows-x86.zip` | Bundles .NET and Windows App SDK runtime | Supported; existing updater selects this exact asset name |
| Lite (experimental CI) | `Dvinholm-Windows-x86-Lite.zip` | Requires .NET **10 Desktop Runtime x86** and the **Windows App SDK runtime** compatible with the build | **Not supported** until separately validated |

Lite is **not** an independent stable release just because CI publishes a prerelease artifact. Install prerequisites before launching Lite on Windows. It uses the same application and J2534 backend, but its Windows App SDK bootstrap/runtime dependencies still need a real Windows launch smoke test. Never mix Portable and Lite DLLs in the same installation folder.

## Release/Stable build profile

Since published **v0.2.3**, normal Release builds default
to `DvinholmDevelopmentFeatures=false`, so **Start demo**, **Add test
profiles**, **Preview UDS example (offline)** and **Export synthetic UDS
TXT** are absent from the production interface and compiled handlers.
The generator classes `CanDemoFrameGenerator`, `UdsOfflineSample` and
`UdsOfflineTextReport` are not compiled into Stable binaries. This is
a build-time separation, not merely a disabled-button state.

Development CI, synthetic regression tests and unsigned experimental MSIX
explicitly opt in with `DvinholmDevelopmentFeatures=true`. The published v0.2.3
uses false and validates its real packaged DLL for excluded
fixture symbols. VIN/DID/DTC reads, real CAN Monitor, profile editing,
import/export and ordinary CSV remain available.

No saved ECU profiles are deleted, including data previously generated
through Development. Historical Stable v0.2.2 remains available separately.

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

## CAN Monitor CSV safety (Development)

The CAN Monitor CSV writer preserves the existing five-field capture
schema (timestamp, id, type, dlc, quoted data) and adds the sixth
SYNTHETIC_DEMO source field only for explicitly synthetic demonstrations.
Display text now escapes commas, double quotes and line breaks to keep
each frame in its intended CSV columns. Unexpected spreadsheet formula
prefixes in textual fields are neutralized with a leading apostrophe
before CSV quoting. Numeric timestamp and DLC use invariant decimal.
Eight automated tests cover compatibility and malformed display text.
This is a CSV formatting hardening measure and does not claim any
physical adapter or ECU testing.

## Roundtrip verification of GitHub Actions MSIX artifacts (Development)

The experimental MSIX workflow now **uploads and downloads** its own
Tiny-Lite-MSIX-UNSIGNED-TEST-ONLY artifact, using actions/download-artifact@v7
in a new directory. The read-only verifier
eng/Test-LiteMsixDownloadedArtifact.ps1 requires exactly two root files:
Dvinholm-Lite-x86-UNSIGNED.msix and its .sha256 companion. It rejects
unexpected files or nested directories, checks that the downloaded MSIX
hash matches the **pre-upload build output**, and independently verifies the
strict sidecar digest. Eight Windows x86 simulated-file regression cases
cover missing/extra files and altered package or checksum.

Unlike verifying only the checksum beside the upload, this detects corruption
or accidental file substitution **between build output and downloaded
Actions artifact**. It does not prove authenticity against an attacker with
access to both build and CI, nor signed MSIX package trust, Store-less LTSC
installation or GUI startup. The test fixtures contain no production keys.

## Experimental unsigned MSIX SHA-256 sidecar (Development)

The Tiny-Lite-MSIX-UNSIGNED-TEST-ONLY Actions artifact now contains two
files: Dvinholm-Lite-x86-UNSIGNED.msix and its single-entry
Dvinholm-Lite-x86-UNSIGNED.msix.sha256 checksum. Windows PowerShell 5.1
eng/Test-LiteMsixSha256.ps1 generates and checks the lowercase SHA-256 digest
bound to that exact unsigned filename after the real package has passed
MakeAppx and static validation. Eight Windows x86 regression tests cover
valid digest, changed package bytes, missing package/manifest, wrong filename,
uppercase or malformed digest, and an extra manifest entry.

After extracting both files into one folder, run from a repository checkout:

    powershell.exe -NoProfile -File .\eng\Test-LiteMsixSha256.ps1 -PackagePath 'Dvinholm-Lite-x86-UNSIGNED.msix' -ChecksumPath 'Dvinholm-Lite-x86-UNSIGNED.msix.sha256' -Mode Verify

The checksum tool computes SHA-256 using .NET cryptography and does not depend on the Get-FileHash cmdlet, which may be missing in x86 Windows PowerShell 5.1 test hosts.

This checksum detects accidental corruption, **not publisher authenticity**.
An adversary can replace both package and checksum. Trusted MSIX signing,
runtime dependency provisioning, LTSC install and GUI launch are unverified.

## Windows SDK unpack gate for actual unsigned MSIX (Development)

The experimental Tiny Lite MSIX workflow now passes the **actual generated
MSIX** to the installed Windows SDK `MakeAppx.exe unpack` tool before artifact
upload, in addition to its existing archive checks. The script
`eng/Test-LiteMsixSdkUnpack.ps1` finds a compatible local Windows SDK tool,
extracts into a unique temporary directory and cleans up even on failure.

Eight independent checks verify: (1) SDK unpack tool availability;
(2) successful extraction; (3) x86 Lite manifest identity and development
Publisher; (4) package-root x86 PE executable; (5) .NET 10 runtimeconfig;
(6) the five branded assets; (7) exclusion of debug files and bundled runtime
DLLs; and (8) unsigned-only status. This supplements but **does not replace**
static MSIX validation and simulated runtime prerequisites.

**Successful unpack is not successful installation.** No signing/trust validation,
AppX registration, dependency provisioning, Windows LTSC interactive start or
physical J2534 test occurs. CI must not delete or alter installed application
files or certificate stores.

## Public signing certificate preflight (Development; not signing)

The CI MSIX is intentionally **unsigned**. An actual Store-less installation
requires a signed package whose certificate matches the identity Publisher,
and whose signing chain the destination Windows computer trusts. A development
self-signed certificate is only suitable for controlled testing, never an
unattended Stable distribution.

This **read-only** preflight takes a **public DER `.cer`** and a development
MSIX; it never requests a PFX/private key, writes trust stores, signs a package
or installs dependencies:

    powershell.exe -NoProfile -File .\eng\Test-LiteMsixSigningCertificate.ps1 -PackagePath 'C:\Downloads\Dvinholm-Lite-x86-UNSIGNED.msix' -CertificatePath 'C:\Certificates\Dvinholm-public.cer'

Checks: exact Dvinholm.Lite x86 package identity and certificate Subject
matching the MSIX manifest Publisher, current validity, explicit *only*
code-signing EKU, digital-signature KeyUsage, and non-CA BasicConstraints.
Sixteen Windows test cases generate only ephemeral local test certificates, export
**public .cer data**, and ensure known wrong inputs fail.

The public-certificate preflight now uses an intentionally conservative
cryptographic profile: **RSA public keys of at least 2048 bits**, with an
X.509 certificate signature using RSA PKCS#1 and SHA-256, SHA-384 or SHA-512.
It rejects SHA-1 and ECDSA certificates for this **development-only** path
until alternate signing identities have been tested separately. This does
not validate a digital signature on the MSIX, verify Windows trust, or
require the certificate private key. Eight additional temporary public
certificate fixtures cover weak RSA, stronger RSA, SHA-1, SHA-384/512 and
ECDSA variants.
The SHA-1 rejection fixture uses synthetic DER with two SHA-1 signature-
algorithm OIDs, since .NET 10 deliberately refuses to generate a new
SHA-1 certificate. Its signature is not valid; the fixture exercises
metadata rejection only, never cryptographic signature validation. The check still requires a correct publisher and DER file.

The certificate parser now requires **byte-for-byte DER equality** between the
provided public .cer file and the decoded X.509 certificate. It therefore
rejects PEM encoding, container wrappers and trailing bytes rather than merely
checking the filename extension. Eight additional synthetic Windows tests
cover wrong encoding/extension, missing or excessive EKU, missing key usage,
missing basic constraints and invalid MSIX identity.

**Passing this is NOT proof of a trusted certificate or a valid signed
MSIX.** You must separately perform signing with secure key management,
verify the signed package and Windows machine trust, provide official offline
Windows App SDK x86 framework/runtime and .NET 10 x86 prerequisites, and
launch on an actual LTSC machine. Do not check private keys into the repo or
turn off Windows signing enforcement. The manifest development Publisher may
need to change for a production certificate, as an identity migration rather
than silently reusing the development package name.

Official references:
https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview
https://learn.microsoft.com/en-us/windows/msix/package/sign-msix-package-guide

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

Stable v0.2.3 was published on 2026-10-24 with the audited self-contained Windows x86 Portable ZIP and SHA256SUMS.txt; the release tag points to `91ecc34`. The historical v0.2.2 release remains available. Lite ZIP and unsigned MSIX artifacts are still experimental Development outputs, not Stable.

Official Windows App SDK deployment guidance: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview
