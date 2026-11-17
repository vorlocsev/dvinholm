#requires -Version 5.1
<#
.SYNOPSIS
Review or install a local JPG vehicle render into the nine-model Aurora catalog.
.DESCRIPTION
Dry-run by default. -Apply stages and atomically replaces the JPG and manifest,
with rollback copies if either replacement fails. No network or vehicle I/O.
.EXAMPLE
.\eng\Install-VolvoVehicleArtwork.ps1 -ModelId s80-p2 -SourceJpg C:\Review\s80-p2.jpg
.\eng\Install-VolvoVehicleArtwork.ps1 -ModelId s80-p2 -SourceJpg C:\Review\s80-p2.jpg -Apply
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('xc90-p2','xc70-p2','v70-p2','s60-p2','s80-p2',
                 'xc90-spa','xc70-p3','v70-p3','s80-p3')]
    [string]$ModelId,
    [Parameter(Mandatory = $true)]
    [string]$SourceJpg,
    [switch]$Apply
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifestPath = Join-Path $repo 'assets\vehicles\manifest.json'
$destination = Join-Path $repo ("assets\vehicles\" + $ModelId + '.jpg')
$projectFile = Join-Path $repo 'src\Dvinholm.App\Dvinholm.App.csproj'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $projectFile -PathType Leaf) -or
    -not (Test-Path -LiteralPath $destination -PathType Leaf)) {
    throw 'This script must run from a Dvinholm source checkout with the full nine-image catalog.'
}
if (-not [IO.Path]::IsPathRooted($SourceJpg) -or $SourceJpg.StartsWith('\\')) {
    throw 'SourceJpg must be a local absolute filesystem path, not a UNC/network path.'
}
$source = [IO.Path]::GetFullPath($SourceJpg)
if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or
    ([IO.File]::GetAttributes($source) -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'SourceJpg must be an existing regular local file.'
}
if ([string]::Equals($source, [IO.Path]::GetFullPath($destination),
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The source and destination JPG must differ.'
}
$bytes = [IO.File]::ReadAllBytes($source)
if ($bytes.Length -lt 20000 -or $bytes.Length -gt 2000000 -or
    $bytes[0] -ne 255 -or $bytes[1] -ne 216 -or
    $bytes[$bytes.Length - 2] -ne 255 -or $bytes[$bytes.Length - 1] -ne 217) {
    throw 'JPG bytes must be a complete 20 KB to 2 MB JPEG.'
}
Add-Type -AssemblyName System.Drawing
$stream = [IO.MemoryStream]::new($bytes, $false)
$image = $null
try {
    $image = [Drawing.Image]::FromStream($stream, $false, $true)
    if ($image.RawFormat.Guid -ne [Drawing.Imaging.ImageFormat]::Jpeg.Guid -or
        $image.Width -ne 1280 -or $image.Height -ne 510) {
        throw 'Artwork must be a real 1280 x 510 JPEG.'
    }
}
finally {
    if ($null -ne $image) { $image.Dispose() }
    $stream.Dispose()
}
$rows = @((([IO.File]::ReadAllText($manifestPath,
    [Text.Encoding]::UTF8)) | ConvertFrom-Json))
if ($rows.Count -ne 9) { throw 'Expected nine immutable model records.' }
$matches = @($rows | Where-Object { $_.modelId -ceq $ModelId })
if ($matches.Count -ne 1 -or $matches[0].file -cne ($ModelId + '.jpg')) {
    throw 'Model and filename must match exactly one catalog record.'
}
$entry = $matches[0]
# Use .NET Framework-compatible SHA-256 APIs (Windows PowerShell 5.1).
$sha = [Security.Cryptography.SHA256]::Create()
try {
    $hash = ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
}
finally { $sha.Dispose() }
$preview = [pscustomobject]@{
    modelId = $ModelId
    expectedPixels = '1280x510'
    incomingBytes = $bytes.Length
    incomingSha256 = $hash
    previousSha256 = $entry.sha256
    applyRequested = [bool]$Apply
    applied = $false
}
if (-not $Apply) { Write-Output $preview; return }

$entry.bytes = $bytes.Length
$entry.width = 1280
$entry.height = 510
$entry.sha256 = $hash
$stamp = [Guid]::NewGuid().ToString('N')
$stagedJpg = $destination + '.' + $stamp + '.tmp'
$stagedManifest = $manifestPath + '.' + $stamp + '.tmp'
$jpgBackup = $destination + '.' + $stamp + '.bak'
$manifestBackup = $manifestPath + '.' + $stamp + '.bak'
$jpgReplaced = $false
$manifestReplaced = $false
$completed = $false
try {
    [IO.File]::WriteAllBytes($stagedJpg, $bytes)
    $newJson = (ConvertTo-Json -InputObject $rows -Depth 6) + [Environment]::NewLine
    [IO.File]::WriteAllText($stagedManifest, $newJson, [Text.UTF8Encoding]::new($false))
    $check = @(([IO.File]::ReadAllText($stagedManifest, [Text.Encoding]::UTF8) | ConvertFrom-Json))
    if ($check.Count -ne 9 -or
        @($check | Where-Object { $_.modelId -ceq $ModelId -and $_.sha256 -ceq $hash }).Count -ne 1) {
        throw 'Manifest staging did not round-trip exactly.'
    }
    [IO.File]::Replace($stagedJpg, $destination, $jpgBackup)
    $jpgReplaced = $true
    [IO.File]::Replace($stagedManifest, $manifestPath, $manifestBackup)
    $manifestReplaced = $true
    # A successful replace is still checked from the bytes now on disk.
    $verify = [Security.Cryptography.SHA256]::Create()
    try {
        $actualHash = ([BitConverter]::ToString($verify.ComputeHash(
            [IO.File]::ReadAllBytes($destination)))).Replace('-', '').ToLowerInvariant()
    }
    finally { $verify.Dispose() }
    if ($actualHash -cne $hash) { throw 'Post-replacement JPG digest mismatch.' }
    $completed = $true
    $preview.applied = $true
    Write-Output $preview
}
catch {
    if ($manifestReplaced -and [IO.File]::Exists($manifestBackup)) {
        [IO.File]::Replace($manifestBackup, $manifestPath, $null)
    }
    if ($jpgReplaced -and [IO.File]::Exists($jpgBackup)) {
        [IO.File]::Replace($jpgBackup, $destination, $null)
    }
    throw
}
finally {
    foreach ($path in @($stagedJpg, $stagedManifest)) {
        if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
    }
    if ($completed) {
        foreach ($path in @($jpgBackup, $manifestBackup)) {
            if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
        }
    }
}
