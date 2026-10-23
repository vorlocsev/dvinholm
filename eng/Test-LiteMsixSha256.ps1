#requires -Version 5.1
<#
Generate or check SHA-256 sidecar for the exact unsigned Development MSIX.
A digest detects accidental corruption, NOT substitution or publisher trust.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$ChecksumPath,
    [Parameter(Mandatory)][ValidateSet('Write','Verify')][string]$Mode
)
$ErrorActionPreference = 'Stop'
$name = 'Dvinholm-Lite-x86-UNSIGNED.msix'
$hashName = "$name.sha256"
if ([IO.Path]::GetFileName($PackagePath) -cne $name) {
    throw "MSIX checksum: expected exactly $name."
}
if ([IO.Path]::GetFileName($ChecksumPath) -cne $hashName) {
    throw "MSIX checksum: expected exactly $hashName."
}
$package = (Resolve-Path -LiteralPath $PackagePath -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $package -PathType Leaf)) {
    throw 'MSIX checksum: package is not a file.'
}
$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hash -cnotmatch '^[0-9a-f]{64}$') {
    throw 'MSIX checksum: computed SHA-256 digest is invalid.'
}
if ($Mode -eq 'Write') {
    $output = [IO.Path]::GetFullPath($ChecksumPath)
    [IO.File]::WriteAllText($output, "$hash  $name" + [Environment]::NewLine, [Text.Encoding]::ASCII)
    Write-Host "MSIX checksum: wrote SHA-256 sidecar for $name."
} else {
    $checksum = (Resolve-Path -LiteralPath $ChecksumPath -ErrorAction Stop).Path
    if (-not (Test-Path -LiteralPath $checksum -PathType Leaf)) {
        throw 'MSIX checksum: manifest is not a file.'
    }
    [byte[]]$bytes = [IO.File]::ReadAllBytes($checksum)
    if (@($bytes | Where-Object { $_ -gt 127 }).Count -gt 0) {
        throw 'MSIX checksum: manifest must be ASCII.'
    }
    $contents = [Text.Encoding]::ASCII.GetString($bytes)
    $format = '\A([0-9a-f]{64})  Dvinholm-Lite-x86-UNSIGNED[.]msix\r?\n\z'
    $matched = [regex]::Match($contents, $format)
    if (-not $matched.Success) {
        throw 'MSIX checksum: expected one lowercase SHA-256 entry for the exact unsigned filename.'
    }
    if ($matched.Groups[1].Value -cne $hash) {
        throw 'MSIX checksum: SHA-256 mismatch for unsigned package.'
    }
    Write-Host "MSIX checksum: verified SHA-256 for $name."
}
