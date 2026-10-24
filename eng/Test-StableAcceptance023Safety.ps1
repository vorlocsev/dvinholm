#requires -Version 5.1
<#
Eight fail-closed Windows integration scenarios against the REAL staged Stable
0.2.3 ZIP. No GUI launch, installation, J2534 connection or release creation.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArchiveDirectory,
    [Parameter(Mandatory)][string]$ExpectedCommit
)
$ErrorActionPreference = 'Stop'
if ($ExpectedCommit -cnotmatch '^[0-9a-f]{40}$') {
    throw 'Use a real exact-commit SHA for the eight safety tests.'
}
$source = (Resolve-Path -LiteralPath $ArchiveDirectory -ErrorAction Stop).Path
$prepare = Join-Path $PSScriptRoot 'Prepare-StableAcceptance023.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-acceptance-safety-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -ErrorAction Stop | Out-Null
$passed = 0

function ExpectFailure([string]$label, [scriptblock]$action) {
    try {
        & $action
    }
    catch {
        if ($_.Exception.Message -like 'UNEXPECTED_SUCCESS:*') {
            throw
        }
        $script:passed++
        Write-Host "PASS $script:passed/8: $label (rejected safely)"
        return
    }
    throw "UNEXPECTED_SUCCESS: $label"
}
function AssertMissing([string]$path, [string]$label) {
    if (Test-Path -LiteralPath $path) {
        throw "$label unexpectedly created an extraction directory."
    }
}
try {
    # 1: An actual verified production ZIP extracts to a NEW directory and
    # records both the expected SHA and audit digest. It is NOT started.
    $good = Join-Path $root 'valid-extraction'
    & $prepare -ArchiveDirectory $source -ExpectedCommit $ExpectedCommit -DestinationDirectory $good
    $report = Join-Path $good 'ACCEPTANCE-0.2.3.txt'
    if (-not (Test-Path -LiteralPath (Join-Path $good 'Dvinholm.App.exe') -PathType Leaf) -or
        -not (Test-Path -LiteralPath $report -PathType Leaf)) {
        throw 'Valid candidate did not generate the extracted application and report.'
    }
    $content = [IO.File]::ReadAllText($report)
    if (-not $content.Contains($ExpectedCommit) -or
        -not $content.Contains('WinUI has not been launched')) {
        throw 'Acceptance report is missing commit identity or the manual-launch disclaimer.'
    }
    $passed++
    Write-Host "PASS $passed/8: validated actual Stable ZIP and manual-only report"

    # 2: Never replace an existing installation, even an empty-looking folder.
    $existing = Join-Path $root 'existing-install'
    New-Item -ItemType Directory -Path $existing | Out-Null
    $sentinel = Join-Path $existing 'keep-user-data.txt'
    [IO.File]::WriteAllText($sentinel, 'retain exactly')
    ExpectFailure 'pre-existing installation directory' {
        & $prepare -ArchiveDirectory $source -ExpectedCommit $ExpectedCommit -DestinationDirectory $existing
    }
    if ([IO.File]::ReadAllText($sentinel) -cne 'retain exactly') {
        throw 'Existing installation contents changed.'
    }

    # 3: Invalid SHA is refused before archive extraction.
    $badFormat = Join-Path $root 'invalid-sha'
    ExpectFailure 'invalid SHA syntax' {
        & $prepare -ArchiveDirectory $source -ExpectedCommit '1234' -DestinationDirectory $badFormat
    }
    AssertMissing $badFormat 'Invalid SHA'

    # 4: A well-formed SHA for another commit must fail metadata gate 8/8.
    $prefix = if ($ExpectedCommit[0] -eq '0') { '1' } else { '0' }
    $wrongSha = $prefix + $ExpectedCommit.Substring(1)
    $wrongDestination = Join-Path $root 'wrong-commit'
    ExpectFailure 'mismatched exact commit' {
        & $prepare -ArchiveDirectory $source -ExpectedCommit $wrongSha -DestinationDirectory $wrongDestination
    }
    AssertMissing $wrongDestination 'Wrong commit'

    # Clone ONE candidate and mutate only its checksum/extra files in later
    # cases. The original CI artifact is never modified.
    $changed = Join-Path $root 'candidate-copy'
    New-Item -ItemType Directory -Path $changed | Out-Null
    $zipName = 'Dvinholm-Windows-x86.zip'
    $sumName = 'SHA256SUMS.txt'
    Copy-Item -LiteralPath (Join-Path $source $zipName) -Destination $changed
    Copy-Item -LiteralPath (Join-Path $source $sumName) -Destination $changed
    $manifestPath = Join-Path $changed $sumName
    $originalManifest = [IO.File]::ReadAllText($manifestPath, [Text.Encoding]::ASCII)

    # 5: Reject tampered SHA manifest; never repair it silently.
    [IO.File]::WriteAllText($manifestPath,
        (('0' * 64) + '  ' + $zipName + [Environment]::NewLine), [Text.Encoding]::ASCII)
    $badDigest = Join-Path $root 'bad-digest'
    ExpectFailure 'tampered SHA-256 sidecar' {
        & $prepare -ArchiveDirectory $changed -ExpectedCommit $ExpectedCommit -DestinationDirectory $badDigest
    }
    AssertMissing $badDigest 'Digest mismatch'

    # 6: Reject missing manifest, even if the ZIP would otherwise load.
    Remove-Item -LiteralPath $manifestPath -Force
    $missingChecksum = Join-Path $root 'no-checksum'
    ExpectFailure 'missing SHA-256 sidecar' {
        & $prepare -ArchiveDirectory $changed -ExpectedCommit $ExpectedCommit -DestinationDirectory $missingChecksum
    }
    AssertMissing $missingChecksum 'Missing manifest'

    # 7: Reject extra files in the artifact root.
    [IO.File]::WriteAllText($manifestPath, $originalManifest, [Text.Encoding]::ASCII)
    $extraFile = Join-Path $changed 'unexpected.txt'
    [IO.File]::WriteAllText($extraFile, 'not allowed')
    $extras = Join-Path $root 'extra-file'
    ExpectFailure 'unexpected extra artifact file' {
        & $prepare -ArchiveDirectory $changed -ExpectedCommit $ExpectedCommit -DestinationDirectory $extras
    }
    AssertMissing $extras 'Extra file'

    # 8: Refuse to create arbitrary parent trees or install paths.
    $missingParent = Join-Path (Join-Path $root 'absent-parent') 'target'
    ExpectFailure 'missing destination parent' {
        & $prepare -ArchiveDirectory $source -ExpectedCommit $ExpectedCommit -DestinationDirectory $missingParent
    }
    AssertMissing $missingParent 'Missing parent'

    if ($passed -ne 8) { throw "Expected 8 safety gates, counted $passed." }
    Write-Host 'All eight Stable 0.2.3 acceptance safety gates passed. NO GUI OR RELEASE ACTION.'
}
finally {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Force -Recurse
    }
}
