#requires -Version 5.1
<#
Eight synthetic PowerShell 5.1 checks for quoted RC icon paths.
Does not run cl.exe or rc.exe or touch installed files; one temporary RC file is removed.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'NativeIconResourcePath.ps1')
$script:passed = 0

function Assert-Converted {
    param([string]$Title, [string]$InputPath, [string]$Expected)
    $actual = ConvertTo-NativeIconResourcePath -Path $InputPath
    if ($actual -cne $Expected) {
        throw "FAIL $Title. Expected [$Expected], got [$actual]."
    }
    $script:passed++
    Write-Host "PASS $Title"
}

function Assert-Rejected {
    param([AllowEmptyString()][string]$InputPath)
    $rejected = $false
    try {
        $null = ConvertTo-NativeIconResourcePath -Path $InputPath
    } catch {
        $rejected = $true
    }
    if (-not $rejected) {
        throw 'Unsafe icon resource path was accepted.'
    }
}

Assert-Converted '1. ordinary drive path' 'C:\build\assets\icon.ico' 'C:/build/assets/icon.ico'
Assert-Converted '2. path with spaces' 'C:\Program Files\Dvinholm\Dvinholm.ico' 'C:/Program Files/Dvinholm/Dvinholm.ico'
Assert-Converted '3. backslash-n, -t and -r are not RC escape sequences' 'D:\new\tools\release\icon.ico' 'D:/new/tools/release/icon.ico'
$unicode = [string][char]0x041A + [char]0x043E + [char]0x0434
Assert-Converted '4. Unicode path content is retained' ('C:\' + $unicode + '\icon.ico') ('C:/' + $unicode + '/icon.ico')
# A successful conversion is not enough: ASCII .rc files lose Cyrillic
# filename bytes even when the converted path string is still correct.
$rcProbe = [IO.Path]::GetTempFileName()
try {
    Write-NativeIconResourceFile -OutputPath $rcProbe -IconPath ('C:\' + $unicode + '\icon.ico')
    $bytes = [IO.File]::ReadAllBytes($rcProbe)
    $decoded = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    $expectedResource = "#pragma code_page(65001)`r`n1 ICON `"C:/$unicode/icon.ico`"`r`n"
    if ($decoded -cne $expectedResource) {
        throw 'Unicode icon path did not survive RC file serialization.'
    }
} finally {
    Remove-Item -LiteralPath $rcProbe -Force -ErrorAction SilentlyContinue
}

Assert-Converted '5. UNC path' '\\builder\share\icons\icon.ico' '//builder/share/icons/icon.ico'
Assert-Converted '6. existing forward slashes are unchanged' 'D:/build/icons/icon.ico' 'D:/build/icons/icon.ico'

Assert-Rejected 'C:\bad"quote\icon.ico'
$script:passed++
Write-Host 'PASS 7. quoted path is rejected'

foreach ($bad in @(
    '',
    '   ',
    ('C:\icons' + [char]10 + '\icon.ico'),
    ('C:\icons' + [char]13 + '\icon.ico')
)) {
    Assert-Rejected $bad
}
$script:passed++
Write-Host 'PASS 8. blank and multiline paths are rejected'

if ($script:passed -ne 8) {
    throw "Expected eight native RC icon path gates; observed $script:passed."
}
Write-Host 'Native RC icon resource path: 8/8 offline checks passed.'
