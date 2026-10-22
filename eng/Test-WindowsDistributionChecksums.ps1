#requires -Version 5.1
<#
  Create or verify a checksum manifest for the two exact Development ZIPs.
  Archives are never modified; this checks downloaded bytes, not code signing.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Directory,
    [switch]$Generate
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $Directory -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Distribution directory does not exist: $root"
}
$names = @('Dvinholm-Windows-x86.zip', 'Dvinholm-Windows-x86-Lite.zip')
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
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
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
    $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($actualHash -ine $expectedHash) {
        throw "SHA-256 mismatch for $name. Download or packaging output is corrupt."
    }
    Write-Host "Verified SHA-256: $name"
}
if ($seen.Count -ne $names.Count) {
    throw 'SHA256SUMS.txt is missing a required distribution entry.'
}
Write-Host 'SHA-256 checksums verified for Portable and Lite.'
