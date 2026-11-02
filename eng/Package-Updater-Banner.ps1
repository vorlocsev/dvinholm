#requires -Version 5.1
<#
Create a dependency-free native x86 Launcher.exe with the existing verified
PowerShell 5.1 updater engine in Binaries. No .NET 10 or WinApp SDK needed.
#>
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$ZipPath,
 [string]$NativeDirectory='artifacts/native',
 [ValidatePattern('^(?:ci-[1-9][0-9]{0,8}|v[0-9]+\.[0-9]+\.[0-9]+|unversioned)$')][string]$BuildTag='unversioned'
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$native=[IO.Path]::GetFullPath($NativeDirectory)
$target=[IO.Path]::GetFullPath($ZipPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
$staging=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-launcher-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory((Join-Path $staging 'Binaries'))|Out-Null
$files=@{
 'Launcher.exe'=(Join-Path $native 'Launcher.exe')
 'Binaries/Updater-Banner.ps1'=(Join-Path $PSScriptRoot 'Updater-Banner.ps1')
 'Binaries/Launcher-ReleaseDiscovery.ps1'=(Join-Path $PSScriptRoot 'Launcher-ReleaseDiscovery.ps1')
 'Binaries/Launcher-VerifiedDownload.ps1'=(Join-Path $PSScriptRoot 'Launcher-VerifiedDownload.ps1')
 'Binaries/Launcher-ApplyPrepared.ps1'=(Join-Path $PSScriptRoot 'Launcher-ApplyPrepared.ps1')
 'Binaries/Launcher-RebootRecovery.ps1'=(Join-Path $PSScriptRoot 'Launcher-RebootRecovery.ps1')
 'Binaries/Inspect-LauncherRecovery.ps1'=(Join-Path $PSScriptRoot 'Inspect-LauncherRecovery.ps1')
 'Binaries/Launcher-AuroraTheme.ps1'=(Join-Path $PSScriptRoot 'Launcher-AuroraTheme.ps1')
 'Binaries/Launcher-ServiceCockpit.ps1'=(Join-Path $PSScriptRoot 'Launcher-ServiceCockpit.ps1')
 'Binaries/J2534-DriverManager.ps1'=(Join-Path $PSScriptRoot 'J2534-DriverManager.ps1')
 'Binaries/J2534-DriverUI.ps1'=(Join-Path $PSScriptRoot 'J2534-DriverUI.ps1')
 'Binaries/Manage-Lite-Dependencies.ps1'=(Join-Path $PSScriptRoot 'Manage-Lite-Dependencies.ps1')
 'Binaries/Assert-LauncherSelfUpdatePackage.ps1'=(Join-Path $PSScriptRoot 'Assert-LauncherSelfUpdatePackage.ps1')
 'Binaries/Invoke-LauncherReplacement.ps1'=(Join-Path $PSScriptRoot 'Invoke-LauncherReplacement.ps1')
 'Binaries/Dvinholm-icon.png'=(Join-Path $root 'assets\dvinholm-icon.png')
 'Binaries/dvinholm-banner.webp'=(Join-Path $root 'assets\dvinholm-banner.webp')
 'Binaries/launcher-service-cockpit-banner.jpg'=(Join-Path $root 'assets\launcher-service-cockpit-banner.jpg')
 'Binaries/launcher-approved-reference-atlas.jpg'=(Join-Path $root 'assets\launcher-approved-reference-atlas.jpg')
}
try{
 # The launcher and the app have independent version identities.
 [IO.File]::WriteAllText((Join-Path $staging 'Binaries\launcher-build.txt'),
  $BuildTag,[Text.Encoding]::ASCII)
 foreach($key in $files.Keys){
  $src=$files[$key]
  if(-not(Test-Path -LiteralPath $src -PathType Leaf)){throw "Missing launcher input: $src"}
  Copy-Item -LiteralPath $src -Destination (Join-Path $staging $key.Replace('/','\'))
 }
 if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target -Force}
 [IO.Compression.ZipFile]::CreateFromDirectory($staging,$target,[IO.Compression.CompressionLevel]::Optimal,$false)
 $zip=[IO.Compression.ZipFile]::OpenRead($target)
 try{
  $actual=@($zip.Entries|Where-Object{-not $_.FullName.EndsWith('/')})
  if($actual.Count -ne ($files.Count+1)){throw 'Unexpected launcher ZIP file count.'}
  if($null -eq $zip.GetEntry('Binaries/launcher-build.txt')){
   throw 'Missing native launcher build receipt.'
  }
  foreach($key in $files.Keys){
   $entry=$zip.GetEntry($key)
   if($null -eq $entry){throw "Archive missing $key"}
   $expected=[IO.File]::ReadAllBytes($files[$key])
   $s=$entry.Open();$mem=New-Object IO.MemoryStream
   try{$s.CopyTo($mem);$result=$mem.ToArray()}
   finally{$s.Dispose();$mem.Dispose()}
   if($result.Length -ne $expected.Length){throw "Archive file size mismatch: $key"}
   if(([Convert]::ToBase64String($result)) -cne ([Convert]::ToBase64String($expected))){
    throw "Archive file content mismatch: $key"
   }
  }
 }finally{$zip.Dispose()}
 if((Get-Item -LiteralPath $target).Length -gt 750KB){
  throw 'Standalone native launcher package exceeded 750 KiB.'
 }
 Write-Host 'Verified native x86 Launcher.exe, two bundled PS files and Dvinholm logo.'
}finally{
 if(Test-Path -LiteralPath $staging){Remove-Item -LiteralPath $staging -Recurse -Force}
}
