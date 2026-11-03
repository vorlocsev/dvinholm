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
$widths = @(640,800,980,1280,1440)
# Trust neither the filename nor a merely nonempty title string. These must
# match the actual six page titles assigned by the WinUI workspace presenter.
$expectedTitles = @{
    ru = @{
        Connection='Подключение'; Updates='Обновление'; CemPin='CEM PIN'
        CemConfiguration='Конфигурация CEM'; Dtc='Ошибки / DTC'; Ecu='Блоки ECU'
    }
    en = @{
        Connection='Connection'; Updates='Updates'; CemPin='CEM PIN'
        CemConfiguration='CEM configuration'; Dtc='Faults (DTC)'; Ecu='Modules (ECU)'
    }
}
$manifestData = @{}
$manifestDpi = @{}
$declaredSha = ''
$hasMissingDeclaration = $false
foreach ($lang in @('ru','en')) {
    $directory = Join-Path $root $lang
    $manifestPath = Join-Path $directory ("Aurora-{0}-capture-manifest.json" -f $lang)
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "The $lang capture manifest is missing. Generate each language separately."
    }
    $rows = @(Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json)
    if ($rows.Count -gt 30) { throw 'Unexpected extra records in screenshot manifest.' }
    $seen = @{}
    $dpis = @{}
    $observedDpi = $null
    foreach ($entry in $rows) {
        $page = [string]$entry.page
        $width = [int]$entry.requestedWidthDip
        if ($pages -cnotcontains $page -or $widths -notcontains $width) {
            throw "Unexpected page or DIP width in $lang manifest."
        }
        $key = "$page/$width"
        if ($seen.ContainsKey($key)) { throw "Duplicate $lang screenshot entry: $key" }
        $seen[$key] = $true
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
$html = New-Object System.Collections.Generic.List[string]
$manualChecks = @(
    'Banner / emblem / Aurora colors versus approved image',
    'Exactly six navigation routes and sidebar alignment',
    'Title, real J2534 status and RU/EN switch at compact/wide widths',
    'Connection: real adapter, CAN speed and session state',
    'Updates: real version, channel and safe non-installable actions',
    'CEM PIN/configuration: honest evidence and disabled write actions',
    'DTC/ECU: table captions, schematic disclaimers and wrapping',
    'No clipped actions, missing footer or unintended horizontal scrolling'
)
[void]$html.Add('<!doctype html><html lang="en"><meta charset="utf-8">')
[void]$html.Add('<meta name="viewport" content="width=device-width,initial-scale=1">')
[void]$html.Add('<title>PRIVATE Dvinholm Aurora screenshot review</title>')
[void]$html.Add('<style>body{font:15px Segoe UI,Arial,sans-serif;background:#071625;color:#e9f6ff;margin:20px}h1,h2{color:#9ce9ff}p{max-width:85ch}section{margin:24px 0;border-top:1px solid #31516b;padding:10px 0}.pair{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,430px),1fr));gap:14px}figure{margin:0;background:#10263b;border:1px solid #365875;padding:10px;border-radius:10px}figcaption{margin-bottom:8px}img{width:100%;height:auto;display:block}.missing{color:#f2b979;border:1px dashed #bc8655;padding:16px}.privacy{color:#f1c48f}</style>')
[void]$html.Add('<h1>Dvinholm Aurora: RU / EN visual review</h1>')
[void]$html.Add('<p class="privacy">PRIVATE LOCAL REPORT. NOT a visual PASS, not a vehicle/J2534 test. Inspect screenshots manually against the ORIGINAL approved six-page image. Never upload before redaction. Captures are linked, not copied or embedded.</p>')
[void]$html.Add('<p>Recorded DPI describes the actual Dvinholm window, not independently certified Windows Display scaling. Capture and review separate RU/EN sets at 96, 120 and 144 DPI (100%, 125%, 150%) when supported.</p>')
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
foreach ($page in $pages) {
    [void]$html.Add('<section><h2>' + $page + '</h2>')
    foreach ($width in $widths) {
        [void]$html.Add('<h3>' + $width + ' client DIP</h3><div class="pair">')
        foreach ($lang in @('ru','en')) {
            $key = "$page/$width"
            if ($manifestData[$lang].ContainsKey($key)) {
                $name = "Aurora-$page-$lang-$width-dip.png"
                $dpi = $manifestDpi[$lang][$key]
                $scale = [Math]::Round($dpi * 100.0 / 96.0)
                [void]$html.Add('<figure><figcaption>' + $lang.ToUpperInvariant() +
                    ' | ' + $dpi + ' DPI (about ' + $scale + '%) | unreviewed</figcaption><img alt="' +
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
    [void]$html.Add('</section>')
}
$html.Insert(6,'<p>Validated local PNG/manifest pairs: ' + $present +
    ' / 60. Missing is NOT RUN; present is NOT visual PASS.</p>')
[void]$html.Add('</html>')
[IO.File]::WriteAllText($output,($html -join [Environment]::NewLine),
    [Text.UTF8Encoding]::new($false))
Write-Host "Local report created: $output. Validated images: $present/60. No upload."
