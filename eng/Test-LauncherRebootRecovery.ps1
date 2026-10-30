#requires -Version 5.1
<#
Eight deterministic offline sign-in recovery tests. Real 17-file native ZIP
and in-memory-equivalent registry files in a dedicated temporary fixture;
never registers HKCU Run, launches Launcher.exe, or modifies installed app.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ZipPath,
 [Parameter(Mandatory)][string]$ExpectedTag)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=(Resolve-Path -LiteralPath $ZipPath).Path
$archiveHash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$root=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-selfswap-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
$checks=0
function Fixture([string]$name){
 # All fixture installations MUST live under the strictly scoped selfswap
 # directory so the production replacement helper honors -OfflineFixture.
 $parent=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-selfswap-'+[guid]::NewGuid().ToString('N'))
 [IO.Directory]::CreateDirectory($parent)|Out-Null
 $script:fixtureParents.Add($parent)
 $install=Join-Path $parent 'install'
 [IO.Compression.ZipFile]::ExtractToDirectory($zip,$install)
 $registry=Join-Path $parent 'registry'
 $worker=Join-Path $parent 'worker'
 [IO.Directory]::CreateDirectory($worker)|Out-Null
 foreach($file in @('Launcher-RebootRecovery.ps1','Invoke-LauncherReplacement.ps1',
  'Assert-LauncherSelfUpdatePackage.ps1')){
  [IO.File]::Copy((Join-Path $PSScriptRoot $file),(Join-Path $worker $file))
 }
 $launcher=Join-Path $worker 'Launcher-RebootRecovery.ps1'
 $replacement=Join-Path $worker 'Invoke-LauncherReplacement.ps1'
 $scriptPath=Join-Path $install 'Binaries\Updater-Banner.ps1'
 [IO.File]::AppendAllText($scriptPath,([Environment]::NewLine+'# fixture '+$name))
 return [pscustomobject]@{Root=$install;Registry=$registry;Worker=$worker;
  Recovery=$launcher;Replacement=$replacement;ScriptPath=$scriptPath;
  OriginalHash=(Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash}
}
$fixtureParents=New-Object Collections.Generic.List[string]
function Boot($fixture,[string]$mode){
 & $fixture.Recovery -Mode $mode -InstallationDirectory $fixture.Root -ExpectedTag $ExpectedTag -OfflineFixture -FixtureRegistryDirectory $fixture.Registry|Out-Null
}
function Apply($fixture,[int]$crash=0){
 & $fixture.Replacement -Mode Apply -InstallationDirectory $fixture.Root -ZipPath $zip -ExpectedSha256 $archiveHash -OfflineFixture -SimulateInterruptionAfter $crash|Out-Null
}
function Refuse([scriptblock]$operation,[bool]$count=$true){
 $failed=$false
 try{& $operation|Out-Null}catch{$failed=$true}
 if(-not $failed){throw 'Invalid native reboot recovery state was accepted.'}
 if($count){$script:checks++}
}
function RegistryEntry($fixture){
 return @(Get-ChildItem -LiteralPath $fixture.Registry -File -ErrorAction SilentlyContinue)
}
try{
 # 1. Arm installs an external recovery checkpoint plus fake HKCU Run
 # without modifying the installed Launcher.
 $armed=Fixture 'armed'
 Boot $armed 'Arm'
 if(-not(Test-Path (Join-Path $armed.Worker 'launcher-recovery.json')) -or
  (RegistryEntry $armed).Count -ne 1 -or
  (Get-FileHash -LiteralPath $armed.ScriptPath -Algorithm SHA256).Hash -cne $armed.OriginalHash){
  throw 'Arming native recovery modified the installation or omitted a marker.'
 }
 $checks++
 # 2. Clean disarm removes both marker and startup entry.
 Boot $armed 'Disarm'
 if((Test-Path (Join-Path $armed.Worker 'launcher-recovery.json')) -or
   (RegistryEntry $armed).Count -ne 0){throw 'Recovery disarm retained registration.'}
 $checks++
 # 3. An interrupted update is restored by an independent sign-in run.
 $crash=Fixture 'interrupted'
 Boot $crash 'Arm'
 Refuse {Apply $crash 2} $false
 Boot $crash 'Resume'
 if((Test-Path (Join-Path $crash.Root '.launcher-transaction')) -or
  (Get-FileHash -LiteralPath $crash.ScriptPath -Algorithm SHA256).Hash -cne $crash.OriginalHash -or
  (RegistryEntry $crash).Count -ne 0){throw 'Interrupted Launcher was not restored at sign-in.'}
 $checks++
 # 4. A fully committed update survives an unexpected worker exit;
 # the recovery procedure validates all new hashes and preserves backup.
 $commit=Fixture 'committed'
 Boot $commit 'Arm'
 Apply $commit
 Boot $commit 'Resume'
 if((Get-FileHash -LiteralPath $commit.ScriptPath -Algorithm SHA256).Hash -ceq $commit.OriginalHash -or
   -not(Test-Path (Join-Path $commit.Root '.launcher-previous\journal.json')) -or
   (RegistryEntry $commit).Count -ne 0){
  throw 'Recovery did not verify and retain committed Launcher version.'
 }
 $checks++
 # 5. Corrupt original backups block recovery and preserve startup entry.
 $damaged=Fixture 'corrupt-backup'
 Boot $damaged 'Arm'
 Refuse {Apply $damaged 2} $false
 [IO.File]::AppendAllText((Join-Path $damaged.Root '.launcher-transaction\backup\Launcher.exe'),'tamper')
 Refuse {Boot $damaged 'Resume'}
 if(-not(Test-Path (Join-Path $damaged.Root '.launcher-transaction\journal.json')) -or
  (RegistryEntry $damaged).Count -ne 1){throw 'Corrupt recovery erased the journal or retry registration.'}
 # 6. Changed trusted external helper must reject startup recovery.
 $tampered=Fixture 'tampered-helper'
 Boot $tampered 'Arm'
 [IO.File]::AppendAllText($tampered.Replacement,'# changed')
 Refuse {Boot $tampered 'Resume'}
 if((RegistryEntry $tampered).Count -ne 1){throw 'Tampered recovery lost its retry entry.'}
 # 7. Changed startup command refuses recovery (never runs new arbitrary command).
 $registry=Fixture 'tampered-registry'
 Boot $registry 'Arm'
 $startup=(RegistryEntry $registry)[0].FullName
 [IO.File]::WriteAllText($startup,'powershell.exe -File untrusted.ps1')
 Refuse {Boot $registry 'Resume'}
 if(-not(Test-Path (Join-Path $registry.Worker 'launcher-recovery.json'))){
  throw 'Untrusted registry entry erased the trusted checkpoint.'
 }
 # 8. Existing checkpoint cannot silently be replaced by a second update.
 $duplicate=Fixture 'double-register'
 Boot $duplicate 'Arm'
 Refuse {Boot $duplicate 'Arm'}
 if($checks -ne 8){throw "Expected 8 native sign-in recovery checks; got $checks."}
 Write-Host 'Native Launcher sign-in recovery: 8/8 offline checks passed.'
}finally{
 foreach($parent in $fixtureParents){
  if(Test-Path -LiteralPath $parent){
   Remove-Item -LiteralPath $parent -Recurse -Force -ErrorAction SilentlyContinue
  }
 }
 if(Test-Path -LiteralPath $root){
  Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
 }
}
