#requires -Version 5.1
<#
Rehearse file replacement and rollback using REAL v0.2.2 release bytes and
the exact unpublished v0.2.3 staged Portable bytes. This is a disposable
file-mirror integration check, not the actual updater process/WinUI GUI.
No installed apps, user profiles, registry, tags or releases are modified.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PreviousArchiveDirectory,
    [Parameter(Mandatory)][string]$CandidateArchiveDirectory,
    [Parameter(Mandatory)][string]$ExpectedCommit
)
$ErrorActionPreference = 'Stop'
if ($ExpectedCommit -cnotmatch '^[0-9a-f]{40}$') {
    throw 'Gate 1/8: require exact lowercase 40-character commit SHA.'
}
$oldDir = (Resolve-Path -LiteralPath $PreviousArchiveDirectory -ErrorAction Stop).Path
$newDir = (Resolve-Path -LiteralPath $CandidateArchiveDirectory -ErrorAction Stop).Path
if ($oldDir -ceq $newDir) { throw 'Gate 1/8: previous and candidate source must differ.' }
$names = @('Dvinholm-Windows-x86.zip', 'SHA256SUMS.txt')
foreach ($source in @($oldDir, $newDir)) {
    $entries = @(Get-ChildItem -LiteralPath $source -Force)
    if ($entries.Count -ne 2 -or @($entries | Where-Object { $_.PSIsContainer }).Count -gt 0 -or
        @($entries | Where-Object { $names -cnotcontains $_.Name }).Count -gt 0) {
        throw 'Gate 1/8: archives require exact flat ZIP and SHA256SUMS.txt.'
    }
}
Write-Host 'PASS 1/8: exact archive names and independent inputs.'

function Confirm-Sha([string]$dir) {
    $sum = [IO.File]::ReadAllText((Join-Path $dir 'SHA256SUMS.txt'), [Text.Encoding]::ASCII)
    $match = [regex]::Match($sum, '\A([0-9a-f]{64})  Dvinholm-Windows-x86[.]zip\r?\n\z')
    if (-not $match.Success) { throw 'Bad SHA256SUMS.txt format.' }
    $actual = (Get-FileHash -LiteralPath (Join-Path $dir 'Dvinholm-Windows-x86.zip') -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($match.Groups[1].Value -cne $actual) { throw 'ZIP differs from strict SHA-256 manifest.' }
}
Confirm-Sha $oldDir
Write-Host 'PASS 2/8: published v0.2.2 ZIP SHA-256 verified.'
& (Join-Path $PSScriptRoot 'Test-ReleaseCandidate023.ps1') -ArchiveDirectory $newDir -ExpectedCommit $ExpectedCommit -Channel stable
if (-not $?) { throw 'Gate 3/8: candidate validation failed.' }
Write-Host 'PASS 3/8: exact v0.2.3 Stable ZIP checksum, binary and commit verified.'
& (Join-Path $PSScriptRoot 'Test-WindowsDistribution.ps1') -Flavor Portable -ZipPath (Join-Path $oldDir 'Dvinholm-Windows-x86.zip') -AllowLegacyV022DebugFiles
if (-not $?) { throw 'Gate 4/8: v0.2.2 ZIP layout unsafe.' }
Write-Host 'PASS 4/8: published old Portable ZIP layout checked (two historic debug files permitted).'

Add-Type -AssemblyName System.IO.Compression
if (-not ('System.IO.Compression.ZipFile' -as [type])) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
}

function Mirror([string]$from, [string]$to) {
    & robocopy $from $to /MIR /R:0 /W:0 /NFL /NDL /NP /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "Robocopy failed (code $LASTEXITCODE) from $from to $to."
    }
}

function Snapshot([string]$dir) {
    $result = @{}
    foreach ($file in @(Get-ChildItem -LiteralPath $dir -File -Recurse -Force)) {
        $relative = $file.FullName.Substring($dir.Length).TrimStart('\').Replace('\', '/')
        $result[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    return ,$result
}
function AssertSame([hashtable]$actual, [hashtable]$expected, [string]$stage) {
    if ($actual.Count -ne $expected.Count) {
        throw "$stage file count mismatch $($actual.Count) versus $($expected.Count)."
    }
    foreach ($name in $expected.Keys) {
        if (-not $actual.ContainsKey($name) -or $actual[$name] -cne $expected[$name]) {
            throw "$stage missing or byte-mismatched: $name"
        }
    }
}

$root = Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-stable-upgrade-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
try {
    $previous = Join-Path $root 'previous-022'
    $candidate = Join-Path $root 'candidate-023'
    $installed = Join-Path $root 'disposable-install'
    $backup = Join-Path $root 'backup'
    foreach ($dir in @($previous, $candidate, $installed, $backup)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $oldDir 'Dvinholm-Windows-x86.zip'), $previous)
    [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $newDir 'Dvinholm-Windows-x86.zip'), $candidate)
    $oldAsm = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $previous 'Dvinholm.App.dll'))
    $newAsm = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $candidate 'Dvinholm.App.dll'))
    if ($oldAsm.Version.ToString(3) -cne '0.2.2' -or $newAsm.Version.ToString(3) -cne '0.2.3') {
        throw 'Gate 5/8: previous and candidate assemblies are not 0.2.2 -> 0.2.3.'
    }
    Write-Host 'PASS 5/8: actual old/new assembly versions match the upgrade direction.'

    # Entirely disposable old install. Add one synthetic obsolete payload to
    # check /MIR cleanup. Save immutable baseline including that fixture.
    Mirror $previous $installed
    [IO.File]::WriteAllText((Join-Path $installed 'obsolete-from-old-version.dll'), 'fixture only')
    $baseline = Snapshot $installed
    Mirror $installed $backup
    AssertSame (Snapshot $backup) $baseline 'Backup'
    Write-Host 'PASS 6/8: exact-byte backup of disposable v0.2.2 payload.'

    # User data lives separately from the Portable installation directory.
    $userData = Join-Path $root 'independent-user-profile.json'
    [IO.File]::WriteAllText($userData, '{"profile":"preserve","source":"fixture"}', [Text.Encoding]::UTF8)
    $userDigest = (Get-FileHash -LiteralPath $userData -Algorithm SHA256).Hash
    Mirror $candidate $installed
    AssertSame (Snapshot $installed) (Snapshot $candidate) 'Forward mirror'
    if (Test-Path -LiteralPath (Join-Path $installed 'obsolete-from-old-version.dll')) {
        throw 'Gate 7/8: old file retained by forward mirror.'
    }
    if ((Get-FileHash -LiteralPath $userData -Algorithm SHA256).Hash -cne $userDigest) {
        throw 'Gate 7/8: external user data modified.'
    }
    Write-Host 'PASS 7/8: real 0.2.3 files mirror exactly, obsolete file removed, user data untouched.'

    Mirror $backup $installed
    AssertSame (Snapshot $installed) $baseline 'Rollback mirror'
    if ((Get-FileHash -LiteralPath $userData -Algorithm SHA256).Hash -cne $userDigest) {
        throw 'Gate 8/8: external profile modified after rollback.'
    }
    Write-Host 'PASS 8/8: rollback restores exact disposable 0.2.2 bytes and user data.'
    Write-Host 'All eight real-artifact offline upgrade gates passed. No installed app or release touched.'
}
finally {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Force -Recurse
    }
}
