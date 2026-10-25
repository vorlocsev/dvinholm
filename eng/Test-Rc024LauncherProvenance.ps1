#requires -Version 5.1
<#
Eight offline negative checks on the real Windows RC 0.2.4 batch launcher.
The expected failure occurs before ZIP extraction and never launches WinUI.
Only private, disposable test directories are created.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$LauncherPath
)
$ErrorActionPreference = 'Stop'
$launcher = (Resolve-Path -LiteralPath $LauncherPath -ErrorAction Stop).Path
$root = Join-Path ([IO.Path]::GetTempPath()) (
    'dvinholm-rc024-provenance-' + [Guid]::NewGuid().ToString('N'))
$kit = Join-Path $root 'kit'
New-Item -ItemType Directory -Path $kit -Force | Out-Null
$goodSha = '0123456789abcdef0123456789abcdef01234567'
if ($goodSha.Length -ne 40) { throw 'Invalid local test SHA fixture.' }
$cases = @(
    @{ Name = '1/8 empty provenance'; Value = '' },
    @{ Name = '2/8 39 lowercase hex characters'; Value = $goodSha.Substring(0, 39) },
    @{ Name = '3/8 41 lowercase hex characters'; Value = $goodSha + '0' },
    @{ Name = '4/8 uppercase hex'; Value = $goodSha.ToUpperInvariant() },
    @{ Name = '5/8 non-hex character'; Value = $goodSha.Substring(0, 39) + 'g' },
    @{ Name = '6/8 leading whitespace'; Value = ' ' + $goodSha },
    @{ Name = '7/8 extra line after valid first SHA'; Value = $goodSha + "`r`n" + 'trailer' },
    @{ Name = '8/8 CMD metacharacter injection attempt'; Value = $goodSha + '" & echo injected>injection-probe.txt & rem "' }
)
try {
    Copy-Item -LiteralPath $launcher -Destination (Join-Path $kit 'START-ACCEPTANCE-024.cmd')
    $provenance = Join-Path $kit 'BUILD_COMMIT_024.txt'
    foreach ($case in $cases) {
        [IO.File]::WriteAllText($provenance, [string]$case.Value, [Text.Encoding]::ASCII)
        Push-Location $kit
        try {
            # Capture the expected rejection, inspect status, and immediately
            # clear $LASTEXITCODE so it cannot poison a passing CI step.
            $output = @(& cmd.exe /d /c 'START-ACCEPTANCE-024.cmd --prepare-only' 2>&1)
            $status = $LASTEXITCODE
            $global:LASTEXITCODE = 0
        }
        finally {
            Pop-Location
        }
        if ($status -eq 0) {
            throw "$($case.Name): unexpectedly accepted untrusted SHA provenance."
        }
        if (($output -join "`n") -notmatch 'Invalid exact lowercase 40-character build commit') {
            throw "$($case.Name): rejected by the wrong guard: $($output -join ' ')"
        }
        if (Test-Path -LiteralPath (Join-Path $root 'Dvinholm-024-acceptance')) {
            throw "$($case.Name): invalid provenance created an acceptance target."
        }
        if (Test-Path -LiteralPath (Join-Path $kit 'injection-probe.txt')) {
            throw "$($case.Name): untrusted CMD metacharacters executed."
        }
        Write-Host "Rejected RC 0.2.4 provenance: $($case.Name)."
    }
    Write-Host 'All eight malicious or invalid RC 0.2.4 provenance cases safely rejected; no GUI launched.'
}
finally {
    $global:LASTEXITCODE = 0
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
