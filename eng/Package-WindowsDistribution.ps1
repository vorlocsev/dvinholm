#requires -Version 5.1
<#
.SYNOPSIS
  Builds a clean distribution ZIP without diagnostic symbols or debug tools.
.DESCRIPTION
  Packages the complete publish tree (no runtime stripping, no trimming),
  omitting only explicit debug byproducts. Keeps the updater-compatible ZIP
  root layout and uses high-compression ZIP Deflate when 7-Zip is available.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Portable', 'Lite')][string]$Flavor,
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$ZipPath
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $PublishDirectory -ErrorAction Stop).Path
$archive = [IO.Path]::GetFullPath($ZipPath)
if (-not (Test-Path -LiteralPath (Join-Path $source 'Dvinholm.App.exe') -PathType Leaf)) {
    throw "$Flavor publish does not include Dvinholm.App.exe"
}
$parent = Split-Path -Path $archive -Parent
New-Item -ItemType Directory -Path $parent -Force | Out-Null

$temp = Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null
try {
    Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $temp -Recurse -Force

    # Package a read-only prerequisite checker in Lite itself. Its source is
    # pinned to this repository and copied only after publishing the app.
    if ($Flavor -eq 'Lite') {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Test-LitePrerequisites.ps1') -Destination (Join-Path $temp 'Check-Lite-Prerequisites.ps1') -Force
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Check-Lite-Prerequisites.cmd') -Destination (Join-Path $temp 'Check-Lite-Prerequisites.cmd') -Force
    }

    # PDBs and diagnostic helpers are not required to run a Release build.
    # Do not remove arbitrary DLL, EXE, .json, .pri or resource files here.
    $debugExtensions = @('.pdb', '.mdb', '.dbg', '.ilk', '.iobj', '.ipch',
                         '.map', '.dmp', '.mdmp', '.etl', '.binlog',
                         '.trx', '.coverage', '.coveragexml')
    $debugTools = @('vsdbg.exe', 'windbg.exe', 'cdb.exe', 'ntsd.exe',
                    'dotnet-dump.exe', 'dotnet-trace.exe',
                    'dotnet-counters.exe', 'dotnet-monitor.exe',
                    'createdump.exe')
    $removed = @(
        Get-ChildItem -LiteralPath $temp -File -Recurse -Force |
            Where-Object {
                ($debugExtensions -contains $_.Extension.ToLowerInvariant()) -or
                ($debugTools -contains $_.Name.ToLowerInvariant())
            }
    )
    foreach ($file in $removed) {
        Remove-Item -LiteralPath $file.FullName -Force
    }

    if (-not (Test-Path -LiteralPath (Join-Path $temp 'Assets\Dvinholm.ico') -PathType Leaf)) {
        throw "$Flavor publish does not include the branded icon."
    }

    if (Test-Path -LiteralPath $archive) {
        Remove-Item -LiteralPath $archive -Force
    }
    $sevenZip = $null
    $cmd = Get-Command '7z.exe' -ErrorAction SilentlyContinue
    if ($cmd) {
        $sevenZip = $cmd.Source
    } elseif (Test-Path -LiteralPath 'C:\Program Files\7-Zip\7z.exe') {
        $sevenZip = 'C:\Program Files\7-Zip\7z.exe'
    }

    Push-Location $temp
    try {
        if ($sevenZip) {
            & $sevenZip a -tzip -mm=Deflate -mx=9 -mfb=258 -mpass=15 -y $archive '*' | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "7-Zip failed with exit code $LASTEXITCODE." }
        } else {
            Compress-Archive -Path '*' -DestinationPath $archive -CompressionLevel Optimal
        }
    }
    finally { Pop-Location }

    $unpackedBytes = (Get-ChildItem -LiteralPath $temp -Recurse -File |
        Measure-Object -Property Length -Sum).Sum
    $zipBytes = (Get-Item -LiteralPath $archive).Length
    Write-Host "$Flavor ZIP: $zipBytes bytes; uncompressed payload: $unpackedBytes bytes"
    Write-Host "Excluded $($removed.Count) debugging byproducts/tools: $($removed.Name -join ', ')"
}
finally {
    if (Test-Path -LiteralPath $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force
    }
}
