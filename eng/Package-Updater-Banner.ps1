#requires -Version 5.1
<#
  Build a separate ~20 KiB updater GUI archive. Requires ONLY inbox
  Windows PowerShell 5.1 + .NET Framework on the target computer.
  Does not package Dvinholm.App or any .NET 10/WinApp runtime.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ZipPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root=(Resolve-Path -LiteralPath $PSScriptRoot).Path
$names=@('Updater-Banner.cmd','Updater-Banner.ps1','Manage-Lite-Dependencies.ps1')
$target=[IO.Path]::GetFullPath($ZipPath)
$parent=Split-Path -Parent $target
New-Item -ItemType Directory -Path $parent -Force|Out-Null
$temp=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-banner-package-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp|Out-Null
try{
 foreach($name in $names){
  Copy-Item -LiteralPath (Join-Path $root $name) -Destination (Join-Path $temp $name)
 }
 if(Test-Path $target){Remove-Item -LiteralPath $target -Force}
 Compress-Archive -Path (Join-Path $temp '*') -DestinationPath $target -CompressionLevel Optimal
 $zip=[IO.Compression.ZipFile]::OpenRead($target)
 try{
  if($zip.Entries.Count -ne $names.Count){
   throw 'Standalone updater archive has an unexpected file count.'
  }
  foreach($name in $names){
   $entry=$zip.GetEntry($name)
   if($null -eq $entry){throw "Updater archive is missing $name."}
   $stream=$entry.Open();$memory=[IO.MemoryStream]::new()
   try{$stream.CopyTo($memory);$bytes=$memory.ToArray()}
   finally{$stream.Dispose();$memory.Dispose()}
   $expected=[IO.File]::ReadAllBytes((Join-Path $root $name))
   if($bytes.Length -ne $expected.Length){
    throw "Updater banner script content mismatch: $name"
   }
   for($i=0;$i -lt $expected.Length;$i++){
    if($bytes[$i] -ne $expected[$i]){throw "Updater bundle content mismatch: $name"}
   }
  }
 }finally{$zip.Dispose()}
 if((Get-Item $target).Length -gt 250KB){
  throw 'Updater Banner archive is not lightweight (limit: 250 KiB).'
 }
 Write-Host "Verified standalone Updater Banner ZIP: $($names.Count) source-matched files."
}finally{
 if(Test-Path $temp){Remove-Item $temp -Recurse -Force}
}
