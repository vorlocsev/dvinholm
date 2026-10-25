#requires -Version 5.1
<#
Eight read-only integration gates for REAL published Stable v0.2.3 Portable
to the downloaded candidate Stable v0.2.4 Portable upgrade.
The candidate directory has TWO ZIPs and the unchanged original two-line
SHA256SUMS.txt; never recalculate hashes for downloaded artifacts.
All mirror and rollback operations occur within a new, disposable temp root.
Does not start WinUI, mutate a real install, update user profiles or publish.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PreviousArchiveDirectory,
    [Parameter(Mandatory)][string]$CandidateArchiveDirectory,
    [Parameter(Mandatory)][string]$ExpectedCommit
)
$ErrorActionPreference = 'Stop'
$publishedCommit = '91ecc343608c931eb033523e3db6012a6c5dbd58'
if ($ExpectedCommit -cnotmatch '^[0-9a-f]{40}$') {
    throw 'Gate 1/8: candidate SHA must be exactly 40 lowercase hexadecimal characters.'
}
$old = (Resolve-Path -LiteralPath $PreviousArchiveDirectory -ErrorAction Stop).Path
$new = (Resolve-Path -LiteralPath $CandidateArchiveDirectory -ErrorAction Stop).Path
if ($old -ceq $new) { throw 'Gate 1/8: old and candidate directories must differ.' }
function Assert-ExactFiles([string]$directory, [string[]]$names) {
    $entries = @(Get-ChildItem -LiteralPath $directory -Force)
    if ($entries.Count -ne $names.Count -or
        @($entries | Where-Object {
            $_.PSIsContainer -or $names -cnotcontains $_.Name
        }).Count -ne 0) {
        throw "Unexpected candidate or release file set in $directory."
    }
    foreach ($name in $names) {
        if (-not (Test-Path -LiteralPath (Join-Path $directory $name) -PathType Leaf)) {
            throw "Missing required artifact: $name."
        }
    }
}
Assert-ExactFiles $old @('Dvinholm-Windows-x86.zip', 'SHA256SUMS.txt')
Assert-ExactFiles $new @('Dvinholm-Windows-x86.zip', 'Dvinholm-Windows-x86-Lite.zip', 'SHA256SUMS.txt')
Write-Host 'PASS 1/8: exact published and staged artifact sets.'

# Never accept a lookalike "old Stable" ZIP or previous release metadata.
& (Join-Path $PSScriptRoot 'Test-ReleaseCandidate023.ps1') -ArchiveDirectory $old -ExpectedCommit $publishedCommit -Channel stable
if (-not $?) { throw 'Gate 2/8: published v0.2.3 Portable does not match its pinned Stable commit.' }
Write-Host 'PASS 2/8: published v0.2.3 Portable archive and immutable release commit.'

$lines = [IO.File]::ReadAllLines((Join-Path $new 'SHA256SUMS.txt'), [Text.Encoding]::ASCII)
if ($lines.Count -ne 2 -or
    $lines[0] -cnotmatch '^[0-9a-f]{64}  Dvinholm-Windows-x86[.]zip$' -or
    $lines[1] -cnotmatch '^[0-9a-f]{64}  Dvinholm-Windows-x86-Lite[.]zip$') {
    throw 'Gate 3/8: candidate two-line manifest must use canonical order, filenames and lowercase hashes.'
}
& (Join-Path $PSScriptRoot 'Test-WindowsDistributionChecksums.ps1') -Directory $new
if (-not $?) { throw 'Gate 3/8: candidate Portable or Lite differs from its downloaded manifest.' }
Write-Host 'PASS 3/8: both candidate ZIP hashes match the unmodified downloaded manifest.'

$temp = Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-upgrade-023-to-024-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null
try {
    $candidateSingle = Join-Path $temp 'candidate-portable-and-original-sha'
    New-Item -ItemType Directory -Path $candidateSingle -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $new 'Dvinholm-Windows-x86.zip') -Destination $candidateSingle
    # Subset only the ORIGINAL first line, never compute/rewrite the digest.
    [IO.File]::WriteAllText((Join-Path $candidateSingle 'SHA256SUMS.txt'),
        $lines[0] + [Environment]::NewLine, [Text.Encoding]::ASCII)
    & (Join-Path $PSScriptRoot 'Test-ReleaseCandidate024.ps1') -ArchiveDirectory $candidateSingle -ExpectedCommit $ExpectedCommit -Channel stable
    if (-not $?) { throw 'Gate 4/8: candidate v0.2.4 Portable version or SHA identity failed.' }
    Write-Host 'PASS 4/8: staged 0.2.4 exact commit, production-only assembly and Portable ZIP.'

    Add-Type -AssemblyName System.IO.Compression
    if (-not ('System.IO.Compression.ZipFile' -as [type])) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
    }
    $previous = Join-Path $temp 'published-v023'
    $candidate = Join-Path $temp 'staged-v024'
    $installed = Join-Path $temp 'disposable-install'
    $backup = Join-Path $temp 'backup'
    foreach ($dir in @($previous, $candidate, $installed, $backup)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $old 'Dvinholm-Windows-x86.zip'), $previous)
    [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $new 'Dvinholm-Windows-x86.zip'), $candidate)
    $before = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $previous 'Dvinholm.App.dll'))
    $after = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $candidate 'Dvinholm.App.dll'))
    if ($before.Version.ToString(3) -cne '0.2.3' -or
        $after.Version.ToString(3) -cne '0.2.4') {
        throw 'Gate 5/8: unexpected upgrade direction, require 0.2.3 -> 0.2.4.'
    }
    Write-Host 'PASS 5/8: actual old and new compiled assemblies prove the upgrade direction.'

    function Mirror([string]$from, [string]$to) {
        & robocopy $from $to /MIR /R:0 /W:0 /NFL /NDL /NP /NJH /NJS | Out-Null
        $result = $LASTEXITCODE
        if ($result -lt 0 -or $result -ge 8) {
            throw "Robocopy failed ($result) from $from to $to."
        }
        $global:LASTEXITCODE = 0
    }
    function Snapshot([string]$directory) {
        $result = @{}
        foreach ($file in @(Get-ChildItem -LiteralPath $directory -File -Recurse -Force)) {
            $relative = $file.FullName.Substring($directory.Length).TrimStart('\').Replace('\', '/')
            $result[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
        return ,$result
    }
    function Assert-Same([hashtable]$actual, [hashtable]$expected, [string]$phase) {
        if ($actual.Count -ne $expected.Count) {
            throw "$phase: file count differs ($($actual.Count) vs $($expected.Count))."
        }
        foreach ($name in $expected.Keys) {
            if (-not $actual.ContainsKey($name) -or $actual[$name] -cne $expected[$name]) {
                throw "$phase: missing or altered file $name."
            }
        }
    }
    Mirror $previous $installed
    [IO.File]::WriteAllText((Join-Path $installed 'obsolete-from-v023.dll'), 'synthetic disposable fixture only')
    $baseline = Snapshot $installed
    Mirror $installed $backup
    Assert-Same (Snapshot $backup) $baseline 'Backup'
    Write-Host 'PASS 6/8: complete, byte-exact backup of disposable published 0.2.3 files.'

    # Separate profiles and CAN speed are intentionally not part of /MIR.
    $userdata = Join-Path $temp 'independent-user-settings'
    New-Item -ItemType Directory -Path $userdata -Force | Out-Null
    foreach ($fixture in @(
        @{ Name = 'ecu-profiles.json'; Value = '{"id":"fixture-ecm"}' },
        @{ Name = 'can-baud-rate.txt'; Value = '125000' },
        @{ Name = 'selected-j2534-dll.txt'; Value = 'test fixture only' }
    )) {
        [IO.File]::WriteAllText((Join-Path $userdata $fixture.Name),
            $fixture.Value, [Text.Encoding]::UTF8)
    }
    $userBaseline = Snapshot $userdata
    Mirror $candidate $installed
    Assert-Same (Snapshot $installed) (Snapshot $candidate) 'Forward upgrade'
    Assert-Same (Snapshot $userdata) $userBaseline 'Forward user data'
    if (Test-Path -LiteralPath (Join-Path $installed 'obsolete-from-v023.dll')) {
        throw 'Gate 7/8: obsolete 0.2.3 payload was not removed.'
    }
    Write-Host 'PASS 7/8: exact staged 0.2.4 mirror, obsolete old file removed, three settings unchanged.'

    Mirror $backup $installed
    Assert-Same (Snapshot $installed) $baseline 'Rollback'
    Assert-Same (Snapshot $userdata) $userBaseline 'Rollback user data'
    Write-Host 'PASS 8/8: byte-exact 0.2.3 rollback and all three independent settings preserved.'
    Write-Host 'All eight offline published 0.2.3 -> staged 0.2.4 upgrade checks passed. NO GUI OR INSTALLED APP TOUCHED.'
}
finally {
    $global:LASTEXITCODE = 0
    if (Test-Path -LiteralPath $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force
    }
}
