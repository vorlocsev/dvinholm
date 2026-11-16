#requires -Version 5.1
<#
.SYNOPSIS
    Re-check a privately generated CEM PIN bench template against its local ZIP.
.DESCRIPTION
    Strict, read-only, offline validation. No adapter, CAN, CEM, PIN, VIN,
    GitHub or network access. A matching checksum never proves provenance
    or hardware acceptance; every physical gate stays NotTested.
.EXAMPLE
    & '.\eng\Verify-CemPinBenchAcceptance.ps1' -ReportJson 'C:\Bench\cem-pin-bench-acceptance.json' -BuildZip 'C:\Review\Dvinholm-Windows-x64.zip' -IConfirmOfflineNoSecrets
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ReportJson,
    [Parameter(Mandatory=$true)][string]$BuildZip,
    [switch]$IConfirmOfflineNoSecrets
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IConfirmOfflineNoSecrets) {
    throw 'Explicit offline/no-secrets confirmation is required.'
}
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or
    $PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Windows PowerShell 5.1 is required.'
}
function Get-RegularLocalFile([string]$path, [string]$expectedExtension, [long]$maximumBytes) {
    if (-not [IO.Path]::IsPathRooted($path) -or $path.StartsWith('\\')) {
        throw 'A local, absolute file path is required.'
    }
    $file = Get-Item -LiteralPath $path -Force -ErrorAction Stop
    if ($file.PSIsContainer -or
        ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        [IO.Path]::GetExtension($file.FullName) -ine $expectedExtension -or
        $file.Length -lt 1 -or $file.Length -gt $maximumBytes) {
        throw 'Report or ZIP is not a regular local file of the expected size/type.'
    }
    return $file
}
function Assert-ExactProperties($node, [string[]]$fields) {
    if ($null -eq $node -or $null -eq $node.PSObject -or
        $node -isnot [pscustomobject]) {
        throw 'Expected one JSON object.'
    }
    $actual = @($node.PSObject.Properties | ForEach-Object { $_.Name })
    if ($actual.Count -ne $fields.Count) {
        throw 'Unexpected number of JSON fields.'
    }
    foreach ($expected in $fields) {
        if ($actual -cnotcontains $expected) {
            throw 'Required exact JSON field is missing or renamed.'
        }
    }
}
function Assert-FalseBoolean($value) {
    if ($value -isnot [bool] -or $value -ne $false) {
        throw 'Report must not claim any verified hardware, credential or authorization.'
    }
}
function Compute-Sha256([string]$filename) {
    $hash = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($filename)
    try {
        return ([BitConverter]::ToString($hash.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $stream.Dispose()
        $hash.Dispose()
    }
}
$reportFile = Get-RegularLocalFile $ReportJson '.json' 65536
$archiveFile = Get-RegularLocalFile $BuildZip '.zip' ([long]10 * 1024 * 1024 * 1024)
if ([IO.Path]::GetFileName($reportFile.FullName) -cne 'cem-pin-bench-acceptance.json') {
    throw 'Unexpected private acceptance report name.'
}
$json = [IO.File]::ReadAllText($reportFile.FullName, [Text.Encoding]::UTF8)
$report = ConvertFrom-Json -InputObject $json -ErrorAction Stop
$expectedRoot = @(
    'schema', 'status', 'origin', 'preparedAtUtc',
    'sourceCommitOperatorDeclared', 'sourceCommitIndependentlyVerified',
    'buildZipSha256', 'buildZipBytes', 'exactPhysicalCemIdentityVerified',
    'pinAcquisitionVerified', 'mayIssueCemRequest',
    'mayWriteConfiguration', 'gates'
)
Assert-ExactProperties $report $expectedRoot
if ($report.schema -cne 'dvinholm.cem-pin.bench-preparation.v1' -or
    $report.status -cne 'NOT_TESTED' -or
    $report.origin -cne 'OPERATOR_PREPARED_OFFLINE') {
    throw 'Unknown CEM PIN bench-report identity or misleading status.'
}
if ($report.sourceCommitOperatorDeclared -isnot [string] -or
    $report.sourceCommitOperatorDeclared -cnotmatch '^[a-f0-9]{40}$' -or
    $report.buildZipSha256 -isnot [string] -or
    $report.buildZipSha256 -cnotmatch '^[a-f0-9]{64}$') {
    throw 'Invalid operator-declared SHA or ZIP digest.'
}
$parsedTime = [DateTimeOffset]::MinValue
if ($report.preparedAtUtc -isnot [string] -or
    $report.preparedAtUtc -cnotmatch '(Z|\+00:00)$' -or
    -not [DateTimeOffset]::TryParse(
        $report.preparedAtUtc,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind,
        [ref]$parsedTime)) {
    throw 'Invalid or non-UTC preparation timestamp.'
}
foreach ($name in @(
    'sourceCommitIndependentlyVerified', 'exactPhysicalCemIdentityVerified',
    'pinAcquisitionVerified', 'mayIssueCemRequest', 'mayWriteConfiguration'
)) {
    Assert-FalseBoolean $report.$name
}
if ($report.buildZipBytes -isnot [int] -and $report.buildZipBytes -isnot [long]) {
    throw 'ZIP length must be a JSON integer.'
}
if ([long]$report.buildZipBytes -ne [long]$archiveFile.Length) {
    throw 'ZIP length changed after preparation.'
}
$ids = @(
    'ExactHeadBuildAndDriver', 'OwnerConsentAndServiceEntitlement',
    'IndependentCemHardwareSoftwareIdentity', 'DocumentedReadOnlyTransportAndPartNumber',
    'VerifiedResponseSourceAndRepeatability', 'CancellationAndTimeoutOnBench',
    'DisconnectAndFilterCleanupOnBench', 'NoSecretsInLogsAndUiRecovery'
)
$gates = @($report.gates)
if ($gates.Count -ne $ids.Count) {
    throw 'The CEM PIN bench report must contain exactly eight gates.'
}
for ($i = 0; $i -lt $ids.Count; $i++) {
    $gate = $gates[$i]
    Assert-ExactProperties $gate @('gateId','status','evidenceVerified','comment')
    if ($gate.gateId -cne $ids[$i] -or
        $gate.status -cne 'NotTested' -or
        $gate.comment -cne 'No physical bench evidence supplied') {
        throw 'Unexpected gate, reordered gates, or fabricated physical acceptance.'
    }
    Assert-FalseBoolean $gate.evidenceVerified
}
# Recheck local ZIP independently; no extraction, installation or execution.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($archiveFile.FullName)
try {
    $executables = @($archive.Entries | Where-Object {
        $_.FullName -ieq 'Dvinholm.exe' -and $_.Length -gt 0
    })
    if ($executables.Count -ne 1) {
        throw 'ZIP no longer has exactly one nonempty root Dvinholm.exe.'
    }
}
finally { $archive.Dispose() }
if ((Compute-Sha256 $archiveFile.FullName) -cne $report.buildZipSha256) {
    throw 'ZIP SHA-256 differs from the prepared private report.'
}
# Do not output paths, user information or original JSON.
[pscustomobject]@{
    result = 'NOT_TESTED'
    reportStructureValid = $true
    locallySuppliedZipHashMatches = $true
    physicalCemIdentityVerified = $false
    pinVerified = $false
    sourceCommitIndependentlyVerified = $false
    gateCount = 8
}
