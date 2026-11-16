#requires -Version 5.1
<#
.SYNOPSIS
Prepare a private, offline, eight-gate CEM PIN bench-acceptance template.
.DESCRIPTION
No vehicle, adapter, VIN or PIN operations. Source SHA is operator-declared.
A local ZIP is checked for one root Dvinholm.exe and hashed, but its provenance
and physical hardware compatibility remain NOT TESTED.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$BuildZip,
  [Parameter(Mandatory=$true)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$CommitSha,
  [Parameter(Mandatory=$true)][string]$OutputDirectory,
  [switch]$IConfirmOfflineNoSecrets
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IConfirmOfflineNoSecrets) {
  throw 'Explicit offline/no-secrets confirmation required; no files created.'
}
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or
    $PSVersionTable.PSEdition -ne 'Desktop') {
  throw 'Windows PowerShell 5.1 is required.'
}
if (-not [IO.Path]::IsPathRooted($OutputDirectory) -or
    $OutputDirectory.StartsWith('\\') -or
    -not [IO.Path]::IsPathRooted($BuildZip) -or
    $BuildZip.StartsWith('\\')) {
  throw 'OutputDirectory and BuildZip must be absolute local, non-UNC paths.'
}
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\','/')
$target = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\','/')
if ($target -ieq $repoRoot -or
    $target.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar,
       [StringComparison]::OrdinalIgnoreCase)) {
  throw 'Bench evidence must be OUTSIDE the repository.'
}
if (Test-Path -LiteralPath $target) {
  throw 'Refusing to reuse or overwrite an existing output directory.'
}
$zip = Get-Item -LiteralPath $BuildZip -Force -ErrorAction Stop
if ($zip.PSIsContainer -or
    [IO.Path]::GetExtension($zip.FullName) -ine '.zip' -or
    ($zip.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
    $zip.Length -lt 1) {
  throw 'BuildZip is not a regular, nonempty, local ZIP.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip.FullName)
try {
  $executables = @($archive.Entries | Where-Object {
    $_.FullName -ieq 'Dvinholm.exe' -and $_.Length -gt 0
  })
  if ($executables.Count -ne 1) {
    throw 'Archive must contain exactly one nonempty root Dvinholm.exe.'
  }
}
finally { $archive.Dispose() }
# Use streaming .NET SHA-256 instead of PowerShell module auto-loading.
# The Windows x86 test host may lack the usual hash command under -NoProfile,
# despite having full .NET cryptographic primitives available.
$sha256 = [Security.Cryptography.SHA256]::Create()
$stream = [IO.File]::OpenRead($zip.FullName)
try {
  $zipSha = ([BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
}
finally {
  $stream.Dispose()
  $sha256.Dispose()
}
if ($zipSha -cnotmatch '^[a-f0-9]{64}$') { throw 'ZIP SHA-256 unavailable.' }
$ids = @(
  'ExactHeadBuildAndDriver',
  'OwnerConsentAndServiceEntitlement',
  'IndependentCemHardwareSoftwareIdentity',
  'DocumentedReadOnlyTransportAndPartNumber',
  'VerifiedResponseSourceAndRepeatability',
  'CancellationAndTimeoutOnBench',
  'DisconnectAndFilterCleanupOnBench',
  'NoSecretsInLogsAndUiRecovery'
)
$checks = @(
  foreach ($id in $ids) {
    [pscustomobject][ordered]@{
      gateId = $id
      status = 'NotTested'
      evidenceVerified = $false
      comment = 'No physical bench evidence supplied'
    }
  }
)
$document = [pscustomobject][ordered]@{
  schema = 'dvinholm.cem-pin.bench-preparation.v1'
  status = 'NOT_TESTED'
  origin = 'OPERATOR_PREPARED_OFFLINE'
  preparedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
  sourceCommitOperatorDeclared = $CommitSha.ToLowerInvariant()
  sourceCommitIndependentlyVerified = $false
  buildZipSha256 = $zipSha
  buildZipBytes = [long]$zip.Length
  exactPhysicalCemIdentityVerified = $false
  pinAcquisitionVerified = $false
  mayIssueCemRequest = $false
  mayWriteConfiguration = $false
  gates = $checks
}
$serialized = ConvertTo-Json -InputObject $document -Depth 7
$parent = [IO.Path]::GetDirectoryName($target)
if ([string]::IsNullOrWhiteSpace($parent) -or
    -not (Test-Path -LiteralPath $parent -PathType Container)) {
  throw 'Output parent directory must exist.'
}
if ((Get-Item -LiteralPath $parent -Force).Attributes -band
     [IO.FileAttributes]::ReparsePoint) {
  throw 'Output parent must not be a symbolic link.'
}
[IO.Directory]::CreateDirectory($target) | Out-Null
$output = Join-Path $target 'cem-pin-bench-acceptance.json'
$temporary = Join-Path $target ('.cem-pin-' + [Guid]::NewGuid().ToString('N') + '.tmp')
try {
  [IO.File]::WriteAllText($temporary, $serialized, [Text.UTF8Encoding]::new($false))
  [IO.File]::Move($temporary, $output)
}
finally {
  if (Test-Path -LiteralPath $temporary) {
    Remove-Item -LiteralPath $temporary -Force
  }
}
Write-Output ([pscustomobject]@{
  result = 'NOT_TESTED'
  gateCount = $ids.Count
  zipSha256 = $zipSha
  reportCreated = $true
  note = 'Private offline checklist only; no vehicle or PIN procedure executed'
})
