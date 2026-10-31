#requires -Version 5.1
<#
Configure the installed Visual Studio x86 MSVC developer environment and
compile the two native launchers in THIS PowerShell process. No JavaScript
action or environment export to other CI steps is needed. All setup is read-only.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)

$ErrorActionPreference = 'Stop'
$programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
if ([string]::IsNullOrWhiteSpace($programFilesX86)) {
    throw 'Cannot find Program Files (x86) on this Windows x64 runner.'
}

$vswhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
    throw 'Visual Studio installation locator is missing (vswhere.exe).'
}

$installations = @(& $vswhere -latest -products '*' -requires 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64' -property installationPath)
if ($LASTEXITCODE -ne 0 -or $installations.Count -ne 1 -or
    [string]::IsNullOrWhiteSpace($installations[0])) {
    throw 'No Visual Studio installation with the MSVC x86/x64 tools was found.'
}

$vsDevCmd = Join-Path $installations[0] 'Common7\Tools\VsDevCmd.bat'
if (-not (Test-Path -LiteralPath $vsDevCmd -PathType Leaf)) {
    throw 'Selected Visual Studio installation has no VsDevCmd.bat.'
}

# VsDevCmd is a batch file. Run it through cmd.exe and import only the
# compiler/SDK variables into this process, not arbitrary runner settings.
# The command emits "set" only on successful initialization.
$command = '"{0}" -no_logo -arch=x86 -host_arch=x64 >nul && set' -f $vsDevCmd
$environmentLines = @(& cmd.exe /d /c $command)
if ($LASTEXITCODE -ne 0) {
    throw 'VsDevCmd failed to initialize the x86 MSVC developer environment.'
}

$allowedVariables = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($name in @(
    'PATH', 'INCLUDE', 'LIB', 'LIBPATH', 'VSINSTALLDIR', 'VCINSTALLDIR',
    'VCToolsInstallDir', 'VCToolsVersion', 'VisualStudioVersion',
    'WindowsSdkDir', 'WindowsSDKVersion', 'UniversalCRTSdkDir', 'UCRTVersion',
    'VSCMD_ARG_TGT_ARCH', 'VSCMD_ARG_HOST_ARCH', 'VSCMD_VER'
)) {
    [void]$allowedVariables.Add($name)
}

$seen = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($line in $environmentLines) {
    if ($line -notmatch '^([^=]+)=(.*)$') { continue }
    $name = $Matches[1]
    if (-not $allowedVariables.Contains($name)) { continue }
    [Environment]::SetEnvironmentVariable($name, $Matches[2], 'Process')
    [void]$seen.Add($name)
}
foreach ($required in @('PATH', 'INCLUDE', 'LIB', 'LIBPATH',
    'VCToolsInstallDir', 'WindowsSdkDir', 'VSCMD_ARG_TGT_ARCH')) {
    if (-not $seen.Contains($required)) {
        throw "VsDevCmd did not export required compiler variable $required."
    }
}
if ($env:VSCMD_ARG_TGT_ARCH -ne 'x86' -or
    $env:VSCMD_ARG_HOST_ARCH -ne 'x64') {
    throw 'MSVC environment architecture mismatch (expected Hostx64/x86).'
}

$cl = Get-Command 'cl.exe' -ErrorAction Stop
$rc = Get-Command 'rc.exe' -ErrorAction Stop
if ($cl.Source -notmatch '[\\/]Hostx64[\\/]x86[\\/]cl\.exe$') {
    throw 'MSVC compiler is not the expected Hostx64/x86 toolchain.'
}
Write-Host 'Using installed MSVC Hostx64/x86 and Windows SDK resource compiler.'

& (Join-Path $PSScriptRoot 'Build-Launchers.ps1') -OutputDirectory $OutputDirectory
if ($LASTEXITCODE -ne 0) {
    throw 'Native x86 Launcher build returned a non-zero exit status.'
}
