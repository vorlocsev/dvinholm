#requires -Version 5.1
<#
  Create or verify a checksum manifest for the two exact Development ZIPs.
  Archives are never modified; this checks downloaded bytes, not code signing.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Directory,
    [switch]$Generate,
    [switch]$LiteOnly,
    [switch]$WithUpdaterBanner
)

$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 on minimal test hosts may not expose Get-FileHash.
# Use the built-in .NET Framework SHA-256 implementation without modules.
function Get-Sha256Hex {
    param([Parameter(Mandatory)][string]$Path)
    $stream = [IO.File]::OpenRead($Path)
    $hasher = [Security.Cryptography.SHA256]::Create()
    try {
        $digest = $hasher.ComputeHash($stream)
        return ([BitConverter]::ToString($digest) -replace '-', '').ToLowerInvariant()
    }
    finally {
        $hasher.Dispose()
        $stream.Dispose()
    }
}
$root = (Resolve-Path -LiteralPath $Directory -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Distribution directory does not exist: $root"
}
$names = if ($LiteOnly) { @('Dvinholm-Windows-x86-Lite.zip') } else { @('Dvinholm-Windows-x86.zip', 'Dvinholm-Windows-x86-Lite.zip') }
# Updater Banner is a standalone bootstrap/tool, NOT another app flavor.
# The Lite-only app policy remains unchanged.
if ($WithUpdaterBanner) {
    $names = @($names) + @('Dvinholm-Updater-Banner-Windows-x86.zip')
}
$manifest = Join-Path $root 'SHA256SUMS.txt'

if ($Generate) {
    # Resolve every ZIP before writing a new manifest. Never hash an Actions
    # upload-artifact wrapper or accidentally a similarly named file.
    $hashes = @()
    foreach ($name in $names) {
        $path = Join-Path $root $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing distribution ZIP: $name"
        }
        $hash = Get-Sha256Hex -Path $path
        $hashes += "$hash  $name"
    }
    [IO.File]::WriteAllLines($manifest, [string[]]$hashes, [Text.Encoding]::ASCII)
    Write-Host 'Generated SHA256SUMS.txt from the final Portable and Lite ZIP bytes.'
}

if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
    throw 'Missing SHA256SUMS.txt in distribution directory.'
}
$records = @([IO.File]::ReadAllLines($manifest))
if ($records.Count -ne $names.Count) {
    throw "SHA256SUMS.txt must contain exactly $($names.Count) entries."
}
$seen = [System.Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($record in $records) {
    if ($record -cnotmatch '^([0-9a-fA-F]{64})  (.+)$') {
        throw "Malformed SHA256SUMS.txt entry: $record"
    }
    $expectedHash = $Matches[1]
    $name = $Matches[2]
    if ($names -cnotcontains $name) {
        throw "Unexpected checksum filename: $name"
    }
    if (-not $seen.Add($name)) {
        throw "Duplicate checksum entry: $name"
    }
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing distribution ZIP: $name"
    }
    $actualHash = Get-Sha256Hex -Path $path
    if ($actualHash -ine $expectedHash) {
        throw "SHA-256 mismatch for $name. Download or packaging output is corrupt."
    }
    Write-Host "Verified SHA-256: $name"
}
if ($seen.Count -ne $names.Count) {
    throw 'SHA256SUMS.txt is missing a required distribution entry.'
}
Write-Host 'SHA-256 checksums verified for Portable and Lite.'
