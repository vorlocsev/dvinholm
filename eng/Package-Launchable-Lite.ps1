#requires -Version 5.1
<#
Full first-run Lite distribution:
  Launcher.exe  - native update manager
  Dvinholm.exe  - native application starter
  Binaries\     - Lite app, preflight, updater engine, brand asset
The legacy flat Lite ZIP remains published for existing updater clients.
#>
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$LiteZip,
 [Parameter(Mandatory)][string]$UpdaterZip,
 [Parameter(Mandatory)][string]$NativeDirectory,
 [Parameter(Mandatory)][string]$ZipPath
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$target=[IO.Path]::GetFullPath($ZipPath)
$lite=(Resolve-Path -LiteralPath $LiteZip).Path
$updater=(Resolve-Path -LiteralPath $UpdaterZip).Path
$native=(Resolve-Path -LiteralPath $NativeDirectory).Path
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
$temp=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-full-lite-'+[guid]::NewGuid().ToString('N'))
$tools=Join-Path $temp 'tools'
$binary=Join-Path $temp 'bundle\Binaries'
try{
 [IO.Directory]::CreateDirectory($binary)|Out-Null
 [IO.Directory]::CreateDirectory($tools)|Out-Null
 [IO.Compression.ZipFile]::ExtractToDirectory($lite,$binary)
 [IO.Compression.ZipFile]::ExtractToDirectory($updater,$tools)
 foreach($name in @('Updater-Banner.ps1','Manage-Lite-Dependencies.ps1','Launcher-AuroraTheme.ps1','J2534-DriverManager.ps1','J2534-DriverUI.ps1','Dvinholm-icon.png','dvinholm-banner.webp')){
  $source=Join-Path (Join-Path $tools 'Binaries') $name
  if(-not(Test-Path -LiteralPath $source -PathType Leaf)){
   throw "Standalone tools package missing $name"
  }
  Copy-Item -LiteralPath $source -Destination (Join-Path $binary $name) -Force
 }
 foreach($name in @('Launcher.exe','Dvinholm.exe')){
  $source=if($name -eq 'Launcher.exe'){
   Join-Path $tools $name
  }else{
   Join-Path $native $name
  }
  if(-not(Test-Path -LiteralPath $source -PathType Leaf)){throw "Missing $name"}
  Copy-Item -LiteralPath $source -Destination (Join-Path (Join-Path $temp 'bundle') $name)
 }
 foreach($name in @('Dvinholm.App.exe','Dvinholm.App.runtimeconfig.json',
   'Start-Dvinholm-Lite.cmd','Check-Lite-Prerequisites.ps1',
   'Manage-Lite-Dependencies.ps1','Updater-Banner.ps1',
   'Launcher-AuroraTheme.ps1','Dvinholm-icon.png','dvinholm-banner.webp')){
  if(-not(Test-Path -LiteralPath (Join-Path $binary $name) -PathType Leaf)){
   throw "Combined Lite bundle is missing Binaries/$name"
  }
 }
 if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target -Force}
 [IO.Compression.ZipFile]::CreateFromDirectory(
  (Join-Path $temp 'bundle'),$target,[IO.Compression.CompressionLevel]::Optimal,$false)
 Write-Host "Verified Launcher.exe + Dvinholm.exe + Binaries/ Lite bundle: $((Get-Item $target).Length) bytes."
}finally{
 if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp -Recurse -Force}
}
