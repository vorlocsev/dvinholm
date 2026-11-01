#requires -Version 5.1
<#
Offline Windows x86 PE icon-resource acceptance test.
Checks the resources INSIDE both native executables, not loose .ico files.
No Dvinholm process launch, driver access or network.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Directory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DvinholmNativeIconResource {
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  public static extern IntPtr LoadLibraryEx(string name, IntPtr handle, uint flags);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, EntryPoint="FindResourceW", SetLastError=true)]
  public static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern uint SizeofResource(IntPtr module, IntPtr resource);
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern IntPtr LockResource(IntPtr loaded);
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern bool FreeLibrary(IntPtr module);
}
'@
$hashes=@{}
foreach($filename in @('Launcher.exe','Dvinholm.exe')){
 $file=Join-Path ([IO.Path]::GetFullPath($Directory)) $filename
 if(-not(Test-Path -LiteralPath $file -PathType Leaf)){throw "Missing $filename"}
 $module=[DvinholmNativeIconResource]::LoadLibraryEx($file,[IntPtr]::Zero,2)
 if($module -eq [IntPtr]::Zero){throw "Unable to inspect resources: $filename"}
 try{
  $iconGroup=[DvinholmNativeIconResource]::FindResource(
   $module,[IntPtr]::new(1),[IntPtr]::new(14))
  if($iconGroup -eq [IntPtr]::Zero){throw "No RT_GROUP_ICON in $filename"}
  $length=[DvinholmNativeIconResource]::SizeofResource($module,$iconGroup)
  $loaded=[DvinholmNativeIconResource]::LoadResource($module,$iconGroup)
  $ptr=[DvinholmNativeIconResource]::LockResource($loaded)
  if($ptr -eq [IntPtr]::Zero -or $length -lt 6){throw "Invalid icon group: $filename"}
  $count=[Runtime.InteropServices.Marshal]::ReadInt16($ptr,4)
  if($count -lt 7){throw "Expected 7 icon resolutions in $filename; got $count"}
 }finally{[void][DvinholmNativeIconResource]::FreeLibrary($module)}
 $icon=[Drawing.Icon]::ExtractAssociatedIcon($file)
 if($null -eq $icon){throw "No icon image from $filename"}
 try{
  $bitmap=$icon.ToBitmap()
  try{
   $ms=[IO.MemoryStream]::new()
   try{
    $bitmap.Save($ms,[Drawing.Imaging.ImageFormat]::Png)
    $hash=[Security.Cryptography.SHA256]::Create()
    try{$hashes[$filename]=[Convert]::ToBase64String($hash.ComputeHash($ms.ToArray()))}
    finally{$hash.Dispose()}
   }finally{$ms.Dispose()}
  }finally{$bitmap.Dispose()}
 }finally{$icon.Dispose()}
 Write-Host "$filename includes $count native icon resource sizes."
}
if($hashes['Launcher.exe'] -ceq $hashes['Dvinholm.exe']){
 throw 'Both EXEs have an identical icon; Northern Launcher and Dvinholm app must remain distinct.'
}
Write-Host 'Native EXE icons 3/3 offline checks passed.'
