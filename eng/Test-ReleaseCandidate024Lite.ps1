#requires -Version 5.1
<#
Read-only audit for Dvinholm v0.2.4 Lite (framework-dependent x86) release candidate.
-GenerateChecksum is permitted only for locally staged CI output; a
downloaded artifact is always checked without overwriting its manifest.
Never installs dependencies, signs, starts WinUI or publishes a GitHub release. Simulated host metadata is NOT an actual Windows readiness or GUI test.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArchiveDirectory,
    [Parameter(Mandatory)][string]$ExpectedCommit,
    [switch]$GenerateChecksum,
    [ValidateSet('rc', 'stable')][string]$Channel = 'rc'
)
$ErrorActionPreference = 'Stop'
if ($ExpectedCommit -cnotmatch '^[0-9a-f]{40}$') {
    throw 'RC gate 1/8: expected exact lowercase 40-digit commit SHA.'
}
$root = (Resolve-Path -LiteralPath $ArchiveDirectory -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw 'RC gate 1/8: candidate root is not a directory.'
}
$zipName = 'Dvinholm-Windows-x86-Lite.zip'
$sumName = 'SHA256SUMS.txt'
$zipPath = Join-Path $root $zipName
$sumPath = Join-Path $root $sumName
$entries = @(Get-ChildItem -LiteralPath $root -Force)
$expectedCount = if ($GenerateChecksum) { 1 } else { 2 }
if ($entries.Count -ne $expectedCount -or
    @($entries | Where-Object { $_.PSIsContainer }).Count -ne 0) {
    throw "RC gate 1/8: expected $expectedCount flat files and no extras."
}
$seenNames = @($entries | ForEach-Object { $_.Name })
if (-not ($seenNames -ccontains $zipName) -or
    (-not $GenerateChecksum -and -not ($seenNames -ccontains $sumName))) {
    throw 'RC gate 1/8: unexpected candidate filenames.'
}
Write-Host 'RC gate 1/8: exact filenames, commit input and no nested entries.'

if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
    throw 'RC gate 2/8: missing Lite ZIP.'
}
$length = (Get-Item -LiteralPath $zipPath).Length
if ($length -lt 1MB -or $length -ge 30MB) {
    throw "RC gate 2/8: Lite ZIP size outside 1 to 30 MiB ($length)."
}
Write-Host "RC gate 2/8: Lite ZIP within size guardrails ($length bytes)."

# Pure .NET SHA256 works in Windows PowerShell 5.1 x86/x64.
$sha256 = [Security.Cryptography.SHA256]::Create()
$source = [IO.File]::OpenRead($zipPath)
try {
    $digest = [BitConverter]::ToString($sha256.ComputeHash($source)).
        Replace('-', '').ToLowerInvariant()
} finally {
    $source.Dispose()
    $sha256.Dispose()
}
if ($GenerateChecksum) {
    [IO.File]::WriteAllText($sumPath,
        "$digest  $zipName" + [Environment]::NewLine,
        [Text.Encoding]::ASCII)
}
$manifest = [IO.File]::ReadAllText($sumPath, [Text.Encoding]::ASCII)
$match = [regex]::Match($manifest,
    '\A([0-9a-f]{64})  Dvinholm-Windows-x86-Lite[.]zip\r?\n\z')
if (-not $match.Success) {
    throw 'RC gate 3/8: expected exactly one lowercase SHA-256 manifest entry.'
}
if ($match.Groups[1].Value -cne $digest) {
    throw 'RC gate 3/8: checksum differs from the real Lite ZIP.'
}
Write-Host 'RC gate 3/8: SHA-256 matches strict single-entry sidecar.'

# Existing validators reject unsafe ZIP paths, duplicates, debug symbols,
# missing branded icons, missing runtimes and broken x86 apphost metadata.
& (Join-Path $PSScriptRoot 'Test-WindowsDistribution.ps1') -Flavor Lite -ZipPath $zipPath
if (-not $?) { throw 'RC gate 4/8: ZIP layout audit failed.' }
& (Join-Path $PSScriptRoot 'Test-LiteBundledPreflight.ps1') -Flavor Lite -ZipPath $zipPath
if (-not $?) { throw 'RC gate 4/8: Lite prerequisite-checker audit failed.' }
Write-Host 'RC gate 4/8: safe archive paths, icon, runtime exclusion, prerequisite scripts and no debug tools.'

& (Join-Path $PSScriptRoot 'Test-WindowsDistributionExtraction.ps1') -Flavor Lite -ZipPath $zipPath -SimulatedLiteHost
if (-not $?) { throw 'RC gate 5/8: extracted Lite x86/simulated-host audit failed.' }
Write-Host 'RC gate 5/8: actual x86 PE and framework-dependent x86 app and SIMULATED host prerequisites verified.'

Add-Type -AssemblyName System.IO.Compression
if (-not ('System.IO.Compression.ZipFile' -as [type])) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
}
$temp = Join-Path ([IO.Path]::GetTempPath()) (
    'Dvinholm-v024-rc-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null
try {
    $dll = Join-Path $temp 'Dvinholm.App.dll'
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $part = $archive.GetEntry('Dvinholm.App.dll')
        if ($null -eq $part) {
            throw 'RC gate 6/8: managed app DLL missing at archive root.'
        }
        $inputStream = $part.Open()
        $outputStream = [IO.File]::Create($dll)
        try { $inputStream.CopyTo($outputStream) }
        finally {
            $outputStream.Dispose()
            $inputStream.Dispose()
        }
    } finally { $archive.Dispose() }
    # Inspect the real packaged Release DLL for forbidden synthetic metadata.
    # Production excludes both fixture types and their UI event handlers.
    $metadata = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($dll))
    foreach ($name in @(
        'CanDemoFrameGenerator', 'UdsOfflineSample', 'UdsOfflineTextReport',
        'AddTestProfiles_Click', 'PreviewUdsSample_Click',
        'ExportOfflineUdsTxt_Click', 'DemoMonitor_Click', 'EmitDemoFrames',
        'SYNTHETIC_DEMO'
    )) {
        if ($metadata.Contains($name)) {
            throw "RC gate 6/8: Development-only code in Stable binary: $name"
        }
    }
    foreach ($name in @('ReadVin_Click', 'ReadDid_Click', 'ReadDtc_Click')) {
        if (-not $metadata.Contains($name)) {
            throw "RC gate 6/8: live read-only diagnostic handler missing: $name"
        }
    }
    Write-Host 'RC gate 6/8: Stable DLL excludes demo/test code but retains VIN/DID/DTC handlers.'

    $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($dll)
    if ($assemblyName.Name -cne 'Dvinholm.App' -or
        $assemblyName.Version.ToString(3) -cne '0.2.4') {
        throw "RC gate 7/8: compiled assembly is not Dvinholm.App 0.2.4."
    }
    Write-Host 'RC gate 7/8: compiled AssemblyVersion is 0.2.4.'

    $productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion
    $expected = "0.2.4+$ExpectedCommit.$Channel"
    if ($productVersion -cne $expected) {
        throw "RC gate 8/8: informational version mismatch ($productVersion versus $expected)."
    }
    Write-Host "RC gate 8/8: v0.2.4 metadata bound to exact commit $ExpectedCommit."
} finally {
    if (Test-Path -LiteralPath $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force
    }
}
Write-Host 'All eight Dvinholm 0.2.4 RC checks passed. VALIDATION ONLY; NOT PUBLISHED.'
