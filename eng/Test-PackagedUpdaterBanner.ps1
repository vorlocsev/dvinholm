#requires -Version 5.1
<#
  Run the *packaged* standalone Updater Banner from a fresh extraction.
  Windows PowerShell 5.1 and inbox .NET Framework only. No UI modal dialog,
  download, installation or Dvinholm process is started.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ZipPath)

$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipFile=(Resolve-Path -LiteralPath $ZipPath -ErrorAction Stop).Path
$expected=@('Updater-Banner.cmd','Updater-Banner.ps1','Manage-Lite-Dependencies.ps1')
$source=(Resolve-Path -LiteralPath $PSScriptRoot).Path
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-banner-zip-smoke-'+[guid]::NewGuid().ToString('N'))
$psExe=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$checks=0

function Start-PackagedMode([string]$mode,[string]$expectedMessage) {
 $psi=New-Object System.Diagnostics.ProcessStartInfo
 $psi.FileName=$psExe
 $psi.Arguments='-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+
  (Join-Path $testRoot 'Updater-Banner.ps1')+'" -Mode '+$mode+' -Channel Development'
 $psi.WorkingDirectory=$testRoot
 $psi.UseShellExecute=$false
 $psi.CreateNoWindow=$true
 $psi.RedirectStandardOutput=$true
 $psi.RedirectStandardError=$true
 $p=[Diagnostics.Process]::Start($psi)
 if($null -eq $p){throw "Packaged $mode process failed to start."}
 try {
  if(-not $p.WaitForExit(90000)){
   try{$p.Kill()}catch{}
   throw "Packaged $mode process exceeded the 90-second safety limit."
  }
  $stdout=$p.StandardOutput.ReadToEnd()
  $stderr=$p.StandardError.ReadToEnd()
  if($p.ExitCode -ne 0){
   throw "Packaged $mode exited with $($p.ExitCode): $stderr $stdout"
  }
  if($expectedMessage -and -not $stdout.Contains($expectedMessage)){
   throw "Packaged $mode did not report the expected checks: $stdout"
  }
 }finally{$p.Dispose()}
}
try {
 $zip=[IO.Compression.ZipFile]::OpenRead($zipFile)
 try {
  $entries=@($zip.Entries)
  if($entries.Count -ne $expected.Count){throw 'Unexpected standalone ZIP entry count.'}
  $actual=@($entries|ForEach-Object { [string]$_.FullName })
  foreach($name in $actual){
   if($name -cnotin $expected){throw "Unexpected archive entry: $name"}
  }
  if(@($actual|Select-Object -Unique).Count -ne $expected.Count){
   throw 'Duplicate entries in packaged standalone ZIP.'
  }
  $checks++
 }finally{$zip.Dispose()}
 [IO.Directory]::CreateDirectory($testRoot)|Out-Null
 [IO.Compression.ZipFile]::ExtractToDirectory($zipFile,$testRoot)
 foreach($name in $expected){
  if(-not(Test-Path -LiteralPath (Join-Path $testRoot $name) -PathType Leaf)){
   throw "Extracted ZIP missing $name."
  }
 }
 $checks++
 foreach($name in $expected){
  $a=(Get-FileHash -LiteralPath (Join-Path $source $name) -Algorithm SHA256).Hash
  $b=(Get-FileHash -LiteralPath (Join-Path $testRoot $name) -Algorithm SHA256).Hash
  if($a -cne $b){throw "Extracted standalone file differs from source: $name"}
 }
 $checks++
 $launcher=Get-Content -LiteralPath (Join-Path $testRoot 'Updater-Banner.cmd') -Raw
 if(-not $launcher.Contains('%~dp0Updater-Banner.ps1') -or
    -not $launcher.Contains('-Mode UI')){
  throw 'Extracted CMD does not launch its adjacent standalone PowerShell script.'
 }
 $checks++
 if(-not(Test-Path -LiteralPath $psExe -PathType Leaf)){
  throw 'Inbox Windows PowerShell 5.1 host not found.'
 }
 Start-PackagedMode 'SelfTest' '8/8 interrupted install recovery checks passed'
 $checks++
 Start-PackagedMode 'UiSmokeTest' '7/7 Windows PowerShell 5.1 WinForms smoke checks passed'
 $checks++
 Start-PackagedMode 'Probe' ''
 $checks++
 if($checks -ne 7){throw 'Wrong packaged Updater Banner smoke check count.'}
 Write-Host 'Packaged Updater Banner 7/7 isolated Windows smoke checks passed.'
}finally{
 if(Test-Path -LiteralPath $testRoot){
  Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
 }
}
