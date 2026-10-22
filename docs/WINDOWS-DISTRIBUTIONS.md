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

The read-only tool examines the actual Lite runtimeconfig file, queries the
x86 dotnet installation for .NET 10 Desktop/shared runtimes, and checks for
a current-user registered x86 Microsoft.WindowsAppRuntime.2.5 framework package.
It rejects x64-only packages and mismatched SDK lines. An optional pair of
fixture files exists for regression tests, always marked SIMULATED INPUT.

The tool does not install packages or launch WinUI. Microsoft Visual C++
Redistributable x86 may also be necessary for an unpackaged app. Passing the
inspection cannot confirm the entire Windows App SDK runtime package set,
runtime bootstrapping or whether the GUI opens. Install official prerequisites,
then test the extracted Lite Dvinholm.App.exe interactively on Windows.
Never combine Portable and Lite files in one folder.

Microsoft guidance:
https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps

## Distribution rules

The application is published for `win-x86` without Trimming or Native AOT. The self-contained Portable output stays complete, including native WinUI resources and .NET runtime files. Never delete arbitrary DLLs, JSON, PRI, MUI, WinMD, or locale folders to reduce the ZIP size.

Both downloads are packaged by `eng/Package-WindowsDistribution.ps1`. It omits only debugging byproducts (PDB, dumps, tracing/coverage files) and explicit debugger utilities. High ZIP Deflate compression is used when 7-Zip is available, with `Compress-Archive -CompressionLevel Optimal` as a compatible fallback. These are ordinary ZIP archives compatible with the built-in Dvinholm updater.

`eng/Test-WindowsDistribution.ps1` is a required packaging gate. It checks executable and icon, runtime manifests, no debugger payload, and verifies Portable has bundled .NET and Lite does **not**. CI also requires Lite ZIP to be smaller than Portable ZIP and writes the size comparison to the job summary. These tests do not replace a Windows launch test, especially for Lite.

## Stable releases

**Before any future Stable release is uploaded**, verify its final ZIP with `Test-WindowsDistribution.ps1`. Debugging symbols and tools are excluded from the Stable download, while CI retains test diagnostics. Release checksum (`SHA256SUMS.txt`) must be calculated **after** cleaning and compressing the final ZIP, so the checksum identifies exactly the published artifact.

The historical Stable release v0.2.2 is unchanged. New distribution changes and experimental Lite artifacts are CI outputs until a later stable release is explicitly prepared, tested and published.

Official Windows App SDK deployment guidance: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview
