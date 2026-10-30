#requires -Version 5.1
<#
Seven offline structural checks for the combined first-run native x86 ZIP.
No Microsoft installer, WinUI startup, GitHub download, or J2534 adapter.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ZipPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$path=(Resolve-Path -LiteralPath $ZipPath).Path
$zip=[IO.Compression.ZipFile]::OpenRead($path)
$checks=0
try{
 $all=@($zip.Entries|Where-Object{-not $_.FullName.EndsWith('/')})
 $names=@($all|ForEach-Object{$_.FullName})
 if(@($names|Select-Object -Unique).Count -ne $names.Count){throw 'Duplicate ZIP file names'}
 $checks++
 foreach($name in @('Launcher.exe','Dvinholm.exe','Binaries/Dvinholm.App.exe',
  'Binaries/Dvinholm.App.runtimeconfig.json',
  'Binaries/Check-Lite-Prerequisites.ps1','Binaries/Start-Dvinholm-Lite.cmd',
  'Binaries/Manage-Lite-Dependencies.ps1','Binaries/Assert-LauncherSelfUpdatePackage.ps1','Binaries/Invoke-LauncherReplacement.ps1','Binaries/launcher-build.txt','Binaries/Updater-Banner.ps1',
  'Binaries/Launcher-AuroraTheme.ps1','Binaries/Launcher-ServiceCockpit.ps1','Binaries/Launcher-ReleaseDiscovery.ps1','Binaries/Launcher-VerifiedDownload.ps1','Binaries/Launcher-ApplyPrepared.ps1','Binaries/Launcher-RebootRecovery.ps1','Binaries/Inspect-LauncherRecovery.ps1',
  'Binaries/J2534-DriverManager.ps1','Binaries/J2534-DriverUI.ps1',
  'Binaries/Dvinholm-icon.png','Binaries/dvinholm-banner.webp','Binaries/launcher-service-cockpit-banner.jpg')){
  if($names -cnotcontains $name){throw "Missing first-run archive member $name"}
 }
 $checks++
 if($names -ccontains 'Dvinholm.App.exe' -or
    $names -ccontains 'Manage-Lite-Dependencies.ps1'){
  throw 'Application binaries escaped the Binaries folder'
 }
 $checks++
 foreach($name in @('Launcher.exe','Dvinholm.exe','Binaries/Dvinholm.App.exe')){
  $entry=$zip.GetEntry($name)
  $stream=$entry.Open();$mem=New-Object IO.MemoryStream
  try{$stream.CopyTo($mem);$data=$mem.ToArray()}
  finally{$stream.Dispose();$mem.Dispose()}
  if($data.Length -lt 512 -or $data[0] -ne 77 -or $data[1] -ne 90){
   throw "Invalid PE: $name"
  }
  $offset=[BitConverter]::ToInt32($data,60)
  if($offset -lt 64 -or $offset+6 -ge $data.Length -or
     [BitConverter]::ToUInt32($data,$offset) -ne 0x4550 -or
     [BitConverter]::ToUInt16($data,$offset+4) -ne 0x14c){
   throw "Invalid x86 PE: $name"
  }
 }
 $checks++
 foreach($name in @('coreclr.dll','Binaries/coreclr.dll','Binaries/dotnet.exe')){
  if($names -ccontains $name){throw 'Self-contained .NET payload in Lite ZIP'}
 }
 $checks++
 if(@($names|Where-Object{$_ -like '*.pdb' -or $_ -like '*.dmp'}).Count -ne 0){
  throw 'Debug files included in release Lite ZIP'
 }
 $checks++
 if($names -ccontains 'Updater-Banner.cmd' -or
    $names -ccontains 'Binaries/Updater-Banner.cmd'){
  throw 'The new distribution must launch via Launcher.exe, not CMD'
 }
 $checks++
 if($checks -ne 7){throw 'Unexpected check count'}
 Write-Host 'Launchable Lite ZIP: 7/7 offline structural checks passed.'
}finally{$zip.Dispose()}
