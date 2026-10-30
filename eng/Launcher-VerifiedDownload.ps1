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
  return [pscustomobject]@{Tag=$tag;ZipHash=$realZip;FilesVerified=15}
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


# Explicit local storage maintenance. No network calls, execution of staged
# members, replacement of installed files or automatic startup deletion.
$launcherStorageFiles=@(
 'Launcher.exe',
 'Binaries/Updater-Banner.ps1',
 'Binaries/Launcher-AuroraTheme.ps1',
 'Binaries/Launcher-ServiceCockpit.ps1',
 'Binaries/Launcher-ReleaseDiscovery.ps1',
 'Binaries/Launcher-VerifiedDownload.ps1',
 'Binaries/Launcher-ApplyPrepared.ps1',
 'Binaries/J2534-DriverManager.ps1',
 'Binaries/J2534-DriverUI.ps1',
 'Binaries/Manage-Lite-Dependencies.ps1',
 'Binaries/Dvinholm-icon.png',
 'Binaries/dvinholm-banner.webp',
 'Binaries/launcher-service-cockpit-banner.jpg',
 'Binaries/Assert-LauncherSelfUpdatePackage.ps1',
 'Binaries/Invoke-LauncherReplacement.ps1',
 'Binaries/launcher-build.txt'
)
function Assert-NativeCleanupFolder([string]$path) {
 $p=[IO.Path]::GetFullPath($path)
 while($p){
  if(Test-Path -LiteralPath $p){
   $item=Get-Item -LiteralPath $p -Force
   if(-not $item.PSIsContainer -or
      ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
    throw 'Unsafe native Launcher storage directory.'
   }
  }
  $next=[IO.Path]::GetDirectoryName($p)
  if(-not $next -or $next -ceq $p){break}
  $p=$next
 }
}
function Assert-NativeCleanupTree([string]$folder) {
 Assert-NativeCleanupFolder $folder
 if(-not(Test-Path -LiteralPath $folder -PathType Container)){
  throw 'Native Launcher cleanup target is not an ordinary directory.'
 }
 $allowed=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 foreach($name in @('Dvinholm-Updater-Banner-Windows-x86.zip','SHA256SUMS.txt','prepared.json','staged','staged/Binaries')){
  [void]$allowed.Add($name.Replace('/','\'))
 }
 foreach($name in $launcherStorageFiles){
  [void]$allowed.Add(('staged/'+$name).Replace('/','\'))
 }
 $entries=@(Get-ChildItem -LiteralPath $folder -Recurse -Force -ErrorAction Stop)
 if($entries.Count -gt 45){throw 'Native Launcher cleanup directory has too many entries.'}
 $total=[long]0
 foreach($item in $entries){
  if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
   throw 'Native Launcher cleanup refuses reparse points.'
  }
  $rel=$item.FullName.Substring($folder.Length).TrimStart('\')
  if(-not $allowed.Contains($rel)){
   throw ('Native Launcher cleanup refuses unexpected entry: '+$rel)
  }
  if(-not $item.PSIsContainer){
   $total+=[long]$item.Length
   if($item.Length -gt 2MB -or $total -gt 8MB){
    throw 'Native Launcher cleanup size budget exceeded.'
   }
  }
 }
}
function Invoke-NativeLauncherStorageCleanup {
 [CmdletBinding()]
 param(
  [Parameter(Mandatory)][string]$StorageRoot,
  [Parameter(Mandatory)][ValidateSet('Development','Stable')][string]$SelectedChannel,
  [Parameter(Mandatory)][string]$InstalledBuild,
  [Parameter(Mandatory)][string]$InstallationDirectory,
  [datetime]$NowUtc=[DateTime]::UtcNow
 )
 $channelFolder=Join-Path $StorageRoot $SelectedChannel
 Assert-NativeCleanupFolder $StorageRoot
 Assert-NativeCleanupFolder $channelFolder
 if(-not(Test-Path -LiteralPath $channelFolder -PathType Container)){
  return [pscustomobject]@{Discarded=0;ObsoletePrepared=0;Preserved=0}
 }
 # Serialize with installation/undo/finalize worker. Reuse exactly the
 # installation-scoped mutex name used by Launcher-ApplyPrepared.ps1.
 $install=[IO.Path]::GetFullPath($InstallationDirectory).TrimEnd('\')
 $sha=[Security.Cryptography.SHA256]::Create()
 try{$digest=$sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($install.ToLowerInvariant()))}
 finally{$sha.Dispose()}
 $key=([BitConverter]::ToString($digest)-replace '-','').Substring(0,24)
 $mutex=[Threading.Mutex]::new($false,('Local\Dvinholm.NativeLauncher.'+$key))
 $owned=$false
 $discarded=0;$obsolete=0;$preserved=0
 try{
  try{$owned=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$owned=$true}
  if(-not $owned){throw 'Native Launcher update is active. Cleanup blocked.'}
  $directories=@(Get-ChildItem -LiteralPath $channelFolder -Directory -Force -ErrorAction Stop)
  foreach($dir in $directories){
   if(($dir.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
    throw 'Native Launcher storage contains a directory link.'
   }
   $name=[string]$dir.Name
   $ready=($name -ceq 'prepared')
   $oldDownload=($name -cmatch '^download-[0-9a-f]{32}$' -and
      $dir.LastWriteTimeUtc -lt $NowUtc.ToUniversalTime().AddHours(-48))
   $discard=($name -cmatch '^discard-[0-9a-f]{32}$')
   $targetObsolete=$false
   if($ready){
    $recordPath=Join-Path $dir.FullName 'prepared.json'
    if(Test-Path -LiteralPath $recordPath -PathType Leaf){
     $recordItem=Get-Item -LiteralPath $recordPath -Force
     if($recordItem.Length -gt 0 -and $recordItem.Length -le 4096 -and
        ($recordItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0){
      try{
       $receipt=Get-Content -LiteralPath $recordPath -Raw -ErrorAction Stop|ConvertFrom-Json -ErrorAction Stop
       if([string]$receipt.Channel -ceq $SelectedChannel -and
          [string]$receipt.Tag -cmatch '^(?:ci-[1-9][0-9]{0,8}|v[0-9]+\.[0-9]+\.[0-9]+)$'){
        $relation=Get-LauncherBuildRelation $SelectedChannel $InstalledBuild ([string]$receipt.Tag)
        # No missing or unverified installed build can retire a usable update.
        $targetObsolete=($relation -cin @('Current','Installed build newer'))
       }
      }catch{}
     }
    }
   }
   if(-not($oldDownload -or $discard -or $targetObsolete)){
    $preserved++
    continue
   }
   Assert-NativeCleanupTree $dir.FullName
   if(-not $discard){
    $newName= 'discard-'+[guid]::NewGuid().ToString('N')
    $newPath=Join-Path $channelFolder $newName
    Move-Item -LiteralPath $dir.FullName -Destination $newPath -ErrorAction Stop
   }else{$newPath=$dir.FullName}
   # If deletion is interrupted, the bounded discard-* directory is safe
   # to inspect and retry explicitly on the next launch.
   Assert-NativeCleanupTree $newPath
   Remove-Item -LiteralPath $newPath -Recurse -Force -ErrorAction Stop
   $discarded++
   if($targetObsolete){$obsolete++}
  }
  return [pscustomobject]@{Discarded=$discarded;ObsoletePrepared=$obsolete;Preserved=$preserved}
 }finally{
  if($owned){$mutex.ReleaseMutex()}
  $mutex.Dispose()
 }
}
