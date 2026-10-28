#requires -Version 5.1
<#
  Validate source-matched, ZIP-root Lite prerequisite scripts.
  The inspector itself is read-only; this validator never launches WinUI.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Portable','Lite')][string]$Flavor,
    [Parameter(Mandatory)][string]$ZipPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
if (-not ('System.IO.Compression.ZipFile' -as [type])) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
}
$zip = [IO.Compression.ZipFile]::OpenRead(
    (Resolve-Path -LiteralPath $ZipPath -ErrorAction Stop).Path)
try {
    $bundled = @(
        @{ Name = 'Check-Lite-Prerequisites.cmd'; Repo = 'Check-Lite-Prerequisites.cmd' },
        @{ Name = 'Check-Lite-Prerequisites.ps1'; Repo = 'Test-LitePrerequisites.ps1' },
        @{ Name = 'Manage-Lite-Dependencies.ps1'; Repo = 'Manage-Lite-Dependencies.ps1' },
        @{ Name = 'Start-Dvinholm-Lite.cmd'; Repo = 'Start-Dvinholm-Lite.cmd' }
    )
    foreach ($tool in $bundled) {
        $entry = $zip.GetEntry($tool.Name)
        if ($Flavor -eq 'Portable') {
            if ($null -ne $entry) {
                throw "Portable unexpectedly ships Lite checker: $($tool.Name)"
            }
            continue
        }
        if ($null -eq $entry) {
            throw "Lite ZIP is missing root prerequisite checker: $($tool.Name)"
        }
        $path = Join-Path $PSScriptRoot $tool.Repo
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing checked-in Lite checker source: $($tool.Repo)"
        }
        $expected = [IO.File]::ReadAllBytes($path)
        $stream = $entry.Open()
        $memory = [IO.MemoryStream]::new()
        try {
            $stream.CopyTo($memory)
            $actual = $memory.ToArray()
        }
        finally {
            $stream.Dispose()
            $memory.Dispose()
        }
        if ($expected.Length -ne $actual.Length) {
            throw "Lite checker does not match repository source: $($tool.Name)"
        }
        for ($i = 0; $i -lt $expected.Length; $i++) {
            if ($expected[$i] -ne $actual[$i]) {
                throw "Lite checker does not match repository source: $($tool.Name)"
            }
        }
    }
    if ($Flavor -eq 'Portable') {
        Write-Host 'Verified Portable excludes Lite checker.'
    } else {
        Write-Host 'Verified bundled Lite checker matches repository source.'
    }
}
finally {
    $zip.Dispose()
}
