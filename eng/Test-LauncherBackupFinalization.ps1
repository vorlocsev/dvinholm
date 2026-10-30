#requires -Version 5.1
<#
Eight isolated tests for explicit retirement of a verified native Launcher
backup. Uses the real CI-produced Launcher ZIP. No real installation touched.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ZipPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=(Resolve-Path -LiteralPath $ZipPath).Path
$replace=Join-Path $PSScriptRoot 'Invoke-LauncherReplacement.ps1'
$zipHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash.ToLowerInvariant()
$roots=New-Object Collections.Generic.List[string]
$checks=0
function Fixture([string]$name){
 $parent=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-selfswap-'+[guid]::NewGuid().ToString('N'))
 $roots.Add($parent)
 $root=Join-Path $parent 'install'
 [IO.Compression.ZipFile]::ExtractToDirectory($zip,$root)
 $app=Join-Path $root 'Binaries\Dvinholm.App.exe'
 [IO.File]::WriteAllText($app,'unrelated application must remain unchanged',[Text.Encoding]::ASCII)
 $original=Join-Path $root 'Binaries\Updater-Banner.ps1'
 [IO.File]::AppendAllText($original,([Environment]::NewLine+'# old '+$name))
 & $replace -Mode Apply -InstallationDirectory $root -ZipPath $zip -ExpectedSha256 $zipHash -OfflineFixture|Out-Null
 if(-not(Test-Path -LiteralPath (Join-Path $root '.launcher-previous\journal.json'))){
  throw 'Fixture missing committed Launcher backup.'
 }
 return [pscustomobject]@{Root=$root;App=$app;
  NewHash=(Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash;
  Original=$original}
}
function Reject([scriptblock]$operation){
 $didReject=$false
 try{& $operation|Out-Null}catch{$didReject=$true}
 if(-not $didReject){throw 'Native Launcher backup retirement accepted unsafe state.'}
 $script:checks++
}
function Finalize($f){
 & $replace -Mode Finalize -InstallationDirectory $f.Root|Out-Null
}
try{
 # 1. An intact committed backup can be explicitly released.
 $good=Fixture 'valid'
 Finalize $good
 if((Test-Path -LiteralPath (Join-Path $good.Root '.launcher-previous')) -or
    (Test-Path -LiteralPath (Join-Path $good.Root '.launcher-retired')) -or
    (Get-FileHash -LiteralPath $good.Original -Algorithm SHA256).Hash -cne $good.NewHash -or
    [IO.File]::ReadAllText($good.App) -cne 'unrelated application must remain unchanged'){
  throw 'Finalizing a native Launcher backup changed live files or left a backup.'
 }
 $checks++
 # 2. Replacing again is allowed only AFTER successful retirement.
 & $replace -Mode Apply -InstallationDirectory $good.Root -ZipPath $zip -ExpectedSha256 $zipHash -OfflineFixture|Out-Null
 if(-not(Test-Path -LiteralPath (Join-Path $good.Root '.launcher-previous\journal.json'))){
  throw 'Native Launcher could not create a new verified backup.'
 }
 $checks++
 # 3. A tampered original backup rejects finalization and preserves journal.
 $badOld=Fixture 'bad-original'
 [IO.File]::AppendAllText((Join-Path $badOld.Root '.launcher-previous\backup\Launcher.exe'),'tampered')
 Reject {Finalize $badOld}
 if(-not(Test-Path -LiteralPath (Join-Path $badOld.Root '.launcher-previous\journal.json'))){
  throw 'Corrupt original backup was erased.'
 }
 # 4. Modified current files must not silently discard the rollback point.
 $badCurrent=Fixture 'modified-installed'
 [IO.File]::AppendAllText($badCurrent.Original,'modified')
 Reject {Finalize $badCurrent}
 if(-not(Test-Path -LiteralPath (Join-Path $badCurrent.Root '.launcher-previous'))){
  throw 'Finalization deleted a backup for a modified installation.'
 }
 # 5. Interrupted transactions block any backup retirement.
 $active=Fixture 'interrupted'
 [IO.Directory]::CreateDirectory((Join-Path $active.Root '.launcher-transaction'))|Out-Null
 Reject {Finalize $active}
 # 6. Unexpected content nested in a backup cannot be recursively deleted.
 $unexpected=Fixture 'unexpected'
 [IO.File]::WriteAllText((Join-Path $unexpected.Root '.launcher-previous\unknown.txt'),'keep this')
 Reject {Finalize $unexpected}
 if(-not(Test-Path -LiteralPath (Join-Path $unexpected.Root '.launcher-previous\unknown.txt'))){
  throw 'Native Launcher backup cleanup deleted unrelated content.'
 }
 # 7. A crash after atomic rename, with partial deletion of journal, is
 # safely resumable without touching the working Launcher installation.
 $resumable=Fixture 'resume'
 Move-Item -LiteralPath (Join-Path $resumable.Root '.launcher-previous') -Destination (Join-Path $resumable.Root '.launcher-retired')
 Remove-Item -LiteralPath (Join-Path $resumable.Root '.launcher-retired\journal.json') -Force
 Finalize $resumable
 if(Test-Path -LiteralPath (Join-Path $resumable.Root '.launcher-retired')){
  throw 'Partly retired snapshot was not cleaned.'
 }
 if((Get-FileHash -LiteralPath $resumable.Original -Algorithm SHA256).Hash -cne $resumable.NewHash){
  throw 'Finalization modified live Launcher files.'
 }
 $checks++
 # 8. Even during recovery, unexpected retired files are never removed.
 $badRetired=Fixture 'retired-with-unexpected'
 Move-Item -LiteralPath (Join-Path $badRetired.Root '.launcher-previous') -Destination (Join-Path $badRetired.Root '.launcher-retired')
 [IO.File]::WriteAllText((Join-Path $badRetired.Root '.launcher-retired\unrelated.dat'),'preserve')
 Reject {Finalize $badRetired}
 if(-not(Test-Path -LiteralPath (Join-Path $badRetired.Root '.launcher-retired\unrelated.dat'))){
  throw 'Unexpected retired content was removed.'
 }
 if($checks -ne 8){throw 'Expected eight native Launcher backup retirement checks.'}
 Write-Host 'Native Launcher backup finalization: 8/8 offline checks passed.'
}finally{
 foreach($folder in $roots){
  if(Test-Path -LiteralPath $folder){
   Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
  }
 }
}
