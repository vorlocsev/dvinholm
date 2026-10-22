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
    [string]$VcRedistStateFile,
    [ValidateSet('X86','X64','Arm64')][string]$FixtureHostArchitecture = 'X64'
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

$options = (Get-Content -LiteralPath $manifest -Raw |
    ConvertFrom-Json -ErrorAction Stop).runtimeOptions
if ($null -eq $options) {
    throw 'Lite runtimeconfig.json does not have runtimeOptions.'
}
$references = @()
if ($null -ne $options.framework) { $references += $options.framework }
if ($null -ne $options.frameworks) { $references += @($options.frameworks) }
$references = @($references | Where-Object { $null -ne $_ })
if ($references.Count -eq 0) {
    throw 'Lite runtimeconfig.json has no .NET shared framework reference.'
}

# Never silently discard an unknown shared framework and claim readiness.
$seenFrameworks = [System.Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($reference in $references) {
    $name = [string]$reference.name
    if ($name -notin @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
        throw "Lite runtimeconfig contains an unsupported shared framework: $name."
    }
    if (-not $seenFrameworks.Add($name)) {
        throw "Lite runtimeconfig has a duplicate shared framework: $name."
    }
    $versionString = [string]$reference.version
    if ($versionString -notmatch '^10\.\d+\.\d+$') {
        throw "Lite requires a valid .NET 10 shared framework version for $name, got '$versionString'."
    }
    try { $null = [Version]$versionString }
    catch { throw "Lite shared framework version is invalid: $versionString." }
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
        Get-AppxPackage -Name '*AppRuntime*' -ErrorAction Stop |
            ForEach-Object { "$($_.Name)|$($_.Architecture)|$($_.Version)" }
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
# The presence of a higher version is NOT sufficient under all .NET
# rollForward modes: Disable demands exact; LatestPatch forbids minor roll.
# Reject policies that cannot be safely modeled rather than false-certify.
if ($null -ne $options.applyPatches -and $options.applyPatches -eq $false) {
    throw 'Lite preflight cannot certify runtimeconfig applyPatches=false; use default patch servicing.'
}
foreach ($reference in $references) {
    if ($null -ne $reference.applyPatches -and $reference.applyPatches -eq $false) {
        throw "Lite preflight cannot certify applyPatches=false for $($reference.name)."
    }

    $minimum = [Version]([string]$reference.version)
    $policy = 'Minor'
    if ($null -ne $options.rollForward) {
        $policy = [string]$options.rollForward
    }
    if ($null -ne $reference.rollForward) {
        $policy = [string]$reference.rollForward
    }
    if ($policy -notin @('Minor', 'LatestMinor', 'LatestPatch', 'Disable')) {
        throw "Lite preflight cannot certify .NET rollForward '$policy' for $($reference.name)."
    }

    $compatible = @($installed | Where-Object {
        if ($_.Name -ne $reference.name -or $_.Version.Major -ne 10) {
            return $false
        }
        # Do not use switch here: PowerShell overwrites $_ with the
        # switched policy string, losing the installed runtime object.
        if ($policy -eq 'Disable') {
            return $_.Version -eq $minimum
        }
        if ($policy -eq 'LatestPatch') {
            return ($_.Version.Minor -eq $minimum.Minor -and
                $_.Version -ge $minimum)
        }
        return $_.Version -ge $minimum
    })
    if ($compatible.Count -eq 0) {
        throw "Missing .NET 10 x86 shared runtime $($reference.name) for requested $minimum with rollForward=$policy."
    }
}

# SDK 2.5.1 Stable uses Microsoft.WindowsAppRuntime.2 (major only).
$sdkMajor = [int]($WindowsAppSdkLine.Split('.')[0])
$sdkMinor = [int]($WindowsAppSdkLine.Split('.')[1])
$minimumSdkVersion = [Version]("$WindowsAppSdkLine.0.0")
# Singleton is one backwards-compatible package across App SDK releases.
# For App SDK 2.x it encodes SDK major+8000 as its MSIX version major.
# Checking only name/architecture would falsely accept an older Singleton.
$minimumSingletonVersion = [Version]("{0}.{1}.0.0" -f (8000 + $sdkMajor), $sdkMinor)
if ($fixtureMode) {
    $hostArchitecture = $FixtureHostArchitecture
}
else {
    if ($PSBoundParameters.ContainsKey('FixtureHostArchitecture')) {
        throw 'FixtureHostArchitecture is only allowed with three fixture files.'
    }
    $hostArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
}
if ($hostArchitecture -notin @('X86','X64','Arm64')) {
    throw "Unsupported host architecture $hostArchitecture."
}
$packages = @()
foreach ($line in $packageLines) {
    $parts = ([string]$line).Trim() -split '\|', 3
    if ($parts.Count -ne 3) { continue }
    try { $version = [Version]$parts[2] } catch { continue }
    $packages += [pscustomobject]@{
        Name = $parts[0].Trim()
        Architecture = $parts[1].Trim()
        Version = $version
    }
}
function Assert-SdkPackage {
    param([string]$Name,[string]$Architecture,[Version]$MinimumVersion)
    $matching = @($packages | Where-Object {
        $_.Name -eq $Name -and $_.Architecture -eq $Architecture -and
        ($null -eq $MinimumVersion -or $_.Version -ge $MinimumVersion)
    })
    if ($matching.Count -eq 0) {
        throw "Missing Windows App SDK package $Name for $Architecture, minimum version $MinimumVersion."
    }
}
Assert-SdkPackage -Name "Microsoft.WindowsAppRuntime.$sdkMajor" -Architecture 'X86' -MinimumVersion $minimumSdkVersion
Assert-SdkPackage -Name "MicrosoftCorporationII.WinAppRuntime.Main.$sdkMajor" -Architecture $hostArchitecture -MinimumVersion $minimumSdkVersion
Assert-SdkPackage -Name 'MicrosoftCorporationII.WinAppRuntime.Singleton' -Architecture $hostArchitecture -MinimumVersion $minimumSingletonVersion
$ddlm = @($packages | Where-Object {
    $m = [regex]::Match($_.Name,'^Microsoft\.WinAppRuntime\.DDLM\.(\d+\.\d+\.\d+\.\d+)-x8$')
    $m.Success -and $_.Architecture -eq 'X86' -and
    [Version]$m.Groups[1].Value -ge $minimumSdkVersion -and
    $_.Version -ge $minimumSdkVersion
})
if ($ddlm.Count -eq 0) {
    throw "Missing Windows App SDK DDLM x86 package matching release $WindowsAppSdkLine."
}
Write-Host "Verified Framework x86, Main/Singleton $hostArchitecture and DDLM x86."

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
Write-Host 'Lite .NET 10 x86 and Windows App SDK x86 prerequisite inspection passed.'
Write-Host 'This is NOT a WinUI launch test; actual GUI launch and SDK bootstrap remain to verify.'
