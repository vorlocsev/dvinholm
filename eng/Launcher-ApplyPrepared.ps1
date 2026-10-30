#requires -Version 5.1
<#
Post-exit handoff for an explicitly prepared native Launcher release.
Library mode is sourced by Updater-Banner.ps1. The detached Worker is a
trusted COPY outside the installation, and rechecks the prepared package.
No downloaded PowerShell script is executed before installation.
#>
[CmdletBinding()]
param(
 [ValidateSet('Library','Worker')][string]$ApplyMode='Library',
 [ValidateSet('Development','Stable')][string]$ApplyChannel='Development',
 [string]$InstallationDirectory,
 [string]$PreparedDirectory,
 [int]$WaitForProcessId=0,
 [ValidatePattern('^(?:ci-[1-9][0-9]{0,8}|v[0-9]+\.[0-9]+\.[0-9]+)$')][string]$ExpectedTag,
 [ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedZipSha256,
 [ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedSumsSha256,
 [switch]$OfflineFixture,
 [int]$SimulateInterruptionAfter=0
)
$ErrorActionPreference='Stop'
$launcherHandoffFiles=@('Launcher-ApplyPrepared.ps1','Invoke-LauncherReplacement.ps1',
 'Assert-LauncherSelfUpdatePackage.ps1','Launcher-VerifiedDownload.ps1',
 'Launcher-RebootRecovery.ps1')
function Assert-LauncherFolder([string]$path){
 $full=[IO.Path]::GetFullPath($path)
 $at=$full
 while($at){
  if(Test-Path -LiteralPath $at){
   $item=Get-Item -LiteralPath $at -Force
   if(-not $item.PSIsContainer -or
       ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
    throw 'Launcher handoff requires ordinary, non-linked directories.'
   }
  }
  $next=[IO.Path]::GetDirectoryName($at)
  if(-not $next -or $next -ceq $at){break}
  $at=$next
 }
}
function Invoke-LauncherWorker {
 $install=[IO.Path]::GetFullPath($InstallationDirectory).TrimEnd('\')
 $prepared=[IO.Path]::GetFullPath($PreparedDirectory).TrimEnd('\')
 Assert-LauncherFolder $install
 Assert-LauncherFolder $prepared
 $archive=Join-Path $prepared 'Dvinholm-Updater-Banner-Windows-x86.zip'
 $manifest=Join-Path $prepared 'SHA256SUMS.txt'
 $recordPath=Join-Path $prepared 'prepared.json'
 if(-not(Test-Path -LiteralPath $recordPath -PathType Leaf)){throw 'Launcher preparation receipt missing.'}
 $recordInfo=Get-Item -LiteralPath $recordPath -Force
 if($recordInfo.Length -gt 4096 -or
    ($recordInfo.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
  throw 'Invalid launcher preparation receipt.'
 }
 $record=Get-Content -LiteralPath $recordPath -Raw|ConvertFrom-Json
 if([string]$record.Channel -cne $ApplyChannel -or
    [string]$record.Tag -cne $ExpectedTag -or
    [string]$record.ZipSha256 -cne $ExpectedZipSha256 -or
    [bool]$record.Applied){
  throw 'Prepared native Launcher receipt does not match trusted release metadata.'
 }
 $verify=Join-Path $PSScriptRoot 'Launcher-VerifiedDownload.ps1'
 $replace=Join-Path $PSScriptRoot 'Invoke-LauncherReplacement.ps1'
 $recovery=Join-Path $PSScriptRoot 'Launcher-RebootRecovery.ps1'
 . $verify
 # A separate temporary stage re-verifies all files, digest and tag. The
 # earlier "prepared/staged" folder is intentionally never trusted.
 $validation=Join-Path $PSScriptRoot ('checked-'+[guid]::NewGuid().ToString('N'))
 $hasher=[Security.Cryptography.SHA256]::Create()
 try{
  $digest=$hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($install.ToLowerInvariant()))
 }finally{$hasher.Dispose()}
 $key=([BitConverter]::ToString($digest)-replace '-','').Substring(0,24)
 $mutexName='Local\Dvinholm.NativeLauncher.'+$key
 $mutex=[Threading.Mutex]::new($false,$mutexName)
 $owned=$false
 try{
  try{$owned=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$owned=$true}
  if(-not $owned){throw 'Another native Launcher replacement is already running.'}
  Assert-PreparedLauncherArchive -ZipPath $archive -SumsPath $manifest -ZipSha256 $ExpectedZipSha256 -SumsSha256 $ExpectedSumsSha256 -ExpectedTag $ExpectedTag -StageDirectory $validation|Out-Null
  # Must wait for the PowerShell GUI PID, not the short-lived native EXE.
  $arguments=@{Mode='Apply';InstallationDirectory=$install;ZipPath=$archive;
   ExpectedSha256=$ExpectedZipSha256;WaitForProcessId=$WaitForProcessId}
  if($OfflineFixture){
   $arguments.OfflineFixture=$true
   if($SimulateInterruptionAfter -gt 0){$arguments.SimulateInterruptionAfter=$SimulateInterruptionAfter}
  }
  if(-not $OfflineFixture){
   # Register a trusted outside-install HKCU Run recovery before mutating
   # any live Launcher file; abort entirely if registration is unavailable.
   & $recovery -Mode Arm -InstallationDirectory $install -ExpectedTag $ExpectedTag|Out-Null
  }
  try{
   & $replace @arguments|Out-Null
   $backup=Join-Path $install '.launcher-previous\journal.json'
   if(-not(Test-Path -LiteralPath $backup -PathType Leaf)){
    throw 'Native Launcher replacement did not commit a verified backup.'
   }
   $built=Join-Path $install 'Binaries\launcher-build.txt'
   if([IO.File]::ReadAllText($built,[Text.Encoding]::ASCII).Trim() -cne $ExpectedTag){
    throw 'Installed native Launcher build receipt mismatch.'
   }
   if(-not $OfflineFixture){
    $exe=Join-Path $install 'Launcher.exe'
    $next=Start-Process -FilePath $exe -WorkingDirectory $install -PassThru -ErrorAction Stop
    if($null -eq $next){throw 'Restarting native Launcher failed.'}
    $next.Dispose()
   }
   if(-not $OfflineFixture){
    & $recovery -Mode Disarm -InstallationDirectory $install|Out-Null
   }
   Write-Host ('Native Launcher '+$ExpectedTag+' installed and '+$(if($OfflineFixture){'verified offline.'}else{'restarted.'}))
  }catch{
   $failure=[string]$_.Exception.Message
   # Recover interrupted writes or undo an already committed swap when
   # spawning the replacement fails. Never start from unverified backups.
   if(Test-Path -LiteralPath (Join-Path $install '.launcher-transaction')){
    & $replace -Mode Recover -InstallationDirectory $install|Out-Null
   }elseif(Test-Path -LiteralPath (Join-Path $install '.launcher-previous\journal.json')){
    & $replace -Mode Undo -InstallationDirectory $install|Out-Null
   }
   if(-not $OfflineFixture -and
      -not(Test-Path -LiteralPath (Join-Path $install '.launcher-transaction'))){
    # The original launcher can be started again only after the failed
    # transaction is absent or its verified rollback completed.
    try{Start-Process -FilePath (Join-Path $install 'Launcher.exe') -WorkingDirectory $install -ErrorAction Stop|Out-Null}catch{}
   }
   if(-not $OfflineFixture -and
      -not(Test-Path -LiteralPath (Join-Path $install '.launcher-transaction'))){
    try{& $recovery -Mode Disarm -InstallationDirectory $install|Out-Null}catch{
     throw ('Native update failed, but recovery is still armed for next sign-in: '+$failure)
    }
   }
   throw ('Native Launcher apply failed; verified rollback attempted: '+$failure)
  }
 }finally{
  if(Test-Path -LiteralPath $validation){
   Remove-Item -LiteralPath $validation -Recurse -Force -ErrorAction SilentlyContinue
  }
  if($owned){$mutex.ReleaseMutex()}
  $mutex.Dispose()
 }
}
function Start-LauncherPreparedHandoff {
 [CmdletBinding()]
 param([Parameter(Mandatory)][string]$ChannelName,
  [Parameter(Mandatory)][int]$GuiProcessId)
 if($ChannelName -cnotin @('Stable','Development') -or $GuiProcessId -le 0){
  throw 'Invalid Launcher handoff channel or UI process.'
 }
 $install=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')
 $ready=Join-Path (Join-Path (Join-Path $base 'LauncherUpdates') $ChannelName) 'prepared'
 Assert-LauncherFolder $install
 Assert-LauncherFolder $ready
 if(-not(Test-Path -LiteralPath (Join-Path $ready 'prepared.json') -PathType Leaf)){
  throw 'Download and verify the Launcher update before installing.'
 }
 if(Test-Path -LiteralPath (Join-Path $install '.launcher-transaction')){
  throw 'Interrupted native Launcher update requires verified recovery first.'
 }
 if(Test-Path -LiteralPath (Join-Path $install '.launcher-previous')){
  throw 'Previous native Launcher backup must be managed before another update.'
 }
 if(Test-Path -LiteralPath (Join-Path $install '.launcher-retired')){
  throw 'Unfinished native Launcher backup retirement must be finalized before another update.'
 }
 # The selected release and digests are fetched AGAIN from trusted GitHub
 # metadata immediately before handoff, not accepted from mutable local JSON.
 $script:Channel=$ChannelName
 $release=LatestLauncher
 if(-not $release.Published){throw 'No published native Launcher package for this channel.'}
 $relation=Get-LauncherBuildRelation $ChannelName (Read-LauncherInstalledBuild) $release.Tag
 if($relation -cne 'Newer release available'){
  throw ('Native Launcher apply requires a newer build in the same channel: '+$relation)
 }
 $expectedZip=([string]$release.Zip.digest).Substring(7)
 $expectedSums=([string]$release.Sums.digest).Substring(7)
 $handoffParent=Join-Path $base 'LauncherHandoff'
 Assert-LauncherFolder $handoffParent
 [IO.Directory]::CreateDirectory($handoffParent)|Out-Null
 $handoff=Join-Path $handoffParent ('worker-'+[guid]::NewGuid().ToString('N'))
 [IO.Directory]::CreateDirectory($handoff)|Out-Null
 try{
  foreach($name in $launcherHandoffFiles){
   $from=Join-Path $PSScriptRoot $name
   $to=Join-Path $handoff $name
   $item=Get-Item -LiteralPath $from -Force -ErrorAction Stop
   if($item.PSIsContainer -or
      ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
    throw 'Cannot copy non-ordinary trusted Launcher helper.'
   }
   [IO.File]::Copy($from,$to,$false)
   if((Get-FileHash -LiteralPath $from -Algorithm SHA256).Hash -cne
      (Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash){
    throw 'Launcher handoff trusted helper copy differs from installed version.'
   }
  }
  $script=Join-Path $handoff 'Launcher-ApplyPrepared.ps1'
  $args='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$script+
    '" -ApplyMode Worker -ApplyChannel '+$ChannelName+' -InstallationDirectory "'+$install+
    '" -PreparedDirectory "'+$ready+'" -WaitForProcessId '+$GuiProcessId+
    ' -ExpectedTag '+$release.Tag+' -ExpectedZipSha256 '+$expectedZip+
    ' -ExpectedSumsSha256 '+$expectedSums
  $ps=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
  $process=Start-Process -FilePath $ps -ArgumentList $args -WindowStyle Hidden -PassThru -ErrorAction Stop
  if($null -eq $process){throw 'Unable to launch independent native Launcher worker.'}
  $process.Dispose()
  return $true
 }catch{
  if(Test-Path -LiteralPath $handoff){
   Remove-Item -LiteralPath $handoff -Recurse -Force -ErrorAction SilentlyContinue
  }
  throw
 }
}
if($ApplyMode -eq 'Worker'){
 $result='failure'
 $detail='Unknown native Launcher worker error.'
 try{
  Invoke-LauncherWorker
  $result='success'
  $detail=('Native Launcher '+$ExpectedTag+' applied.')
 }catch{
  $detail=[string]$_.Exception.Message
  if(-not $OfflineFixture){
   try{
    $root=[IO.Path]::GetFullPath($InstallationDirectory).TrimEnd('\')
    if(-not(Test-Path -LiteralPath (Join-Path $root '.launcher-transaction')) -and
       -not(Test-Path -LiteralPath (Join-Path $root '.launcher-previous'))){
     Start-Process -FilePath (Join-Path $root 'Launcher.exe') -WorkingDirectory $root -ErrorAction Stop|Out-Null
    }
   }catch{}
  }
  throw
 }finally{
  try{
   # Persist a minimal success/error record for the next launcher session.
   $logRoot=[IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($PreparedDirectory))
   Assert-LauncherFolder $logRoot
   [IO.File]::WriteAllText((Join-Path $logRoot 'last-apply.log'),
    ([DateTime]::UtcNow.ToString('o')+' | '+$result+' | '+$detail),
    [Text.UTF8Encoding]::new($false))
  }catch{}
 }
}
