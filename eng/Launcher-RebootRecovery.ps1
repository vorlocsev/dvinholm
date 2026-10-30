#requires -Version 5.1
<#
Standalone, trusted outside-install recovery for native Launcher updates.
HKCU Run registration is opt-in and installed only after explicit native
update confirmation, before mutating any Launcher file. OfflineFixture
uses a bounded temp-only fake registry; never writes a real startup entry.
#>
[CmdletBinding()]
param(
 [Parameter(Mandatory)][ValidateSet('Arm','Resume','Disarm')][string]$Mode,
 [Parameter(Mandatory)][string]$InstallationDirectory,
 [string]$ExpectedTag,
 [switch]$OfflineFixture,
 [string]$FixtureRegistryDirectory
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($InstallationDirectory).TrimEnd('\')
$helper=Join-Path $PSScriptRoot 'Invoke-LauncherReplacement.ps1'
$checkpoint=Join-Path $PSScriptRoot 'launcher-recovery.json'
$registryPath='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$sha=[Security.Cryptography.SHA256]::Create()
try{$digest=$sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($root.ToLowerInvariant()))}
finally{$sha.Dispose()}
$key=([BitConverter]::ToString($digest)-replace '-','').Substring(0,24)
$entryName='Dvinholm.NativeRecovery.'+$key
$mutexName='Local\Dvinholm.NativeLauncher.'+$key

function Assert-RecoveryAncestors([string]$path){
 $p=[IO.Path]::GetFullPath($path)
 while($p){
  if(Test-Path -LiteralPath $p){
   $item=Get-Item -LiteralPath $p -Force
   if(-not $item.PSIsContainer -or
       ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
    throw 'Native Launcher recovery refuses reparse-point directories.'
   }
  }
  $next=[IO.Path]::GetDirectoryName($p)
  if(-not $next -or $next -ceq $p){break}
  $p=$next
 }
}
function Fingerprint([string]$file){
 $item=Get-Item -LiteralPath $file -Force -ErrorAction Stop
 if($item.PSIsContainer -or
   ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
  throw 'Native Launcher recovery refuses linked or non-file helpers.'
 }
 return (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Get-CommandValue {
 if($root.Contains('"') -or $PSCommandPath.Contains('"')){
  throw 'Unsupported native recovery command quoting.'
 }
 $ps=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
 return ('"'+$ps+'" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+
  $PSCommandPath+'" -Mode Resume -InstallationDirectory "'+$root+'"')
}
function Assert-RegistryFixture {
 if(-not $OfflineFixture){return}
 if(-not $FixtureRegistryDirectory){throw 'Offline startup registry fixture path required.'}
 $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
 $target=[IO.Path]::GetFullPath($FixtureRegistryDirectory).TrimEnd('\')
 if(-not $target.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -or
    $target -notmatch '(?i)[\\/]dvinholm-selfswap-[0-9a-f]{32}[\\/]registry$'){
  throw 'Offline recovery registry must be inside a dedicated test fixture.'
 }
 Assert-RecoveryAncestors $target
}
function Read-Startup {
 if($OfflineFixture){
  $path=Join-Path $FixtureRegistryDirectory ($entryName+'.txt')
  if(Test-Path -LiteralPath $path -PathType Leaf){
   return [IO.File]::ReadAllText($path,[Text.Encoding]::UTF8)
  }
  return ''
 }
 $value=(Get-ItemProperty -LiteralPath $registryPath -Name $entryName -ErrorAction SilentlyContinue)
 if($null -eq $value){return ''}
 return [string]$value.$entryName
}
function Write-Startup([string]$value){
 if($OfflineFixture){
  [IO.Directory]::CreateDirectory($FixtureRegistryDirectory)|Out-Null
  [IO.File]::WriteAllText((Join-Path $FixtureRegistryDirectory ($entryName+'.txt')),
   $value,[Text.UTF8Encoding]::new($false))
 }else{
  if(-not(Test-Path -LiteralPath $registryPath)){New-Item -Path $registryPath -Force|Out-Null}
  New-ItemProperty -LiteralPath $registryPath -Name $entryName -PropertyType String -Value $value -Force|Out-Null
 }
}
function Remove-Startup {
 if($OfflineFixture){
  $path=Join-Path $FixtureRegistryDirectory ($entryName+'.txt')
  if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}
 }else{
  Remove-ItemProperty -LiteralPath $registryPath -Name $entryName -ErrorAction Stop
 }
}
function Read-Checkpoint {
 $item=Get-Item -LiteralPath $checkpoint -Force -ErrorAction Stop
 if($item.Length -le 0 -or $item.Length -gt 4096 -or
    ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Invalid native recovery checkpoint.'}
 $data=Get-Content -LiteralPath $checkpoint -Raw | ConvertFrom-Json -ErrorAction Stop
 if([int]$data.Version -ne 1 -or [string]$data.InstallationDirectory -cne $root -or
    [string]$data.RunEntry -cne $entryName -or
    [string]$data.RunCommand -cne (Get-CommandValue) -or
    [string]$data.HelperHash -cnotmatch '^[0-9a-f]{64}$' -or
    [string]$data.ExpectedTag -cnotmatch '^(?:ci-[1-9][0-9]{0,8}|v[0-9]+\.[0-9]+\.[0-9]+)$'){
  throw 'Native recovery checkpoint has invalid scope or identity.'
 }
 if((Fingerprint $helper) -cne [string]$data.HelperHash){
  throw 'Trusted native recovery helper has changed since registration.'
 }
 return $data
}
function Verify-Commit($data) {
 $journal=Join-Path $root '.launcher-previous\journal.json'
 if(-not(Test-Path -LiteralPath $journal -PathType Leaf)){
  throw 'Recovery found no current transaction or committed backup.'
 }
 $file=Get-Item -LiteralPath $journal -Force
 if($file.Length -gt 10KB -or
    ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
  throw 'Unsafe native recovery committed journal.'
 }
 $record=Get-Content -LiteralPath $journal -Raw|ConvertFrom-Json -ErrorAction Stop
 if([int]$record.Version -ne 1 -or @($record.Entries).Count -ne 17){
  throw 'Invalid committed native recovery journal.'
 }
 $names=@('Launcher.exe','Binaries/Updater-Banner.ps1',
  'Binaries/Launcher-AuroraTheme.ps1','Binaries/Launcher-ServiceCockpit.ps1',
  'Binaries/Launcher-ReleaseDiscovery.ps1','Binaries/Launcher-VerifiedDownload.ps1',
  'Binaries/Launcher-ApplyPrepared.ps1','Binaries/Launcher-RebootRecovery.ps1',
  'Binaries/J2534-DriverManager.ps1',
  'Binaries/J2534-DriverUI.ps1','Binaries/Manage-Lite-Dependencies.ps1',
  'Binaries/Dvinholm-icon.png','Binaries/dvinholm-banner.webp',
  'Binaries/launcher-service-cockpit-banner.jpg',
  'Binaries/Assert-LauncherSelfUpdatePackage.ps1',
  'Binaries/Invoke-LauncherReplacement.ps1','Binaries/launcher-build.txt')
 for($i=0;$i -lt $names.Count;$i++){
  $item=$record.Entries[$i]
  if([string]$item.Name -cne $names[$i] -or
    [string]$item.NewHash -cnotmatch '^[0-9a-f]{64}$'){
   throw 'Committed native Launcher file list differs from trusted allowlist.'
  }
  $target=Join-Path $root $names[$i].Replace('/','\')
  if((Fingerprint $target) -cne [string]$item.NewHash){
   throw 'Committed native Launcher content mismatch.'
  }
 }
 $receipt=Join-Path $root 'Binaries\launcher-build.txt'
 if([IO.File]::ReadAllText($receipt,[Text.Encoding]::ASCII).Trim() -cne [string]$data.ExpectedTag){
  throw 'Committed native Launcher tag is not expected release.'
 }
}
Assert-RecoveryAncestors $root
Assert-RecoveryAncestors $PSScriptRoot
Assert-RegistryFixture
if($Mode -eq 'Arm'){
 if($ExpectedTag -cnotmatch '^(?:ci-[1-9][0-9]{0,8}|v[0-9]+\.[0-9]+\.[0-9]+)$'){
  throw 'Cannot register native recovery without a verified expected release tag.'
 }
 if(Test-Path -LiteralPath $checkpoint){throw 'Native Launcher recovery is already armed.'}
 if(Read-Startup){throw 'Native Launcher recovery startup entry already exists.'}
 if(-not(Test-Path -LiteralPath $helper -PathType Leaf)){
  throw 'Native Launcher trusted external recovery helper is unavailable.'
 }
 $record=@{Version=1;InstallationDirectory=$root;RunEntry=$entryName;
  RunCommand=(Get-CommandValue);HelperHash=(Fingerprint $helper);
  ExpectedTag=$ExpectedTag;CreatedUtc=[DateTime]::UtcNow.ToString('o')}
 $temporary=$checkpoint+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
 [IO.File]::WriteAllText($temporary,($record|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
 [IO.File]::Move($temporary,$checkpoint)
 try{Write-Startup ([string]$record.RunCommand)}catch{
  Remove-Item -LiteralPath $checkpoint -Force -ErrorAction SilentlyContinue
  throw
 }
 Write-Host 'Native Launcher recovery on next sign-in armed.'
 exit 0
}
$data=Read-Checkpoint
if((Read-Startup) -cne [string]$data.RunCommand){
 throw 'Native Launcher recovery registry entry does not match the checkpoint.'
}
if($Mode -eq 'Disarm'){
 # Caller may only disarm if no uncommitted transaction remains.
 if(Test-Path -LiteralPath (Join-Path $root '.launcher-transaction')){
  throw 'Cannot disarm recovery while native transaction remains.'
 }
 Remove-Startup
 Remove-Item -LiteralPath $checkpoint -Force -ErrorAction Stop
 Write-Host 'Native Launcher sign-in recovery disarmed.'
 exit 0
}
$mutex=[Threading.Mutex]::new($false,$mutexName)
$owned=$false
try{
 try{$owned=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$owned=$true}
 if(-not $owned){
  # An active updater still owns the same installation lock; leave the
  # persistent startup marker intact for a subsequent Windows sign-in.
  throw 'Another native Launcher updater is still running.'
 }
 if(Get-Process -Name 'Dvinholm.App' -ErrorAction SilentlyContinue){
  throw 'Close Dvinholm before native Launcher sign-in recovery.'
 }
 $txn=Join-Path $root '.launcher-transaction'
 $hasTransaction=Test-Path -LiteralPath $txn
 $hasCommitted=Test-Path -LiteralPath (Join-Path $root '.launcher-previous')
 if($hasTransaction){
  & $helper -Mode Recover -InstallationDirectory $root|Out-Null
  if(Test-Path -LiteralPath $txn){throw 'Native Launcher recovery left an unresolved journal.'}
 }elseif($hasCommitted){
  # Commit survived a crash before process restart/disarm. Validate all
  # installed files before treating this as a fully committed update.
  Verify-Commit $data
 }
 # Nothing to recover before a journal is committed: no live file changed.
 Remove-Startup
 Remove-Item -LiteralPath $checkpoint -Force
 if($hasTransaction -or $hasCommitted){
  if(-not $OfflineFixture){
   Start-Process -FilePath (Join-Path $root 'Launcher.exe') -WorkingDirectory $root -ErrorAction Stop|Out-Null
  }
 }
 Write-Host ('Native Launcher sign-in recovery resolved: '+
  $(if($hasTransaction){'rolled back'}elseif($hasCommitted){'verified committed'}else{'no transaction'}))
}finally{
 if($owned){$mutex.ReleaseMutex()}
 $mutex.Dispose()
}
