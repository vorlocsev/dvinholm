#requires -Version 5.1
<#
.SYNOPSIS
    Build a PRIVATE local HTML review of nine real WinUI Volvo JPG screenshots
    in both RU and EN, captured via Capture-UnifiedAuroraWinUI3.ps1.
.NOTES
    Offline only. Does not launch Dvinholm, install tools, query vehicles,
    publish images, or infer visual acceptance from screenshot hashes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$CaptureRoot,
    [switch]$IConfirmPrivateOfflineReview
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IConfirmPrivateOfflineReview) {
    throw 'Confirm that both private capture folders were checked for secrets.'
}
if ($env:CI -eq 'true' -or $env:GITHUB_ACTIONS -eq 'true') {
    throw 'Private screenshot review is disabled in CI.'
}
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or
    $PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Use Windows PowerShell 5.1 on the local review desktop.'
}
Add-Type -AssemblyName System.Drawing
$root = [IO.Path]::GetFullPath($CaptureRoot).TrimEnd('\','/')
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\','/')
if ($root.Equals($repoRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $root.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Keep private screenshot output outside the repository.'
}
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw 'The capture root does not exist.'
}
$output = Join-Path $root 'Aurora-local-vehicle-review.html'
if (Test-Path -LiteralPath $output) {
    throw 'Review output already exists. Use a fresh capture folder.'
}
$source = Join-Path $repoRoot 'assets\vehicles\manifest.json'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw 'Reviewed JPG source manifest missing from local checkout.'
}
$decodedModels = Get-Content -LiteralPath $source -Encoding UTF8 -Raw | ConvertFrom-Json
if ($decodedModels -isnot [System.Array]) {
    throw 'Reviewed JPG manifest must be an array.'
}
$models = @($decodedModels)
if ($models.Count -ne 9) { throw 'Expected nine source JPG models.' }
$ids = @{}
foreach ($model in $models) {
    $id = [string]$model.modelId
    if ($id -cnotmatch '^[a-z0-9-]{3,24}$' -or $ids.ContainsKey($id) -or
        [string]$model.file -cne ($id + '.jpg') -or
        [string]$model.label -cnotmatch '^Volvo .+ \u00b7 (P2|P3|SPA)$' -or
        [int]$model.width -ne 1280 -or [int]$model.height -ne 510) {
        throw 'Invalid or duplicate reviewed JPG catalog entry.'
    }
    $ids[$id] = $model
}
$expectedIds = @('xc90-p2','xc70-p2','v70-p2','s60-p2','s80-p2',
                 'xc90-spa','xc70-p3','v70-p3','s80-p3')
if ((@($ids.Keys | Sort-Object) -join '|') -cne (@($expectedIds | Sort-Object) -join '|')) {
    throw 'The reviewed model catalog does not match the nine supported models.'
}
# Independently verify each source JPG against its declared manifest digest,
# actual decoder and dimensions BEFORE producing an HTML review. Embed the
# checked source bytes directly: no remote requests, sidecar copies or
# path-dependent links to the source checkout.
$sourceImages = @{}
foreach ($id in $expectedIds) {
    $model = $ids[$id]
    $sourceJpg = Join-Path $repoRoot ("assets\vehicles\" + [string]$model.file)
    if (-not (Test-Path -LiteralPath $sourceJpg -PathType Leaf)) {
        throw "Source JPG missing for model $id; review NOT RUN."
    }
    $file = Get-Item -LiteralPath $sourceJpg -Force
    if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $file.Length -lt 20000 -or $file.Length -gt 2000000 -or
        $file.Length -ne [int]$model.bytes) {
        throw "Source JPG is linked, oversized or mismatches manifest length: $id."
    }
    $jpgBytes = [IO.File]::ReadAllBytes($sourceJpg)
    if ($jpgBytes.Length -lt 4 -or $jpgBytes[0] -ne 255 -or
        $jpgBytes[1] -ne 216 -or $jpgBytes[$jpgBytes.Length - 2] -ne 255 -or
        $jpgBytes[$jpgBytes.Length - 1] -ne 217) {
        throw "Source JPG header/trailer invalid for model $id."
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $actualSha = ([BitConverter]::ToString(
            $sha.ComputeHash($jpgBytes))).Replace('-', '').ToLowerInvariant()
    }
    finally { $sha.Dispose() }
    if ([string]$model.sha256 -cnotmatch '^[0-9a-f]{64}
$sourceCommit = ''
$globalDpi = $null
$globalSize = ''
foreach ($lang in @('ru','en')) {
    $dir = Join-Path $root $lang
    $manifestPath = Join-Path $dir ("Aurora-{0}-vehicle-models-manifest.json" -f $lang)
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Missing $lang vehicle model capture manifest; visual review NOT RUN."
    }
    $decoded = Get-Content -LiteralPath $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
    if ($decoded -isnot [System.Array]) {
        throw "$lang vehicle capture manifest must be a JSON array."
    }
    $rows = @($decoded)
    if ($rows.Count -ne 9) { throw "$lang must have exactly nine real screenshots." }
    $found = @{}
    $pixelHashes = @{}
    $session = ''
    foreach ($entry in $rows) {
        if ($null -eq $entry -or $entry -isnot [pscustomobject]) {
            throw "Invalid $lang screenshot entry."
        }
        $id = [string]$entry.manuallySelectedModelId
        if (-not $ids.ContainsKey($id) -or $found.ContainsKey($id)) {
            throw "Unknown or duplicate $lang manual selection: $id"
        }
        $model = $ids[$id]
        $name = ([string]$model.label).Substring(6)
        $suffix = if ($lang -eq 'ru') {
            ' · справочный рендер, выбран вручную'
        } else {
            ' · reference render, selected manually'
        }
        $filename = "Aurora-Ecu-Model-$id-$lang-1280-dip.png"
        if ([string]$entry.file -cne $filename -or
            [string]$entry.page -cne 'Ecu' -or
            [string]$entry.selectedModelNameVerifiedByUia -cne $name -or
            [string]$entry.verifiedReferenceCaption -cne ([string]$model.label + $suffix) -or
            [string]$entry.operatorSelectedLanguage -cne $lang -or
            [int]$entry.requestedWidthDip -ne 1280 -or
            [int]$entry.requestedHeightDip -ne 800 -or
            $entry.jpgDecodedByWinUiImageOpened -isnot [bool] -or
            $entry.jpgDecodedByWinUiImageOpened -ne $true) {
            throw "$lang $id lacks verified manual selection, JPG decode, or fixed viewport."
        }
        $pixelHash = [string]$entry.artworkViewportSha256
        $pngHash = [string]$entry.screenshotSha256
        if ($pixelHash -cnotmatch '^[0-9a-f]{64}$' -or
            $pngHash -cnotmatch '^[0-9a-f]{64}$' -or
            $pixelHashes.ContainsKey($pixelHash)) {
            throw "$lang $id has absent, malformed or repeated real artwork pixels."
        }
        if ($null -eq $entry.artworkViewportRectPx) {
            throw "Missing actual artwork viewport pixel coordinates: $lang $id."
        }
        $region = $entry.artworkViewportRectPx
        $pixelHashes[$pixelHash] = $id
        $commit = [string]$entry.sourceCommitOperatorDeclared
        if ($commit -cnotmatch '^[0-9a-fA-F]{40}$') {
            throw "Missing operator-declared source SHA for $lang $id."
        }
        if ($sourceCommit -and $sourceCommit -cne $commit.ToLowerInvariant()) {
            throw 'RU/EN capture sets refer to different source commits.'
        }
        $sourceCommit = $commit.ToLowerInvariant()
        $runSession = [string]$entry.captureSessionId
        if ($runSession -cnotmatch '^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$' -or
            ($session -and $session -cne $runSession)) {
            throw "Missing or mixed private $lang capture session."
        }
        $session = $runSession
        $dpi = [int]$entry.windowDpi
        if ($dpi -lt 96 -or $dpi -gt 480) { throw "Invalid $lang capture DPI." }
        if ($null -ne $globalDpi -and $dpi -ne $globalDpi) {
            throw 'RU and EN screenshot DPI differ. Capture both at the same display scale.'
        }
        $globalDpi = $dpi
        $png = Join-Path $dir $filename
        if (-not (Test-Path -LiteralPath $png -PathType Leaf)) {
            throw "Screenshot missing: $lang $filename."
        }
        # Recompute SHA-256 from the saved PNG, never trust the manifest alone.
        $actualPngHash = (Get-FileHash -LiteralPath $png -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualPngHash -cne $pngHash) {
            throw "Screenshot PNG was modified after live capture: $lang $id."
        }
        $img = [System.Drawing.Bitmap]::new($png)
        try {
            if ([int]$entry.actualWidthPx -ne $img.Width -or
                [int]$entry.actualHeightPx -ne $img.Height -or
                $img.Width -lt 1280 -or $img.Height -lt 800) {
                throw "Screenshot resolution or metadata mismatch: $lang $id."
            }
            $size = "$($img.Width)x$($img.Height)"
            if ($globalSize -and $globalSize -cne $size) {
                throw 'RU and EN actual screenshot dimensions differ.'
            }
            $globalSize = $size
            # Recompute the exact same normalized 32-bit crop saved by the
            # interactive capture helper; reject moved or edited artwork.
            $x = [int]$region.x
            $y = [int]$region.y
            $w = [int]$region.width
            $h = [int]$region.height
            if ($x -lt 0 -or $y -lt 0 -or $w -lt 100 -or $h -lt 60 -or
                $x + $w -gt $img.Width -or $y + $h -gt $img.Height) {
                throw "Invalid real artwork crop bounds for $lang $id."
            }
            $crop = $img.Clone(
                [System.Drawing.Rectangle]::new($x, $y, $w, $h),
                [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $stream = [System.IO.MemoryStream]::new()
            $hash = [System.Security.Cryptography.SHA256]::Create()
            try {
                $crop.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $actualCropHash = [BitConverter]::ToString(
                    $hash.ComputeHash($stream.ToArray())).Replace('-', '').ToLowerInvariant()
                if ($actualCropHash -cne $pixelHash) {
                    throw "Actual screenshot artwork pixels differ from capture: $lang $id."
                }
            }
            finally {
                $hash.Dispose()
                $stream.Dispose()
                $crop.Dispose()
            }
        }
        finally { $img.Dispose() }
        $found[$id] = $entry
    }
    $byLanguage[$lang] = $found
}
# All sources are constrained local screenshot basenames, never remote links.
$sb = [System.Collections.Generic.List[string]]::new()
[void]$sb.Add('<!doctype html><html lang="ru"><head><meta charset="utf-8">')
[void]$sb.Add('<title>Dvinholm private Volvo screenshot review</title>')
[void]$sb.Add('<style>body{margin:24px;background:#091b2e;color:#dceafa;font:14px Segoe UI,Arial}h1{font-size:23px}h2{font-size:17px;margin:26px 0 12px}.note{color:#f2c989}.cols{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}figure{min-width:0;margin:0;background:#102e49;padding:10px;border:1px solid #42627d;border-radius:8px}img{width:100%;height:auto;display:block}figcaption{margin-bottom:8px}small{color:#aac6dc}code{overflow-wrap:anywhere}</style></head><body>')
[void]$sb.Add('<h1>Dvinholm — Volvo JPG / RU–EN review</h1>')
[void]$sb.Add('<p class="note">Nine source JPGs independently checked against manifest bytes, dimensions and SHA-256; 18 real RU/EN screenshots and unique image-region hashes verified. Image geometry, model identity and Aurora visual parity: NOT APPROVED until human inspection.</p>')
[void]$sb.Add('<p>Operator-declared source SHA: <code>' + $sourceCommit +
    '</code>; physical DPI: ' + $globalDpi + '; screenshot size: ' + $globalSize + ' px.</p>')
foreach ($id in $expectedIds) {
    $model = $ids[$id]
    [void]$sb.Add('<section><h2>' +
        [System.Net.WebUtility]::HtmlEncode([string]$model.label) + '</h2><div class="cols">')
    # Triptych: exact reviewed source JPG, real RU screenshot, real EN
    # screenshot. Source image bytes are embedded in the HTML only after
    # validating their exact manifest digest; no URL or source path leaks.
    $sourceImage = $sourceImages[$id]
    [void]$sb.Add('<figure><figcaption>REFERENCE JPG — source bytes verified</figcaption>' +
        '<img alt="' + $id + ' source JPG" src="' + $sourceImage.dataUri +
        '"><small>Source JPG SHA-256: <code>' + $sourceImage.sha256 +
        '</code>; generated reference, NOT OEM-approved</small></figure>')
    foreach ($lang in @('ru','en')) {
        $entry = $byLanguage[$lang][$id]
        $filename = "Aurora-Ecu-Model-$id-$lang-1280-dip.png"
        $relative = "$lang/$filename"
        [void]$sb.Add('<figure><figcaption>' + $lang.ToUpperInvariant() +
            ' — actual WinUI screenshot, appearance NOT CHECKED</figcaption><img alt="' +
            $id + ' ' + $lang + '" src="' + $relative + '"><small>Artwork crop SHA-256: <code>' +
            [string]$entry.artworkViewportSha256 + '</code></small></figure>')
    }
    [void]$sb.Add('</div></section>')
}
[void]$sb.Add('<p class="note">This file is local only. Do not upload private screenshots, VIN, PIN, driver paths or adapter identifiers.</p></body></html>')
[IO.File]::WriteAllText($output, ($sb -join [Environment]::NewLine),
    [Text.UTF8Encoding]::new($false))
Write-Host "Private vehicle screenshot review prepared: $output. RU/EN 18/18; visual sign-off still NOT RUN."
 -or
        $actualSha -cne [string]$model.sha256) {
        throw "Source JPG SHA-256 mismatch with source manifest: $id."
    }
    $stream = [IO.MemoryStream]::new($jpgBytes, $false)
    $image = $null
    try {
        $image = [Drawing.Image]::FromStream($stream, $false, $true)
        if ($image.RawFormat.Guid -ne [Drawing.Imaging.ImageFormat]::Jpeg.Guid -or
            $image.Width -ne 1280 -or $image.Height -ne 510) {
            throw "Source JPG is not a 1280 x 510 JPEG: $id."
        }
    }
    finally {
        if ($null -ne $image) { $image.Dispose() }
        $stream.Dispose()
    }
    $sourceImages[$id] = [pscustomobject]@{
        sha256 = $actualSha
        dataUri = 'data:image/jpeg;base64,' + [Convert]::ToBase64String($jpgBytes)
    }
}
$byLanguage = @{}
$sourceCommit = ''
$globalDpi = $null
$globalSize = ''
foreach ($lang in @('ru','en')) {
    $dir = Join-Path $root $lang
    $manifestPath = Join-Path $dir ("Aurora-{0}-vehicle-models-manifest.json" -f $lang)
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Missing $lang vehicle model capture manifest; visual review NOT RUN."
    }
    $decoded = Get-Content -LiteralPath $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
    if ($decoded -isnot [System.Array]) {
        throw "$lang vehicle capture manifest must be a JSON array."
    }
    $rows = @($decoded)
    if ($rows.Count -ne 9) { throw "$lang must have exactly nine real screenshots." }
    $found = @{}
    $pixelHashes = @{}
    $session = ''
    foreach ($entry in $rows) {
        if ($null -eq $entry -or $entry -isnot [pscustomobject]) {
            throw "Invalid $lang screenshot entry."
        }
        $id = [string]$entry.manuallySelectedModelId
        if (-not $ids.ContainsKey($id) -or $found.ContainsKey($id)) {
            throw "Unknown or duplicate $lang manual selection: $id"
        }
        $model = $ids[$id]
        $name = ([string]$model.label).Substring(6)
        $suffix = if ($lang -eq 'ru') {
            ' · справочный рендер, выбран вручную'
        } else {
            ' · reference render, selected manually'
        }
        $filename = "Aurora-Ecu-Model-$id-$lang-1280-dip.png"
        if ([string]$entry.file -cne $filename -or
            [string]$entry.page -cne 'Ecu' -or
            [string]$entry.selectedModelNameVerifiedByUia -cne $name -or
            [string]$entry.verifiedReferenceCaption -cne ([string]$model.label + $suffix) -or
            [string]$entry.operatorSelectedLanguage -cne $lang -or
            [int]$entry.requestedWidthDip -ne 1280 -or
            [int]$entry.requestedHeightDip -ne 800 -or
            $entry.jpgDecodedByWinUiImageOpened -isnot [bool] -or
            $entry.jpgDecodedByWinUiImageOpened -ne $true) {
            throw "$lang $id lacks verified manual selection, JPG decode, or fixed viewport."
        }
        $pixelHash = [string]$entry.artworkViewportSha256
        $pngHash = [string]$entry.screenshotSha256
        if ($pixelHash -cnotmatch '^[0-9a-f]{64}$' -or
            $pngHash -cnotmatch '^[0-9a-f]{64}$' -or
            $pixelHashes.ContainsKey($pixelHash)) {
            throw "$lang $id has absent, malformed or repeated real artwork pixels."
        }
        if ($null -eq $entry.artworkViewportRectPx) {
            throw "Missing actual artwork viewport pixel coordinates: $lang $id."
        }
        $region = $entry.artworkViewportRectPx
        $pixelHashes[$pixelHash] = $id
        $commit = [string]$entry.sourceCommitOperatorDeclared
        if ($commit -cnotmatch '^[0-9a-fA-F]{40}$') {
            throw "Missing operator-declared source SHA for $lang $id."
        }
        if ($sourceCommit -and $sourceCommit -cne $commit.ToLowerInvariant()) {
            throw 'RU/EN capture sets refer to different source commits.'
        }
        $sourceCommit = $commit.ToLowerInvariant()
        $runSession = [string]$entry.captureSessionId
        if ($runSession -cnotmatch '^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$' -or
            ($session -and $session -cne $runSession)) {
            throw "Missing or mixed private $lang capture session."
        }
        $session = $runSession
        $dpi = [int]$entry.windowDpi
        if ($dpi -lt 96 -or $dpi -gt 480) { throw "Invalid $lang capture DPI." }
        if ($null -ne $globalDpi -and $dpi -ne $globalDpi) {
            throw 'RU and EN screenshot DPI differ. Capture both at the same display scale.'
        }
        $globalDpi = $dpi
        $png = Join-Path $dir $filename
        if (-not (Test-Path -LiteralPath $png -PathType Leaf)) {
            throw "Screenshot missing: $lang $filename."
        }
        # Recompute SHA-256 from the saved PNG, never trust the manifest alone.
        $actualPngHash = (Get-FileHash -LiteralPath $png -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualPngHash -cne $pngHash) {
            throw "Screenshot PNG was modified after live capture: $lang $id."
        }
        $img = [System.Drawing.Bitmap]::new($png)
        try {
            if ([int]$entry.actualWidthPx -ne $img.Width -or
                [int]$entry.actualHeightPx -ne $img.Height -or
                $img.Width -lt 1280 -or $img.Height -lt 800) {
                throw "Screenshot resolution or metadata mismatch: $lang $id."
            }
            $size = "$($img.Width)x$($img.Height)"
            if ($globalSize -and $globalSize -cne $size) {
                throw 'RU and EN actual screenshot dimensions differ.'
            }
            $globalSize = $size
            # Recompute the exact same normalized 32-bit crop saved by the
            # interactive capture helper; reject moved or edited artwork.
            $x = [int]$region.x
            $y = [int]$region.y
            $w = [int]$region.width
            $h = [int]$region.height
            if ($x -lt 0 -or $y -lt 0 -or $w -lt 100 -or $h -lt 60 -or
                $x + $w -gt $img.Width -or $y + $h -gt $img.Height) {
                throw "Invalid real artwork crop bounds for $lang $id."
            }
            $crop = $img.Clone(
                [System.Drawing.Rectangle]::new($x, $y, $w, $h),
                [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $stream = [System.IO.MemoryStream]::new()
            $hash = [System.Security.Cryptography.SHA256]::Create()
            try {
                $crop.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $actualCropHash = [BitConverter]::ToString(
                    $hash.ComputeHash($stream.ToArray())).Replace('-', '').ToLowerInvariant()
                if ($actualCropHash -cne $pixelHash) {
                    throw "Actual screenshot artwork pixels differ from capture: $lang $id."
                }
            }
            finally {
                $hash.Dispose()
                $stream.Dispose()
                $crop.Dispose()
            }
        }
        finally { $img.Dispose() }
        $found[$id] = $entry
    }
    $byLanguage[$lang] = $found
}
# All sources are constrained local screenshot basenames, never remote links.
$sb = [System.Collections.Generic.List[string]]::new()
[void]$sb.Add('<!doctype html><html lang="ru"><head><meta charset="utf-8">')
[void]$sb.Add('<title>Dvinholm private Volvo screenshot review</title>')
[void]$sb.Add('<style>body{margin:24px;background:#091b2e;color:#dceafa;font:14px Segoe UI,Arial}h1{font-size:23px}h2{font-size:17px;margin:26px 0 12px}.note{color:#f2c989}.cols{display:grid;grid-template-columns:1fr 1fr;gap:14px}figure{min-width:0;margin:0;background:#102e49;padding:10px;border:1px solid #42627d;border-radius:8px}img{width:100%;height:auto;display:block}figcaption{margin-bottom:8px}small{color:#aac6dc}code{overflow-wrap:anywhere}</style></head><body>')
[void]$sb.Add('<h1>Dvinholm — Volvo JPG / RU–EN review</h1>')
[void]$sb.Add('<p class="note">18 local real screenshots and unique image-region hashes verified against manifests. Visual accuracy, cropping, model identity and Aurora parity: NOT APPROVED until human inspection.</p>')
[void]$sb.Add('<p>Operator-declared source SHA: <code>' + $sourceCommit +
    '</code>; physical DPI: ' + $globalDpi + '; screenshot size: ' + $globalSize + ' px.</p>')
foreach ($id in $expectedIds) {
    $model = $ids[$id]
    [void]$sb.Add('<section><h2>' +
        [System.Net.WebUtility]::HtmlEncode([string]$model.label) + '</h2><div class="cols">')
    foreach ($lang in @('ru','en')) {
        $entry = $byLanguage[$lang][$id]
        $filename = "Aurora-Ecu-Model-$id-$lang-1280-dip.png"
        $relative = "$lang/$filename"
        [void]$sb.Add('<figure><figcaption>' + $lang.ToUpperInvariant() +
            ' — actual WinUI screenshot, appearance NOT CHECKED</figcaption><img alt="' +
            $id + ' ' + $lang + '" src="' + $relative + '"><small>Artwork crop SHA-256: <code>' +
            [string]$entry.artworkViewportSha256 + '</code></small></figure>')
    }
    [void]$sb.Add('</div></section>')
}
[void]$sb.Add('<p class="note">This file is local only. Do not upload private screenshots, VIN, PIN, driver paths or adapter identifiers.</p></body></html>')
[IO.File]::WriteAllText($output, ($sb -join [Environment]::NewLine),
    [Text.UTF8Encoding]::new($false))
Write-Host "Private vehicle screenshot review prepared: $output. RU/EN 18/18; visual sign-off still NOT RUN."
