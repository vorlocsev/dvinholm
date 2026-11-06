#Requires -Version 5.1
<#
.SYNOPSIS
    Create a PRIVATE, local RU/EN Aurora screenshot comparison sheet.
.NOTES
    Run only after collecting real screenshots on an interactive Windows desktop.
    Input: CaptureRoot\ru\Aurora-ru-capture-manifest.json and
           CaptureRoot\en\Aurora-en-capture-manifest.json.
    The HTML references the existing local PNGs. No image is copied, embedded,
    uploaded, scored or published. Missing captures are visibly NOT RUN.
.EXAMPLE
    powershell.exe -NoProfile -File eng/Review-UnifiedAuroraScreenshots.ps1 -CaptureRoot C:\AuroraReview -IConfirmPrivateOfflineReview
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$CaptureRoot,
    [Parameter(Mandatory=$true)]
    [switch]$IConfirmPrivateOfflineReview
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IConfirmPrivateOfflineReview) {
    throw 'Confirm the screenshots contain no secrets and the report stays private.'
}
if ($env:CI -eq 'true' -or $env:GITHUB_ACTIONS -eq 'true') {
    throw 'No screenshot review report can be created by GitHub Actions.'
}
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or
    $PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run with Windows PowerShell 5.1 on the review desktop.'
}
$root = [IO.Path]::GetFullPath($CaptureRoot).TrimEnd('\','/')
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\','/')
if ($root.Equals($repoRoot,[StringComparison]::OrdinalIgnoreCase) -or
    $root.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The review directory must not be inside the Git repository.'
}
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw 'The CaptureRoot directory must already exist.'
}
$output = Join-Path $root 'Aurora-local-review.html'
if (Test-Path -LiteralPath $output) {
    throw 'The report already exists. Review it before deliberately removing it and rerunning.'
}
$pages = @('Connection','Updates','CemPin','CemConfiguration','Dtc','Ecu')
$fixedWidthDip = 1280
$fixedHeightDip = 800
$bottomPages = @('CemPin','CemConfiguration','Dtc','Ecu')
$widths = @($fixedWidthDip)
# Trust neither the filename nor a merely nonempty title string. These must
# match the actual six page titles assigned by the WinUI workspace presenter.
$expectedTitles = @{
    ru = @{
        Connection='Подключение'; Updates='Обновление'; CemPin='CEM PIN'
        CemConfiguration='Конфигурация CEM'; Dtc='Ошибки (DTC)'; Ecu='Блоки ECU'
    }
    en = @{
        Connection='Connection'; Updates='Updates'; CemPin='CEM PIN'
        CemConfiguration='CEM configuration'; Dtc='Faults (DTC)'; Ecu='Modules (ECU)'
    }
}
$manifestData = @{}
$manifestDpi = @{}
$topEntries = @{}
$topFoldEvidence = @{}
$captureSessions = @{}
$declaredSha = ''
$hasMissingDeclaration = $false
foreach ($lang in @('ru','en')) {
    $directory = Join-Path $root $lang
    $manifestPath = Join-Path $directory ("Aurora-{0}-capture-manifest.json" -f $lang)
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "The $lang capture manifest is missing. Generate each language separately."
    }
    # Windows PowerShell 5.1 emits a JSON array from ConvertFrom-Json as one
    # pipeline object. A direct @(... | ConvertFrom-Json) therefore nests all
    # screenshot records in one System.Object[], and [int]$entry.requestedWidthDip
    # fails. First receive the decoded array, then enumerate its records.
    $decoded = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $decoded -or $decoded -isnot [System.Array]) {
        throw "The $lang capture manifest must contain a JSON array of screenshot records."
    }
    $rows = @($decoded)
    if ($rows.Count -gt 6) { throw 'Unexpected extra records in fixed-viewport screenshot manifest.' }
    $seen = @{}
    $entries = @{}
    $topFolds = @{}
    $dpis = @{}
    $observedDpi = $null
    $observedSession = ''
    $hasLegacySession = $false
    foreach ($entry in $rows) {
        if ($null -eq $entry -or $entry -isnot [pscustomobject]) {
            throw "The $lang capture manifest contains a non-record JSON value."
        }
        $page = [string]$entry.page
        $width = [int]$entry.requestedWidthDip
        if ($pages -cnotcontains $page -or $widths -notcontains $width -or
            [int]$entry.requestedHeightDip -ne $fixedHeightDip) {
            throw "Unexpected page or fixed viewport dimensions in $lang manifest."
        }
        $key = "$page/$width"
        if ($seen.ContainsKey($key)) { throw "Duplicate $lang screenshot entry: $key" }
        $seen[$key] = $true
        $entries[$key] = $entry
        # Previous captures may have valid pixels without the newer outer
        # top-fold UIA proof. Do not infer scroll position from the filename.
        $topProof = $entry.PSObject.Properties['verifiedOuterScrollPercentAtMost2']
        if ($null -ne $topProof -and $topProof.Value -isnot [bool]) {
            throw "Invalid boolean outer top-fold evidence: $lang $key."
        }
        $topFoldVerified = ($null -ne $topProof -and $topProof.Value -eq $true)
        if ($topFoldVerified -and $bottomPages -cnotcontains $page) {
            throw "Unexpected top-fold scroll proof for non-long page: $lang $key."
        }
        $topFolds[$key] = $topFoldVerified
        # Old manifests without the new per-run token remain readable, but
        # cannot be represented as having verified capture-session continuity.
        $sessionProperty = $entry.PSObject.Properties['captureSessionId']
        $sessionId = if ($null -eq $sessionProperty) { '' } else { [string]$sessionProperty.Value }
        if ($sessionId -and $sessionId -cnotmatch '^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$') {
            throw "Invalid local capture session ID: $lang $key."
        }
        if ($sessionId) {
            if ($observedSession -and $observedSession -cne $sessionId) {
                throw "Mixed capture-session IDs within $lang top pages."
            }
            $observedSession = $sessionId
        } else {
            $hasLegacySession = $true
        }
        if ([string]$entry.operatorSelectedLanguage -cne $lang -or
            [string]$entry.verifiedSelectedLanguage -cne $(if($lang -eq 'ru'){'Русский'}else{'English'})) {
            throw "Unverified language label for $lang $key."
        }
        $expectedTitle = [string]$expectedTitles[$lang][$page]
        if ([string]::IsNullOrWhiteSpace([string]$entry.verifiedVisibleWorkspaceTitle) -or
            [string]$entry.verifiedVisibleWorkspaceTitle -cne $expectedTitle) {
            throw "Page identity is not verified for $lang $key. Expected the exact localized workspace title."
        }
        $name = "Aurora-$page-$lang-$width-dip.png"
        if ([string]$entry.file -cne $name) {
            throw "Screenshot filename differs from the expected safe name: $key."
        }
        $png = Join-Path $directory $name
        if (-not (Test-Path -LiteralPath $png -PathType Leaf)) {
            throw "The $lang $key manifest refers to a missing PNG."
        }
        $info = Get-Item -LiteralPath $png
        if ($info.Length -lt 24 -or $info.Length -gt 30000000) {
            throw "PNG file size is outside safe review bounds: $lang $key."
        }
        $buffer = New-Object byte[] 24
        $stream = [IO.File]::OpenRead($png)
        try {
            if ($stream.Read($buffer,0,24) -ne 24) { throw 'Unreadable PNG header.' }
        }
        finally { $stream.Dispose() }
        $signature = @(137,80,78,71,13,10,26,10)
        for ($i=0; $i -lt 8; $i++) {
            if ($buffer[$i] -ne $signature[$i]) { throw "Invalid PNG signature: $lang $key." }
        }
        if ($buffer[12] -ne 73 -or $buffer[13] -ne 72 -or
            $buffer[14] -ne 68 -or $buffer[15] -ne 82) {
            throw "Missing PNG IHDR chunk: $lang $key."
        }
        $pixelWidth = [int64]$buffer[16]*16777216 + [int64]$buffer[17]*65536 +
            [int64]$buffer[18]*256 + [int64]$buffer[19]
        $pixelHeight = [int64]$buffer[20]*16777216 + [int64]$buffer[21]*65536 +
            [int64]$buffer[22]*256 + [int64]$buffer[23]
        if ($pixelWidth -ne [int]$entry.actualWidthPx -or
            $pixelHeight -ne [int]$entry.actualHeightPx -or
            [int]$entry.actualClientWidthPx -lt 100 -or
            [int]$entry.actualClientWidthPx -gt [int]$entry.actualWidthPx -or
            [int]$entry.actualClientHeightPx -lt 100 -or
            [int]$entry.actualClientHeightPx -gt [int]$entry.actualHeightPx -or
            [int]$entry.windowDpi -lt 96 -or [int]$entry.windowDpi -gt 480 -or
            [Math]::Abs([double]$entry.measuredWidthDip - $width) -gt 5 -or
            [Math]::Abs([double]$entry.measuredClientHeightDip - $fixedHeightDip) -gt 5 -or
            [Math]::Abs([double]$entry.measuredWidthDip -
                ([int]$entry.actualClientWidthPx * 96.0 / [int]$entry.windowDpi)) -gt 1 -or
            [Math]::Abs([double]$entry.measuredClientHeightDip -
                ([int]$entry.actualClientHeightPx * 96.0 / [int]$entry.windowDpi)) -gt 1) {
            throw "PNG dimensions do not match the captured window manifest: $lang $key."
        }
        # A locale pass is one monitor-DPI session, not a silently mixed
        # collection of 100%, 125% and 150% captures.
        $dpi = [int]$entry.windowDpi
        if ($null -ne $observedDpi -and $observedDpi -ne $dpi) {
            throw "Mixed window DPI within $lang captures. Use a separate capture root per scale."
        }
        $observedDpi = $dpi
        $dpis[$key] = $dpi
        $sha = [string]$entry.sourceCommitOperatorDeclared
        if ($sha -and $sha -cnotmatch '^[0-9a-fA-F]{40}$') {
            throw "Invalid operator-declared commit SHA: $lang $key."
        }
        if ($sha) {
            if ($declaredSha -and $declaredSha -cne $sha) {
                throw 'RU and EN captures refer to different operator-declared commits.'
            }
            $declaredSha = $sha
        } else {
            $hasMissingDeclaration = $true
        }
    }
    $manifestData[$lang] = $seen
    $manifestDpi[$lang] = $dpis
    $topEntries[$lang] = $entries
    $topFoldEvidence[$lang] = $topFolds
    if ($observedSession -and $hasLegacySession) {
        throw "New and legacy capture-session evidence mixed within $lang top pages."
    }
    $captureSessions[$lang] = $observedSession
}
# Never show RU/EN captures side-by-side as directly comparable if their
# measured physical DPI differs. A separate review root is required per scale.
foreach ($page in $pages) {
    foreach ($width in $widths) {
        $key = "$page/$width"
        if ($manifestDpi['ru'].ContainsKey($key) -and
            $manifestDpi['en'].ContainsKey($key) -and
            $manifestDpi['ru'][$key] -ne $manifestDpi['en'][$key]) {
            throw "RU/EN window DPI mismatch for $key. Capture both languages at the same scale."
        }
    }
}
if ($declaredSha -and $hasMissingDeclaration) {
    throw 'Some screenshots lack the operator-declared commit while others declare it.'
}

# The capture tool records bottom folds separately; never infer their presence
# from top PNGs or from the operator's scroll checkbox. Both languages may
# independently omit bottom evidence, which the HTML must label NOT RUN.
$bottomData = @{}
foreach ($lang in @('ru','en')) {
    $seenBottom = @{}
    $directory = Join-Path $root $lang
    $bottomManifestPath = Join-Path $directory ("Aurora-{0}-bottom-capture-manifest.json" -f $lang)
    if (Test-Path -LiteralPath $bottomManifestPath -PathType Leaf) {
        $decodedBottom = Get-Content -LiteralPath $bottomManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -eq $decodedBottom -or $decodedBottom -isnot [System.Array]) {
            throw "The $lang bottom capture manifest must be a JSON array."
        }
        $bottomRows = @($decodedBottom)
        if ($bottomRows.Count -gt 4) {
            throw "Unexpected extra bottom records for $lang."
        }
        foreach ($bottomEntry in $bottomRows) {
            if ($null -eq $bottomEntry -or $bottomEntry -isnot [pscustomobject]) {
                throw "Non-record bottom capture evidence for $lang."
            }
            $page = [string]$bottomEntry.page
            $key = "$page/$fixedWidthDip"
            if ($bottomPages -cnotcontains $page) {
                throw "Unexpected bottom page for $lang."
            }
            if ($seenBottom.ContainsKey($key)) {
                throw "Duplicate bottom screenshot: $lang $key."
            }
            if (-not $topEntries[$lang].ContainsKey($key)) {
                throw "Bottom screenshot has no verified matching top page: $lang $key."
            }
            $topEntry = $topEntries[$lang][$key]
            if ([string]$bottomEntry.operatorSelectedLanguage -cne $lang -or
                [string]$bottomEntry.verifiedVisibleWorkspaceTitle -cne
                    [string]$expectedTitles[$lang][$page] -or
                [string]$bottomEntry.scrollPosition -cne 'bottom' -or
                $bottomEntry.verifiedOuterScrollPercentAtLeast98 -isnot [bool] -or
                -not $bottomEntry.verifiedOuterScrollPercentAtLeast98) {
                throw "Bottom fold is not verified by capture UI Automation: $lang $key."
            }
            $bottomSessionProperty = $bottomEntry.PSObject.Properties['captureSessionId']
            $bottomSessionId = if ($null -eq $bottomSessionProperty) {
                ''
            } else {
                [string]$bottomSessionProperty.Value
            }
            if ($bottomSessionId -cne $captureSessions[$lang]) {
                throw "Bottom/top capture-session continuity mismatch: $lang $key."
            }
            # Bottom evidence must match the top capture's declared source,
            # physical viewport and DPI. This does not prove process identity.
            foreach ($field in @(
                'requestedWidthDip','requestedHeightDip','actualWidthPx','actualHeightPx',
                'actualClientWidthPx','actualClientHeightPx','windowDpi')) {
                $property = $bottomEntry.PSObject.Properties[$field]
                if ($null -eq $property -or
                    [string]$property.Value -cne
                        [string]$topEntry.PSObject.Properties[$field].Value) {
                    throw "Bottom/top capture geometry or DPI differs: $lang $key ($field)."
                }
            }
            if ([string]$bottomEntry.sourceCommitOperatorDeclared -cne
                [string]$topEntry.sourceCommitOperatorDeclared) {
                throw "Bottom/top operator-declared source commit differs: $lang $key."
            }
            $bottomName = "Aurora-$page-$lang-$fixedWidthDip-dip-bottom.png"
            if ([string]$bottomEntry.file -cne $bottomName) {
                throw "Unsafe or mismatched bottom screenshot filename: $lang $key."
            }
            $bottomPng = Join-Path $directory $bottomName
            if (-not (Test-Path -LiteralPath $bottomPng -PathType Leaf)) {
                throw "Bottom manifest refers to a missing PNG: $lang $key."
            }
            $bottomInfo = Get-Item -LiteralPath $bottomPng
            if ($bottomInfo.Length -lt 24 -or $bottomInfo.Length -gt 30000000) {
                throw "Bottom PNG outside safe review size: $lang $key."
            }
            $bottomBuffer = New-Object byte[] 24
            $bottomStream = [IO.File]::OpenRead($bottomPng)
            try {
                if ($bottomStream.Read($bottomBuffer,0,24) -ne 24) {
                    throw "Unreadable bottom PNG: $lang $key."
                }
            }
            finally { $bottomStream.Dispose() }
            $bottomSignature = @(137,80,78,71,13,10,26,10)
            for ($i=0; $i -lt 8; $i++) {
                if ($bottomBuffer[$i] -ne $bottomSignature[$i]) {
                    throw "Invalid bottom PNG signature: $lang $key."
                }
            }
            if ($bottomBuffer[12] -ne 73 -or $bottomBuffer[13] -ne 72 -or
                $bottomBuffer[14] -ne 68 -or $bottomBuffer[15] -ne 82) {
                throw "Missing bottom PNG IHDR: $lang $key."
            }
            $bottomPixelWidth = [int64]$bottomBuffer[16]*16777216 +
                [int64]$bottomBuffer[17]*65536 + [int64]$bottomBuffer[18]*256 +
                [int64]$bottomBuffer[19]
            $bottomPixelHeight = [int64]$bottomBuffer[20]*16777216 +
                [int64]$bottomBuffer[21]*65536 + [int64]$bottomBuffer[22]*256 +
                [int64]$bottomBuffer[23]
            if ($bottomPixelWidth -ne [int]$bottomEntry.actualWidthPx -or
                $bottomPixelHeight -ne [int]$bottomEntry.actualHeightPx) {
                throw "Bottom PNG pixel dimensions mismatch: $lang $key."
            }
            $seenBottom[$key] = $true
        }
    }
    $bottomData[$lang] = $seenBottom
}

$html = New-Object System.Collections.Generic.List[string]
$manualChecks = @(
    'Banner / emblem / Aurora colors versus approved image',
    'Exactly six navigation routes and sidebar alignment',
    'Title, real J2534 status and RU/EN switch in the fixed 1280x800 DIP viewport',
    'Connection: real adapter, CAN speed and session state',
    'Updates: real version, channel and safe non-installable actions',
    'CEM PIN/configuration: honest evidence and disabled write actions',
    'DTC/ECU: table captions, schematic disclaimers and wrapping',
    'No clipped actions, missing footer or unintended horizontal scrolling',
    'Bottom folds: last CEM PIN, configuration, DTC and ECU controls fully readable'
)
[void]$html.Add('<!doctype html><html lang="en"><meta charset="utf-8">')
[void]$html.Add('<meta name="viewport" content="width=device-width,initial-scale=1">')
[void]$html.Add('<title>PRIVATE Dvinholm Aurora screenshot review</title>')
[void]$html.Add('<style>body{font:15px Segoe UI,Arial,sans-serif;background:#071625;color:#e9f6ff;margin:20px}h1,h2{color:#9ce9ff}p{max-width:85ch}section{margin:24px 0;border-top:1px solid #31516b;padding:10px 0}.pair{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,430px),1fr));gap:14px}figure{margin:0;background:#10263b;border:1px solid #365875;padding:10px;border-radius:10px}figcaption{margin-bottom:8px}img{width:100%;height:auto;display:block}.missing{color:#f2b979;border:1px dashed #bc8655;padding:16px}.privacy{color:#f1c48f}</style>')
[void]$html.Add('<h1>Dvinholm Aurora: RU / EN visual review</h1>')
[void]$html.Add('<p class="privacy">PRIVATE LOCAL REPORT. NOT a visual PASS, not a vehicle/J2534 test. Inspect screenshots manually against the ORIGINAL approved six-page image. Never upload before redaction. Captures are linked, not copied or embedded.</p>')
[void]$html.Add('<p>Recorded DPI describes the actual Dvinholm window, not independently certified Windows Display scaling. Capture and review separate RU/EN sets at 96, 120, 144 and 192 DPI (100%, 125%, 150%, 200%) when supported.</p>')
[void]$html.Add('<h2>Manual visual acceptance checklist (all unreviewed)</h2><ol>')
foreach ($criterion in $manualChecks) {
    [void]$html.Add('<li>' + $criterion + ' | NOT RUN until human comparison</li>')
}
[void]$html.Add('</ol>')
if ($declaredSha) {
    [void]$html.Add('<p>Operator-declared source commit (not authenticated): <code>' + $declaredSha + '</code></p>')
} else {
    [void]$html.Add('<p>No source commit authenticated or operator-declared.</p>')
}
$present = 0
$bottomPresent = 0
foreach ($lang in @('ru','en')) {
    if ($captureSessions[$lang]) {
        [void]$html.Add('<p>' + $lang.ToUpperInvariant() +
            ': local capture-session token consistent across top pages (not authenticated).</p>')
    } else {
        [void]$html.Add('<p>' + $lang.ToUpperInvariant() +
            ': legacy capture session NOT VERIFIED; source metadata cannot establish run continuity.</p>')
    }
}
foreach ($page in $pages) {
    [void]$html.Add('<section><h2>' + $page + '</h2>')
    foreach ($width in $widths) {
        [void]$html.Add('<h3>' + $width + ' × ' + $fixedHeightDip + ' client DIP</h3><div class="pair">')
        foreach ($lang in @('ru','en')) {
            $key = "$page/$width"
            if ($manifestData[$lang].ContainsKey($key)) {
                $name = "Aurora-$page-$lang-$width-dip.png"
                $dpi = $manifestDpi[$lang][$key]
                $scale = [Math]::Round($dpi * 100.0 / 96.0)
                $topPositionLabel = if ($bottomPages -ccontains $page) {
                    if ($topFoldEvidence[$lang][$key]) {
                        'outer top UIA verified'
                    } else {
                        'top position NOT VERIFIED'
                    }
                } else {
                    'top scroll not probed'
                }
                [void]$html.Add('<figure><figcaption>' + $lang.ToUpperInvariant() +
                    ' | ' + $dpi + ' DPI (about ' + $scale + '%) | ' +
                    $topPositionLabel + ' | visual NOT RUN</figcaption><img alt="' +
                    $page + ' ' + $lang + '" src="' + $lang + '/' + $name +
                    '"></figure>')
                $present++
            } else {
                [void]$html.Add('<figure class="missing">' + $lang.ToUpperInvariant() +
                    ' | NOT RUN (missing screenshot)</figure>')
            }
        }
        [void]$html.Add('</div>')
    }
    if ($bottomPages -ccontains $page) {
        [void]$html.Add('<h3>Outer workspace bottom | separately captured and unreviewed</h3><div class="pair">')
        foreach ($lang in @('ru','en')) {
            $key = "$page/$fixedWidthDip"
            if ($bottomData[$lang].ContainsKey($key)) {
                $bottomName = "Aurora-$page-$lang-$fixedWidthDip-dip-bottom.png"
                $dpi = $manifestDpi[$lang][$key]
                $scale = [Math]::Round($dpi * 100.0 / 96.0)
                [void]$html.Add('<figure><figcaption>' + $lang.ToUpperInvariant() +
                    ' | bottom UIA verified | ' + $dpi + ' DPI (about ' + $scale +
                    '%) | visual NOT RUN</figcaption><img alt="' + $page +
                    ' ' + $lang + ' bottom" src="' + $lang + '/' + $bottomName +
                    '"></figure>')
                $bottomPresent++
            } else {
                [void]$html.Add('<figure class="missing">' + $lang.ToUpperInvariant() +
                    ' | NOT RUN (missing bottom screenshot)</figure>')
            }
        }
        [void]$html.Add('</div>')
    }
    [void]$html.Add('</section>')
}
$html.Insert(6,'<p>Validated local PNG/manifest pairs: ' + $present +
    ' / 12. Missing is NOT RUN; present is NOT visual PASS.</p>')
$html.Insert(7,'<p>Validated bottom-fold PNG/manifest pairs: ' + $bottomPresent +
    ' / 8. Missing is NOT RUN; present is NOT visual PASS.</p>')
[void]$html.Add('</html>')
[IO.File]::WriteAllText($output,($html -join [Environment]::NewLine),
    [Text.UTF8Encoding]::new($false))
Write-Host "Local report created: $output. Validated top images: $present/12; bottom images: $bottomPresent/8. No upload."
