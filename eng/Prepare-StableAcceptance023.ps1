#requires -Version 5.1
<#
Prepares a verified, isolated Stable 0.2.3 Portable extraction for human
Windows 11 LTSC testing. Never launches WinUI or modifies an installation.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArchiveDirectory,
    [Parameter(Mandatory)][string]$ExpectedCommit,
    [Parameter(Mandatory)][string]$DestinationDirectory
)
$ErrorActionPreference = 'Stop'
if ($ExpectedCommit -cnotmatch '^[0-9a-f]{40}$') {
    throw 'ExpectedCommit must be the exact lowercase 40-character SHA.'
}
$source = (Resolve-Path -LiteralPath $ArchiveDirectory -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw 'ArchiveDirectory is not a directory.'
}
$target = [IO.Path]::GetFullPath($DestinationDirectory)
if (Test-Path -LiteralPath $target) {
    throw 'Destination exists: never modify an installed application.'
}
$parent = Split-Path -Path $target -Parent
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    throw 'Destination parent directory must already exist.'
}
$verifier = Join-Path $PSScriptRoot 'Test-ReleaseCandidate023.ps1'
if (-not (Test-Path -LiteralPath $verifier -PathType Leaf)) {
    throw 'Missing validator: use the full acceptance kit.'
}
& $verifier -ArchiveDirectory $source -ExpectedCommit $ExpectedCommit -Channel stable
if (-not $?) { throw 'Eight RC gates failed.' }

$zip = Join-Path $source 'Dvinholm-Windows-x86.zip'
$digest = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$temp = Join-Path $parent ('dvinholm-acceptance-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $temp -ErrorAction Stop
try {
    Add-Type -AssemblyName System.IO.Compression
    if (-not ('System.IO.Compression.ZipFile' -as [type])) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
    }
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, $temp)
    if (-not (Test-Path -LiteralPath (Join-Path $temp 'Dvinholm.App.exe') -PathType Leaf)) {
        throw 'Extracted application is missing.'
    }
    $report = @(
        'Dvinholm Stable 0.2.3 | HUMAN ACCEPTANCE | NOT PUBLISHED'
        "Exact main commit: $ExpectedCommit"
        "Portable ZIP SHA-256: $digest"
        '8 ZIP/DLL checks passed. WinUI has not been launched by this script.'
        ''
        '[ ] Windows 11 LTSC: window and icon appear; close and relaunch.'
        '[ ] Start demo, Add test profiles, synthetic UDS controls absent.'
        '[ ] No J2534: disconnected; live CAN/VIN/DTC disabled.'
        '[ ] Software CAN ID input accepts 7E0,7E8.'
        '[ ] Empty capture CSV has standard 5-column header.'
        '[ ] Personal ECU profiles persist after restart.'
        '[ ] CAN speed selection persists after restart.'
        '[ ] Existing installed v0.2.2 and its data remain unchanged.'
        '[ ] Live update v0.2.2 -> v0.2.3 only after public release.'
    )
    [IO.File]::WriteAllLines((Join-Path $temp 'ACCEPTANCE-0.2.3.txt'),
        [string[]]$report, [Text.UTF8Encoding]::new($false))
    if (Test-Path -LiteralPath $target) {
        throw 'Destination appeared during extraction: refusing overwrite.'
    }
    Move-Item -LiteralPath $temp -Destination $target -ErrorAction Stop
    Write-Host "Validated acceptance extraction: $target"
    Write-Host 'No application launched, no installed files modified.'
}
finally {
    if (Test-Path -LiteralPath $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force
    }
}
