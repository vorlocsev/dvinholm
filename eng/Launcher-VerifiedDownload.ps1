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


# The UI calls this only from a separate worker after user confirmation.
# It prepares an immutable snapshot for a future *independent* installation
# transaction and never touches the running Launcher.exe.
function Prepare-NativeLauncher {
 $script:state.LauncherCurrent=Read-LauncherInstalledBuild
 $release=LatestLauncher
 if(-not $release.Published){
  throw 'No independent native Launcher package published for this channel.'
 }
 $script:state.LauncherAvailable=[string]$release.Tag
 $relation=Get-LauncherBuildRelation $Channel $script:state.LauncherCurrent $script:state.LauncherAvailable
 if($relation -cin @('Current','Installed build newer')){
  throw 'No newer native Launcher release to prepare.'
 }
 $store=Join-Path $base 'LauncherUpdates'
 $channelStore=Join-Path $store $Channel
 foreach($folder in @((Split-Path -Parent $base),$base,$store,$channelStore)){
  Assert-ManagedFolder $folder
 }
 [IO.Directory]::CreateDirectory($channelStore)|Out-Null
 $ready=Join-Path $channelStore 'prepared'
 if(Test-Path -LiteralPath $ready){
  throw 'A prepared Launcher package already exists; inspect it before downloading another.'
 }
 $id=[guid]::NewGuid().ToString('N')
 $pending=Join-Path $channelStore ('download-'+$id)
 [IO.Directory]::CreateDirectory($pending)|Out-Null
 try{
  $archive=Join-Path $pending $launcherZipName
  $manifest=Join-Path $pending $sumsName
  $script:state.DownloadId=$id
  $script:state.DownloadTotal=[long]$release.Zip.size
  Save-State
  Progress 'Downloading native Launcher ZIP and release checksums...'
  Invoke-WebRequest -Uri $release.Zip.browser_download_url -UseBasicParsing -OutFile $archive -TimeoutSec 300
  Invoke-WebRequest -Uri $release.Sums.browser_download_url -UseBasicParsing -OutFile $manifest -TimeoutSec 60
  if((Get-Item -LiteralPath $archive).Length -ne [long]$release.Zip.size -or
     (Get-Item -LiteralPath $manifest).Length -ne [long]$release.Sums.size){
   throw 'Downloaded native Launcher asset size differs from release metadata.'
  }
  Progress 'Verifying native Launcher asset digests, manifest and staged files...'
  $digest=[string]$release.Zip.digest
  $sumDigest=[string]$release.Sums.digest
  $validated=Assert-PreparedLauncherArchive -ZipPath $archive -SumsPath $manifest -ZipSha256 $digest.Substring(7) -SumsSha256 $sumDigest.Substring(7) -ExpectedTag $release.Tag -StageDirectory (Join-Path $pending 'staged')
  $receipt=@{Channel=$Channel;Tag=$validated.Tag;ZipSha256=$validated.ZipHash;
   CreatedUtc=[DateTime]::UtcNow.ToString('o');Applied=$false}
  [IO.File]::WriteAllText((Join-Path $pending 'prepared.json'),
   ($receipt|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
  # Same-parent rename is the commit boundary; never replace running files.
  Move-Item -LiteralPath $pending -Destination $ready -ErrorAction Stop
  $script:state.LauncherDetail=('Native Launcher '+$release.Tag+
   ' downloaded and verified. Staged only; no installation performed.')
  Progress ('Native Launcher '+$release.Tag+' prepared safely. No installed files changed.')
 }finally{
  if(Test-Path -LiteralPath $pending){
   Remove-Item -LiteralPath $pending -Recurse -Force -ErrorAction SilentlyContinue
  }
  $script:state.DownloadId=''
  $script:state.DownloadTotal=0
  Save-State
 }
}
