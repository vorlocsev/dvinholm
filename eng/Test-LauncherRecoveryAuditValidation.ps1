#requires -Version 5.1
<#
Eight deterministic, read-only audit receipt validation gates.
All receipts are synthetic under isolated Windows TEMP folders.
No registry change, native updater, GUI, network, driver or ECU access.
#>
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$inspector=Join-Path $PSScriptRoot 'Inspect-LauncherRecovery.ps1'
$parents=New-Object Collections.Generic.List[string]
$checks=0
function New-Fixture {
 $parent=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-selfswap-'+[guid]::NewGuid().ToString('N'))
 $parents.Add($parent)
 $install=Join-Path $parent 'install'
 $bin=Join-Path $install 'Binaries'
 $handoff=Join-Path $parent 'handoff'
 $worker=Join-Path $handoff ('worker-'+[guid]::NewGuid().ToString('N'))
 [IO.Directory]::CreateDirectory($bin)|Out-Null
 [IO.Directory]::CreateDirectory($worker)|Out-Null
 [IO.File]::WriteAllText((Join-Path $bin 'launcher-build.txt'),'ci-4265')
 $sha=[Security.Cryptography.SHA256]::Create()
 try{$hash=$sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($install.ToLowerInvariant()))}
 finally{$sha.Dispose()}
 $token=([BitConverter]::ToString($hash)-replace '-','').Substring(0,24)
 return [pscustomobject]@{
  Parent=$parent;Install=$install;Handoff=$handoff;Worker=$worker;
  Registry=(Join-Path $parent 'registry');Token=$token
 }
}
function Write-Receipt {
 param($f,[string]$when,[string]$outcome,[string]$code,
  [string]$keyOverride='',[string]$workerOverride='')
 $worker=if($workerOverride){$workerOverride}else{$f.Worker}
 $key=if($keyOverride){$keyOverride}else{$f.Token}
 [IO.Directory]::CreateDirectory($worker)|Out-Null
 $receipt=@{Version=1;InstallationKey=$key;AttemptUtc=$when;
  Outcome=$outcome;Code=$code}|ConvertTo-Json -Compress
 [IO.File]::WriteAllText((Join-Path $worker 'recovery-last.json'),
  $receipt,[Text.UTF8Encoding]::new($false))
}
function Check {
 param($f,[string]$expectedOutcome,[string]$expectedCode)
 $files=@(Get-ChildItem -LiteralPath $f.Handoff -Filter 'recovery-last.json' -File -Recurse)
 $before=@($files|ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash })
 $inspectArgs=@{InstallationDirectory=$f.Install;HandoffDirectory=$f.Handoff;
  OfflineFixture=$true;FixtureRegistryDirectory=$f.Registry}
 $data=(& $inspector @inspectArgs | ConvertFrom-Json)
 $after=@($files|ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash })
 if([string]$data.Status -cne 'Clean' -or
    [string]$data.LastAttemptOutcome -cne $expectedOutcome -or
    [string]$data.LastAttemptCode -cne $expectedCode){
  throw ('Unexpected audit validation outcome: '+[string]$data.LastAttemptOutcome+
   '/'+[string]$data.LastAttemptCode+', expected '+$expectedOutcome+'/'+$expectedCode)
 }
 if(@(Compare-Object $before $after).Count -ne 0){
  throw 'Read-only audit validation modified fixture data.'
 }
 $script:checks++
}
function Stamp([DateTime]$utc){
 return $utc.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
}
try{
 $now=[DateTime]::UtcNow
 $valid=Stamp $now
 # 1. A fresh matching success must remain visible to the user.
 $success=New-Fixture
 Write-Receipt $success $valid 'Succeeded' 'NoTransaction'
 Check $success 'Succeeded' 'NoTransaction'
 # 2. A blocked attempt may be reported, without permitting repair.
 $blocked=New-Fixture
 Write-Receipt $blocked $valid 'Blocked' 'BackupCorrupt'
 Check $blocked 'Blocked' 'BackupCorrupt'
 # 3. Regex-shaped but impossible calendar dates are rejected.
 $impossible=New-Fixture
 Write-Receipt $impossible '2026-02-30T12:00:00.000Z' 'Succeeded' 'NoTransaction'
 Check $impossible 'NotRecorded' 'NotRecorded'
 # 4. Future timestamps beyond five minutes cannot overshadow valid history.
 $future=New-Fixture
 Write-Receipt $future (Stamp $now.AddMinutes(10)) 'Succeeded' 'NoTransaction'
 Check $future 'NotRecorded' 'NotRecorded'
 # 5. Recovery outcomes older than 30 days are not presented as recent.
 $stale=New-Fixture
 Write-Receipt $stale (Stamp $now.AddDays(-31)) 'Succeeded' 'NoTransaction'
 Check $stale 'NotRecorded' 'NotRecorded'
 # 6. Success cannot be paired with a failure-only reason code.
 $mismatch=New-Fixture
 Write-Receipt $mismatch $valid 'Succeeded' 'BackupCorrupt'
 Check $mismatch 'NotRecorded' 'NotRecorded'
 # 7. Another installation's receipt cannot be shown, even if recent.
 $other=New-Fixture
 Write-Receipt $other $valid 'Succeeded' 'NoTransaction' ('0'*24)
 Check $other 'NotRecorded' 'NotRecorded'
 # 8. Multiple workers: latest valid entry wins; invalid newer entry skipped.
 $mixed=New-Fixture
 Write-Receipt $mixed (Stamp $now.AddHours(-1)) 'Succeeded' 'NoTransaction'
 $newer=Join-Path $mixed.Handoff ('worker-'+[guid]::NewGuid().ToString('N'))
 Write-Receipt $mixed $valid 'Started' 'AttemptStarted' '' $newer
 $bad=Join-Path $mixed.Handoff ('worker-'+[guid]::NewGuid().ToString('N'))
 Write-Receipt $mixed (Stamp $now.AddMinutes(1)) 'Blocked' 'NoTransaction' '' $bad
 Check $mixed 'Started' 'AttemptStarted'
 if($checks -ne 8){throw ('Expected 8 audit validation gates; observed '+$checks)}
 Write-Host 'Launcher recovery audit strict validation: 8/8 offline gates passed.'
}finally{
 foreach($parent in $parents){
  if(Test-Path -LiteralPath $parent){
   Remove-Item -LiteralPath $parent -Recurse -Force -ErrorAction SilentlyContinue
  }
 }
}
