# Windows distributions

Dvinholm is a WinUI 3 Windows **x86** application. The main distribution remains a no-install Portable ZIP. The optional **Lite** ZIP is a framework-dependent alternative for machines whose runtimes are already installed.

| Variant | ZIP | Runtime dependencies | Automatic updater |
|---|---|---|---|
| Portable (default) | `Dvinholm-Windows-x86.zip` | Bundles .NET and Windows App SDK runtime | Supported; existing updater selects this exact asset name |
| Lite (experimental CI) | `Dvinholm-Windows-x86-Lite.zip` | Requires .NET **10 Desktop Runtime x86** and the **Windows App SDK runtime** compatible with the build | **Not supported** until separately validated |

Lite is **not** an independent stable release just because CI publishes a prerelease artifact. Install prerequisites before launching Lite on Windows. It uses the same application and J2534 backend, but its Windows App SDK bootstrap/runtime dependencies still need a real Windows launch smoke test. Never mix Portable and Lite DLLs in the same installation folder.

## Lite host prerequisite inspection (Development)

Extract the experimental Lite ZIP into its own directory. For the current
Windows App SDK NuGet reference 2.5.1, run the following in Windows PowerShell
5.1 from a repository checkout (adjust the Lite folder):

    .\eng\Test-LitePrerequisites.ps1 -LiteDirectory 'C:\Dvinholm-Lite' -WindowsAppSdkLine '2.5'

The read-only tool validates **every** declared Lite runtimeconfig .NET shared
framework before querying the x86 dotnet installation for .NET 10
Desktop/shared runtimes. Unknown, duplicate, malformed or non-10.x .NET
framework declarations fail closed. It also checks for
all four current-user Windows App SDK runtime packages: Framework x86
(Microsoft.WindowsAppRuntime.2), Main (MicrosoftCorporationII.WinAppRuntime.Main.2),
Singleton (MicrosoftCorporationII.WinAppRuntime.Singleton), and DDLM x86
(Microsoft.WinAppRuntime.DDLM.2.5.*-x8). Stable 2.x uses the major version
in Framework/Main names, not the SDK minor version. On x64 Windows,
Main/Singleton are x64 while this app needs x86 Framework/DDLM.
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

## Distribution rules

The application is published for `win-x86` without Trimming or Native AOT. The self-contained Portable output stays complete, including native WinUI resources and .NET runtime files. Never delete arbitrary DLLs, JSON, PRI, MUI, WinMD, or locale folders to reduce the ZIP size.

Both downloads are packaged by `eng/Package-WindowsDistribution.ps1`. It omits only debugging byproducts (PDB, dumps, tracing/coverage files) and explicit debugger utilities. High ZIP Deflate compression is used when 7-Zip is available, with `Compress-Archive -CompressionLevel Optimal` as a compatible fallback. These are ordinary ZIP archives compatible with the built-in Dvinholm updater.

`eng/Test-WindowsDistribution.ps1` is a required packaging gate. It checks executable and icon, runtime manifests, no debugger payload, and verifies Portable has bundled .NET and Lite does **not**. CI also requires Lite ZIP to be smaller than Portable ZIP and writes the size comparison to the job summary. These tests do not replace a Windows launch test, especially for Lite.

## Stable releases

**Before any future Stable release is uploaded**, verify its final ZIP with `Test-WindowsDistribution.ps1`. Debugging symbols and tools are excluded from the Stable download, while CI retains test diagnostics. Release checksum (`SHA256SUMS.txt`) must be calculated **after** cleaning and compressing the final ZIP, so the checksum identifies exactly the published artifact.

The historical Stable release v0.2.2 is unchanged. New distribution changes and experimental Lite artifacts are CI outputs until a later stable release is explicitly prepared, tested and published.

Official Windows App SDK deployment guidance: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview
