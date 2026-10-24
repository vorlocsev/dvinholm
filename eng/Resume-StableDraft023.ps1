#requires -Version 5.1
<#
Resume the original v0.2.3 unpublished draft. Fail closed on any mismatch.
Without -Publish this verifies the two draft assets read-only.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$ExpectedCommit,
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
$approved = '91ecc343608c931eb033523e3db6012a6c5dbd58'
if ($Repository -cne 'vorlocsev/dvinholm' -or $ExpectedCommit -cne $approved) {
    throw 'Only the originally approved v0.2.3 candidate may be resumed.'
}
if ([string]::IsNullOrEmpty($env:GH_TOKEN)) { throw 'GH_TOKEN required.' }
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Test-ReleaseCandidate023.ps1') -PathType Leaf)) {
    throw 'Original Stable verifier is missing.'
}
foreach ($runId in @(40082040984, 40082040981)) {
    $runJson = & gh api "repos/$Repository/actions/runs/$runId"
    if ($LASTEXITCODE -ne 0) { throw "Cannot authenticate CI run $runId." }
    $run = $runJson | ConvertFrom-Json
    if ($run.head_sha -cne $approved -or $run.conclusion -cne 'success' -or $run.status -cne 'completed') {
        throw "Approved candidate CI run $runId is not green on the exact commit."
    }
}
Write-Host 'Verified original CI #3347 and Stable staging #18.'
# Unlike GET release-by-tag, authenticated LIST includes unpublished drafts.
$listJson = & gh api "repos/$Repository/releases?per_page=100"
if ($LASTEXITCODE -ne 0) { throw 'Cannot list releases.' }
$matches = @($listJson | ConvertFrom-Json | Where-Object { $_.tag_name -ceq 'v0.2.3' })
if ($matches.Count -ne 1) { throw "Expected one v0.2.3 draft; found $($matches.Count)." }
$draft = $matches[0]
if (-not $draft.draft -or $draft.prerelease -or $draft.target_commitish -cne $approved -or $draft.name -cne 'Dvinholm 0.2.3') {
    throw 'Draft metadata does not match original approval.'
}
$expectedNames = @('Dvinholm-Windows-x86.zip', 'SHA256SUMS.txt')
$assets = @($draft.assets)
if ($assets.Count -ne 2 -or
    @($assets | Where-Object { $expectedNames -cnotcontains $_.name -or $_.state -cne 'uploaded' -or $_.digest -cnotmatch '^sha256:[0-9a-f]{64}$' }).Count -ne 0 -or
    @($assets | Select-Object -ExpandProperty name -Unique).Count -ne 2) {
    throw 'Draft must contain exactly the two fully uploaded SHA256-addressed assets.'
}
Write-Host "Verified existing unpublished draft $($draft.id)."
$root = Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-023-draft-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
try {
    foreach ($asset in $assets) {
        $dest = Join-Path $root $asset.name
        $url = "https://api.github.com/repos/$Repository/releases/assets/$($asset.id)"
        & curl.exe --fail --location --silent --show-error --header 'Accept: application/octet-stream' --header "Authorization: Bearer $env:GH_TOKEN" --output $dest $url
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $dest -PathType Leaf)) {
            throw "Cannot download draft asset $($asset.name)."
        }
        $actual = (Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash.ToLowerInvariant()
        if ("sha256:$actual" -cne $asset.digest) {
            throw "Draft asset $($asset.name) differs from GitHub SHA-256 digest."
        }
    }
    Write-Host 'Both draft assets match independent GitHub SHA-256 digests.'
    & (Join-Path $PSScriptRoot 'Test-ReleaseCandidate023.ps1') -ArchiveDirectory $root -ExpectedCommit $approved -Channel stable
    if (-not $?) { throw 'Candidate verifier rejected the downloaded draft assets.' }
    Write-Host 'Eight Stable binary/archive gates passed on downloaded draft bytes.'
    if (-not $Publish) {
        Write-Host 'Read-only verification succeeded. Draft remains unpublished.'
        return
    }
    $updatedJson = & gh api --method PATCH -F draft=false "repos/$Repository/releases/$($draft.id)"
    if ($LASTEXITCODE -ne 0) { throw 'GitHub rejected draft publication.' }
    $updated = $updatedJson | ConvertFrom-Json
    if ($updated.draft -or $updated.prerelease -or $updated.tag_name -cne 'v0.2.3' -or $updated.target_commitish -cne $approved -or $updated.id -ne $draft.id) {
        throw 'Unexpected metadata after publication.'
    }
    $liveJson = & gh api "repos/$Repository/releases/$($draft.id)"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot audit published release.' }
    $live = $liveJson | ConvertFrom-Json
    if ($live.draft -or $live.prerelease -or $live.tag_name -cne 'v0.2.3' -or $live.target_commitish -cne $approved -or @($live.assets).Count -ne 2) {
        throw 'Published release metadata audit failed.'
    }
    foreach ($asset in $assets) {
        $matching = @($live.assets | Where-Object { $_.name -ceq $asset.name })
        if ($matching.Count -ne 1 -or $matching[0].digest -cne $asset.digest) {
            throw 'Published asset SHA-256 digest differs from approved draft.'
        }
    }
    Write-Host 'Stable v0.2.3 published and independently verified.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
