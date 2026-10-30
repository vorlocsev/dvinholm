#requires -Version 5.1
<#
Eight end-to-end offline tests for the detached worker's verified apply,
rollback and reboot-safe journal preservation. No live app or network.
#>
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$ZipPath,
 [Parameter(Mandatory)][string]$SumsPath,
 [Parameter(Mandatory)][string]$ExpectedTag
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=(Resolve-Path -LiteralPath $ZipPath).Path
$manifest=(Resolve-Path -LiteralPath $SumsPath).Path
$script=Join-Path $PSScriptRoot 'Launcher-ApplyPrepared.ps1'
$replacement=Join-Path $PSScriptRoot 'Invoke-LauncherReplacement.ps1'
$zipHash=(Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$sumsHash=(Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()
$checks=0
$roots=New-Object Collections.Generic.List[string]
function New-Fixture([string]$suffix){
 $parent=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-selfswap-'+[guid]::NewGuid().ToString('N'))
 $roots.Add($parent)
 $install=Join-Path $parent 'install'
 [IO.Compression.ZipFile]::ExtractToDirectory($archive,$install)
 $prepared=Join-Path $parent 'prepared'
 [IO.Directory]::CreateDirectory($prepared)|Out-Null
 [IO.File]::Copy($archive,(Join-Path $prepared 'Dvinholm-Updater-Banner-Windows-x86.zip'))
 [IO.File]::Copy($manifest,(Join-Path $prepared 'SHA256SUMS.txt'))
 [IO.File]::WriteAllText((Join-Path $prepared 'prepared.json'),
  (@{Channel='Development';Tag=$ExpectedTag;ZipSha256=$zipHash;Applied=$false}|ConvertTo-Json -Compress),
  [Text.UTF8Encoding]::new($false))
 [IO.File]::WriteAllText((Join-Path $install 'Binaries\Dvinholm.App.exe'),
  'main application untouched',[Text.Encoding]::ASCII)
 $original=Join-Path $install 'Binaries\Updater-Banner.ps1'
 [IO.File]::AppendAllText($original,([Environment]::NewLine+'# original '+$suffix))
 return [pscustomobject]@{Install=$install;Prepared=$prepared;Original=$original;
   OriginalHash=(Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash}
}
function Apply($fixture,[string]$tag,[string]$digest,[string]$sum,[int]$crash=0) {
 & $script -Mode Worker -Channel Development -InstallationDirectory $fixture.Install -PreparedDirectory $fixture.Prepared -ExpectedTag $tag -ExpectedZipSha256 $digest -ExpectedSumsSha256 $sum -OfflineFixture -SimulateInterruptionAfter $crash |Out-Null
}
function Reject([scriptblock]$action){
 $rejected=$false
 try{& $action|Out-Null}catch{$rejected=$true}
 if(-not $rejected){throw 'Unsafe Launcher apply worker accepted invalid state.'}
 $script:checks++
}
function Fingerprint([string]$path){
 return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}
try{
 # 1. Verified package applies only after checking the prepared receipt.
 $ok=New-Fixture 'valid'
 Apply $ok $ExpectedTag $zipHash $sumsHash
 if((Fingerprint $ok.Original) -eq $ok.OriginalHash){
  throw 'Verified worker did not install the new script.'
 }
 if([IO.File]::ReadAllText((Join-Path $ok.Install 'Binaries\launcher-build.txt')).Trim() -cne $ExpectedTag){
  throw 'Worker installed incorrect build.'
 }
 $checks++
 # 2. Transaction remains undoable while unrelated app contents survive.
 if(-not(Test-Path (Join-Path $ok.Install '.launcher-previous\journal.json')) -or
    [IO.File]::ReadAllText((Join-Path $ok.Install 'Binaries\Dvinholm.App.exe')) -cne
      'main application untouched'){
  throw 'Backup or main app isolation was lost.'
 }
 $checks++
 # 3. Explicit Undo replays verified original backup after committed update.
 & $replacement -Mode Undo -InstallationDirectory $ok.Install|Out-Null
 if((Fingerprint $ok.Original) -ne $ok.OriginalHash -or
    (Test-Path (Join-Path $ok.Install '.launcher-previous'))){
  throw 'Undo did not restore the original install.'
 }
 $checks++
 # 4. No matched GitHub digest means no installed bytes change.
 $wrongDigest=New-Fixture 'digest'
 Reject {Apply $wrongDigest $ExpectedTag ('0'*64) $sumsHash}
 if((Fingerprint $wrongDigest.Original) -ne $wrongDigest.OriginalHash){
  throw 'Wrong digest changed the live install.'
 }
 # 5. Rehashed manifest asset is mandatory.
 $wrongSums=New-Fixture 'manifest'
 Reject {Apply $wrongSums $ExpectedTag $zipHash ('0'*64)}
 # 6. Release tag mismatch is rejected even with valid ZIP and manifest.
 $wrongTag=New-Fixture 'tag'
 Reject {Apply $wrongTag 'ci-999999' $zipHash $sumsHash}
 # 7. Tampered mutable local preparation receipt is rejected.
 $wrongReceipt=New-Fixture 'receipt'
 [IO.File]::WriteAllText((Join-Path $wrongReceipt.Prepared 'prepared.json'),
  (@{Channel='Stable';Tag=$ExpectedTag;ZipSha256=$zipHash;Applied=$false}|ConvertTo-Json -Compress))
 Reject {Apply $wrongReceipt $ExpectedTag $zipHash $sumsHash}
 # 8. Simulated crash midway triggers verified automatic rollback.
 $crash=New-Fixture 'interrupted'
 Reject {Apply $crash $ExpectedTag $zipHash $sumsHash 2}
 if((Fingerprint $crash.Original) -ne $crash.OriginalHash -or
    (Test-Path (Join-Path $crash.Install '.launcher-transaction'))){
  throw 'Interrupted application was not recovered.'
 }
 if($checks -ne 8){throw 'Expected eight offline Launcher apply checks.'}
 Write-Host 'Native Launcher apply handoff: 8/8 offline recovery checks passed.'
}finally{
 foreach($root in $roots){
  if(Test-Path -LiteralPath $root){
   Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
  }
 }
}
