#requires -Version 7.0
<#
Manual only. Signs a built x64 ZIP using a pre-existing Windows code-signing
ECDSA P-256 certificate. Never exports a private key or runs in GitHub Actions.
The output is NOT a trusted release until its SPKI fingerprint is independently
verified and pinned in Dvinholm.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$PackagePath,
  [Parameter(Mandatory)][string]$ChecksumManifest,
  [Parameter(Mandatory)][string]$Tag,
  [Parameter(Mandatory)][string]$CertificateThumbprint,
  [Parameter(Mandatory)][string]$SignaturePath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:GITHUB_ACTIONS -eq 'true') { throw 'Signing is not permitted in GitHub Actions.' }
if ($Tag -cnotmatch '^(v[0-9]+\.[0-9]+\.[0-9]+|ci-[0-9]+)$') {
  throw 'Invalid official x64 release tag.'
}
if ($CertificateThumbprint -notmatch '^[a-fA-F0-9]{40}$') { throw 'Invalid certificate thumbprint.' }
$zip = (Resolve-Path -LiteralPath $PackagePath).Path
$sums = (Resolve-Path -LiteralPath $ChecksumManifest).Path
if ([IO.Path]::GetFileName($zip) -cne 'Dvinholm-Windows-x64.zip' -or
    [IO.Path]::GetFileName($sums) -cne 'SHA256SUMS.txt') {
  throw 'The x64 ZIP and checksum manifest must have their exact release filenames.'
}
$size = (Get-Item -LiteralPath $zip).Length
if ($size -le 0 -or $size -gt 500MB) { throw 'ZIP size rejected.' }
$sumSize = (Get-Item -LiteralPath $sums).Length
if ($sumSize -le 0 -or $sumSize -gt 65536) { throw 'Checksum manifest size rejected.' }
$digest = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$foundHashes = @()
$raw = [IO.File]::ReadAllText($sums, [Text.Encoding]::ASCII)
foreach ($line in ($raw -split "\n")) {
  $record = $line.TrimEnd("\r")
  if ($record.Length -eq 0) { continue }
  if ($record -cnotmatch '^([0-9a-fA-F]{64})  ([A-Za-z0-9._-]+)$') {
    throw 'Malformed checksum record.'
  }
  # Explicit capture because PowerShell -notmatch does not set $Matches.
  [void]($record -cmatch '^([0-9a-fA-F]{64})  ([A-Za-z0-9._-]+)$')
  if ($Matches[2] -ceq 'Dvinholm-Windows-x64.zip') {
    $foundHashes += $Matches[1].ToLowerInvariant()
  }
}
if ($foundHashes.Count -ne 1 -or $foundHashes[0] -cne $digest) {
  throw 'Exact x64 digest missing, duplicated, or mismatched.'
}

$cert = Get-Item -LiteralPath ("Cert:\CurrentUser\My\" + $CertificateThumbprint.ToUpperInvariant())
$now = [DateTime]::UtcNow
if (-not $cert.HasPrivateKey -or $cert.NotBefore.ToUniversalTime() -gt $now -or
    $cert.NotAfter.ToUniversalTime() -le $now) {
  throw 'Private key unavailable or certificate outside validity period.'
}
$eku = @($cert.Extensions | Where-Object {
  $_ -is [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]
})
if ($eku.Count -ne 1 -or
    @($eku[0].EnhancedKeyUsages | Where-Object { $_.Value -eq '1.3.6.1.5.5.7.3.3' }).Count -lt 1) {
  throw 'An ECDSA certificate with code-signing EKU is required.'
}

$key = [Security.Cryptography.X509Certificates.ECDsaCertificateExtensions]::GetECDsaPrivateKey($cert)
if ($null -eq $key) { throw 'Only ECDSA P-256 signing certificates are supported.' }
try {
  if ($key.KeySize -ne 256) { throw 'Signing key must be P-256.' }
  $payload = [Text.Encoding]::ASCII.GetBytes(
    "Dvinholm x64 release attestation v1\nTag: $Tag\nAsset: Dvinholm-Windows-x64.zip\nSHA256: $digest\n")
  $sig = $key.SignData($payload,
    [Security.Cryptography.HashAlgorithmName]::SHA256,
    [Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)
  if ($sig.Length -ne 64) { throw 'Wrong detached P1363 signature size.' }

  $spki = $key.ExportSubjectPublicKeyInfo()
  $verifier = [Security.Cryptography.ECDsa]::Create()
  try {
    $consumed = 0
    $verifier.ImportSubjectPublicKeyInfo($spki, [ref]$consumed)
    if ($consumed -ne $spki.Length -or -not $verifier.VerifyData(
      $payload, $sig, [Security.Cryptography.HashAlgorithmName]::SHA256,
      [Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)) {
      throw 'Local signature self-verification failed.'
    }
  } finally { $verifier.Dispose() }

  $target = [IO.Path]::GetFullPath($SignaturePath)
  if ([IO.Path]::GetFileName($target) -cne 'Dvinholm-Windows-x64.sig') {
    throw 'Output must be named Dvinholm-Windows-x64.sig.'
  }
  $encoded = [Text.Encoding]::ASCII.GetBytes([Convert]::ToBase64String($sig) + "\n")
  if ($encoded.Length -ne 89) { throw 'Wrong detached signature file size.' }
  $stream = [IO.FileStream]::new($target, [IO.FileMode]::CreateNew,
    [IO.FileAccess]::Write, [IO.FileShare]::None)
  try { $stream.Write($encoded); $stream.Flush($true) }
  finally { $stream.Dispose() }
  $fingerprint = [Convert]::ToHexString(
    [Security.Cryptography.SHA256]::HashData($spki)).ToLowerInvariant()
  Write-Host ("Signed tag: " + $Tag)
  Write-Host ("ZIP SHA256: " + $digest)
  Write-Host ("Signer SPKI SHA256: " + $fingerprint)
  Write-Host 'Not installable until the signer is independently approved and pinned.'
} finally { $key.Dispose() }
