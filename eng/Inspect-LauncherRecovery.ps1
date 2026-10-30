#requires -Version 5.1
<#
Read-only acceptance inspector for a native Launcher installation.
It DOES NOT execute a checkpoint, recover a transaction, write HKCU,
launch an EXE, reboot the PC, or modify an installed file.
#>
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$InstallationDirectory,
 [string]$HandoffDirectory,
 [ValidateSet('Json','Text')][string]$Format='Json',
 [switch]$OfflineFixture,
 [string]$FixtureRegistryDirectory
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($InstallationDirectory).TrimEnd('\')
if(-not $HandoffDirectory){
 $HandoffDirectory=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Dvinholm\UpdaterBanner\LauncherHandoff'
}
$handoff=[IO.Path]::GetFullPath($HandoffDirectory).TrimEnd('\')
$regPath='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$sha=[Security.Cryptography.SHA256]::Create()
try{$digest=$sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($root.ToLowerInvariant()))}
finally{$sha.Dispose()}
$token=([BitConverter]::ToString($digest)-replace '-','').Substring(0,24)
$entry='Dvinholm.NativeRecovery.'+$token

function Assert-PlainAncestors([string]$path){
 $p=[IO.Path]::GetFullPath($path)
 while($p){
  if(Test-Path -LiteralPath $p){
   $item=Get-Item -LiteralPath $p -Force -ErrorAction Stop
   if(-not $item.PSIsContainer -or
      ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
    throw 'Recovery inspection refuses linked/non-directory ancestors.'
   }
  }
  $next=[IO.Path]::GetDirectoryName($p)
  if(-not $next -or $next -ceq $p){break}
  $p=$next
 }
}
function Plain-File([string]$path,[long]$maximum){
 if(-not(Test-Path -LiteralPath $path -PathType Leaf)){return $false}
 $item=Get-Item -LiteralPath $path -Force -ErrorAction Stop
 return (-not $item.PSIsContainer -and
  ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0 -and
  $item.Length -gt 0 -and $item.Length -le $maximum)
}
function Read-RunEntry {
 if($OfflineFixture){
  if(-not $FixtureRegistryDirectory){throw 'Offline Run fixture is required.'}
  $full=[IO.Path]::GetFullPath($FixtureRegistryDirectory).TrimEnd('\')
  $temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
  if(-not $full.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or
     $full -notmatch '(?i)[\\/]dvinholm-selfswap-[0-9a-f]{32}[\\/]registry$'){
   throw 'Offline Run fixture is not inside a dedicated test directory.'
  }
  Assert-PlainAncestors $full
  $file=Join-Path $full ($entry+'.txt')
  if(-not(Test-Path -LiteralPath $file)){return ''}
  if(-not(Plain-File $file 4096)){throw 'Invalid offline recovery Run entry.'}
  return [IO.File]::ReadAllText($file,[Text.Encoding]::UTF8)
 }
 $found=Get-ItemProperty -LiteralPath $regPath -Name $entry -ErrorAction SilentlyContinue
 if($null -eq $found){return ''}
 return [string]$found.$entry
}
Assert-PlainAncestors $root
Assert-PlainAncestors $handoff
if(-not(Test-Path -LiteralPath $root -PathType Container)){
 throw 'The selected native Launcher installation is missing.'
}
$transaction=Join-Path $root '.launcher-transaction'
$previous=Join-Path $root '.launcher-previous'
$retired=Join-Path $root '.launcher-retired'
foreach($folder in @($transaction,$previous,$retired)){
 Assert-PlainAncestors $folder
}
$hasTxn=Test-Path -LiteralPath $transaction -PathType Container
$hasPrev=Test-Path -LiteralPath $previous -PathType Container
$hasRetired=Test-Path -LiteralPath $retired -PathType Container
$build=Join-Path $root 'Binaries\launcher-build.txt'
$tag='Unknown'
if(Plain-File $build 48){
 $raw=[IO.File]::ReadAllText($build,[Text.Encoding]::ASCII).Trim()
 if($raw -cmatch '^(?:ci-[1-9][0-9]{0,8}|v[0-9]+\.[0-9]+\.[0-9]+)$'){$tag=$raw}
}
$startup=Read-RunEntry
$hasRun=($startup.Length -gt 0)
$checkpoints=New-Object Collections.Generic.List[object]
$invalidWorkers=0
$auditEntries=New-Object Collections.Generic.List[object]
# An audit is advisory only: reject future, stale, impossible or contradictory
# receipts so a damaged/forged record cannot masquerade as a completed recovery.
$auditNotBefore=[DateTime]::UtcNow.AddDays(-30)
$auditNotAfter=[DateTime]::UtcNow.AddMinutes(5)
if(Test-Path -LiteralPath $handoff -PathType Container){
 $folders=@(Get-ChildItem -LiteralPath $handoff -Directory -Force -ErrorAction Stop)
 if($folders.Count -gt 128){throw 'Too many native Launcher handoff folders for bounded inspection.'}
 foreach($worker in $folders){
  if($worker.Name -cnotmatch '^worker-[0-9a-f]{32}$'){continue}
  if(($worker.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
   $invalidWorkers++
   continue
  }
  # Optional outcome receipts never influence trust or repair decisions.
  # A partial/invalid file is ignored; no raw exception details are shown.
  $auditFile=Join-Path $worker.FullName 'recovery-last.json'
  if(Plain-File $auditFile 2048){
   try{
    $audit=Get-Content -LiteralPath $auditFile -Raw -ErrorAction Stop|
     ConvertFrom-Json -ErrorAction Stop
    $outcome=[string]$audit.Outcome
    $code=[string]$audit.Code
    $when=[string]$audit.AttemptUtc
    $parsedTime=[DateTime]::MinValue
    $dateValid=($when -cmatch '^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z
   }catch{}
  }
  $recordPath=Join-Path $worker.FullName 'launcher-recovery.json'
  if(-not(Test-Path -LiteralPath $recordPath)){continue}
  if(-not(Plain-File $recordPath 4096)){
   $invalidWorkers++;continue
  }
  try{
   $data=Get-Content -LiteralPath $recordPath -Raw -ErrorAction Stop |
    ConvertFrom-Json -ErrorAction Stop
   if([string]$data.InstallationDirectory -cne $root){continue}
   $scriptPath=Join-Path $worker.FullName 'Launcher-RebootRecovery.ps1'
   $helper=Join-Path $worker.FullName 'Invoke-LauncherReplacement.ps1'
   $ps=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
   $command='"'+$ps+'" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+
     $scriptPath+'" -Mode Resume -InstallationDirectory "'+$root+'"'
   $valid=(
    [int]$data.Version -eq 1 -and
    [string]$data.RunEntry -ceq $entry -and
    [string]$data.RunCommand -ceq $command -and
    [string]$data.ExpectedTag -cmatch '^(?:ci-[1-9][0-9]{0,8}|v[0-9]+\.[0-9]+\.[0-9]+)$' -and
    [string]$data.HelperHash -cmatch '^[0-9a-f]{64}$' -and
    (Plain-File $helper 128KB) -and
    (Plain-File $scriptPath 128KB))
   if($valid){
    $actual=(Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash.ToLowerInvariant()
    $valid=($actual -ceq [string]$data.HelperHash)
   }
   $checkpoints.Add([pscustomobject]@{
    Trusted=[bool]$valid;Command=[string]$data.RunCommand;
    Tag=[string]$data.ExpectedTag
   })
  }catch{$invalidWorkers++}
 }
}
$trusted=($checkpoints.Count -eq 1 -and [bool]$checkpoints[0].Trusted -and
  $hasRun -and $startup -ceq [string]$checkpoints[0].Command -and
  $invalidWorkers -eq 0)
$status='NeedsInspection'
$detail='Unexpected or conflicting native recovery state; do not delete journals or startup entries.'
if($hasTxn){
 if($trusted -and -not $hasPrev -and -not $hasRetired){
  $status='RecoveryPending'
  $detail='Interrupted native update with a matching trusted sign-in recovery checkpoint.'
 }
}elseif($hasRun -or $checkpoints.Count -gt 0 -or $invalidWorkers -gt 0){
 if($trusted -and -not $hasRetired){
  $status='RestartPending'
  $detail='Trusted recovery is armed; verify it completes at next Windows sign-in.'
 }
}elseif($hasRetired){
 $status='RetirementPending'
 $detail='Interrupted backup retirement; use the explicit verified finalization action.'
}elseif($hasPrev){
 $status='BackupAvailable'
 $detail='Committed backup retained; there is no armed native recovery.'
}else{
 $status='Clean'
 $detail='No native update transaction, committed backup or startup recovery registration.'
}
$recentAudit=$null
if($auditEntries.Count -gt 0){
 $recentAudit=@($auditEntries|Sort-Object -Property AttemptUtc -Descending)[0]
}
$report=[ordered]@{
 Status=$status
 Detail=$detail
 InstalledTag=$tag
 TransactionPresent=[bool]$hasTxn
 BackupPresent=[bool]$hasPrev
 RetiredPresent=[bool]$hasRetired
 StartupEntryPresent=[bool]$hasRun
 MatchingCheckpointCount=[int]$checkpoints.Count
 CheckpointTrusted=[bool]$trusted
 LastAttemptUtc=$(if($null -ne $recentAudit){[string]$recentAudit.AttemptUtc}else{''})
 LastAttemptOutcome=$(if($null -ne $recentAudit){[string]$recentAudit.Outcome}else{'NotRecorded'})
 LastAttemptCode=$(if($null -ne $recentAudit){[string]$recentAudit.Code}else{'NotRecorded'})
}
if($Format -ceq 'Text'){
 Write-Output ("Native Launcher recovery: "+$status)
 Write-Output ("Installed: "+$tag)
 Write-Output $detail
 Write-Output ("Transaction: "+$hasTxn+"; backup: "+$hasPrev+
  "; retirement: "+$hasRetired+"; Run entry: "+$hasRun)
 Write-Output ("Last sign-in recovery: "+[string]$report.LastAttemptOutcome+
  " / "+[string]$report.LastAttemptCode+" / "+[string]$report.LastAttemptUtc)
}else{
 $report|ConvertTo-Json -Compress
}
 -and
     [DateTime]::TryParseExact($when,"yyyy-MM-ddTHH:mm:ss.fff'Z'",
      [Globalization.CultureInfo]::InvariantCulture,
      ([Globalization.DateTimeStyles]::AssumeUniversal -bor
       [Globalization.DateTimeStyles]::AdjustToUniversal),[ref]$parsedTime))
    $pairValid=(
     ($outcome -ceq 'Started' -and $code -ceq 'AttemptStarted') -or
     ($outcome -ceq 'Succeeded' -and
      $code -cin @('RollbackRestored','CommittedVerified','NoTransaction')) -or
     ($outcome -ceq 'Blocked' -and
      $code -cin @('StartupMismatch','CheckpointInvalid','BackupCorrupt',
       'CommittedMismatch','UpdaterBusy','ApplicationRunning',
       'RestartFailed','UnexpectedError')))
    if([int]$audit.Version -eq 1 -and
       [string]$audit.InstallationKey -ceq $token -and
       $dateValid -and $parsedTime -ge $auditNotBefore -and
       $parsedTime -le $auditNotAfter -and $pairValid){
     $auditEntries.Add([pscustomobject]@{
      AttemptUtc=$when
      Outcome=$outcome
      Code=$code
     })
    }
   }catch{}
  }
  $recordPath=Join-Path $worker.FullName 'launcher-recovery.json'
  if(-not(Test-Path -LiteralPath $recordPath)){continue}
  if(-not(Plain-File $recordPath 4096)){
   $invalidWorkers++;continue
  }
  try{
   $data=Get-Content -LiteralPath $recordPath -Raw -ErrorAction Stop |
    ConvertFrom-Json -ErrorAction Stop
   if([string]$data.InstallationDirectory -cne $root){continue}
   $scriptPath=Join-Path $worker.FullName 'Launcher-RebootRecovery.ps1'
   $helper=Join-Path $worker.FullName 'Invoke-LauncherReplacement.ps1'
   $ps=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
   $command='"'+$ps+'" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+
     $scriptPath+'" -Mode Resume -InstallationDirectory "'+$root+'"'
   $valid=(
    [int]$data.Version -eq 1 -and
    [string]$data.RunEntry -ceq $entry -and
    [string]$data.RunCommand -ceq $command -and
    [string]$data.ExpectedTag -cmatch '^(?:ci-[1-9][0-9]{0,8}|v[0-9]+\.[0-9]+\.[0-9]+)$' -and
    [string]$data.HelperHash -cmatch '^[0-9a-f]{64}$' -and
    (Plain-File $helper 128KB) -and
    (Plain-File $scriptPath 128KB))
   if($valid){
    $actual=(Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash.ToLowerInvariant()
    $valid=($actual -ceq [string]$data.HelperHash)
   }
   $checkpoints.Add([pscustomobject]@{
    Trusted=[bool]$valid;Command=[string]$data.RunCommand;
    Tag=[string]$data.ExpectedTag
   })
  }catch{$invalidWorkers++}
 }
}
$trusted=($checkpoints.Count -eq 1 -and [bool]$checkpoints[0].Trusted -and
  $hasRun -and $startup -ceq [string]$checkpoints[0].Command -and
  $invalidWorkers -eq 0)
$status='NeedsInspection'
$detail='Unexpected or conflicting native recovery state; do not delete journals or startup entries.'
if($hasTxn){
 if($trusted -and -not $hasPrev -and -not $hasRetired){
  $status='RecoveryPending'
  $detail='Interrupted native update with a matching trusted sign-in recovery checkpoint.'
 }
}elseif($hasRun -or $checkpoints.Count -gt 0 -or $invalidWorkers -gt 0){
 if($trusted -and -not $hasRetired){
  $status='RestartPending'
  $detail='Trusted recovery is armed; verify it completes at next Windows sign-in.'
 }
}elseif($hasRetired){
 $status='RetirementPending'
 $detail='Interrupted backup retirement; use the explicit verified finalization action.'
}elseif($hasPrev){
 $status='BackupAvailable'
 $detail='Committed backup retained; there is no armed native recovery.'
}else{
 $status='Clean'
 $detail='No native update transaction, committed backup or startup recovery registration.'
}
$recentAudit=$null
if($auditEntries.Count -gt 0){
 $recentAudit=@($auditEntries|Sort-Object -Property AttemptUtc -Descending)[0]
}
$report=[ordered]@{
 Status=$status
 Detail=$detail
 InstalledTag=$tag
 TransactionPresent=[bool]$hasTxn
 BackupPresent=[bool]$hasPrev
 RetiredPresent=[bool]$hasRetired
 StartupEntryPresent=[bool]$hasRun
 MatchingCheckpointCount=[int]$checkpoints.Count
 CheckpointTrusted=[bool]$trusted
 LastAttemptUtc=$(if($null -ne $recentAudit){[string]$recentAudit.AttemptUtc}else{''})
 LastAttemptOutcome=$(if($null -ne $recentAudit){[string]$recentAudit.Outcome}else{'NotRecorded'})
 LastAttemptCode=$(if($null -ne $recentAudit){[string]$recentAudit.Code}else{'NotRecorded'})
}
if($Format -ceq 'Text'){
 Write-Output ("Native Launcher recovery: "+$status)
 Write-Output ("Installed: "+$tag)
 Write-Output $detail
 Write-Output ("Transaction: "+$hasTxn+"; backup: "+$hasPrev+
  "; retirement: "+$hasRetired+"; Run entry: "+$hasRun)
 Write-Output ("Last sign-in recovery: "+[string]$report.LastAttemptOutcome+
  " / "+[string]$report.LastAttemptCode+" / "+[string]$report.LastAttemptUtc)
}else{
 $report|ConvertTo-Json -Compress
}
