#requires -Version 5.1
<#
Eight offline native Launcher sign-in audit checks. Every installation,
HKCU Run replacement and update ZIP is isolated under Windows TEMP.
No real Run value, real installation, executable, reboot or ECU touched.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ZipPath,
 [Parameter(Mandatory)][string]$ExpectedTag)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=(Resolve-Path -LiteralPath $ZipPath).Path
$zipHash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$inspector=Join-Path $PSScriptRoot 'Inspect-LauncherRecovery.ps1'
$parents=New-Object Collections.Generic.List[string]
$checks=0
function Fixture([string]$label){
 $parent=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-selfswap-'+[guid]::NewGuid().ToString('N'))
 $parents.Add($parent)
 $install=Join-Path $parent 'install'
 [IO.Compression.ZipFile]::ExtractToDirectory($zip,$install)
 $worker=Join-Path (Join-Path $parent 'handoff') ('worker-'+[guid]::NewGuid().ToString('N'))
 $registry=Join-Path $parent 'registry'
 [IO.Directory]::CreateDirectory($worker)|Out-Null
 foreach($name in @('Launcher-RebootRecovery.ps1','Invoke-LauncherReplacement.ps1','Assert-LauncherSelfUpdatePackage.ps1')){
  [IO.File]::Copy((Join-Path $PSScriptRoot $name),(Join-Path $worker $name))
 }
 # Distinct from verified archive, so journal replacement has real work.
 [IO.File]::AppendAllText((Join-Path $install 'Binaries\Updater-Banner.ps1'),'# old fixture')
 return [pscustomobject]@{
  Root=$install;Parent=$parent;Worker=$worker;Registry=$registry;
  Recovery=(Join-Path $worker 'Launcher-RebootRecovery.ps1');
  Replace=(Join-Path $worker 'Invoke-LauncherReplacement.ps1');
  Log=(Join-Path $worker 'recovery-last.json')
 }
}
function Recovery($f,[string]$mode,[bool]$restartFailure=$false){
 $args=@{Mode=$mode;InstallationDirectory=$f.Root;ExpectedTag=$ExpectedTag;
  OfflineFixture=$true;FixtureRegistryDirectory=$f.Registry}
 if($restartFailure){$args.SimulateRestartFailure=$true}
 & $f.Recovery @args|Out-Null
}
function Apply($f,[int]$interrupt=0){
 & $f.Replace -Mode Apply -InstallationDirectory $f.Root -ZipPath $zip -ExpectedSha256 $zipHash -OfflineFixture -SimulateInterruptionAfter $interrupt|Out-Null
}
function Reject([scriptblock]$action){
 $rejected=$false
 try{& $action|Out-Null}catch{$rejected=$true}
 if(-not $rejected){throw 'Unsafe audit fixture unexpectedly succeeded.'}
}
function Inspect($f){
 $s=& $inspector -InstallationDirectory $f.Root -HandoffDirectory (Join-Path $f.Parent 'handoff') -OfflineFixture -FixtureRegistryDirectory $f.Registry
 return ($s|ConvertFrom-Json)
}
function Assert-Log($f,[string]$outcome,[string]$code){
 if(-not(Test-Path -LiteralPath $f.Log -PathType Leaf)){throw 'Sign-in recovery audit missing.'}
 $info=Get-Item -LiteralPath $f.Log -Force
 if($info.Length -gt 2048){throw 'Sign-in recovery audit exceeded its size limit.'}
 $data=Get-Content -LiteralPath $f.Log -Raw|ConvertFrom-Json
 $report=Inspect $f
 if([string]$data.Outcome -cne $outcome -or [string]$data.Code -cne $code -or
    [string]$report.LastAttemptOutcome -cne $outcome -or
    [string]$report.LastAttemptCode -cne $code -or
    [string]$report.LastAttemptUtc -cnotmatch '^\d{4}-\d\d-\d\dT'){
  throw ('Sign-in audit mismatch: expected '+$outcome+' '+$code)
 }
 # The log must not contain any installation location or raw registry command.
 $raw=[IO.File]::ReadAllText($f.Log)
 if($raw.Contains($f.Root) -or $raw.Contains($f.Parent) -or $raw.Contains('powershell.exe')){
  throw 'Sensitive installation path or command leaked into audit.'
 }
}
try{
 # 1. Before any sign-in, the inspector reports no attempt recorded.
 $fresh=Fixture 'fresh'
 if((Inspect $fresh).LastAttemptOutcome -cne 'NotRecorded'){throw 'Unexpected audit for untouched installation.'}
 $checks++
 # 2. Arming alone does not fabricate a successful sign-in.
 Recovery $fresh 'Arm'
 if(Test-Path -LiteralPath $fresh.Log){throw 'Arming created a false sign-in audit.'}
 $checks++
 # 3. Recovery with no journal succeeds and is recorded/read back.
 Recovery $fresh 'Resume'
 Assert-Log $fresh 'Succeeded' 'NoTransaction'
 $checks++
 # 4. Interrupted native update is rolled back with an audit receipt.
 $interrupted=Fixture 'interrupted'
 Recovery $interrupted 'Arm'
 Reject {Apply $interrupted 2}
 Recovery $interrupted 'Resume'
 Assert-Log $interrupted 'Succeeded' 'RollbackRestored'
 if(Test-Path (Join-Path $interrupted.Root '.launcher-transaction')){throw 'Interrupted transaction not cleared.'}
 $checks++
 # 5. A committed release is verified and recorded, with backup kept.
 $commit=Fixture 'committed'
 Recovery $commit 'Arm'
 Apply $commit
 Recovery $commit 'Resume'
 Assert-Log $commit 'Succeeded' 'CommittedVerified'
 if(-not(Test-Path (Join-Path $commit.Root '.launcher-previous\journal.json'))){throw 'Committed rollback was lost.'}
 $checks++
 # 6. Invalid rollback backup blocks action, records code, keeps journal.
 $corrupt=Fixture 'corrupt'
 Recovery $corrupt 'Arm'
 Reject {Apply $corrupt 2}
 [IO.File]::AppendAllText((Join-Path $corrupt.Root '.launcher-transaction\backup\Launcher.exe'),'tamper')
 Reject {Recovery $corrupt 'Resume'}
 Assert-Log $corrupt 'Blocked' 'BackupCorrupt'
 if(-not(Test-Path (Join-Path $corrupt.Root '.launcher-transaction\journal.json'))){throw 'Corrupt backup journal lost.'}
 $checks++
 # 7. Changed HKCU Run command blocks action and produces no raw text.
 $altered=Fixture 'startup'
 Recovery $altered 'Arm'
 $run=@(Get-ChildItem -LiteralPath $altered.Registry -File)[0].FullName
 [IO.File]::WriteAllText($run,'untrusted.exe')
 Reject {Recovery $altered 'Resume'}
 Assert-Log $altered 'Blocked' 'StartupMismatch'
 $checks++
 # 8. Failed restart logged; subsequent safe sign-in supersedes that code.
 $retry=Fixture 'retry'
 Recovery $retry 'Arm'
 Reject {Apply $retry 2}
 Reject {Recovery $retry 'Resume' $true}
 Assert-Log $retry 'Blocked' 'RestartFailed'
 Recovery $retry 'Resume'
 Assert-Log $retry 'Succeeded' 'NoTransaction'
 $checks++
 if($checks -ne 8){throw ('Expected 8 native recovery audit cases; got '+$checks)}
 Write-Host 'Native Launcher sign-in recovery audit 8/8 offline checks passed.'
}finally{
 foreach($p in $parents){
  if(Test-Path -LiteralPath $p){
   Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction SilentlyContinue
  }
 }
}
