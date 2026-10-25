#requires -Version 5.1
<#
Entry point called ONLY by START-STABLE-ACCEPTANCE-024.cmd from the
read-only Stable staging acceptance kit. The sole source of commit identity
is the exact kit receipt file, validated before passing it to another script.
PrepareOnly must never launch a GUI, modify an installation or publish.
#>
[CmdletBinding()]
param([switch]$PrepareOnly)
$ErrorActionPreference = 'Stop'
$kit = (Resolve-Path -LiteralPath (Split-Path -Path $PSScriptRoot -Parent)).Path
$parent = Split-Path -Path $kit -Parent
$provenance = Join-Path $kit 'BUILD_COMMIT_024_STABLE.txt'
if (-not (Test-Path -LiteralPath $provenance -PathType Leaf)) {
    throw 'Missing BUILD_COMMIT_024_STABLE.txt: download the complete same-run Stable acceptance kit.'
}
# Read the WHOLE file. The strict regex refuses whitespace, extra lines,
# uppercase hex, quotes or shell operators. Never interpolate untrusted
# receipt text into CMD. One optional final CRLF or LF is allowed.
$raw = [IO.File]::ReadAllText($provenance)
if ($raw -cnotmatch '\A[0-9a-f]{40}(\r?\n)?\z') {
    throw 'Invalid exact lowercase 40-character Stable 0.2.4 commit receipt.'
}
$commit = $raw.Substring(0, 40)
$candidate = Join-Path $parent 'candidate'
$destination = Join-Path $parent 'Dvinholm-024-stable-acceptance'
$prepare = Join-Path $PSScriptRoot 'Prepare-StableAcceptance024.ps1'
if (-not (Test-Path -LiteralPath $candidate -PathType Container)) {
    throw 'Missing sibling candidate folder: extract the three-file staged artifact beside the kit.'
}
if (-not (Test-Path -LiteralPath $prepare -PathType Leaf)) {
    throw 'Missing Stable 0.2.4 verifier script: use the complete acceptance kit.'
}
if (Test-Path -LiteralPath $destination) {
    throw 'Stable acceptance destination already exists: refusing overwrite; relaunch the verified EXE directly.'
}
# Audit both original ZIPs and the downloaded SHA256SUMS, then isolate
# only Portable. The preparation script fails closed if any check fails.
& $prepare -ArchiveDirectory $candidate -ExpectedCommit $commit -DestinationDirectory $destination
if (-not $?) {
    throw 'Stable 0.2.4 verification and private extraction did not complete.'
}
$exe = Join-Path $destination 'Dvinholm.App.exe'
$receipt = Join-Path $destination 'ACCEPTANCE-0.2.4.txt'
$private = Join-Path $destination 'acceptance-user-data'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf) -or
    -not (Test-Path -LiteralPath $receipt -PathType Leaf) -or
    -not (Test-Path -LiteralPath $private -PathType Container)) {
    throw 'Stable acceptance output missing verified EXE, receipt or private data folder.'
}
if ($PrepareOnly) {
    Write-Host 'PASS: exact-SHA Stable 0.2.4 acceptance staged privately. WinUI NOT launched.'
    return
}
# Direct EXE relaunch is also isolated by its versioned receipt; this env
# var is an additional constrained setting for this launch only.
$env:DVINHOLM_ACCEPTANCE_DATA_DIR = $private
Write-Host 'Attempting to open verified isolated Stable 0.2.4. GUI acceptance remains manual.'
Start-Process -FilePath $exe -WorkingDirectory $destination
Write-Host "Human checklist: $receipt"
Write-Host 'STAGING ONLY: published Stable v0.2.3 and installed user data unchanged.'
