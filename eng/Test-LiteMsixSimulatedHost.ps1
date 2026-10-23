#requires -Version 5.1
<#
 CI-only gate. Inspect a freshly built UNSIGNED Lite MSIX against synthetic
 x86 .NET 10/Desktop and Windows App SDK 2.5.1 registration. No host
 AppX registration, installs, certificate changes, network, or WinUI.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackagePath -ErrorAction Stop).Path
$temp = Join-Path ([IO.Path]::GetTempPath()) ('Dvinholm-msix-fixture-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null
try {
    $runtimes = Join-Path $temp 'runtimes.txt'
    $frameworks = Join-Path $temp 'frameworks.txt'
    @(
        'Microsoft.NETCore.App 10.0.2 [C:\synthetic-dotnet-x86\shared\Microsoft.NETCore.App]'
        'Microsoft.WindowsDesktop.App 10.0.2 [C:\synthetic-dotnet-x86\shared\Microsoft.WindowsDesktop.App]'
    ) | Set-Content -LiteralPath $runtimes -Encoding UTF8
    @(
        'Microsoft.WindowsAppRuntime.2|X86|2.5.1.0|CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US'
    ) | Set-Content -LiteralPath $frameworks -Encoding UTF8
    Write-Host 'Checking actual built MSIX against SIMULATED Windows x86 host (never a real host readiness test).'
    & (Join-Path $PSScriptRoot 'Test-LiteMsixOfflinePrerequisites.ps1') -PackagePath $package -RuntimeListFile $runtimes -FrameworkPackageListFile $frameworks
    Write-Host 'Actual MSIX package metadata matches the synthetic offline dependency inventory.'
} finally {
    Remove-Item -LiteralPath $temp -Force -Recurse -ErrorAction SilentlyContinue
}
