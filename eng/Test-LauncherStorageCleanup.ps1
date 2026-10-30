#requires -Version 5.1
<#
Eight offline Windows PowerShell 5.1 cleanup gates in a private temp tree.
No network calls, runtime installation, GUI start, driver access or CAN.
#>
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Launcher-ReleaseDiscovery.ps1')
. (Join-Path $PSScriptRoot 'Launcher-VerifiedDownload.ps1')
$root=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-storage-fixtures-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
$now=[datetime]'2026-10-30T12:00:00Z'
$checks=0
function Fixture([string]$name,[string]$channel='Development'){
 $home=Join-Path $root $name
 $storage=Join-Path $home 'LauncherUpdates'
 $folder=Join-Path $storage $channel
 [IO.Directory]::CreateDirectory($folder)|Out-Null
 $install=Join-Path $home 'installation'
 [IO.Directory]::CreateDirectory($install)|Out-Null
 return [pscustomobject]@{Home=$home;Storage=$storage;Folder=$folder;Install=$install;Channel=$channel}
}
function Cleanup($fixture,[string]$build='ci-120'){
 return Invoke-NativeLauncherStorageCleanup -StorageRoot $fixture.Storage -SelectedChannel $fixture.Channel -InstalledBuild $build -InstallationDirectory $fixture.Install -NowUtc $now
}
function Download($fixture,[string]$name,[bool]$old){
 $folder=Join-Path $fixture.Folder $name
 [IO.Directory]::CreateDirectory($folder)|Out-Null
 [IO.File]::WriteAllText((Join-Path $folder 'SHA256SUMS.txt'),'partial', [Text.Encoding]::ASCII)
 $timestamp=if($old){$now.AddHours(-72)}else{$now.AddHours(-2)}
 [IO.Directory]::SetLastWriteTimeUtc($folder,$timestamp)
 return $folder
}
function Prepared($fixture,[string]$channel,[string]$tag){
 $folder=Join-Path $fixture.Folder 'prepared'
 [IO.Directory]::CreateDirectory($folder)|Out-Null
 $record=@{Channel=$channel;Tag=$tag;ZipSha256=('a'*64);Applied=$false}
 [IO.File]::WriteAllText((Join-Path $folder 'prepared.json'),
  ($record|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
 return $folder
}
try{
 # 1. Only known, older-than-48h interrupted downloads are discarded.
 $a=Fixture 'old'
 $aDir=Download $a ('download-'+[guid]::NewGuid().ToString('N')) $true
 $stats=Cleanup $a
 if((Test-Path $aDir) -or $stats.Discarded -ne 1){throw 'Old abandoned download not cleaned.'}
 $checks++
 # 2. Fresh pending downloads are preserved in full.
 $b=Fixture 'recent'
 $bDir=Download $b ('download-'+[guid]::NewGuid().ToString('N')) $false
 $stats=Cleanup $b
 if(-not(Test-Path $bDir) -or $stats.Discarded -ne 0){throw 'Active-looking download was deleted.'}
 $checks++
 # 3. Prepared matching installed build is obsolete, but no install changes.
 $c=Fixture 'current'
 $cDir=Prepared $c 'Development' 'ci-120'
 $testApp=Join-Path $c.Install 'Dvinholm.App.exe'
 [IO.File]::WriteAllText($testApp,'untouched')
 $stats=Cleanup $c
 if((Test-Path $cDir) -or $stats.ObsoletePrepared -ne 1 -or
    [IO.File]::ReadAllText($testApp) -cne 'untouched'){
  throw 'Prepared current build not discarded safely.'
 }
 $checks++
 # 4. A newer prepared update is preserved.
 $d=Fixture 'newer'
 $dDir=Prepared $d 'Development' 'ci-121'
 $stats=Cleanup $d
 if(-not(Test-Path $dDir) -or $stats.Discarded -ne 0){throw 'Newer prepared update was erased.'}
 $checks++
 # 5. Unknown/mismatched metadata never authorizes prepared deletion.
 $e=Fixture 'wrong-channel'
 $eDir=Prepared $e 'Stable' 'ci-120'
 $stats=Cleanup $e
 if(-not(Test-Path $eDir) -or $stats.Discarded -ne 0){throw 'Cross-channel prepared snapshot was erased.'}
 $checks++
 # 6. A stale directory with any unrecognized content fails closed.
 $f=Fixture 'unexpected-file'
 $fDir=Download $f ('download-'+[guid]::NewGuid().ToString('N')) $true
 [IO.File]::WriteAllText((Join-Path $fDir 'foreign.data'),'keep')
 $rejected=$false
 try{Cleanup $f|Out-Null}catch{$rejected=$true}
 if(-not $rejected -or -not(Test-Path (Join-Path $fDir 'foreign.data'))){
  throw 'Unsafe interrupted download content was deleted.'
 }
 $checks++
 # 7. Crash after atomic rename can be completed from known discard-* tree.
 $g=Fixture 'resumable'
 $gDir=Download $g ('discard-'+[guid]::NewGuid().ToString('N')) $false
 $stats=Cleanup $g
 if((Test-Path $gDir) -or $stats.Discarded -ne 1){
  throw 'Partly discarded directory could not be cleaned on retry.'
 }
 $checks++
 # 8. Stable and Development storage are isolated.
 $h=Fixture 'isolated'
 $hDev=Download $h ('download-'+[guid]::NewGuid().ToString('N')) $true
 $hStable=Join-Path $h.Storage 'Stable'
 [IO.Directory]::CreateDirectory($hStable)|Out-Null
 $safe=Join-Path $hStable 'prepared'
 [IO.Directory]::CreateDirectory($safe)|Out-Null
 [IO.File]::WriteAllText((Join-Path $safe 'prepared.json'),'stable remains')
 $stats=Cleanup $h
 if((Test-Path $hDev) -or -not(Test-Path (Join-Path $safe 'prepared.json')) -or
    $stats.Discarded -ne 1){
  throw 'Cleanup crossed release channel boundaries.'
 }
 $checks++
 if($checks -ne 8){throw 'Unexpected native Launcher storage test count.'}
 Write-Host 'Native Launcher storage maintenance: 8/8 offline checks passed.'
}finally{
 if(Test-Path -LiteralPath $root){
  Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
 }
}
