#requires -Version 5.1
<#
Eight checks against the actual CI-built standalone Launcher ZIP.
Runs read-only inspector and its background UI worker on a disposable
temp installation. Never performs installs, boots, network calls,
real registry writes or physical J2534 operations.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ZipPath,
 [Parameter(Mandatory)][string]$ExpectedTag)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=(Resolve-Path -LiteralPath $ZipPath).Path
$root=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-packaged-inspection-'+[guid]::NewGuid().ToString('N'))
$install=Join-Path $root 'install'
$stateFile=Join-Path $root 'worker-state.json'
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
$checks=0
function Assert-Status([string]$expected){
 $raw=(& $script:inspector -InstallationDirectory $install -Format Json|Out-String).Trim()
 if(-not $raw){throw 'Packaged Launcher inspector returned no JSON.'}
 $data=ConvertFrom-Json -InputObject $raw
 if([string]$data.Status -cne $expected){
  throw ('Expected packaged inspection '+$expected+' but got '+[string]$data.Status)
 }
 return $data
}
function Worker-Status([string]$expected){
 if(Test-Path $stateFile){Remove-Item -LiteralPath $stateFile -Force}
 $powershell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
 $ui=Join-Path $install 'Binaries\Updater-Banner.ps1'
 $arguments='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+
  $ui+'" -Mode InspectLauncher -Channel Development -StateFile "'+$stateFile+'"'
 $worker=Start-Process -FilePath $powershell -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
 try{
  if([int]$worker.ExitCode -ne 0){throw 'Packaged inspect worker failed.'}
 }finally{$worker.Dispose()}
 $snapshot=Get-Content -LiteralPath $stateFile -Raw|ConvertFrom-Json
 if($snapshot.Done -ne $true -or $snapshot.Failed -ne $false -or
    -not([string]$snapshot.LauncherDetail)){
  throw 'Launcher inspect worker did not return a successful recovery snapshot.'
 }
 $result=ConvertFrom-Json -InputObject ([string]$snapshot.LauncherDetail)
 if([string]$result.Status -cne $expected){
  throw ('Packaged UI inspection expected '+$expected+', got '+[string]$result.Status)
 }
 return $snapshot
}
try{
 # 1. Exactly 18 strictly allowlisted native Launcher files.
 $entries=@($zip.Entries|Where-Object{-not $_.FullName.EndsWith('/')})
 if($entries.Count -ne 18 -or
    @($entries|Where-Object{$_.FullName -ceq 'Binaries/Inspect-LauncherRecovery.ps1'}).Count -ne 1){
  throw 'Bundled recovery inspector missing or native file count changed.'
 }
 $checks++
 [IO.Compression.ZipFile]::ExtractToDirectory($archive,$install)
 $script:inspector=Join-Path $install 'Binaries\Inspect-LauncherRecovery.ps1'
 # 2. Bundled inspector bytes are identical to verified source code.
 $expectedSource=Join-Path $PSScriptRoot 'Inspect-LauncherRecovery.ps1'
 if((Get-FileHash -LiteralPath $script:inspector -Algorithm SHA256).Hash -cne
    (Get-FileHash -LiteralPath $expectedSource -Algorithm SHA256).Hash){
  throw 'Native Launcher recovery inspector does not match repository sources.'
 }
 $checks++
 # 3. Read-only tool accepts an ordinary standalone installation.
 $state=Assert-Status 'Clean'
 if([string]$state.InstalledTag -cne $ExpectedTag -or $state.StartupEntryPresent){
  throw 'Packaged inspector did not read native build tag or reported ghost startup.'
 }
 $checks++
 # 4. Current version + committed backup is clearly distinguished.
 [IO.Directory]::CreateDirectory((Join-Path $install '.launcher-previous'))|Out-Null
 $state=Assert-Status 'BackupAvailable'
 if(-not $state.BackupPresent){throw 'Packaged recovery did not see committed backup.'}
 $checks++
 # 5. Orphan transaction triggers fail-closed inspection status.
 [IO.Directory]::CreateDirectory((Join-Path $install '.launcher-transaction'))|Out-Null
 $state=Assert-Status 'NeedsInspection'
 if(-not $state.TransactionPresent){throw 'Packaged recovery hid unresolved native transaction.'}
 $checks++
 Remove-Item -LiteralPath (Join-Path $install '.launcher-transaction') -Recurse -Force
 Remove-Item -LiteralPath (Join-Path $install '.launcher-previous') -Recurse -Force
 $nativeHash=(Get-FileHash -LiteralPath $script:inspector -Algorithm SHA256).Hash
 $versionFile=Join-Path $install 'Binaries\launcher-build.txt'
 $versionHash=(Get-FileHash -LiteralPath $versionFile -Algorithm SHA256).Hash
 # 6. Actual bundled Updater Banner worker propagates Clean as JSON.
 $null=Worker-Status 'Clean'
 $checks++
 # 7. Actual bundled worker propagates a retained backup status.
 [IO.Directory]::CreateDirectory((Join-Path $install '.launcher-previous'))|Out-Null
 $null=Worker-Status 'BackupAvailable'
 $checks++
 # 8. Neither direct nor UI inspection altered installed bytes or removed backup.
 if((Get-FileHash -LiteralPath $script:inspector -Algorithm SHA256).Hash -cne $nativeHash -or
    (Get-FileHash -LiteralPath $versionFile -Algorithm SHA256).Hash -cne $versionHash -or
    -not(Test-Path (Join-Path $install '.launcher-previous'))){
  throw 'Packaged recovery inspection changed installed Launcher bytes or backup.'
 }
 $checks++
 if($checks -ne 8){throw 'Wrong packaged native Launcher recovery test count.'}
 Write-Host 'Packaged Launcher recovery UI/inspector: 8/8 offline checks passed.'
}finally{
 $zip.Dispose()
 if(Test-Path -LiteralPath $root){
  Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
 }
}
