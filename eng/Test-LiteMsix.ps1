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
 Write-Host 'All eight Lite MSIX checks passed. This is an UNSIGNED test package.'
} finally { $archive.Dispose() }
