#requires -Version 5.1
param([Parameter(Mandatory)][string]$PackagePath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$path=(Resolve-Path -LiteralPath $PackagePath).Path
$archive=[IO.Compression.ZipFile]::OpenRead($path)
try {
 $entries=@($archive.Entries | ForEach-Object { $_.FullName.Replace('\','/') })
 $manifest=$archive.GetEntry('AppxManifest.xml')
 if(-not $manifest) { throw 'MSIX check 1: missing manifest' }
 $stream=$manifest.Open()
 try { $reader=New-Object IO.StreamReader($stream); try { [xml]$doc=$reader.ReadToEnd() } finally { $reader.Dispose() } } finally { $stream.Dispose() }
 $ns=New-Object Xml.XmlNamespaceManager($doc.NameTable)
 $ns.AddNamespace('a','http://schemas.microsoft.com/appx/manifest/foundation/windows10')
 $identity=$doc.SelectSingleNode('/a:Package/a:Identity',$ns)
 if($identity.ProcessorArchitecture -ne 'x86') { throw 'MSIX check 2: not x86' }
 if($identity.Name -ne 'Dvinholm.Lite') { throw 'MSIX check 3: wrong identity' }
 $app=$doc.SelectSingleNode('/a:Package/a:Applications/a:Application',$ns)
 if(-not $app -or $app.Executable -ne 'Dvinholm.App.exe') { throw 'MSIX check 4: wrong executable' }
 if(@($entries | Where-Object { $_ -match '(^|/)Dvinholm\.App\.exe$' }).Count -ne 1) { throw 'MSIX check 5: missing executable' }
 foreach($name in @('coreclr.dll','hostfxr.dll','hostpolicy.dll','Microsoft.UI.Xaml.dll')) {
   if(@($entries | Where-Object { $_ -match ('(^|/)' + [regex]::Escape($name) + '$') }).Count -gt 0) { throw "MSIX check 6: bundled runtime: $name" }
 }
 foreach($asset in @('MsixSquare44x44Logo.png','MsixSquare150x150Logo.png','MsixWide310x150Logo.png','MsixSplash620x300.png','MsixStoreLogo.png')) {
   if(@($entries | Where-Object { $_ -like "*/$asset" }).Count -eq 0) { throw "MSIX check 7: missing $asset" }
 }
 if(@($entries | Where-Object { $_ -match '\.(pdb|dmp|binlog|trx)$' }).Count -gt 0) { throw 'MSIX check 8: debug payload found' }
 # Check that the opt-in unsigned prototype remains eligible for Windows 11 LTSC
 # and uses the expected full-trust desktop identity. These are metadata
 # checks only: no signature, runtime availability, or installation is tested.
 if($identity.GetAttribute('Publisher') -ne 'CN=Dvinholm Development') {
   throw 'MSIX check 9: unexpected development publisher'
 }
 $families=@($doc.SelectNodes('/a:Package/a:Dependencies/a:TargetDeviceFamily',$ns))
 if($families.Count -ne 1 -or $families[0].GetAttribute('Name') -ne 'Windows.Desktop') {
   throw 'MSIX check 10: expected one Windows.Desktop target family'
 }
 $osText=$families[0].GetAttribute('MinVersion')
 try { $osMinimum=[Version]$osText }
 catch { throw 'MSIX check 11: invalid minimum Windows version' }
 if($osMinimum -gt [Version]'10.0.26100.0' -or $osMinimum -lt [Version]'10.0.17763.0') {
   throw 'MSIX check 11: minimum Windows version outside supported range'
 }
 $fullTrust=$doc.SelectSingleNode('/*[local-name()="Package"]/*[local-name()="Capabilities"]/*[local-name()="Capability" and @Name="runFullTrust"]')
 if($null -eq $fullTrust) { throw 'MSIX check 12: missing runFullTrust capability' }
 $applications=@($doc.SelectNodes('/a:Package/a:Applications/a:Application',$ns))
 if($applications.Count -ne 1 -or $applications[0].GetAttribute('Id') -ne 'App' -or
     $applications[0].GetAttribute('EntryPoint') -ne 'Windows.FullTrustApplication') {
   throw 'MSIX check 13: invalid full-trust app registration'
 }
 if(-not ($entries -ccontains 'Dvinholm.App.exe')) {
   throw 'MSIX check 14: main executable must be at package root'
 }
 # A packaged, framework-dependent WinUI app must declare a Framework
 # package dependency; Store-less Windows may not download it for the user.
 # Intentionally pin metadata to Microsoft.WindowsAppSDK 2.5.1 until a future
 # SDK upgrade updates this contract and its offline deployment guidance.
 $sdkDeps=@($doc.SelectNodes('/a:Package/a:Dependencies/a:PackageDependency',$ns))
 if($sdkDeps.Count -ne 1 -or $sdkDeps[0].GetAttribute('Name') -ne 'Microsoft.WindowsAppRuntime.2') {
   throw 'MSIX check 15: expected exactly one Windows App SDK framework dependency'
 }
 if($sdkDeps[0].GetAttribute('MinVersion') -ne '2.5.1.0') {
   throw 'MSIX check 16: unexpected Windows App SDK minimum version'
 }
 if($sdkDeps[0].GetAttribute('Publisher') -ne 'CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US') {
   throw 'MSIX check 17: unexpected Windows App SDK dependency publisher'
 }
 $config=$archive.GetEntry('Dvinholm.App.runtimeconfig.json')
 if(-not $config) { throw 'MSIX check 18: .NET runtimeconfig missing from package root' }
 $configStream=$config.Open()
 try {
   $configReader=New-Object IO.StreamReader($configStream)
   try {
     $runtimeConfig=$configReader.ReadToEnd() | ConvertFrom-Json -ErrorAction Stop
   } catch {
     throw 'MSIX check 19: .NET runtimeconfig invalid JSON'
   } finally { $configReader.Dispose() }
 } finally { $configStream.Dispose() }
 $framework=$runtimeConfig.runtimeOptions.framework
 if($null -eq $framework -or $framework.name -ne 'Microsoft.NETCore.App' -or
     [string]$framework.version -notmatch '^10\.[0-9]+\.[0-9]+
} finally { $archive.Dispose() }
) {
   throw 'MSIX check 19: expected .NET 10 shared runtime framework'
 }
 if($entries -ccontains 'AppxSignature.p7x') {
   throw 'MSIX check 20: signed payload in UNSIGNED test-only artifact'
 }
 Write-Host 'All Lite MSIX checks passed. This is an UNSIGNED test package.'
} finally { $archive.Dispose() }
