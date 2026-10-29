#requires -Version 5.1
<#
Offline Windows smoke of the real extracted native launcher package.
No GUI dialog, GitHub request, runtime installation or ECU access.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ZipPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipFile=(Resolve-Path -LiteralPath $ZipPath).Path
$expected=@('Launcher.exe','Binaries/Updater-Banner.ps1',
 'Binaries/Manage-Lite-Dependencies.ps1',
 'Binaries/Launcher-AuroraTheme.ps1','Binaries/Launcher-ServiceCockpit.ps1','Binaries/dvinholm-banner.webp','Binaries/launcher-service-cockpit-banner.jpg','Binaries/J2534-DriverManager.ps1',
 'Binaries/J2534-DriverUI.ps1','Binaries/Dvinholm-icon.png')
$source=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$root=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-native-smoke-'+[guid]::NewGuid().ToString('N'))
$ps=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$checks=0
function Invoke-Mode([string]$mode,[string]$marker) {
 $script=Join-Path $root 'Binaries\Updater-Banner.ps1'
 $info=New-Object Diagnostics.ProcessStartInfo
 $info.FileName=$ps
 $info.Arguments='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$script+'" -Mode '+$mode+' -Channel Development'
 $info.UseShellExecute=$false;$info.CreateNoWindow=$true
 $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
 $p=[Diagnostics.Process]::Start($info)
 if($null -eq $p){throw "Unable to start $mode"}
 try{
  if(-not $p.WaitForExit(90000)){try{$p.Kill()}catch{};throw "$mode timed out"}
  $stdout=$p.StandardOutput.ReadToEnd()
  $stderr=$p.StandardError.ReadToEnd()
  if($p.ExitCode -ne 0 -or ($marker -and -not $stdout.Contains($marker))){
   throw "$mode failed: $stdout $stderr"
  }
 }finally{$p.Dispose()}
}
try{
 $z=[IO.Compression.ZipFile]::OpenRead($zipFile)
 try{
  $files=@($z.Entries|Where-Object{-not $_.FullName.EndsWith('/')})
  if($files.Count -ne $expected.Count){throw 'Unexpected launcher package entries'}
  foreach($e in $files){
   if($e.FullName -cnotin $expected){throw "Unexpected launcher path $($e.FullName)"}
  }
  $checks++
 }finally{$z.Dispose()}
 [IO.Compression.ZipFile]::ExtractToDirectory($zipFile,$root)
 foreach($name in $expected){
  if(-not(Test-Path -LiteralPath (Join-Path $root $name.Replace('/','\')) -PathType Leaf)){
   throw "Missing extracted $name"
  }
 }
 $checks++
 $exe=[IO.File]::ReadAllBytes((Join-Path $root 'Launcher.exe'))
 $pe=[BitConverter]::ToInt32($exe,60)
 if($exe.Length -lt 512 -or $exe[0] -ne 77 -or $exe[1] -ne 90 -or
    $pe -lt 64 -or [BitConverter]::ToUInt16($exe,$pe+4) -ne 0x14c){
  throw 'Launcher.exe is not a native x86 PE'
 }
 $checks++
 foreach($entry in @(
  @('Binaries/Updater-Banner.ps1','eng/Updater-Banner.ps1'),
  @('Binaries/Manage-Lite-Dependencies.ps1','eng/Manage-Lite-Dependencies.ps1'),
  @('Binaries/Dvinholm-icon.png','assets/dvinholm-icon.png'),
  @('Binaries/dvinholm-banner.webp','assets/dvinholm-banner.webp'),
  @('Binaries/launcher-service-cockpit-banner.jpg','assets/launcher-service-cockpit-banner.jpg'),
  @('Binaries/Launcher-AuroraTheme.ps1','eng/Launcher-AuroraTheme.ps1'),
  @('Binaries/Launcher-ServiceCockpit.ps1','eng/Launcher-ServiceCockpit.ps1'),
  @('Binaries/J2534-DriverManager.ps1','eng/J2534-DriverManager.ps1'),
  @('Binaries/J2534-DriverUI.ps1','eng/J2534-DriverUI.ps1')
 )){
  $a=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root $entry[0])).Hash
  $b=(Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $source $entry[1])).Hash
  if($a -cne $b){throw "Bundled file differs from repository: $($entry[0])"}
 }
 $checks++
 Invoke-Mode 'SelfTest' '8/8 interrupted install recovery checks passed'
 $checks++
 Invoke-Mode 'UiSmokeTest' '7/7 Windows PowerShell 5.1 WinForms smoke checks passed'
 $checks++
 Invoke-Mode 'Probe' ''
 $checks++
 if($checks -ne 7){throw 'Wrong launcher smoke test count'}
 Write-Host 'Native launcher ZIP: 7/7 offline Windows smoke checks passed.'
}finally{
 if(Test-Path -LiteralPath $root){
  Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
 }
}
