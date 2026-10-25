#requires -Version 5.1
<#
Stage a version-checked, isolated Windows 11 LTSC acceptance copy from the
three-file *unpublished* Stable v0.2.4 GitHub Actions artifact. Never launches
WinUI, installs anything, edits a real app directory, or publishes a release.
Both original ZIPs and the original combined SHA256SUMS are checked; only
Portable is extracted for manual acceptance. Lite remains test-only.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArchiveDirectory,
    [Parameter(Mandatory)][string]$ExpectedCommit,
    [Parameter(Mandatory)][string]$DestinationDirectory
)
$ErrorActionPreference = 'Stop'
if ($ExpectedCommit -cnotmatch '^[0-9a-f]{40}$') {
    throw 'Stable acceptance requires an exact lowercase 40-character commit SHA.'
}
$source = (Resolve-Path -LiteralPath $ArchiveDirectory -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw 'Stable acceptance archive source must be a directory.'
}
$target = [IO.Path]::GetFullPath($DestinationDirectory)
if (Test-Path -LiteralPath $target) {
    throw 'Stable acceptance destination already exists: refusing overwrite.'
}
$parent = Split-Path -Path $target -Parent
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    throw 'Stable acceptance destination parent must already exist.'
}
$expectedNames = @('Dvinholm-Windows-x86.zip',
                   'Dvinholm-Windows-x86-Lite.zip', 'SHA256SUMS.txt')
$entries = @(Get-ChildItem -LiteralPath $source -Force)
if ($entries.Count -ne 3 -or @($entries | Where-Object {
    $_.PSIsContainer -or $expectedNames -cnotcontains $_.Name
}).Count -ne 0) {
    throw 'Stable acceptance requires exactly two ZIPs and one flat SHA256SUMS.'
}
$manifest = Join-Path $source 'SHA256SUMS.txt'
$lines = [IO.File]::ReadAllLines($manifest, [Text.Encoding]::ASCII)
if ($lines.Count -ne 2 -or
    $lines[0] -cnotmatch '^[0-9a-f]{64}  Dvinholm-Windows-x86[.]zip$' -or
    $lines[1] -cnotmatch '^[0-9a-f]{64}  Dvinholm-Windows-x86-Lite[.]zip$') {
    throw 'Stable acceptance checksum file must have two canonical lowercase SHA-256 entries.'
}
& (Join-Path $PSScriptRoot 'Test-WindowsDistributionChecksums.ps1') -Directory $source
if (-not $?) { throw 'Stable acceptance combined SHA-256 verification failed.' }

# Validation scratch is never moved into the resulting acceptance copy.
$audit = Join-Path $parent ('dvinholm-stable024-audit-' + [Guid]::NewGuid().ToString('N'))
$stage = Join-Path $parent ('dvinholm-stable024-stage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $audit -ErrorAction Stop | Out-Null
try {
    $portable = Join-Path $audit 'portable'
    $lite = Join-Path $audit 'lite'
    New-Item -ItemType Directory -Path $portable, $lite -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source 'Dvinholm-Windows-x86.zip') -Destination $portable
    Copy-Item -LiteralPath (Join-Path $source 'Dvinholm-Windows-x86-Lite.zip') -Destination $lite
    # Subset ORIGINAL downloaded checksum entries. Never compute a new hash.
    [IO.File]::WriteAllText((Join-Path $portable 'SHA256SUMS.txt'),
        $lines[0] + [Environment]::NewLine, [Text.Encoding]::ASCII)
    [IO.File]::WriteAllText((Join-Path $lite 'SHA256SUMS.txt'),
        $lines[1] + [Environment]::NewLine, [Text.Encoding]::ASCII)

    & (Join-Path $PSScriptRoot 'Test-ReleaseCandidate024.ps1') -ArchiveDirectory $portable -ExpectedCommit $ExpectedCommit -Channel stable
    if (-not $?) { throw 'Stable acceptance Portable eight-gate binary audit failed.' }
    & (Join-Path $PSScriptRoot 'Test-ReleaseCandidate024Lite.ps1') -ArchiveDirectory $lite -ExpectedCommit $ExpectedCommit -Channel stable
    if (-not $?) { throw 'Stable acceptance Lite eight-gate binary audit failed.' }

    Add-Type -AssemblyName System.IO.Compression
    if (-not ('System.IO.Compression.ZipFile' -as [type])) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
    }
    [IO.Compression.ZipFile]::ExtractToDirectory(
        (Join-Path $source 'Dvinholm-Windows-x86.zip'), $stage)
    if (-not (Test-Path -LiteralPath (Join-Path $stage 'Dvinholm.App.exe') -PathType Leaf)) {
        throw 'Verified Stable acceptance Portable EXE missing after extraction.'
    }
    # An existing acceptance-mode receipt is recognized by the app on normal
    # direct EXE restart, so all profiles and CAN settings stay isolated.
    $report = @(
        'Dvinholm Stable 0.2.4 | HUMAN WINDOWS ACCEPTANCE | STAGED, NOT PUBLISHED'
        "Exact source commit: $ExpectedCommit"
        "Portable SHA-256: $($lines[0].Substring(0, 64))"
        "Lite SHA-256: $($lines[1].Substring(0, 64))"
        'Portable and Lite eight-gate binary audits passed. No GUI was started.'
        'Never run the raw unprepared ZIP from the installed application folder.'
        ''
        '[ ] Launch verified isolated EXE on Windows 11 LTSC; restart it.'
        '[ ] Window title/version, branding and icon are correct.'
        '[ ] Demo/test/preview UDS controls are absent.'
        '[ ] Disconnected J2534 detection and live-action buttons are safe.'
        '[ ] CAN speed save/restart/reload uses isolated settings.'
        '[ ] ECU JSON import/export/edit/restart uses isolated profiles.'
        '[ ] CAN Monitor layout/filter and CSV export behave as expected.'
        '[ ] Update channel UI remains disabled in acceptance mode.'
        '[ ] Existing installed Stable 0.2.3 and user data are unchanged.'
        'Manual GUI acceptance is NOT part of GitHub Actions CI.'
    )
    [IO.File]::WriteAllLines((Join-Path $stage 'ACCEPTANCE-0.2.4.txt'),
        [string[]]$report, [Text.UTF8Encoding]::new($false))
    New-Item -ItemType Directory -Path (Join-Path $stage 'acceptance-user-data') -Force | Out-Null
    if (Test-Path -LiteralPath $target) {
        throw 'Stable acceptance destination appeared during extraction: refusing overwrite.'
    }
    # Same-parent atomic rename fails closed if the destination appears.
    [IO.Directory]::Move($stage, $target)
    Write-Host "Verified isolated Stable 0.2.4 acceptance copy: $target"
    Write-Host 'NOT PUBLISHED. No GUI, installed app, updater or J2534 touched.'
}
finally {
    if (Test-Path -LiteralPath $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
    if (Test-Path -LiteralPath $audit) {
        Remove-Item -LiteralPath $audit -Recurse -Force
    }
}
