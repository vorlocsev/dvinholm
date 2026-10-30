#requires -Version 5.1
<#
Eight isolated offline acceptance-state checks on Windows PowerShell 5.1.
The inspector never writes to the registry, installation, or checkpoints.
Arm uses the existing trusted recovery helper with an isolated fake
HKCU Run entry only inside a dedicated Windows temp fixture.
#>
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$inspector=Join-Path $PSScriptRoot 'Inspect-LauncherRecovery.ps1'
$roots=New-Object Collections.Generic.List[string]
$count=0
function New-Fixture([string]$name){
 $parent=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-selfswap-'+[guid]::NewGuid().ToString('N'))
 $roots.Add($parent)
 $install=Join-Path $parent 'install'
 $bin=Join-Path $install 'Binaries'
 $handoff=Join-Path $parent 'handoff'
 $worker=Join-Path $handoff ('worker-'+[guid]::NewGuid().ToString('N'))
 $registry=Join-Path $parent 'registry'
 [IO.Directory]::CreateDirectory($bin)|Out-Null
 [IO.Directory]::CreateDirectory($worker)|Out-Null
 [IO.File]::WriteAllText((Join-Path $bin 'launcher-build.txt'),'ci-4259')
 foreach($file in @('Launcher-RebootRecovery.ps1','Invoke-LauncherReplacement.ps1')){
  [IO.File]::Copy((Join-Path $PSScriptRoot $file),(Join-Path $worker $file))
 }
 return [pscustomobject]@{Name=$name;Install=$install;Handoff=$handoff;
  Worker=$worker;Registry=$registry}
}
function Arm($f){
 & (Join-Path $f.Worker 'Launcher-RebootRecovery.ps1') -Mode Arm -InstallationDirectory $f.Install -ExpectedTag 'ci-4260' -OfflineFixture -FixtureRegistryDirectory $f.Registry|Out-Null
}
function Inspect($f){
 $raw=& $inspector -InstallationDirectory $f.Install -HandoffDirectory $f.Handoff -OfflineFixture -FixtureRegistryDirectory $f.Registry
 return ($raw | ConvertFrom-Json)
}
function Expect($f,[string]$status,[bool]$trusted=$false){
 $before=@(Get-ChildItem -Path $f.Worker -Force).Count
 $s=Inspect $f
 if([string]$s.Status -cne $status -or [bool]$s.CheckpointTrusted -ne $trusted){
  throw ('Read-only recovery inspection '+$f.Name+' expected '+$status+
   ', got '+[string]$s.Status+'.')
 }
 $after=@(Get-ChildItem -Path $f.Worker -Force).Count
 if($before -ne $after){throw 'Read-only inspector modified recovery worker.'}
 $script:count++
}
try{
 # 1. No transaction/checkpoint/Run entry: ordinary clean launcher.
 $clean=New-Fixture 'clean'
 Expect $clean 'Clean'
 # 2. Previously committed native update has a backup, but no startup.
 $backup=New-Fixture 'backup'
 [IO.Directory]::CreateDirectory((Join-Path $backup.Install '.launcher-previous'))|Out-Null
 Expect $backup 'BackupAvailable'
 # 3. Partly retired backup remains, but no startup recovery is armed.
 $retired=New-Fixture 'retired'
 [IO.Directory]::CreateDirectory((Join-Path $retired.Install '.launcher-retired'))|Out-Null
 Expect $retired 'RetirementPending'
 # 4. Update writes interrupted, trusted outside-install checkpoint present.
 $pending=New-Fixture 'pending'
 Arm $pending
 [IO.Directory]::CreateDirectory((Join-Path $pending.Install '.launcher-transaction'))|Out-Null
 Expect $pending 'RecoveryPending' $true
 # 5. Committed backup + still-armed registration: restart pending.
 $restart=New-Fixture 'restart'
 Arm $restart
 [IO.Directory]::CreateDirectory((Join-Path $restart.Install '.launcher-previous'))|Out-Null
 Expect $restart 'RestartPending' $true
 # 6. Startup Run value modified after registration must block acceptance.
 $wrongRun=New-Fixture 'wrong-run'
 Arm $wrongRun
 $startup=@(Get-ChildItem -LiteralPath $wrongRun.Registry -File)[0].FullName
 [IO.File]::WriteAllText($startup,'untrusted.exe')
 Expect $wrongRun 'NeedsInspection'
 # 7. Replacement helper modified after arming cannot be trusted.
 $modified=New-Fixture 'modified-helper'
 Arm $modified
 [IO.File]::AppendAllText((Join-Path $modified.Worker 'Invoke-LauncherReplacement.ps1'),'# changed')
 Expect $modified 'NeedsInspection'
 # 8. An interrupted write without a matching Run checkpoint must be flagged.
 $orphan=New-Fixture 'orphan'
 [IO.Directory]::CreateDirectory((Join-Path $orphan.Install '.launcher-transaction'))|Out-Null
 Expect $orphan 'NeedsInspection'
 if($count -ne 8){throw ('Expected eight recovery acceptance states, got '+$count)}
 Write-Host 'Native Launcher read-only Windows acceptance: 8/8 checks passed.'
}finally{
 foreach($folder in $roots){
  if(Test-Path -LiteralPath $folder){
   Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
  }
 }
}
