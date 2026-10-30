#requires -Version 5.1
<#
Windows PowerShell 5.1 process-level acceptance. Eight independent gates.
Each operation is a new powershell.exe process, never a dot-sourced call.
Only the first gate writes a uniquely scoped HKCU Run value, removed in
finally. All replacements are isolated temp fixture installs; no GUI,
network, live installation, .NET dependency installer or J2534 hardware.
#>
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$ZipPath,
 [Parameter(Mandatory)][string]$ExpectedTag
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=(Resolve-Path -LiteralPath $ZipPath).Path
$zipHash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$ps=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
if(-not(Test-Path -LiteralPath $ps -PathType Leaf)){throw 'Windows PowerShell 5.1 is missing.'}
$runKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$fixtures=New-Object Collections.Generic.List[object]
$checks=0
$realRegistryName=''
function New-Fixture([string]$label){
 $parent=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-selfswap-'+[guid]::NewGuid().ToString('N'))
 $install=Join-Path $parent 'install'
 [IO.Compression.ZipFile]::ExtractToDirectory($archive,$install)
 $worker=Join-Path $parent 'worker'
 $registry=Join-Path $parent 'registry'
 [IO.Directory]::CreateDirectory($worker)|Out-Null
 foreach($filename in @('Launcher-RebootRecovery.ps1',
  'Invoke-LauncherReplacement.ps1','Assert-LauncherSelfUpdatePackage.ps1')){
  [IO.File]::Copy((Join-Path $PSScriptRoot $filename),(Join-Path $worker $filename))
 }
 $scriptPath=Join-Path $install 'Binaries\Updater-Banner.ps1'
 [IO.File]::AppendAllText($scriptPath,([Environment]::NewLine+'# before '+$label))
 $fixture=[pscustomobject]@{
  Label=$label;Parent=$parent;Install=$install;Worker=$worker;Registry=$registry;
  Recovery=(Join-Path $worker 'Launcher-RebootRecovery.ps1');
  Replacement=(Join-Path $worker 'Invoke-LauncherReplacement.ps1');
  Original=$scriptPath;
  OldHash=(Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash
 }
 $script:fixtures.Add($fixture)
 return $fixture
}
function Invoke-Child($f,[string]$action,[bool]$actualRun=$false,[int]$crashAt=0,[bool]$restartFailure=$false){
 $scriptPath=if($action -ceq 'Apply'){$f.Replacement}else{$f.Recovery}
 $mode=if($action -ceq 'Apply'){'Apply'}else{$action}
 $args='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+
  $scriptPath+'" -Mode '+$mode+' -InstallationDirectory "'+$f.Install+'"'
 if($action -ceq 'Apply'){
  $args+=' -ZipPath "'+$archive+'" -ExpectedSha256 '+$zipHash+' -OfflineFixture'
  if($crashAt -gt 0){$args+=' -SimulateInterruptionAfter '+$crashAt}
 }else{
  $args+=' -ExpectedTag '+$ExpectedTag
  if(-not $actualRun){$args+=' -OfflineFixture -FixtureRegistryDirectory "'+$f.Registry+'"'}
  if($restartFailure){$args+=' -SimulateRestartFailure'}
 }
 $logDir=Join-Path $f.Parent 'process-logs'
 [IO.Directory]::CreateDirectory($logDir)|Out-Null
 $id=[guid]::NewGuid().ToString('N')
 $output=Join-Path $logDir ('out-'+$id+'.txt')
 $errorLog=Join-Path $logDir ('err-'+$id+'.txt')
 $process=Start-Process -FilePath $ps -ArgumentList $args -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $output -RedirectStandardError $errorLog -ErrorAction Stop
 try{
  return [pscustomobject]@{
   Code=[int]$process.ExitCode
   Output=([IO.File]::ReadAllText($output))
   Errors=([IO.File]::ReadAllText($errorLog))
  }
 }finally{$process.Dispose()}
}
function Pass($result,[string]$label){
 if($result.Code -ne 0){throw ($label+': expected success but got '+$result.Code+': '+$result.Errors)}
}
function Reject($result,[string]$label){
 if($result.Code -eq 0){throw ($label+': expected safe rejection, but helper reported success.')}
}
function Digest([string]$filename){
 return (Get-FileHash -LiteralPath $filename -Algorithm SHA256).Hash
}
function FakeValueCount($f){
 return @(Get-ChildItem -LiteralPath $f.Registry -File -ErrorAction SilentlyContinue).Count
}
try{
 # 1. Real Windows registry provider: separate process arms and disarms a
 # unique HKCU Run entry under this throwaway runner user.
 $real=New-Fixture 'real-hkcu'
 $sha=[Security.Cryptography.SHA256]::Create()
 try{$digest=$sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($real.Install.ToLowerInvariant()))}
 finally{$sha.Dispose()}
 $realRegistryName='Dvinholm.NativeRecovery.'+
  ([BitConverter]::ToString($digest)-replace '-','').Substring(0,24)
 if(Get-ItemProperty -LiteralPath $runKey -Name $realRegistryName -ErrorAction SilentlyContinue){
  throw 'Unexpected pre-existing recovery registry fixture entry.'
 }
 Pass (Invoke-Child $real 'Arm' $true) 'HKCU Arm'
 $run=Get-ItemProperty -LiteralPath $runKey -Name $realRegistryName -ErrorAction Stop
 if([string]$run.$realRegistryName -notmatch '-Mode Resume' -or
    -not(Test-Path (Join-Path $real.Worker 'launcher-recovery.json'))){
  throw 'Real HKCU Run registration did not contain trusted recovery command.'
 }
 Pass (Invoke-Child $real 'Disarm' $true) 'HKCU Disarm'
 if(Get-ItemProperty -LiteralPath $runKey -Name $realRegistryName -ErrorAction SilentlyContinue){
  throw 'Completed update left a real HKCU Run entry.'
 }
 $checks++
 # 2. Separate process with no journal: do not change installed files,
 # but the next sign-in retry marker must clear.
 $early=New-Fixture 'no-journal'
 Pass (Invoke-Child $early 'Arm') 'Arm no journal'
 Pass (Invoke-Child $early 'Resume') 'Resume no journal'
 if((FakeValueCount $early) -ne 0 -or
    (Digest $early.Original) -cne $early.OldHash){
  throw 'No-journal recovery modified original or retained registration.'
 }
 $checks++
 # 3. Crash after two writes, then separate process replays original backups.
 $partial=New-Fixture 'partial-transaction'
 Pass (Invoke-Child $partial 'Arm') 'Arm partial'
 Reject (Invoke-Child $partial 'Apply' $false 2) 'Injected partial apply'
 if(-not(Test-Path (Join-Path $partial.Install '.launcher-transaction\journal.json'))){
  throw 'Injected interruption did not retain journal.'
 }
 Pass (Invoke-Child $partial 'Resume') 'Resume partial'
 if((Digest $partial.Original) -cne $partial.OldHash -or
    (Test-Path (Join-Path $partial.Install '.launcher-transaction')) -or
    (FakeValueCount $partial) -ne 0){
  throw 'Sign-in process did not restore original bytes.'
 }
 $checks++
 # 4. Committed transaction: verify all native files and keep rollback.
 $committed=New-Fixture 'committed'
 Pass (Invoke-Child $committed 'Arm') 'Arm committed'
 Pass (Invoke-Child $committed 'Apply') 'Apply committed'
 Pass (Invoke-Child $committed 'Resume') 'Resume committed'
 if((Digest $committed.Original) -ceq $committed.OldHash -or
    -not(Test-Path (Join-Path $committed.Install '.launcher-previous\journal.json')) -or
    (FakeValueCount $committed) -ne 0){
  throw 'Committed Launcher was not independently verified.'
 }
 $checks++
 # 5. Bad original backup fails closed and preserves both durable markers.
 $corrupt=New-Fixture 'bad-backup'
 Pass (Invoke-Child $corrupt 'Arm') 'Arm bad backup'
 Reject (Invoke-Child $corrupt 'Apply' $false 2) 'Inject corrupted backup'
 [IO.File]::AppendAllText((Join-Path $corrupt.Install '.launcher-transaction\backup\Launcher.exe'),'tampered')
 Reject (Invoke-Child $corrupt 'Resume') 'Refuse corrupt backup'
 if((FakeValueCount $corrupt) -ne 1 -or
  -not(Test-Path (Join-Path $corrupt.Install '.launcher-transaction\journal.json'))){
  throw 'Corrupt backup recovery discarded its safety markers.'
 }
 $checks++
 # 6. Committed files modified after update are never accepted.
 $modified=New-Fixture 'modified-installed'
 Pass (Invoke-Child $modified 'Arm') 'Arm modified'
 Pass (Invoke-Child $modified 'Apply') 'Apply modified'
 [IO.File]::AppendAllText($modified.Original,'post-commit corruption')
 Reject (Invoke-Child $modified 'Resume') 'Refuse modified committed files'
 if((FakeValueCount $modified) -ne 1 -or
    -not(Test-Path (Join-Path $modified.Install '.launcher-previous\journal.json'))){
  throw 'Modified committed build was accepted or registration lost.'
 }
 $checks++
 # 7. Restart failure after verified rollback retains sign-in retry marker
 # even though the journal itself has been cleared; a new process retries.
 $retry=New-Fixture 'failed-restart'
 Pass (Invoke-Child $retry 'Arm') 'Arm restart retry'
 Reject (Invoke-Child $retry 'Apply' $false 2) 'Inject restart retry'
 Reject (Invoke-Child $retry 'Resume' $false 0 $true) 'Injected restart failure'
 if((FakeValueCount $retry) -ne 1 -or
    (Test-Path (Join-Path $retry.Install '.launcher-transaction'))){
  throw 'Interrupted restart lost retry state or did not restore originals.'
 }
 Pass (Invoke-Child $retry 'Resume') 'Retry sign-in startup'
 if((FakeValueCount $retry) -ne 0 -or (Digest $retry.Original) -cne $retry.OldHash){
  throw 'Second sign-in recovery did not complete.'
 }
 $checks++
 # 8. External startup entry modification is rejected, not overwritten.
 $changed=New-Fixture 'modified-command'
 Pass (Invoke-Child $changed 'Arm') 'Arm altered command'
 $fake=@(Get-ChildItem -LiteralPath $changed.Registry -File)[0].FullName
 [IO.File]::WriteAllText($fake,'powershell.exe -File untrusted.ps1')
 Reject (Invoke-Child $changed 'Resume') 'Refuse modified startup'
 if((FakeValueCount $changed) -ne 1 -or
    -not(Test-Path (Join-Path $changed.Worker 'launcher-recovery.json'))){
  throw 'Altered startup entry lost verified checkpoint.'
 }
 $checks++
 if($checks -ne 8){throw ('Expected eight process-level checks, got '+$checks)}
 Write-Host 'Native Launcher separate-process recovery: 8/8 Windows checks passed.'
}finally{
 if($realRegistryName){
  # Cleanup is narrowly scoped to the random per-test registration only.
  Remove-ItemProperty -LiteralPath $runKey -Name $realRegistryName -ErrorAction SilentlyContinue
 }
 foreach($f in $fixtures){
  if(Test-Path -LiteralPath $f.Parent){
   Remove-Item -LiteralPath $f.Parent -Recurse -Force -ErrorAction SilentlyContinue
  }
 }
}
