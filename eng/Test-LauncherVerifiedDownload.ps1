#requires -Version 5.1
<#
Eight offline fixture checks against the REAL native Launcher ZIP and its
actual SHA256SUMS manifest. No HTTP, user installation or execution.
#>
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$ZipPath,
 [Parameter(Mandatory)][string]$SumsPath,
 [Parameter(Mandatory)][string]$ExpectedTag
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Launcher-VerifiedDownload.ps1')
$zip=(Resolve-Path -LiteralPath $ZipPath).Path
$sums=(Resolve-Path -LiteralPath $SumsPath).Path
$root=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-native-download-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
$zipHash=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$sumsHash=(Get-FileHash $sums -Algorithm SHA256).Hash.ToLowerInvariant()
$checks=0
function Verify([string]$archive,[string]$manifest,[string]$zipDigest,
 [string]$manifestDigest,[string]$tag,[string]$name){
 Assert-PreparedLauncherArchive -ZipPath $archive -SumsPath $manifest -ZipSha256 $zipDigest -SumsSha256 $manifestDigest -ExpectedTag $tag -StageDirectory (Join-Path $root $name)|Out-Null
}
function Reject([string]$archive,[string]$manifest,[string]$zipDigest,
 [string]$manifestDigest,[string]$tag,[string]$name,[bool]$existing=$false){
 $target=Join-Path $root $name
 $wasThere=Test-Path -LiteralPath $target
 $failed=$false
 try{Verify $archive $manifest $zipDigest $manifestDigest $tag $name}catch{$failed=$true}
 if(-not $failed){throw ('Unsafe launcher release accepted: '+$name)}
 if(-not $existing -and (Test-Path -LiteralPath $target)){
  throw ('Rejected launcher release staged unexpected files: '+$name)
 }
 if($existing -and -not $wasThere){throw 'Existing fixture unexpectedly absent.'}
 $script:checks++
}
try {
 # 1: full release metadata, final ZIP and manifest agree.
 Verify $zip $sums $zipHash $sumsHash $ExpectedTag 'valid'
 if(-not(Test-Path -LiteralPath (Join-Path $root 'valid\Launcher.exe') -PathType Leaf) -or
    -not(Test-Path -LiteralPath (Join-Path $root 'valid\Binaries\launcher-build.txt') -PathType Leaf)){
  throw 'Valid Launcher archive was not staged.'
 }
 $checks++
 # 2: mismatched ZIP API digest.
 Reject $zip $sums ('0'*64) $sumsHash $ExpectedTag 'wrong-zip-hash'
 # 3: mismatched checksum file API digest.
 Reject $zip $sums $zipHash ('0'*64) $ExpectedTag 'wrong-sums-hash'
 # 4: validly hashed manifest with no standalone Launcher line.
 $removed=Join-Path $root 'missing.txt'
 [IO.File]::WriteAllLines($removed,[string[]]@(('0'*64)+'  unrelated.zip'),[Text.Encoding]::ASCII)
 $removedHash=(Get-FileHash $removed -Algorithm SHA256).Hash.ToLowerInvariant()
 Reject $zip $removed $zipHash $removedHash $ExpectedTag 'missing-zip-entry'
 # 5: validly hashed manifest with two identical Launcher entries.
 $duplicate=Join-Path $root 'duplicate.txt'
 $entry=$zipHash+'  Dvinholm-Updater-Banner-Windows-x86.zip'
 [IO.File]::WriteAllLines($duplicate,[string[]]@($entry,$entry),[Text.Encoding]::ASCII)
 $duplicateHash=(Get-FileHash $duplicate -Algorithm SHA256).Hash.ToLowerInvariant()
 Reject $zip $duplicate $zipHash $duplicateHash $ExpectedTag 'duplicate-zip-entry'
 # 6: rehashed replacement ZIP with original manifest (must reject).
 $changed=Join-Path $root 'changed.zip'
 [IO.File]::Copy($zip,$changed)
 [IO.File]::AppendAllText($changed,'changed')
 $changedHash=(Get-FileHash $changed -Algorithm SHA256).Hash.ToLowerInvariant()
 Reject $changed $sums $changedHash $sumsHash $ExpectedTag 'changed-archive'
 # 7: correct assets but wrong requested release identity.
 $otherTag=if($ExpectedTag -match '^ci-'){'v0.2.4'}else{'ci-999999'}
 Reject $zip $sums $zipHash $sumsHash $otherTag 'wrong-tag'
 # 8: never overwrite an existing staged download directory.
 Reject $zip $sums $zipHash $sumsHash $ExpectedTag 'valid' $true
 if($checks -ne 8){throw 'Wrong native Launcher download test count.'}
 Write-Host 'Launcher release download verification: 8/8 offline checks passed.'
}finally{
 if(Test-Path -LiteralPath $root){Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue}
}
