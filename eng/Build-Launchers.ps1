#requires -Version 5.1
<#
Build x86 /MT Win32 launchers under an x86 MSVC developer environment.
The executables need no .NET 10 Desktop Runtime or Windows App SDK.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$cl=Get-Command 'cl.exe' -ErrorAction Stop
$source=Join-Path $PSScriptRoot 'native\launcher.cpp'
$target=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $target -Force|Out-Null
foreach($spec in @(
 @{Name='Launcher.exe';Define='DVINHOLM_UPDATER'},
 @{Name='Dvinholm.exe';Define='DVINHOLM_DIRECT_LAUNCH'}
)){
 $out=Join-Path $target $spec.Name
 $args=@('/nologo','/std:c++17','/O2','/MT','/DUNICODE','/D_UNICODE','/W4',
         '/EHsc',("/D"+$spec.Define),$source,'/link','/SUBSYSTEM:WINDOWS',
         '/MACHINE:X86',('/OUT:'+$out))
 & $cl.Source @args
 if($LASTEXITCODE -ne 0 -or -not(Test-Path -LiteralPath $out -PathType Leaf)){
  throw "Native x86 launcher compilation failed: $($spec.Name)"
 }
 $bytes=[IO.File]::ReadAllBytes($out)
 if($bytes.Length -lt 512 -or $bytes[0] -ne 77 -or $bytes[1] -ne 90){
  throw "Not a Windows PE: $($spec.Name)"
 }
 $pe=[BitConverter]::ToInt32($bytes,60)
 if($pe -lt 64 -or [BitConverter]::ToUInt16($bytes,$pe+4) -ne 0x14c){
  throw "Unexpected architecture: $($spec.Name)"
 }
 Write-Host "$($spec.Name): verified native x86 PE ($($bytes.Length) bytes)."
}
