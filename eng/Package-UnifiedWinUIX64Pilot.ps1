#requires -Version 5.1
<#
Offline PR-only packaging gate for the new unified WinUI 3 / .NET 10 x64 app.
Never installs, launches, publishes a release, or accesses an ECU.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$ZipPath
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$source = (Resolve-Path -LiteralPath $PublishDirectory -ErrorAction Stop).Path
$destination = [IO.Path]::GetFullPath($ZipPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
$checks = 0
$exe = Join-Path $source 'Dvinholm.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw 'Unified x64 publish must contain the single user-facing Dvinholm.exe.'
}
$checks++
$bytes = [IO.File]::ReadAllBytes($exe)
if ($bytes.Length -lt 1024 -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) {
    throw 'Unified executable is not a valid PE file.'
}
$peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
if ($peOffset -lt 64 -or $peOffset + 6 -gt $bytes.Length -or
    [BitConverter]::ToUInt16($bytes, $peOffset + 4) -ne 0x8664) {
    throw 'Unified application is not Windows x64 (PE machine AMD64).'
}
$checks++
$runtime = @(Get-ChildItem -LiteralPath $source -Recurse -File -Filter 'coreclr.dll')
if ($runtime.Count -eq 0) { throw 'Self-contained .NET runtime coreclr.dll missing.' }
$checks++
$hostfxr = @(Get-ChildItem -LiteralPath $source -Recurse -File -Filter 'hostfxr.dll')
if ($hostfxr.Count -eq 0) { throw 'Self-contained .NET hostfxr.dll missing.' }
$checks++
$winui = @(Get-ChildItem -LiteralPath $source -Recurse -File -Filter 'Microsoft.UI.Xaml.dll')
if ($winui.Count -eq 0) { throw 'Self-contained WinUI 3 Microsoft.UI.Xaml.dll missing.' }
$checks++
if (-not(Test-Path -LiteralPath (Join-Path $source 'Assets\Dvinholm.ico') -PathType Leaf)) {
    throw 'Approved Dvinholm application icon missing from publish.'
}
$checks++
# Do not copy an independent launcher or updater GUI into the unified package.
foreach ($forbidden in @('Launcher.exe', 'Dvinholm.Wpf.exe', 'Dvinholm.UpdateHandoffProbe.exe', 'Updater-Banner.ps1')) {
    if (@(Get-ChildItem -LiteralPath $source -Recurse -File -Filter $forbidden).Count -gt 0) {
        throw ('Unified x64 package accidentally contains '+$forbidden)
    }
}
$checks++
$temp = Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-unified-x64-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temp) | Out-Null
try {
    Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $temp -Recurse -Force
    # Symbols and diagnostics are not shipping runtime dependencies.
    foreach ($file in @(Get-ChildItem -LiteralPath $temp -Recurse -File -Force)) {
        if ($file.Extension.ToLowerInvariant() -in @('.pdb','.dbg','.ilk','.dmp','.mdmp','.trx','.binlog')) {
            Remove-Item -LiteralPath $file.FullName -Force
        }
    }
    if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Force }
    [IO.Compression.ZipFile]::CreateFromDirectory(
        $temp, $destination, [IO.Compression.CompressionLevel]::Optimal, $false)
    $archive = [IO.Compression.ZipFile]::OpenRead($destination)
    try {
        $diskFiles = @(Get-ChildItem -LiteralPath $temp -Recurse -File)
        $zipFiles = @($archive.Entries | Where-Object { -not $_.FullName.EndsWith('/') })
        if ($diskFiles.Count -ne $zipFiles.Count -or $null -eq $archive.GetEntry('Dvinholm.exe')) {
            throw 'Unified ZIP is missing or added unexpected files.'
        }
        $rootLength = $temp.TrimEnd('\').Length + 1
        foreach ($diskFile in $diskFiles) {
            $name = $diskFile.FullName.Substring($rootLength).Replace('\','/')
            $entry = $archive.GetEntry($name)
            if ($null -eq $entry -or $entry.Length -ne $diskFile.Length) {
                throw ('ZIP file metadata mismatch: '+$name)
            }
            $input = $entry.Open()
            $hash = [Security.Cryptography.SHA256]::Create()
            try { $zipDigest = [Convert]::ToBase64String($hash.ComputeHash($input)) }
            finally { $input.Dispose(); $hash.Dispose() }
            $sourceDigest = (Get-FileHash -LiteralPath $diskFile.FullName -Algorithm SHA256).Hash
            $zipHex = [BitConverter]::ToString([Convert]::FromBase64String($zipDigest)).Replace('-','')
            if ($zipHex -cne $sourceDigest) {
                throw ('ZIP SHA-256 mismatch: '+$name)
            }
        }
    } finally { $archive.Dispose() }
    $checks++
    if ($checks -ne 8) { throw 'Unexpected number of unified x64 checks.' }
    Write-Host 'Unified Dvinholm WinUI 3 self-contained x64: 8/8 offline package gates passed.'
    Write-Host ('Package bytes: '+(Get-Item -LiteralPath $destination).Length)
} finally {
    if (Test-Path -LiteralPath $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force
    }
}
