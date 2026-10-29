#requires -Version 5.1
<#
Build x86 /MT Win32 launchers under an x86 MSVC developer environment.
The executables need no .NET 10 Desktop Runtime or Windows App SDK.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$cl=Get-Command 'cl.exe' -ErrorAction Stop
$rc=Get-Command 'rc.exe' -ErrorAction Stop
$source=Join-Path $PSScriptRoot 'native\launcher.cpp'
$root=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$target=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $target -Force|Out-Null
$brandedAppIcon=Join-Path $root 'src\Dvinholm.App\Assets\Dvinholm.ico'
$brandedLauncherIcon=Join-Path $target 'Dvinholm-Launcher.ico'
& (Join-Path $PSScriptRoot 'New-LauncherIcon.ps1') -LogoPath (
 Join-Path $root 'assets\dvinholm-icon.png') -IconPath $brandedLauncherIcon
if(-not(Test-Path -LiteralPath $brandedAppIcon -PathType Leaf)){
 throw 'Dvinholm app ICO missing.'
}
foreach($spec in @(
 @{Name='Launcher.exe';Define='DVINHOLM_UPDATER';Icon=$brandedLauncherIcon},
 @{Name='Dvinholm.exe';Define='DVINHOLM_DIRECT_LAUNCH';Icon=$brandedAppIcon}
)){
 $out=Join-Path $target $spec.Name
 $res=Join-Path $target ($spec.Name+'.res')
 $rcFile=Join-Path $target ($spec.Name+'.rc')
 # Win32 resource compiler embeds ICO in the actual executable, rather than
 # relying on a loose icon file or Windows shortcut metadata.
 $absoluteIcon=(Resolve-Path -LiteralPath $spec.Icon).Path.Replace('\','/')
 [IO.File]::WriteAllText($rcFile,('1 ICON "'+$absoluteIcon+'"'+"\r\n"),
  [Text.Encoding]::ASCII)
 try {
  & $rc.Source '/nologo' ('/fo'+$res) $rcFile
  if($LASTEXITCODE -ne 0 -or -not(Test-Path -LiteralPath $res -PathType Leaf)){
   throw "Native icon resource compilation failed: $($spec.Name)"
  }
  $args=@('/nologo','/std:c++17','/O2','/MT','/DUNICODE','/D_UNICODE','/W4',
          '/EHsc',("/D"+$spec.Define),$source,$res,
          '/link','/SUBSYSTEM:WINDOWS','/MACHINE:X86',('/OUT:'+$out))
  & $cl.Source @args
 }finally{
  Remove-Item -LiteralPath $rcFile,$res -Force -ErrorAction SilentlyContinue
 }
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
