#requires -Version 5.1
<#
Generate an evidence-only, fail-closed readiness report from the ACTUAL
three-file downloaded Stable 0.2.4 staging artifact. It NEVER approves,
signs, tags or publishes a release. Manual GUI, installed-updater and
physical-adapter checks are intentionally marked PENDING.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ArchiveDirectory,
    [Parameter(Mandatory)][string]$ExpectedCommit,
    [Parameter(Mandatory)][string]$ReportPath
)
$ErrorActionPreference = 'Stop'
if ($ExpectedCommit -cnotmatch '^[0-9a-f]{40}$') {
    throw 'Readiness gate 1/8: expected exact 40-character lowercase commit.'
}
$root = (Resolve-Path -LiteralPath $ArchiveDirectory -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw 'Readiness gate 2/8: downloaded staging source is not a directory.'
}
$target = [IO.Path]::GetFullPath($ReportPath)
if (Test-Path -LiteralPath $target) {
    throw 'Readiness gate 2/8: report already exists; refusing overwrite.'
}
$parent = Split-Path -Path $target -Parent
if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
    throw 'Readiness gate 2/8: report parent directory must exist.'
}
$names = @('Dvinholm-Windows-x86.zip', 'Dvinholm-Windows-x86-Lite.zip', 'SHA256SUMS.txt')
$files = @(Get-ChildItem -LiteralPath $root -Force)
if ($files.Count -ne 3 -or @($files | Where-Object {
    $_.PSIsContainer -or $names -cnotcontains $_.Name
}).Count -gt 0) {
    throw 'Readiness gate 3/8: expected exactly two ZIPs and one manifest, no extra entries.'
}
foreach ($name in $names) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $name) -PathType Leaf)) {
        throw "Readiness gate 3/8: missing $name."
    }
}
$manifest = [IO.File]::ReadAllText((Join-Path $root 'SHA256SUMS.txt'), [Text.Encoding]::ASCII)
$matched = [regex]::Match($manifest,
    '\A([0-9a-f]{64})  Dvinholm-Windows-x86[.]zip\r?\n([0-9a-f]{64})  Dvinholm-Windows-x86-Lite[.]zip\r?\n\z')
if (-not $matched.Success) {
    throw 'Readiness gate 4/8: manifest must contain exactly two canonical lowercase SHA-256 entries.'
}
$portable = Join-Path $root $names[0]
$lite = Join-Path $root $names[1]
$pHash = (Get-FileHash -LiteralPath $portable -Algorithm SHA256).Hash.ToLowerInvariant()
if ($pHash -cne $matched.Groups[1].Value) {
    throw 'Readiness gate 5/8: downloaded Portable ZIP fails original SHA256SUMS.'
}
$lHash = (Get-FileHash -LiteralPath $lite -Algorithm SHA256).Hash.ToLowerInvariant()
if ($lHash -cne $matched.Groups[2].Value) {
    throw 'Readiness gate 6/8: downloaded Lite ZIP fails original SHA256SUMS.'
}
$pSize = (Get-Item -LiteralPath $portable).Length
$lSize = (Get-Item -LiteralPath $lite).Length
if ($lSize -le 0 -or $pSize -le 0 -or $lSize -ge $pSize) {
    throw 'Readiness gate 7/8: expected nonzero Lite ZIP smaller than Portable.'
}

# Hold stays in place irrespective of every preceding automated success.
# No source or CI process can assert real GUI or installed-updater approval.
$manual = @(
    'Launch and relaunch isolated Portable on Windows 11 LTSC',
    'Verify window title, version and branded icon',
    'Confirm demo, test and UDS-preview controls absent',
    'Verify disconnected J2534 state and disabled live actions',
    'Save and reload CAN speed using private acceptance settings',
    'Import/export/edit ECU JSON profile and verify persistence',
    'Inspect CAN Monitor layout/filter and offline CSV export',
    'Inspect update channel/error/cancel UI without changing installed Stable'
)
if ($manual.Count -ne 8) {
    throw 'Readiness gate 8/8: expected exactly eight pending human Windows checks.'
}
$tick = [char]96
$lines = @(
    '# Dvinholm Stable 0.2.4 | PRE-RELEASE EVIDENCE, NOT PUBLISHED',
    '',
    '**Release decision: HOLD.** Human GUI acceptance and explicit user publication approval are still required.',
    '',
    "- Candidate Git commit: $tick$ExpectedCommit$tick",
    '- Candidate channel: `stable`, version: `0.2.4`',
    '- Published Stable: `v0.2.3` (unchanged)',
    '- Previous published commit: `91ecc343608c931eb033523e3db6012a6c5dbd58`',
    '',
    '## Downloaded artifact evidence',
    '',
    '| Variant | Size (bytes) | SHA-256 (verified against downloaded sidecar) |',
    '|---|---:|---|',
    "| Portable Windows x86 | $pSize | $tick$pHash$tick |",
    "| Lite Windows x86 | $lSize | $tick$lHash$tick |",
    '',
    'Both ZIPs matched the original two-line downloaded SHA256SUMS.txt. This report was generated only after previous staging steps verified their binary/assembly version and source SHA.',
    'The current workflow may still have subsequent steps; its final green conclusion must be verified independently.',
    '',
    '## Automated checks already required by this staging workflow',
    '',
    '- [x] Production-only Portable and Lite Windows x86 packaging; trimming and demo/debug content excluded',
    '- [x] Eight binary/archive checks per variant and download revalidation',
    '- [x] Published 0.2.3 ZIP to staged 0.2.4 file-replacement and byte-exact rollback simulation',
    '- [x] Private Stable 0.2.4 acceptance copy and no-overwrite rehearsal',
    '- [x] One-click `--prepare-only` CMD rehearsal on downloaded ZIPs',
    '',
    '## Mandatory human Windows 11 LTSC checks (NOT DONE BY CI)',
    ''
)
foreach ($item in $manual) {
    $lines += "- [ ] $item"
}
$lines += @(
    '',
    '## Remaining release blockers',
    '',
    '- [ ] Confirm final GitHub Actions run status is successful for the exact eventual release commit',
    '- [ ] Complete eight interactive Windows 11 LTSC acceptance checks using an isolated copy',
    '- [ ] Manually test the installed application updater and actual user-data preservation',
    '- [ ] Obtain explicit user authorization to publish Stable 0.2.4',
    '',
    'Physical J2534/vehicle tests remain **deferred to a later release**; no hardware success is claimed.',
    'Lite host-prerequisite checks in CI use a simulated host; real Lite installation remains unverified.',
    '',
    '**NO GO for publication. No tag, GitHub Release, update to installed files, or signing was performed by this report.**'
)
$report = ($lines -join [Environment]::NewLine) + [Environment]::NewLine
if ($report -notmatch 'Release decision: HOLD' -or
    @([regex]::Matches($report, '(?m)^- \[ \] (?:Launch|Verify|Confirm demo|Save|Import|Inspect)')).Count -ne 8 -or
    $report -notmatch 'NO GO for publication') {
    throw 'Readiness gate 8/8: safety hold or eight manual pending checks missing.'
}
[IO.File]::WriteAllText($target, $report, [Text.UTF8Encoding]::new($false))
Write-Host "Eight readiness evidence gates passed. HOLD report generated at $target"
Write-Host 'No Stable 0.2.4 release published; Windows GUI and physical J2534 not tested.'
