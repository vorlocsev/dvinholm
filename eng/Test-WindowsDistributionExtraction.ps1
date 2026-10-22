#requires -Version 5.1
<#
  Smoke-check an actual Portable or Lite ZIP after extraction in a disposable
  directory. This verifies x86 apphost and layout; it does not start WinUI.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Portable','Lite')][string]$Flavor,
    [Parameter(Mandatory)][string]$ZipPath
)

$ErrorActionPreference = 'Stop'
$zipFile = (Resolve-Path -LiteralPath $ZipPath -ErrorAction Stop).Path
$preflight = Join-Path $PSScriptRoot 'Test-WindowsDistribution.ps1'
& $preflight -Flavor $Flavor -ZipPath $zipFile
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "$Flavor ZIP did not pass the existing archive preflight."
}

Add-Type -AssemblyName System.IO.Compression
if (-not ('System.IO.Compression.ZipFile' -as [type])) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
}

$folder = Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-extract-' + [Guid]::NewGuid().ToString('N'))
try {
    [IO.Compression.ZipFile]::ExtractToDirectory($zipFile, $folder)

    foreach ($name in @('Dvinholm.App.exe', 'Dvinholm.App.dll',
                         'Dvinholm.App.deps.json',
                         'Dvinholm.App.runtimeconfig.json',
                         'Assets\Dvinholm.ico')) {
        if (-not (Test-Path -LiteralPath (Join-Path $folder $name) -PathType Leaf)) {
            throw "$Flavor extracted ZIP is missing $name."
        }
    }

    # Verify the actual Windows executable is an x86 PE32/PE32+ image.
    # We deliberately inspect the apphost rather than assuming the ZIP
    # filename proves processor architecture.
    $exe = [IO.File]::ReadAllBytes((Join-Path $folder 'Dvinholm.App.exe'))
    if ($exe.Length -lt 128 -or
        $exe[0] -ne 0x4D -or $exe[1] -ne 0x5A) {
        throw "$Flavor executable does not have a valid MZ header."
    }
    $peOffset = [BitConverter]::ToInt32($exe, 0x3C)
    if ($peOffset -lt 64 -or $peOffset -gt $exe.Length - 24 -or
        $exe[$peOffset] -ne 0x50 -or
        $exe[$peOffset + 1] -ne 0x45 -or
        $exe[$peOffset + 2] -ne 0 -or
        $exe[$peOffset + 3] -ne 0) {
        throw "$Flavor executable does not have a valid PE signature."
    }
    $machine = [BitConverter]::ToUInt16($exe, $peOffset + 4)
    if ($machine -ne 0x014C) {
        throw "$Flavor executable architecture is not Windows x86 (PE machine $machine)."
    }

    $core = Test-Path -LiteralPath (Join-Path $folder 'coreclr.dll') -PathType Leaf
    $hostFxr = Test-Path -LiteralPath (Join-Path $folder 'hostfxr.dll') -PathType Leaf
    if ($Flavor -eq 'Portable' -and (-not $core -or -not $hostFxr)) {
        throw 'Portable extraction is missing its bundled runtime.'
    }
    if ($Flavor -eq 'Lite' -and ($core -or $hostFxr)) {
        throw 'Lite extraction unexpectedly carries a bundled runtime.'
    }

    $runtimeJson = Get-Content -LiteralPath (Join-Path $folder 'Dvinholm.App.runtimeconfig.json') -Raw |
        ConvertFrom-Json -ErrorAction Stop
    if ($Flavor -eq 'Lite') {
        $frameworks = @($runtimeJson.runtimeOptions.framework) +
            @($runtimeJson.runtimeOptions.frameworks)
        if (@($frameworks | Where-Object {
                $null -ne $_ -and
                $_.name -in @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App') -and
                $_.version -match '^10\.'
            }).Count -eq 0) {
            throw 'Lite extraction does not declare a compatible .NET 10 shared framework.'
        }
    }

    Write-Host "Verified extracted $Flavor x86 PE, WinUI app payload and runtime contract."
}
finally {
    if (Test-Path -LiteralPath $folder) {
        Remove-Item -LiteralPath $folder -Recurse -Force
    }
}
