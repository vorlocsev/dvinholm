#requires -Version 5.1
<#
Eight offline replacement/recovery checks. Fixtures use the real standalone
ZIP in a temporary installation; never touch a user installation, launch
the app, or run downloaded scripts.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ZipPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=(Resolve-Path -LiteralPath $ZipPath).Path
$apply=Join-Path $PSScriptRoot 'Invoke-LauncherReplacement.ps1'
$hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
$base=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-selfswap-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($base)|Out-Null
$checks=0
function Fixture([string]$name){
 $p=Join-Path $base $name
 [IO.Compression.ZipFile]::ExtractToDirectory($archive,$p)
 # The real combined distribution has an independent WinUI app under
 # Binaries. Its content must never be replaced by the native launcher.
 [IO.File]::WriteAllText((Join-Path $p 'Binaries\Dvinholm.App.exe'),
  'application sentinel',[Text.Encoding]::ASCII)
 return $p
}
function Apply([string]$directory,[string]$digest,[int]$interrupt=0){
 & $apply -Mode Apply -InstallationDirectory $directory -ZipPath $archive -ExpectedSha256 $digest -OfflineFixture -SimulateInterruptionAfter $interrupt|Out-Null
}
function Fail([scriptblock]$action){
 $rejected=$false
 try{& $action|Out-Null}catch{$rejected=$true}
 if(-not $rejected){throw 'Invalid self-update transaction was accepted.'}
}
function Hash([string]$file){
 return (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
}
try{
 $valid=Fixture 'install'
 $oldExe=Hash (Join-Path $valid 'Launcher.exe')
 $oldScript=Join-Path $valid 'Binaries\Updater-Banner.ps1'
 [IO.File]::AppendAllText($oldScript,([Environment]::NewLine+'# original-local fixture'))
 $oldScriptHash=Hash $oldScript
 Apply $valid $hash
 if((Hash (Join-Path $valid 'Launcher.exe')) -ne $oldExe -or
    (Hash $oldScript) -eq $oldScriptHash){
  throw 'Successful launcher transaction did not install archive bytes.'
 }
 $checks++
 if([IO.File]::ReadAllText((Join-Path $valid 'Binaries\Dvinholm.App.exe')) -cne
    'application sentinel'){throw 'Application data changed during launcher update.'}
 $checks++
 if(-not(Test-Path (Join-Path $valid '.launcher-previous\backup\Launcher.exe')) -or
    (Hash (Join-Path $valid '.launcher-previous\backup\Binaries\Updater-Banner.ps1')) -ne
       $oldScriptHash){
  throw 'Launcher rollback snapshot was not preserved.'
 }
 $checks++
 $bad=Fixture 'bad'
 Fail {Apply $bad ('0'*64)}
 if(Test-Path (Join-Path $bad '.launcher-transaction')){
  throw 'Invalid digest created a transaction.'
 }
 $checks++
 Fail {Apply $valid $hash}
 if(Test-Path (Join-Path $valid '.launcher-transaction')){
  throw 'Second update overwrote the previous-version snapshot.'
 }
 $checks++
 $interrupted=Fixture 'interrupted'
 $oldBefore=Hash (Join-Path $interrupted 'Launcher.exe')
 $script=Join-Path $interrupted 'Binaries\Updater-Banner.ps1'
 [IO.File]::AppendAllText($script,([Environment]::NewLine+'# restore-this-copy'))
 $oldText=Hash $script
 Fail {Apply $interrupted $hash 2}
 if(-not(Test-Path (Join-Path $interrupted '.launcher-transaction\journal.json'))){
  throw 'Interrupted launcher transaction left no recovery journal.'
 }
 $checks++
 & $apply -Mode Recover -InstallationDirectory $interrupted |Out-Null
 if(Test-Path (Join-Path $interrupted '.launcher-transaction')){
  throw 'Recovered transaction was not cleared.'
 }
 if((Hash (Join-Path $interrupted 'Launcher.exe')) -ne $oldBefore -or
    (Hash $script) -ne $oldText -or
    -not(Test-Path (Join-Path $interrupted 'Binaries\Dvinholm.App.exe'))){
  throw 'Recovered launch files differ from originals.'
 }
 $checks++
 $corrupt=Fixture 'corrupt'
 Fail {Apply $corrupt $hash 2}
 $backup=Join-Path $corrupt '.launcher-transaction\backup\Launcher.exe'
 [IO.File]::AppendAllText($backup,'tamper')
 Fail {& $apply -Mode Recover -InstallationDirectory $corrupt}
 if(-not(Test-Path (Join-Path $corrupt '.launcher-transaction\journal.json'))){
  throw 'Corrupt rollback deleted its journal.'
 }
 $checks++
 if($checks -ne 8){throw 'Wrong offline launcher replacement test count.'}
 Write-Host 'Launcher replacement 8/8 offline transaction and recovery checks passed.'
}finally{
 if(Test-Path -LiteralPath $base){
  Remove-Item -LiteralPath $base -Recurse -Force -ErrorAction SilentlyContinue
 }
}
