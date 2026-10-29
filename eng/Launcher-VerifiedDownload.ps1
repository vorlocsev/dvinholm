#requires -Version 5.1
<#
Offline verification of a downloaded native Launcher update and manifest.
Caller must get both expected asset digests from validated GitHub release
metadata. This module NEVER installs, executes or starts an archive member.
#>
function Assert-PreparedLauncherArchive {
 [CmdletBinding()]
 param(
  [Parameter(Mandatory)][string]$ZipPath,
  [Parameter(Mandatory)][string]$SumsPath,
  [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ZipSha256,
  [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$SumsSha256,
  [Parameter(Mandatory)][ValidatePattern('^(?:ci-[1-9][0-9]{0,8}|v[0-9]+\.[0-9]+\.[0-9]+)$')][string]$ExpectedTag,
  [Parameter(Mandatory)][string]$StageDirectory
 )
 $zipName='Dvinholm-Updater-Banner-Windows-x86.zip'
 $zipInfo=Get-Item -LiteralPath $ZipPath -Force -ErrorAction Stop
 $sumsInfo=Get-Item -LiteralPath $SumsPath -Force -ErrorAction Stop
 foreach($item in @($zipInfo,$sumsInfo)){
  if($item.PSIsContainer -or
     ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
   throw 'Launcher release assets must be ordinary files.'
  }
 }
 if($zipInfo.Length -lt 1 -or $zipInfo.Length -gt 2MB -or
    $sumsInfo.Length -lt 1 -or $sumsInfo.Length -gt 128KB){
  throw 'Launcher release asset size budget exceeded.'
 }
 if(Test-Path -LiteralPath $StageDirectory){
  throw 'Prepared launcher destination already exists.'
 }
 $realZip=(Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
 $realSums=(Get-FileHash -LiteralPath $SumsPath -Algorithm SHA256).Hash.ToLowerInvariant()
 if($realZip -cne $ZipSha256 -or $realSums -cne $SumsSha256){
  throw 'Launcher release asset does not match trusted GitHub digest.'
 }
 $matches=@([IO.File]::ReadAllLines($sumsInfo.FullName,[Text.Encoding]::ASCII) |
  Where-Object{$_ -cmatch ('^[0-9a-f]{64}  '+[regex]::Escape($zipName)+'$')})
 if($matches.Count -ne 1 -or
    $matches[0].Substring(0,64) -cne $realZip){
  throw 'Launcher ZIP does not match the unique SHA256SUMS entry.'
 }
 $validator=Join-Path $PSScriptRoot 'Assert-LauncherSelfUpdatePackage.ps1'
 if(-not(Test-Path -LiteralPath $validator -PathType Leaf)){
  throw 'Trusted native Launcher archive validator is missing.'
 }
 $created=$false
 try {
  & $validator -ZipPath $ZipPath -ExpectedSha256 $ZipSha256 -StagingDirectory $StageDirectory | Out-Null
  $created=$true
  $receipt=Join-Path $StageDirectory 'Binaries\launcher-build.txt'
  $item=Get-Item -LiteralPath $receipt -Force -ErrorAction Stop
  if($item.PSIsContainer -or $item.Length -lt 3 -or $item.Length -gt 32 -or
     ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
   throw 'Staged native Launcher build receipt invalid.'
  }
  $tag=[IO.File]::ReadAllText($receipt,[Text.Encoding]::ASCII).Trim()
  if($tag -cne $ExpectedTag){
   throw 'Staged native Launcher version does not match GitHub release tag.'
  }
  if((Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $ZipSha256){
   throw 'Launcher ZIP changed during staging.'
  }
  return [pscustomobject]@{Tag=$tag;ZipHash=$realZip;FilesVerified=14}
 }catch{
  # Only the validator or this call created this absent target.
  if($created -and (Test-Path -LiteralPath $StageDirectory)){
   Remove-Item -LiteralPath $StageDirectory -Recurse -Force -ErrorAction SilentlyContinue
  }
  throw
 }
}
