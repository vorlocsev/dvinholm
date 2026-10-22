#requires -Version 5.1
<#
  Read-only prerequisite inspection for an unpackaged, framework-dependent
  Windows x86 Lite build. No WinUI is launched, no packages are changed.
  All three fixture files together explicitly enter SIMULATED mode for CI tests.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$LiteDirectory,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+$')][string]$WindowsAppSdkLine,
    [string]$RuntimeListFile,
    [string]$AppSdkPackageListFile,
    [string]$VcRedistStateFile
)

$ErrorActionPreference = 'Stop'
# No mixing of fake runtime lists with a real machine's VC++/AppX state.
$fixtureArguments = @($RuntimeListFile, $AppSdkPackageListFile, $VcRedistStateFile)
$fixtureCount = @($fixtureArguments | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_)
}).Count
if ($fixtureCount -ne 0 -and $fixtureCount -ne 3) {
    throw 'Supply all three fixture files, or none for a real Windows host.'
}
$fixtureMode = ($fixtureCount -eq 3)
if (-not (Test-Path -LiteralPath $LiteDirectory -PathType Container)) {
    throw "Lite directory does not exist: $LiteDirectory"
}
$root = (Resolve-Path -LiteralPath $LiteDirectory).Path
$manifest = Join-Path $root 'Dvinholm.App.runtimeconfig.json'
if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
    throw 'Lite runtimeconfig.json is missing.'
}
if ((Test-Path -LiteralPath (Join-Path $root 'coreclr.dll')) -or
    (Test-Path -LiteralPath (Join-Path $root 'hostfxr.dll'))) {
    throw 'Selected directory contains bundled .NET files; use Lite, not Portable.'
}

$options = (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json -ErrorAction Stop).runtimeOptions
$references = @()
if ($null -ne $options.framework) { $references += $options.framework }
if ($null -ne $options.frameworks) { $references += @($options.frameworks) }
$references = @($references | Where-Object {
    $null -ne $_ -and $_.name -in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')
})
if ($references.Count -eq 0) {
    throw 'Lite runtimeconfig.json has no recognized .NET shared framework reference.'
}
foreach ($reference in $references) {
    if ([string]$reference.version -notmatch '^10\.') {
        throw "Lite requires .NET 10, but runtimeconfig requests $($reference.version)."
    }
}

# Explicitly inspect Desktop x86 even when runtimeconfig references Core only.
if (@($references | Where-Object { $_.name -eq 'Microsoft.WindowsDesktop.App' }).Count -eq 0) {
    $references += [pscustomobject]@{ name = 'Microsoft.WindowsDesktop.App'; version = '10.0.0' }
}

if ($fixtureMode) {
    Write-Host 'SIMULATED INPUT: fixtures do not validate this Windows machine.'
    $runtimeLines = @(Get-Content -LiteralPath $RuntimeListFile -ErrorAction Stop)
    $packageLines = @(Get-Content -LiteralPath $AppSdkPackageListFile -ErrorAction Stop)
    # One simulated record: Installed|Major|Version, always describing x86.
    $vcRecords = @(Get-Content -LiteralPath $VcRedistStateFile -ErrorAction Stop)
}
else {
    if ($PSVersionTable.PSEdition -eq 'Core' -and
        -not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::Windows)) {
        throw 'This prerequisite inspection must run on Windows.'
    }
    $programFilesX86 = if ([Environment]::Is64BitOperatingSystem) {
        [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    } else {
        [Environment]::GetEnvironmentVariable('ProgramFiles')
    }
    if ([string]::IsNullOrWhiteSpace($programFilesX86)) {
        throw 'Cannot locate the Windows x86 Program Files directory.'
    }
    $dotnetExe = Join-Path $programFilesX86 'dotnet\dotnet.exe'
    if (-not (Test-Path -LiteralPath $dotnetExe -PathType Leaf)) {
        throw "Missing .NET 10 Desktop Runtime x86: no x86 dotnet host at $dotnetExe"
    }
    $runtimeLines = @(& $dotnetExe --list-runtimes 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'Failed to inspect installed .NET x86 runtimes.' }
    $packageLines = @(
        Get-AppxPackage -Name "Microsoft.WindowsAppRuntime.$WindowsAppSdkLine" -ErrorAction Stop |
            ForEach-Object { "$($_.Name)|$($_.Architecture)" }
    )

    # Read both registry views because 32-bit and 64-bit installers can place
    # the x86 key differently. Never infer x86 from an installed x64 redist.
    $vcRecords = @()
    $views = @([Microsoft.Win32.RegistryView]::Registry32)
    if ([Environment]::Is64BitOperatingSystem) {
        $views += [Microsoft.Win32.RegistryView]::Registry64
    }
    foreach ($view in $views) {
        $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
            [Microsoft.Win32.RegistryHive]::LocalMachine, $view)
        try {
            $key = $baseKey.OpenSubKey('SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86')
            if ($null -ne $key) {
                try {
                    $vcRecords += "$($key.GetValue('Installed', 0))|$($key.GetValue('Major', 0))|$($key.GetValue('Version', ''))"
                }
                finally { $key.Dispose() }
            }
        }
        finally { $baseKey.Dispose() }
    }
}

$installed = @()
foreach ($line in $runtimeLines) {
    if ([string]$line -match '^\s*([A-Za-z0-9.]+)\s+(\d+\.\d+\.\d+)\s+\[.+\]\s*$') {
        $installed += [pscustomobject]@{ Name = $Matches[1]; Version = [Version]$Matches[2] }
    }
}
foreach ($reference in $references) {
    $minimum = [Version]([string]$reference.version)
    $compatible = @($installed | Where-Object {
        $_.Name -eq $reference.name -and $_.Version.Major -eq 10 -and $_.Version -ge $minimum
    })
    if ($compatible.Count -eq 0) {
        throw "Missing .NET 10 x86 shared runtime $($reference.name) >= $minimum."
    }
}

$requiredPackage = "Microsoft.WindowsAppRuntime.$WindowsAppSdkLine"
$foundX86 = $false
foreach ($line in $packageLines) {
    $parts = ([string]$line).Trim() -split '\|', 2
    if ($parts.Count -eq 2 -and $parts[0] -eq $requiredPackage -and $parts[1] -eq 'X86') {
        $foundX86 = $true
    }
}
if (-not $foundX86) {
    throw "Missing registered Windows App SDK x86 framework package $requiredPackage."
}
Write-Host 'Lite .NET 10 x86 and Windows App SDK x86 prerequisite inspection passed.'
Write-Host 'This is NOT a WinUI launch test; actual GUI launch and SDK bootstrap remain to verify.'

# Official Microsoft guidance requires VC++ Redistributable for unpackaged
# Windows App SDK apps. The 14.x runtime family covers VS 2015 and later.
# Report only the x86 registry record, irrespective of PowerShell bitness.
function Test-VcRedistX86Record {
    param([string]$Record)
    $fields = $Record.Trim() -split '\|', 3
    if ($fields.Count -ne 3 -or $fields[0] -ne '1') { return $false }
    $major = 0
    if (-not [int]::TryParse($fields[1], [ref]$major) -or $major -lt 14) {
        return $false
    }
    if ($fields[2] -notmatch '^v?([0-9]+)\.[0-9]+\.[0-9]+(?:\.[0-9]+)?$') {
        return $false
    }
    return ([int]$Matches[1] -eq $major)
}

$foundVcX86 = $false
foreach ($record in $vcRecords) {
    if (Test-VcRedistX86Record -Record ([string]$record)) {
        $foundVcX86 = $true
        break
    }
}
if (-not $foundVcX86) {
    throw 'Missing or invalid Microsoft Visual C++ Redistributable x86 (14.x or later). Install the official VC_redist.x86.exe and rerun the check.'
}
Write-Host 'Verified registered Microsoft Visual C++ Redistributable x86 (14.x or later).'
