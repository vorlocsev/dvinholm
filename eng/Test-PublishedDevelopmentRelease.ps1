#requires -Version 5.1
<#
  Read-only audit of public Development CI release assets after GitHub upload.
  Requires separately downloaded GitHub API metadata and all three assets.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReleaseMetadataFile,
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string]$ExpectedTag,
    [Parameter(Mandatory)][string]$ExpectedCommit,
    [switch]$LiteOnly
)

$ErrorActionPreference = 'Stop'
# Use .NET directly: minimal Windows PowerShell 5.1 test hosts may not
# provide Get-FileHash. Stream files to avoid loading ~60 MB ZIPs in memory.
function Get-Sha256Hex {
    param([Parameter(Mandatory)][string]$Path)
    $stream = [IO.File]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $digest = $sha.ComputeHash($stream)
        return ([BitConverter]::ToString($digest) -replace '-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
        $stream.Dispose()
    }
}
$metadataPath = (Resolve-Path -LiteralPath $ReleaseMetadataFile -ErrorAction Stop).Path
$root = (Resolve-Path -LiteralPath $Directory -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Downloaded release directory does not exist: $root"
}
try { $release = Get-Content -LiteralPath $metadataPath -Raw -ErrorAction Stop |
        ConvertFrom-Json -ErrorAction Stop }
catch { throw 'Published CI release metadata is invalid JSON.' }
if ($release.tag_name -cne $ExpectedTag) {
    throw "Published CI release tag mismatch; expected $ExpectedTag."
}
if ([string]$release.target_commitish -cne $ExpectedCommit) {
    throw 'Published CI release points to the wrong commit.'
}
if ($release.prerelease -ne $true -or $release.draft -ne $false) {
    throw 'Published CI release must be a visible prerelease.'
}

$names = if ($LiteOnly) { @('Dvinholm-Windows-x86-Lite.zip', 'SHA256SUMS.txt') } else { @('Dvinholm-Windows-x86.zip', 'Dvinholm-Windows-x86-Lite.zip', 'SHA256SUMS.txt') }
$assetCountWord = if ($LiteOnly) { 'two' } else { 'three' }
$assets = @($release.assets | Where-Object { $null -ne $_ })
if ($assets.Count -ne $names.Count) {
    throw "Published CI release must contain exactly $assetCountWord assets (found $($assets.Count))."
}
$seen = [System.Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($asset in $assets) {
    $name = [string]$asset.name
    if (-not $seen.Add($name)) {
        throw "Duplicate release asset: $name"
    }
    if ($names -cnotcontains $name) {
        throw "Unexpected published release asset: $name"
    }
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Published CI asset not downloaded: $name"
    }
    try { $size = [long]$asset.size }
    catch { throw "Invalid published asset size: $name" }
    if ($size -le 0 -or $size -ne (Get-Item -LiteralPath $path).Length) {
        throw "Published asset size mismatch: $name"
    }
    if ([string]$asset.state -cne 'uploaded') {
        throw "GitHub release asset is not uploaded: $name"
    }
    # GitHub's REST asset digest is server-provided, independent of the
    # SHA256SUMS.txt asset. Validate all three: both ZIPs and the manifest.
    $apiDigest = [string]$asset.digest
    if ($apiDigest -cnotmatch '^sha256:([0-9a-f]{64})$') {
        throw "Missing or unsupported GitHub asset SHA-256 digest: $name"
    }
    $expected = $Matches[1]
    $actual = Get-Sha256Hex -Path $path
    if ($actual -cne $expected) {
        throw "GitHub asset SHA-256 mismatch: $name"
    }
}
if ($seen.Count -ne $names.Count) {
    throw 'Published CI release is missing an expected asset.'
}

# Check the uploaded manifest against downloaded bytes, without
# regenerating it. The same verifier checks pre-upload files in CI.
& (Join-Path $PSScriptRoot 'Test-WindowsDistributionChecksums.ps1') -Directory $root -LiteOnly:$LiteOnly
if (-not $?) { throw 'Published CI checksum verification failed.' }
Write-Host "Verified three uploaded CI release assets and GitHub SHA-256 digests for $ExpectedTag at $ExpectedCommit."
