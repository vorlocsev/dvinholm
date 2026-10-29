#requires -Version 5.1
<#
Offline, opt-in post-exit replacement transaction for a verified launcher ZIP.
This helper must be started from a trusted COPY OUTSIDE the installation
directory; invoking it does not download archives, start Windows applications
or request privileges. The normal UI does not invoke it yet.
#>
[CmdletBinding()]
param(
 [ValidateSet('Apply','Recover')][string]$Mode='Recover',
 [Parameter(Mandatory)][string]$InstallationDirectory,
 [string]$ZipPath,
 [ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedSha256,
 [int]$WaitForProcessId=0,
 [switch]$OfflineFixture,
 [int]$SimulateInterruptionAfter=0
)
$ErrorActionPreference='Stop'
$names=@(
 'Launcher.exe',
 'Binaries/Updater-Banner.ps1',
 'Binaries/Launcher-AuroraTheme.ps1',
 'Binaries/Launcher-ServiceCockpit.ps1',
 'Binaries/J2534-DriverManager.ps1',
 'Binaries/J2534-DriverUI.ps1',
 'Binaries/Manage-Lite-Dependencies.ps1',
 'Binaries/Dvinholm-icon.png',
 'Binaries/dvinholm-banner.webp',
 'Binaries/launcher-service-cockpit-banner.jpg',
 'Binaries/Assert-LauncherSelfUpdatePackage.ps1',
 'Binaries/Invoke-LauncherReplacement.ps1'
)
$root=[IO.Path]::GetFullPath($InstallationDirectory).TrimEnd('\')
$txn=Join-Path $root '.launcher-transaction'
$previous=Join-Path $root '.launcher-previous'
$journal=Join-Path $txn 'journal.json'
$stage=Join-Path $txn 'stage'
$backup=Join-Path $txn 'backup'

function Assert-Ordinary([string]$path,[bool]$folder=$false){
 if(-not(Test-Path -LiteralPath $path)){return}
 $item=Get-Item -LiteralPath $path -Force
 if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
  throw 'Launcher transaction refuses reparse paths.'
 }
 if($folder -and -not $item.PSIsContainer){throw 'Expected a regular directory.'}
 if(-not $folder -and $item.PSIsContainer){throw 'Expected an ordinary file.'}
}
function Assert-Ancestors([string]$path){
 $p=$path
 while($p){
  if(Test-Path -LiteralPath $p){Assert-Ordinary $p $true}
  $next=[IO.Path]::GetDirectoryName($p)
  if(-not $next -or $next -ceq $p){break}
  $p=$next
 }
}
function Fingerprint([string]$path){
 return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Child([string]$parent,[string]$name){
 return Join-Path $parent $name.Replace('/','\')
}
function Put-File([string]$from,[string]$to){
 # Same-volume temporary copy and Windows atomic replacement for existing files.
 $parent=[IO.Path]::GetDirectoryName($to)
 Assert-Ordinary $parent $true
 [IO.Directory]::CreateDirectory($parent)|Out-Null
 Assert-Ordinary $to
 $temp=Join-Path $parent ('.dvinholm-new-'+[guid]::NewGuid().ToString('N')+'.tmp')
 $replacedBackup=Join-Path $parent ('.dvinholm-old-'+[guid]::NewGuid().ToString('N')+'.tmp')
 try{
  [IO.File]::Copy($from,$temp,$false)
  if(Test-Path -LiteralPath $to){
   # .NET Framework 4.x requires a valid backup path on some Windows hosts.
   # The independent verified transaction/backup remains the recovery source.
   [IO.File]::Replace($temp,$to,$replacedBackup)
  }else{
   [IO.File]::Move($temp,$to)
  }
 }finally{
  foreach($leftover in @($temp,$replacedBackup)){
   if(Test-Path -LiteralPath $leftover){
    Remove-Item -LiteralPath $leftover -Force -ErrorAction SilentlyContinue
   }
  }
 }
}
function Read-Journal {
 if(-not(Test-Path -LiteralPath $journal -PathType Leaf)){return $null}
 Assert-Ordinary $journal
 if((Get-Item -LiteralPath $journal).Length -gt 10KB){
  throw 'Launcher transaction journal size exceeded.'
 }
 $data=Get-Content -LiteralPath $journal -Raw|ConvertFrom-Json
 if([int]$data.Version -ne 1 -or @($data.Entries).Count -ne $names.Count){
  throw 'Invalid launcher transaction journal.'
 }
 for($i=0;$i -lt $names.Count;$i++){
  $part=$data.Entries[$i]
  if([string]$part.Name -cne $names[$i] -or
     [string]$part.NewHash -cnotmatch '^[0-9a-f]{64}$' -or
     ([string]$part.OldHash -cne '' -and
      [string]$part.OldHash -cnotmatch '^[0-9a-f]{64}$')){
   throw 'Launcher transaction journal paths or hashes are invalid.'
  }
 }
 return $data
}
function Recover-Interrupted {
 if(-not(Test-Path -LiteralPath $txn)){return}
 Assert-Ordinary $txn $true
 Assert-Ordinary $stage $true
 Assert-Ordinary $backup $true
 $data=Read-Journal
 if($null -eq $data){
  # Journal is written only AFTER all backups have been completed and checked.
  # A pre-journal interruption did not modify any live file.
  Remove-Item -LiteralPath $txn -Recurse -Force
  Write-Host 'Discarded incomplete launcher preparation; live files were unchanged.'
  return
 }
 # Validate EVERY required original backup BEFORE modifying any live file.
 foreach($part in $data.Entries){
  if([string]$part.OldHash){
   $saved=Child $backup ([string]$part.Name)
   Assert-Ordinary $saved
   if(-not(Test-Path -LiteralPath $saved -PathType Leaf) -or
      (Fingerprint $saved) -cne [string]$part.OldHash){
    throw 'Cannot safely recover: original launcher backup is missing or corrupt.'
   }
  }
 }
 foreach($part in $data.Entries){
  $target=Child $root ([string]$part.Name)
  Assert-Ordinary $target
  if([string]$part.OldHash){
   Put-File (Child $backup ([string]$part.Name)) $target
   if((Fingerprint $target) -cne [string]$part.OldHash){
    throw 'Launcher rollback content verification failed.'
   }
  }elseif(Test-Path -LiteralPath $target){
   # A file newly introduced by this update did not exist beforehand.
   # Refuse to delete an unexpected third-party modification.
   if((Fingerprint $target) -cne [string]$part.NewHash){
    throw 'New launcher file was changed; manual inspection is required.'
   }
   Remove-Item -LiteralPath $target -Force
  }
 }
 Remove-Item -LiteralPath $txn -Recurse -Force
 Write-Host 'Interrupted launcher replacement recovered from verified backups.'
}
Assert-Ancestors $root
if(-not(Test-Path -LiteralPath $root -PathType Container) -or
   -not(Test-Path -LiteralPath (Join-Path $root 'Binaries') -PathType Container)){
 throw 'Launcher installation directory is incomplete.'
}
Assert-Ordinary (Join-Path $root 'Binaries') $true
Assert-Ordinary $txn $true
Assert-Ordinary $previous $true
if($Mode -eq 'Recover'){
 Recover-Interrupted
 exit 0
}
if(-not $ZipPath -or -not $ExpectedSha256){
 throw 'A verified launcher archive and digest are required.'
}
if($SimulateInterruptionAfter -ne 0 -and -not $OfflineFixture){
 throw 'Injected interruptions are allowed only in offline test fixtures.'
}
if($OfflineFixture){
 $tempBase=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
 if(-not $root.StartsWith($tempBase,[StringComparison]::OrdinalIgnoreCase) -or
     $root -notmatch '(?i)[\\/]dvinholm-selfswap-[0-9a-f]{32}[\\/][a-z0-9_-]+$'){
  throw 'Offline fixture must be under a dedicated temporary test directory.'
 }
}elseif($WaitForProcessId -le 0){
 throw 'Apply requires the Launcher UI process ID to wait for its exit.'
}
if(Test-Path -LiteralPath $txn){throw 'Interrupted transaction requires Recover first.'}
if(Test-Path -LiteralPath $previous){
 throw 'Launcher backup already exists; preserve it until rollback management is available.'
}
Assert-Ordinary (Join-Path $root 'Launcher.exe')
Assert-Ordinary (Join-Path $root 'Binaries\Updater-Banner.ps1')
if(-not(Test-Path -LiteralPath (Join-Path $root 'Launcher.exe') -PathType Leaf) -or
   -not(Test-Path -LiteralPath (Join-Path $root 'Binaries\Updater-Banner.ps1') -PathType Leaf)){
 throw 'Native launcher installation is incomplete.'
}
# Prepare and revalidate bytes using only the TRUSTED helper script, before
# touching original files. A previously staged folder is never trusted.
[IO.Directory]::CreateDirectory($txn)|Out-Null
try{
 $validator=Join-Path $PSScriptRoot 'Assert-LauncherSelfUpdatePackage.ps1'
 if(-not(Test-Path -LiteralPath $validator -PathType Leaf)){
  throw 'Trusted launch archive validator is missing.'
 }
 & $validator -ZipPath $ZipPath -ExpectedSha256 $ExpectedSha256 -StagingDirectory $stage|Out-Null
 [IO.Directory]::CreateDirectory($backup)|Out-Null
 $items=@()
 foreach($name in $names){
  $target=Child $root $name
  $candidate=Child $stage $name
  Assert-Ordinary $target
  Assert-Ordinary $candidate
  if(-not(Test-Path -LiteralPath $candidate -PathType Leaf)){
   throw 'Verified stage does not contain every launcher file.'
  }
  $old=''
  if(Test-Path -LiteralPath $target -PathType Leaf){
   $old=Fingerprint $target
   $save=Child $backup $name
   [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($save))|Out-Null
   [IO.File]::Copy($target,$save,$false)
   if((Fingerprint $save) -cne $old){throw 'Original launcher backup mismatch.'}
  }
  $items+=@{Name=$name;OldHash=$old;NewHash=(Fingerprint $candidate)}
 }
 if(-not $OfflineFixture -and $WaitForProcessId -gt 0){
  $process=Get-Process -Id $WaitForProcessId -ErrorAction SilentlyContinue
  if($null -ne $process){
   try{
    if(-not $process.WaitForExit(60000)){
     throw 'Launcher UI did not exit within the safety window.'
    }
   }finally{$process.Dispose()}
  }
 }
 if(Get-Process -Name 'Dvinholm.App' -ErrorAction SilentlyContinue){
  throw 'Close Dvinholm before replacing shared launcher support files.'
 }
 $record=@{Version=1;Entries=$items}
 $tempJournal=Join-Path $txn 'journal-prepared.tmp'
 [IO.File]::WriteAllText($tempJournal,($record|ConvertTo-Json -Depth 5),
   [Text.UTF8Encoding]::new($false))
 [IO.File]::Move($tempJournal,$journal)
 $written=0
 foreach($part in $items){
  $candidate=Child $stage $part.Name
  $target=Child $root $part.Name
  if((Fingerprint $candidate) -cne $part.NewHash){
   throw 'Staged launcher file changed after verification.'
  }
  Put-File $candidate $target
  if((Fingerprint $target) -cne $part.NewHash){
   throw 'Installed launcher file differs from staged bytes.'
  }
  $written++
  if($SimulateInterruptionAfter -gt 0 -and $written -eq $SimulateInterruptionAfter){
   throw 'Simulated interruption: recover the prepared launcher journal.'
  }
 }
 Move-Item -LiteralPath $txn -Destination $previous -ErrorAction Stop
 Write-Host 'Verified launcher replacement committed; previous version preserved.'
}catch{
 # Never erase a journal when even one live file may have been replaced.
 # Recover mode can replay verified backups after an interrupted operation.
 if(-not(Test-Path -LiteralPath $journal) -and (Test-Path -LiteralPath $txn)){
  Remove-Item -LiteralPath $txn -Recurse -Force -ErrorAction SilentlyContinue
 }
 throw
}
