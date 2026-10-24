#requires -Version 5.1
<#
Read-only verification of the ACTUALLY DOWNLOADED unsigned Development
MSIX Actions artifact against the original build's bytes and its SHA-256.
No signing, trust-store changes, installation, package registration or UI.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArtifactDirectory,
    [Parameter(Mandatory)][string]$OriginalPackagePath
)
$ErrorActionPreference = 'Stop'
$packageName = 'Dvinholm-Lite-x86-UNSIGNED.msix'
$sidecarName = "$packageName.sha256"
$downloadDir = (Resolve-Path -LiteralPath $ArtifactDirectory -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $downloadDir -PathType Container)) {
    throw 'MSIX artifact roundtrip: downloaded artifact is not a directory.'
}
$entries = @(Get-ChildItem -LiteralPath $downloadDir -Force)
if (@($entries | Where-Object { $_.PSIsContainer }).Count -gt 0) {
    throw 'MSIX artifact roundtrip: unexpected nested directory.'
}
if ($entries.Count -ne 2) {
    throw 'MSIX artifact roundtrip: expected exactly two root files.'
}
$names = @($entries | ForEach-Object { $_.Name })
if (-not ($names -ccontains $packageName) -or
    -not ($names -ccontains $sidecarName)) {
    throw 'MSIX artifact roundtrip: unexpected uploaded filename.'
}
$package = Join-Path $downloadDir $packageName
$sidecar = Join-Path $downloadDir $sidecarName
if (-not (Test-Path -LiteralPath $package -PathType Leaf) -or
    -not (Test-Path -LiteralPath $sidecar -PathType Leaf)) {
    throw 'MSIX artifact roundtrip: package or checksum not a file.'
}
$original = (Resolve-Path -LiteralPath $OriginalPackagePath -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $original -PathType Leaf)) {
    throw 'MSIX artifact roundtrip: build original is not a file.'
}
if ([string]::Equals($original,$package,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'MSIX artifact roundtrip: original and download must be separate files.'
}
function Get-StreamSha256([string]$file) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    $inputStream = [IO.File]::OpenRead($file)
    try {
        [BitConverter]::ToString($algorithm.ComputeHash($inputStream)).Replace('-','').ToLowerInvariant()
    } finally {
        $inputStream.Dispose()
        $algorithm.Dispose()
    }
}
$buildHash = Get-StreamSha256 $original
$downloadHash = Get-StreamSha256 $package
if ($buildHash -cne $downloadHash) {
    throw 'MSIX artifact roundtrip: downloaded package SHA-256 differs from original build.'
}
& (Join-Path $PSScriptRoot 'Test-LiteMsixSha256.ps1') -PackagePath $package -ChecksumPath $sidecar -Mode Verify
Write-Host 'MSIX artifact roundtrip: exact two files verified; downloaded bytes match the original build and strict SHA-256 sidecar.'
Write-Host 'INTEGRITY-ONLY: does not prove code signing, publisher authenticity, LTSC installation or WinUI launch.'
