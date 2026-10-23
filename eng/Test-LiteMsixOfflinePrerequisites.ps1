#requires -Version 5.1
<#
Read-only inspection of declared framework dependencies for the experimental
unsigned Lite MSIX on a Windows host without relying on Microsoft Store.
This does not check signature trust, package installability or WinUI startup.
Both fixture files together activate an explicitly simulated CI-only host.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [string]$RuntimeListFile,
    [string]$FrameworkPackageListFile
)
$ErrorActionPreference = 'Stop'
$hasRuntimeFixture = -not [string]::IsNullOrWhiteSpace($RuntimeListFile)
$hasPackageFixture = -not [string]::IsNullOrWhiteSpace($FrameworkPackageListFile)
if ($hasRuntimeFixture -ne $hasPackageFixture) {
    throw 'Provide both fixture files, or neither for a real Windows host.'
}
$simulated = $hasRuntimeFixture
Add-Type -AssemblyName System.IO.Compression.FileSystem
$path = (Resolve-Path -LiteralPath $PackagePath -ErrorAction Stop).Path
$archive = [IO.Compression.ZipFile]::OpenRead($path)
try {
    $manifestEntry = $archive.GetEntry('AppxManifest.xml')
    $configEntry = $archive.GetEntry('Dvinholm.App.runtimeconfig.json')
    if (-not $manifestEntry -or -not $configEntry) {
        throw 'Lite MSIX is missing package-root manifest or .NET runtimeconfig.'
    }
    $manifestReader = New-Object IO.StreamReader($manifestEntry.Open())
    try { [xml]$manifest = $manifestReader.ReadToEnd() }
    finally { $manifestReader.Dispose() }
    $configReader = New-Object IO.StreamReader($configEntry.Open())
    try { $config = $configReader.ReadToEnd() | ConvertFrom-Json -ErrorAction Stop }
    finally { $configReader.Dispose() }
} finally { $archive.Dispose() }
$ns = New-Object Xml.XmlNamespaceManager($manifest.NameTable)
$ns.AddNamespace('a', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$identity = $manifest.SelectSingleNode('/a:Package/a:Identity', $ns)
$dependency = @($manifest.SelectNodes('/a:Package/a:Dependencies/a:PackageDependency', $ns))
if (-not $identity -or $identity.GetAttribute('Name') -ne 'Dvinholm.Lite' -or
    $identity.GetAttribute('ProcessorArchitecture') -ne 'x86') {
    throw 'Package identity is not Dvinholm Lite x86.'
}
if ($dependency.Count -ne 1 -or
    $dependency[0].GetAttribute('Name') -ne 'Microsoft.WindowsAppRuntime.2') {
    throw 'Lite MSIX must declare one Microsoft.WindowsAppRuntime.2 dependency.'
}
$expectedPublisher = 'CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US'
if ($dependency[0].GetAttribute('Publisher') -ne $expectedPublisher) {
    throw 'Lite MSIX Windows App SDK publisher does not match Microsoft.'
}
try { $sdkMinimum = [Version]$dependency[0].GetAttribute('MinVersion') }
catch { throw 'Invalid Windows App SDK package dependency version.' }
if ($sdkMinimum -lt [Version]'2.5.1.0') {
    throw 'Windows App SDK package dependency is older than 2.5.1.0.'
}
$runtime = $config.runtimeOptions
# Verify every declaration; do not silently accept an unrelated framework
# or apply the NETCore minimum to an independently pinned Desktop reference.
if (-not $runtime) { throw 'Lite MSIX runtimeconfig lacks the expected Microsoft.NETCore.App framework.' }
$references = @()
if ($null -ne $runtime.framework) { $references += $runtime.framework }
if ($null -ne $runtime.frameworks) { $references += @($runtime.frameworks) }
if ($references.Count -eq 0) {
    throw 'Lite MSIX runtimeconfig lacks the expected Microsoft.NETCore.App framework.'
}
$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$requiredMinimum = @{}
foreach ($reference in $references) {
    $name = [string]$reference.name
    if ($name -notin @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App')) {
        throw "Lite MSIX runtimeconfig contains unsupported framework: $name."
    }
    if (-not $seen.Add($name)) {
        throw "Lite MSIX runtimeconfig has duplicate framework: $name."
    }
    $versionText = [string]$reference.version
    if ($versionText -notmatch '^10[.][0-9]+[.][0-9]+$') {
        throw "Lite MSIX does not request a valid .NET 10 runtime version for $name."
    }
    $policy = if ($null -ne $reference.rollForward) { [string]$reference.rollForward } elseif ($null -ne $runtime.rollForward) { [string]$runtime.rollForward } else { 'Minor' }
    if ($policy -ne 'Minor' -or $runtime.applyPatches -eq $false -or
        $reference.applyPatches -eq $false) {
        throw "Offline inspector supports only default .NET Minor roll-forward with patch servicing for $name."
    }
    $requiredMinimum[$name] = [Version]$versionText
}
if (-not $requiredMinimum.ContainsKey('Microsoft.NETCore.App')) {
    throw 'Lite MSIX runtimeconfig lacks the expected Microsoft.NETCore.App framework.'
}
# WindowsDesktop.App is mandatory for WinUI even when .NET lists only Core
# in runtimeconfig: use the Core minimum as the conservative fallback.
if (-not $requiredMinimum.ContainsKey('Microsoft.WindowsDesktop.App')) {
    $requiredMinimum['Microsoft.WindowsDesktop.App'] = $requiredMinimum['Microsoft.NETCore.App']
}
if ($simulated) {
    Write-Host 'SIMULATED INPUT: fixture results do not validate this Windows machine.'
    $runtimeLines = @(Get-Content -LiteralPath $RuntimeListFile -ErrorAction Stop)
    $packageLines = @(Get-Content -LiteralPath $FrameworkPackageListFile -ErrorAction Stop)
} else {
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
        throw 'Real offline prerequisite inspection requires Windows.'
    }
    $programFilesX86 = if ([Environment]::Is64BitOperatingSystem) {
        [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    } else {
        [Environment]::GetEnvironmentVariable('ProgramFiles')
    }
    if ([string]::IsNullOrWhiteSpace($programFilesX86)) {
        throw 'Cannot locate Windows x86 Program Files.'
    }
    $dotnetExe = Join-Path $programFilesX86 'dotnet\dotnet.exe'
    if (-not (Test-Path -LiteralPath $dotnetExe -PathType Leaf)) {
        throw 'Missing x86 dotnet host; install official .NET 10 Desktop Runtime x86.'
    }
    $runtimeLines = @(& $dotnetExe --list-runtimes 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate x86 .NET runtimes.' }
    $packageLines = @(
        Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.2' -ErrorAction Stop |
            ForEach-Object { "$($_.Name)|$($_.Architecture)|$($_.Version)|$($_.Publisher)" }
    )
}
$installedRuntimes = @()
foreach ($line in $runtimeLines) {
    if ([string]$line -match '^\s*([A-Za-z0-9.]+)\s+([0-9]+[.][0-9]+[.][0-9]+)\s+\[.+\]\s*$') {
        $installedRuntimes += [pscustomobject]@{
            Name = $Matches[1]
            Version = [Version]$Matches[2]
        }
    }
}
foreach ($frameworkName in @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App')) {
    $minimumNet = $requiredMinimum[$frameworkName]
    $available = @($installedRuntimes | Where-Object {
        $_.Name -eq $frameworkName -and $_.Version.Major -eq 10
    })
    $requestedMinorPresent = @($available | Where-Object {
        $_.Version.Minor -eq $minimumNet.Minor
    }).Count -gt 0
    $qualifying = @($available | Where-Object {
        $_.Version -ge $minimumNet -and
        (-not $requestedMinorPresent -or $_.Version.Minor -eq $minimumNet.Minor)
    })
    if ($qualifying.Count -eq 0) {
        throw "Missing compatible x86 .NET runtime $frameworkName version $minimumNet or newer under Minor policy."
    }
}
$registeredFrameworks = @()
foreach ($line in $packageLines) {
    $fields = ([string]$line).Trim() -split '\|', 4
    if ($fields.Count -ne 4) { continue }
    try { $version = [Version]$fields[2] }
    catch { continue }
    $registeredFrameworks += [pscustomobject]@{
        Name = $fields[0].Trim()
        Architecture = $fields[1].Trim()
        Version = $version
        Publisher = $fields[3].Trim()
    }
}
$compatibleFramework = @($registeredFrameworks | Where-Object {
    $_.Name -eq 'Microsoft.WindowsAppRuntime.2' -and
    $_.Architecture -eq 'X86' -and
    $_.Version -ge $sdkMinimum -and
    $_.Publisher -eq $expectedPublisher
})
if ($compatibleFramework.Count -eq 0) {
    throw "Missing current-user x86 Microsoft.WindowsAppRuntime.2 framework package at least $sdkMinimum with expected publisher."
}
Write-Host 'Lite MSIX x86 .NET 10 and Windows App SDK Framework prerequisites found.'
Write-Host 'NOT INSTALL-READY: MSIX signature trust, other Windows dependencies, Store-less staging, installation and WinUI launch are NOT verified.'
