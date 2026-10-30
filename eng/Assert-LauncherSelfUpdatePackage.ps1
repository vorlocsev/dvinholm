#requires -Version 5.1
<#
Offline stage-only verification for the independent native launcher ZIP.
The caller supplies a SHA-256 independently obtained from trusted release
metadata (and must also verify the release checksum manifest). This script
does not fetch metadata, run package scripts, or replace installed files.
#>
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$ZipPath,
 [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedSha256,
 [Parameter(Mandatory)][string]$StagingDirectory
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$required=@(
 'Launcher.exe',
 'Binaries/Updater-Banner.ps1',
 'Binaries/Launcher-AuroraTheme.ps1',
 'Binaries/Launcher-ServiceCockpit.ps1',
 'Binaries/Launcher-ReleaseDiscovery.ps1',
 'Binaries/Launcher-VerifiedDownload.ps1',
 'Binaries/Launcher-ApplyPrepared.ps1',
 'Binaries/Launcher-RebootRecovery.ps1',
 'Binaries/Inspect-LauncherRecovery.ps1',
 'Binaries/J2534-DriverManager.ps1',
 'Binaries/J2534-DriverUI.ps1',
 'Binaries/Manage-Lite-Dependencies.ps1',
 'Binaries/Dvinholm-icon.png',
 'Binaries/dvinholm-banner.webp',
 'Binaries/launcher-service-cockpit-banner.jpg',
 'Binaries/Assert-LauncherSelfUpdatePackage.ps1',
 'Binaries/Invoke-LauncherReplacement.ps1',
 'Binaries/launcher-build.txt'
)
$allowed=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach($name in $required){[void]$allowed.Add($name)}

function Assert-OrdinaryAncestors([string]$path){
 $ancestor=[IO.Path]::GetDirectoryName($path)
 while($ancestor){
  if(Test-Path -LiteralPath $ancestor){
   if(-not(Test-Path -LiteralPath $ancestor -PathType Container)){
    throw 'Launcher staging ancestor is not a folder.'
   }
   if(((Get-Item -LiteralPath $ancestor -Force).Attributes -band
      [IO.FileAttributes]::ReparsePoint) -ne 0){
    throw 'Launcher staging ancestor is a reparse point.'
   }
  }
  $next=[IO.Path]::GetDirectoryName($ancestor)
  if(-not $next -or $next -ceq $ancestor){break}
  $ancestor=$next
 }
}
function Assert-NativeX86([string]$file){
 $bytes=[IO.File]::ReadAllBytes($file)
 if($bytes.Length -lt 512 -or $bytes[0] -ne 77 -or $bytes[1] -ne 90){
  throw 'Launcher package does not contain an ordinary Windows executable.'
 }
 $pe=[BitConverter]::ToInt32($bytes,60)
 if($pe -lt 64 -or $pe+24 -ge $bytes.Length -or
    [BitConverter]::ToUInt32($bytes,$pe) -ne 0x4550 -or
    [BitConverter]::ToUInt16($bytes,$pe+4) -ne 0x14c -or
    [BitConverter]::ToUInt16($bytes,$pe+24) -ne 0x10b){
  throw 'Launcher package is not Windows x86 PE32.'
 }
}
$archive=(Resolve-Path -LiteralPath $ZipPath -ErrorAction Stop).Path
if(-not(Test-Path -LiteralPath $archive -PathType Leaf)){
 throw 'Launcher update archive is not a regular file.'
}
if(((Get-Item -LiteralPath $archive -Force).Attributes -band
    [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Launcher ZIP link rejected.'}
if((Get-Item -LiteralPath $archive).Length -gt 2MB){
 throw 'Launcher update archive exceeds the size budget.'
}
$actual=(Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash.ToLowerInvariant()
if($actual -cne $ExpectedSha256){
 throw 'Launcher ZIP SHA-256 does not match the release metadata.'
}
$dest=[IO.Path]::GetFullPath($StagingDirectory)
if(Test-Path -LiteralPath $dest){
 throw 'Launcher staging directory must not exist.'
}
Assert-OrdinaryAncestors $dest
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
$created=$false
try{
 if($zip.Entries.Count -ne $required.Count){
  throw 'Launcher ZIP does not contain the required number of files.'
 }
 $seen=[Collections.Generic.HashSet[string]]::new(
  [StringComparer]::OrdinalIgnoreCase)
 $total=[long]0
 foreach($entry in $zip.Entries){
  $name=[string]$entry.FullName
  # Exact, forward-slash-only whitelist also rejects traversal, ADS,
  # rooted/UNC paths, path aliases, directory entries and untrusted extras.
  if(-not $allowed.Contains($name) -or -not $seen.Add($name)){
   throw ('Unexpected, unsafe or duplicate launcher ZIP entry: '+$name)
  }
  if((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000 -or
     ($entry.ExternalAttributes -band 0x400) -ne 0){
   throw 'Launcher ZIP contains a link/reparse entry.'
  }
  if($entry.Length -lt 1 -or $entry.Length -gt 4MB){
   throw 'Launcher ZIP member exceeds the size budget.'
  }
  $total+=[long]$entry.Length
  if($total -gt 16MB){throw 'Launcher ZIP expansion budget exceeded.'}
 }
 foreach($name in $required){
  if(-not $seen.Contains($name)){throw ('Missing launcher file: '+$name)}
 }
 [IO.Directory]::CreateDirectory($dest)|Out-Null
 $created=$true
 foreach($entry in $zip.Entries){
  $output=Join-Path $dest $entry.FullName.Replace('/','\')
  [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output))|Out-Null
  $source=$entry.Open()
  $file=[IO.File]::Create($output)
  try{$source.CopyTo($file)}finally{$file.Dispose();$source.Dispose()}
  if(([IO.FileInfo]$output).Length -ne $entry.Length){
   throw 'Launcher update member length mismatch.'
  }
 }
 Assert-NativeX86 (Join-Path $dest 'Launcher.exe')
 Write-Host ('Verified launcher archive and staged '+$required.Count+
  ' files without changing the installed Launcher.exe.')
} catch {
 if($created -and (Test-Path -LiteralPath $dest)){
  Remove-Item -LiteralPath $dest -Recurse -Force -ErrorAction SilentlyContinue
 }
 throw
} finally {$zip.Dispose()}
