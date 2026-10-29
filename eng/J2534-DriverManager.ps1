#requires -Version 5.1
<#
J2534 vendor-driver inventory and explicit installer handoff.
Windows PowerShell 5.1; no .NET 10, WinApp or third-party dependencies.
Never loads registered vendor DLLs and never downloads/install automatically.
#>
[CmdletBinding()]
param(
 [ValidateSet('Probe','Verify','Install','SelfTest')][string]$Mode='Probe',
 [string]$InstallerPath,
 [ValidateSet('Generic','VolvoDice','MongooseJlr')][string]$Target='Generic',
 [switch]$Approved
)
$ErrorActionPreference='Stop'
function Test-X86PE([string]$Path) {
 if(-not(Test-Path -LiteralPath $Path -PathType Leaf)){return $false}
 $stream=$null
 try {
  $stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
  if($stream.Length -lt 512){return $false}
  $reader=New-Object IO.BinaryReader($stream)
  if($reader.ReadUInt16() -ne 0x5A4D){return $false}
  $stream.Position=60;$offset=$reader.ReadInt32()
  if($offset -lt 64 -or ($offset+6) -ge $stream.Length){return $false}
  $stream.Position=$offset
  return ($reader.ReadUInt32() -eq 0x4550 -and $reader.ReadUInt16() -eq 0x014c)
 } catch{return $false}
 finally{if($stream){$stream.Dispose()}}
}
function Clean-Text([string]$value,[int]$limit=100) {
 if($null -eq $value){return ''}
 $safe=$value -replace '[\x00-\x1f\x7f]',' '
 if($safe.Length -gt $limit){return $safe.Substring(0,$limit)}
 return $safe
}
function Read-RegisteredDrivers {
 $result=New-Object Collections.Generic.List[object]
 $roots=@(
  @{Path='SOFTWARE\PassThruSupport.04.04';Version='04.04';Compatible=$true},
  @{Path='SOFTWARE\PassThruSupport.02.02';Version='02.02';Compatible=$true},
  @{Path='SOFTWARE\PassThruSupport.05.00';Version='05.00';Compatible=$false}
 )
 $machine=$null
 try{$machine=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,[Microsoft.Win32.RegistryView]::Registry32)}
 catch{return @()}
 try{
  foreach($spec in $roots){
   $root=$null
   try{$root=$machine.OpenSubKey($spec.Path)}
   catch{continue}
   if($null -eq $root){continue}
   try{
    foreach($subName in $root.GetSubKeyNames()){
     $key=$null
     try{
      $key=$root.OpenSubKey($subName)
      if($null -eq $key){continue}
      $dll=[string]$key.GetValue('FunctionLibrary','')
      if(-not $dll){$dll=[string]$key.GetValue('FunctionLibraryLocation','')}
      $name=Clean-Text ([string]$key.GetValue('Name',$subName))
      $vendor=Clean-Text ([string]$key.GetValue('Vendor',''))
      $normalized=[Environment]::ExpandEnvironmentVariables($dll.Trim().Trim('"')).Trim()
      $exists=($normalized -and [IO.Path]::IsPathRooted($normalized) -and (Test-Path -LiteralPath $normalized -PathType Leaf))
      $x86=($exists -and (Test-X86PE $normalized))
      $status=if(-not $spec.Compatible){'Other J2534 API (not used by Dvinholm)'}
              elseif(-not $exists){'DLL missing'}
              elseif(-not $x86){'Not a valid x86 DLL'}
              else{'Registered x86 DLL (not hardware-tested)'}
      $result.Add([pscustomobject]@{
       Name=$name;Vendor=$vendor;Api=$spec.Version
       Registered=$true;DllPath=(Clean-Text $normalized 350)
       Compatible=[bool]($spec.Compatible -and $x86);Status=$status
      })
     }catch{}finally{if($key){$key.Dispose()}}
    }
   }catch{}finally{$root.Dispose()}
  }
 }finally{$machine.Dispose()}
 return @($result.ToArray()|Sort-Object Vendor,Name,Api)
}

# Only read Windows Plug-and-Play metadata. No USB opening, vendor library
# loading or hardware interrogation occurs in either discovery path.
function Read-DicePnpDevices {
 try {
  if(Get-Command Get-PnpDevice -ErrorAction SilentlyContinue){
   return @(Get-PnpDevice -PresentOnly -ErrorAction Stop |
    Select-Object FriendlyName,InstanceId,Status)
  }
 }catch{}
 # Some LTSC installations lack Get-PnpDevice; use the inbox CIM provider.
 try {
  return @(Get-CimInstance -ClassName Win32_PnPEntity -ErrorAction Stop |
   Where-Object { $_.Present -ne $false } |
   ForEach-Object {
    [pscustomobject]@{FriendlyName=$_.Name;InstanceId=$_.PNPDeviceID;Status=$_.Status}
   })
 }catch{return @()}
}
function Get-DiceUsbClassification([object[]]$Devices) {
 $diceMatches=@($Devices|Where-Object{
  $instance=[string]$_.InstanceId
  $name=[string]$_.FriendlyName
  $instance -match '(?i)^USB\\' -and
  $name -match '(?i)\bDiCE\b|Volvo.*Diagnostic Communication'
 })
 $good=@($diceMatches|Where-Object{[string]$_.Status -eq 'OK'})
 $bad=@($diceMatches|Where-Object{
  [string]$_.Status -match '(?i)^(ERROR|DEGRADED|FAIL|PROBLEM)$'
 })
 $health=if($good.Count -gt 0){'OK'}
         elseif($bad.Count -gt 0){'Problem'}
         elseif($diceMatches.Count -gt 0){'Unknown'}
         else{'Absent'}
 $message=switch($health){
  'OK' {'USB DiCE found; Windows reports OK (VIDA communication untested)'}
  'Problem' {'USB DiCE found; Windows reports a driver/device problem'}
  'Unknown' {'USB DiCE found; driver status unknown'}
  default {'DiCE USB not detected; installed VIDA driver cannot be inferred'}
 }
 return [pscustomobject]@{
  UsbDetected=($diceMatches.Count -gt 0)
  UsbCount=$diceMatches.Count
  DriverStatus=$health
  Status=$message
 }
}
# Model-aware detection only; no vehicle access and no assumption that DiCE
# exposes the SAE J2534 FunctionLibrary API.
function Get-TargetDeviceState {
 $entries=@(Read-RegisteredDrivers)
 $mongoose=@($entries|Where-Object{
  (($_.Name+' '+$_.Vendor) -match '(?i)mongoose.*jlr|jlr.*mongoose') -and $_.Compatible
 })
 $dice=Get-DiceUsbClassification @(Read-DicePnpDevices)
 return [pscustomobject]@{
  MongooseJlr=[pscustomobject]@{
   Name='MongoosePro JLR';RegisteredX86Dll=($mongoose.Count -gt 0)
   DllCount=$mongoose.Count
   Status=if($mongoose.Count){'J2534 x86 registered; hardware untested'}else{'No compatible Mongoose JLR x86 J2534 registration'}
  }
  VolvoDice=[pscustomobject]@{
   Name='Volvo DiCE';UsbDetected=$dice.UsbDetected
   UsbCount=$dice.UsbCount;DriverStatus=$dice.DriverStatus
   Status=$dice.Status
   J2534Supported=$false
  }
 }
}
function Test-SignedVendorInstaller([string]$Path) {
 if([string]::IsNullOrWhiteSpace($Path)){throw 'Select the original vendor installer.'}
 $file=Get-Item -LiteralPath $Path -ErrorAction Stop
 if($file.PSIsContainer -or $file.Extension.ToLowerInvariant() -cnotin @('.exe','.msi')){
  throw 'Select a signed vendor .exe or .msi installer.'
 }
 if(($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
  throw 'Installer links are not allowed.'
 }
 if($file.Length -lt 1024 -or $file.Length -gt 1GB){
  throw 'Installer size is outside the permitted range.'
 }
 $signature=Get-AuthenticodeSignature -LiteralPath $file.FullName
 if($null -eq $signature -or $signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate){
  throw 'Installer must have a valid, trusted Authenticode signature.'
 }
 $publisher=Clean-Text $signature.SignerCertificate.Subject 200
 if(-not $publisher){throw 'Installer publisher is unavailable.'}
 if($Target -eq 'MongooseJlr' -and $publisher -notmatch '(?i)Drew Technologies|OPUS IVS'){
  throw 'MongoosePro JLR setup must be signed by Drew Technologies or OPUS IVS.'
 }
 if($Target -eq 'VolvoDice' -and $publisher -notmatch '(?i)Volvo'){
  throw 'Volvo DiCE setup must be signed by Volvo.'
 }
 return [pscustomobject]@{Path=$file.FullName;Publisher=$publisher;Extension=$file.Extension.ToLowerInvariant()}
}
function Install-SelectedVendorPackage {
 if(-not $Approved){throw 'Explicit user approval is required for installation.'}
 $source=Test-SignedVendorInstaller $InstallerPath
 if($source.Extension -eq '.msi'){
  $process=Start-Process -FilePath (Join-Path $env:WINDIR 'System32\msiexec.exe') -ArgumentList @('/i',('"' + $source.Path + '"')) -Verb RunAs -PassThru -Wait
 }else{
  $process=Start-Process -FilePath $source.Path -Verb RunAs -PassThru -Wait
 }
 if($null -eq $process){throw 'Vendor installer did not start.'}
 if($process.ExitCode -notin @(0,3010,1641)){
  throw ('Vendor installer reported exit code '+$process.ExitCode+'. Check its setup log.')
 }
 Write-Output 'Vendor setup finished. Recheck J2534 registry and attach adapter to confirm.'
}
if($Mode -eq 'Probe'){
 [pscustomobject]@{Devices=@(Read-RegisteredDrivers);Targets=(Get-TargetDeviceState)}|ConvertTo-Json -Depth 6 -Compress
 return
}
if($Mode -eq 'Verify'){
 Test-SignedVendorInstaller $InstallerPath |ConvertTo-Json -Compress
 return
}
if($Mode -eq 'Install'){
 Install-SelectedVendorPackage
 return
}
if($Mode -eq 'SelfTest'){
 $count=0
 if(-not (Test-X86PE $PSCommandPath)){$count++}else{throw 'Text script accepted as x86 PE'}
 if(-not (Test-X86PE 'C:\missing-driver\fake.dll')){$count++}else{throw 'Missing DLL accepted'}
 if((Clean-Text ("x"+[char]10+"y")) -eq 'x y'){$count++}else{throw 'Control character was not sanitized'}
 $rejected=$false
 try{Test-SignedVendorInstaller 'C:\missing-vendor-package\setup.exe'|Out-Null}catch{$rejected=$true}
 if($rejected){$count++}else{throw 'Missing installer accepted'}
 $rejected=$false
 try{Install-SelectedVendorPackage}catch{$rejected=$true}
 if($rejected){$count++}else{throw 'Installation without approval accepted'}
 if(([Microsoft.Win32.RegistryView]::Registry32).ToString() -eq 'Registry32'){$count++}else{throw 'No 32-bit registry view'}
 $ok=Get-DiceUsbClassification @([pscustomobject]@{
  FriendlyName='Volvo DiCE';InstanceId='USB\VID_1234&PID_5678';Status='OK'
 })
 if($ok.UsbDetected -and $ok.DriverStatus -eq 'OK'){$count++}
 else{throw 'Healthy USB DiCE classification failed'}
 $lookalike=Get-DiceUsbClassification @(
  [pscustomobject]@{FriendlyName='DiCE';InstanceId='ROOT\FAKE';Status='OK'},
  [pscustomobject]@{FriendlyName='Volvo DiCE';InstanceId='USB\VID_1234';Status='ERROR'}
 )
 if($lookalike.UsbCount -eq 1 -and $lookalike.DriverStatus -eq 'Problem'){$count++}
 else{throw 'Invalid USB DiCE classification was accepted'}
 if($count -ne 8){throw 'Wrong offline driver safety check count'}
 Write-Host 'J2534 driver manager 8/8 offline safety checks passed.'
 return
}
