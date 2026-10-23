#requires -Version 5.1
<#
Read-only Windows SDK MakeAppx unpack gate for the actual experimental
UNSIGNED x86 Lite MSIX. Eight bounded checks, no installation, package
registration, code signing, certificate changes, network or UI launch.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'MSIX SDK unpack check requires Windows.'
}
$package = (Resolve-Path -LiteralPath $PackagePath -ErrorAction Stop).Path
$kitsRoot = Join-Path ([Environment]::GetEnvironmentVariable('ProgramFiles(x86)')) 'Windows Kits\10\bin'
if (-not (Test-Path -LiteralPath $kitsRoot -PathType Container)) {
    throw 'MSIX SDK unpack check 1: Windows SDK bin directory is missing.'
}
$versions = @(Get-ChildItem -LiteralPath $kitsRoot -Directory |
    Where-Object { $_.Name -match '^\d+[.]\d+[.]\d+[.]\d+$' } |
    Sort-Object { [Version]$_.Name } -Descending)
$makeAppx = $null
foreach ($version in $versions) {
    foreach ($arch in @('x86','x64')) {
        $candidate = Join-Path $version.FullName "$arch\makeappx.exe"
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $makeAppx = $candidate
            break
        }
    }
    if ($makeAppx) { break }
}
if (-not $makeAppx) {
    throw 'MSIX SDK unpack check 1: Windows SDK MakeAppx.exe was not found.'
}
Write-Host "MSIX SDK unpack check 1/8: Windows SDK MakeAppx found."
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('Dvinholm-msix-unpack-' + [Guid]::NewGuid().ToString('N'))
$destination = Join-Path $tempRoot 'payload'
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
try {
    & $makeAppx unpack /p $package /d $destination /o | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "MSIX SDK unpack check 2: MakeAppx failed with $LASTEXITCODE."
    }
    if (-not (Test-Path -LiteralPath $destination -PathType Container)) {
        throw 'MSIX SDK unpack check 2: no extracted payload directory.'
    }
    Write-Host 'MSIX SDK unpack check 2/8: actual package unpacked.'

    $manifestFile = Join-Path $destination 'AppxManifest.xml'
    if (-not (Test-Path -LiteralPath $manifestFile -PathType Leaf)) {
        throw 'MSIX SDK unpack check 3: package-root manifest is missing.'
    }
    [xml]$manifest = Get-Content -LiteralPath $manifestFile -Raw
    $ns = New-Object Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace('a', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $identity = $manifest.SelectSingleNode('/a:Package/a:Identity', $ns)
    if ($null -eq $identity -or $identity.GetAttribute('Name') -ne 'Dvinholm.Lite' -or
        $identity.GetAttribute('ProcessorArchitecture') -ne 'x86' -or
        $identity.GetAttribute('Publisher') -ne 'CN=Dvinholm Development') {
        throw 'MSIX SDK unpack check 3: unexpected x86 package identity/publisher.'
    }
    Write-Host 'MSIX SDK unpack check 3/8: manifest identity matches.'

    $exe = Join-Path $destination 'Dvinholm.App.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
        throw 'MSIX SDK unpack check 4: package-root Dvinholm.App.exe missing.'
    }
    $stream = [IO.File]::OpenRead($exe)
    $reader = New-Object IO.BinaryReader($stream)
    try {
        if ($stream.Length -lt 128) { throw 'MSIX SDK unpack check 4: EXE is too short.' }
        $stream.Position = 0x3c
        $peOffset = [int]$reader.ReadUInt32()
        if ($peOffset -le 0 -or ([long]$peOffset + 6) -gt $stream.Length) {
            throw 'MSIX SDK unpack check 4: invalid PE offset.'
        }
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550 -or $reader.ReadUInt16() -ne 0x014c) {
            throw 'MSIX SDK unpack check 4: EXE is not Windows x86 PE.'
        }
    } finally { $reader.Dispose(); $stream.Dispose() }
    Write-Host 'MSIX SDK unpack check 4/8: real application binary is x86 PE.'

    $configPath = Join-Path $destination 'Dvinholm.App.runtimeconfig.json'
    if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
        throw 'MSIX SDK unpack check 5: root runtimeconfig missing.'
    }
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -ErrorAction Stop
    $frameworks = @()
    if ($null -ne $config.runtimeOptions.framework) { $frameworks += $config.runtimeOptions.framework }
    if ($null -ne $config.runtimeOptions.frameworks) { $frameworks += @($config.runtimeOptions.frameworks) }
    if (@($frameworks | Where-Object {
        $_.name -eq 'Microsoft.NETCore.App' -and [string]$_.version -match '^10[.]\d+[.]\d+$'
    }).Count -ne 1) {
        throw 'MSIX SDK unpack check 5: no unambiguous .NET 10 Core framework.'
    }
    Write-Host 'MSIX SDK unpack check 5/8: root .NET 10 runtimeconfig exists.'

    $assets = @('MsixSquare44x44Logo.png','MsixSquare150x150Logo.png',
        'MsixWide310x150Logo.png','MsixSplash620x300.png','MsixStoreLogo.png')
    foreach ($asset in $assets) {
        $matches = @(Get-ChildItem -LiteralPath $destination -Recurse -File |
            Where-Object { $_.Name -ceq $asset })
        if ($matches.Count -ne 1) {
            throw "MSIX SDK unpack check 6: expected one extracted $asset."
        }
    }
    Write-Host 'MSIX SDK unpack check 6/8: five branded visual assets extracted.'

    $unexpected = @('coreclr.dll','hostfxr.dll','hostpolicy.dll','Microsoft.UI.Xaml.dll')
    $allFiles = @(Get-ChildItem -LiteralPath $destination -Recurse -File)
    if (@($allFiles | Where-Object {
        $_.Extension -match '^\.(pdb|dmp|binlog|trx)$' -or $_.Name -in $unexpected
    }).Count -gt 0) {
        throw 'MSIX SDK unpack check 7: debug files or bundled runtime found.'
    }
    Write-Host 'MSIX SDK unpack check 7/8: no debug files or bundled runtimes.'

    if (Test-Path -LiteralPath (Join-Path $destination 'AppxSignature.p7x') -PathType Leaf) {
        throw 'MSIX SDK unpack check 8: unexpectedly signed development artifact.'
    }
    Write-Host 'MSIX SDK unpack check 8/8: remains unsigned test-only.'
    Write-Host 'MakeAppx extracted the real MSIX; NOT INSTALL-READY: trust, dependencies, registration and GUI startup untested.'
} finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
