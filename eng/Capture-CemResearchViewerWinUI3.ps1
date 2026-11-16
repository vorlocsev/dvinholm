#Requires -Version 5.1
<#
.SYNOPSIS
  Capture a PRIVATE real-desktop CEM research dialog screenshot.
.NOTES
  First manually open synthetic, secret-free P2/P3 JSON in the matching
  Dvinholm x64 build and select RU or EN. No files or UI controls are opened
  by this script. Never execute it in CI or publish screenshots automatically.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('ru','en')][string]$Language,
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-fA-F]{40}$')][string]$CommitSha,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][switch]$IConfirmSyntheticNoSecrets,
    [ValidateSet('list','details')][string]$View = 'list',
    [ValidateRange(96,480)][int]$ExpectedDpi = 192
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IConfirmSyntheticNoSecrets) {
    throw 'Confirm that the visible viewer contains synthetic, non-sensitive content.'
}
if ($env:CI -eq 'true' -or $env:GITHUB_ACTIONS -eq 'true' -or
    -not [Environment]::UserInteractive) {
    throw 'Requires a signed-in interactive Windows desktop; not CI.'
}
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or
    $PSVersionTable.PSEdition -ne 'Desktop' -or
    $PSVersionTable.PSVersion.Major -ne 5) {
    throw 'Use Windows PowerShell 5.1 on Windows.'
}

$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\','/')
$output = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\','/')
if ($output.Equals($repoRoot,[StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Private screenshots must be saved outside the Git repository.'
}
$evidenceFile = Join-Path $repoRoot 'artifacts/Aurora-x64-build-evidence.json'
if (-not (Test-Path -LiteralPath $evidenceFile -PathType Leaf)) {
    throw 'Missing exact-head x64 pilot provenance; use full PR artifact.'
}
$evidence = Get-Content -LiteralPath $evidenceFile -Raw -Encoding UTF8 | ConvertFrom-Json
$sha = $CommitSha.ToLowerInvariant()
if ($null -eq $evidence -or
    $evidence.schema -cne 'dvinholm.aurora.x64.provenance.v1' -or
    $evidence.sourceHeadCommitSha -cne $sha -or
    $evidence.checkoutCommitSha -cnotmatch '^[0-9a-f]{40}$' -or
    $evidence.executableSha256 -cnotmatch '^[0-9a-f]{64}$' -or
    $evidence.executableRelativePath -cne 'Dvinholm.exe' -or
    [string]$evidence.pilotRunNumber -notmatch '^[1-9][0-9]*$' -or
    $evidence.expectedWindowTitle -cne ('Dvinholm CI #' + [string]$evidence.pilotRunNumber)) {
    throw 'Source SHA, executable metadata or native title provenance invalid.'
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DvinholmCemDialogNative {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError=true)]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]
    public static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")]
    public static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);
    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError=true)]
    public static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
    [DllImport("user32.dll", SetLastError=true)]
    public static extern bool GetClientRect(IntPtr hwnd,out RECT rect);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hwnd);
}
'@
$perMonitorV2 = [IntPtr](-4)
$setDpi = [DvinholmCemDialogNative]::SetProcessDpiAwarenessContext($perMonitorV2)
if (-not $setDpi -and [DvinholmCemDialogNative]::GetAwarenessFromDpiAwarenessContext(
    [DvinholmCemDialogNative]::GetThreadDpiAwarenessContext()) -ne 2) {
    throw 'Cannot confirm unvirtualized DPI coordinates.'
}
$windows = @(Get-Process -Name 'Dvinholm' -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero })
if ($windows.Count -ne 1) { throw 'Exactly one Dvinholm.exe window required.' }
$process = $windows[0]
$hwnd = $process.MainWindowHandle
$process.Refresh()
if (-not [DvinholmCemDialogNative]::IsWindowVisible($hwnd) -or
    [DvinholmCemDialogNative]::IsIconic($hwnd) -or
    $process.MainWindowTitle -cne $evidence.expectedWindowTitle) {
    throw 'Verified native window must be visible, restored and match pilot title.'
}
if (-not [DvinholmCemDialogNative]::AreDpiAwarenessContextsEqual(
    [DvinholmCemDialogNative]::GetWindowDpiAwarenessContext($hwnd),$perMonitorV2)) {
    throw 'The app must use Per-Monitor V2 DPI awareness.'
}
$exe = $process.Path
if ([string]::IsNullOrWhiteSpace($exe) -or
    -not (Test-Path -LiteralPath $exe -PathType Leaf) -or
    (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant() -cne
        $evidence.executableSha256) {
    throw 'Running Dvinholm.exe SHA-256 differs from exact-head artifact.'
}
$dpi = [DvinholmCemDialogNative]::GetDpiForWindow($hwnd)
if ($dpi -ne $ExpectedDpi) {
    throw "Observed $dpi DPI, expected $ExpectedDpi. Screenshot refused."
}
$outer = New-Object DvinholmCemDialogNative+RECT
$client = New-Object DvinholmCemDialogNative+RECT
if (-not [DvinholmCemDialogNative]::GetWindowRect($hwnd,[ref]$outer) -or
    -not [DvinholmCemDialogNative]::GetClientRect($hwnd,[ref]$client)) {
    throw 'Cannot measure native client/outer dimensions.'
}
$clientW = $client.Right - $client.Left
$clientH = $client.Bottom - $client.Top
if ([Math]::Abs($clientW - 1280 * $dpi / 96.0) -gt 4 -or
    [Math]::Abs($clientH - 800 * $dpi / 96.0) -gt 4) {
    throw 'The app is not using the approved fixed 1280x800 DIP client.'
}
$width = $outer.Right - $outer.Left
$height = $outer.Bottom - $outer.Top
$work = [System.Windows.Forms.Screen]::FromHandle($hwnd).WorkingArea
if ($width -le 0 -or $height -le 0 -or
    $outer.Left -lt $work.Left -or $outer.Top -lt $work.Top -or
    $outer.Right -gt $work.Right -or $outer.Bottom -gt $work.Bottom) {
    throw 'The entire native window must fit on the selected monitor.'
}
$root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
if ($null -eq $root) { throw 'Cannot inspect WinUI Accessibility tree.' }
function Get-CemAnchor([string]$id) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
}
$expectedTitle = if ($Language -eq 'ru') {
    'Локальный каталог CEM P2/P3 (офлайн)'
} else {
    'Local CEM P2/P3 research (offline)'
}
function Assert-CemViewer {
    $modal = Get-CemAnchor 'DvinholmCemResearchDialog'
    $filter = Get-CemAnchor 'DvinholmCemResearchGenerationFilter'
    $list = Get-CemAnchor 'DvinholmCemResearchClaimsList'
    $warning = Get-CemAnchor 'DvinholmCemResearchSafetyWarning'
    $detail = Get-CemAnchor 'DvinholmCemResearchClaimDetails'
    if ($null -eq $modal -or $null -eq $filter -or
        $null -eq $list -or $null -eq $warning -or $null -eq $detail) {
        throw 'CEM viewer not open or UI Automation anchors missing.'
    }
    if ($modal.Current.Name -cne $expectedTitle -or
        $modal.Current.IsOffscreen) {
        throw 'The live modal language or visibility does not match the request.'
    }
    if ($filter.Current.ControlType -ne
        [System.Windows.Automation.ControlType]::ComboBox -or
        $list.Current.ControlType -ne
        [System.Windows.Automation.ControlType]::List) {
        throw 'CEM viewer generation filter or list UIA role is invalid.'
    }
    $target = if ($View -eq 'details') { $detail } else { $list }
    if ($target.Current.IsOffscreen) {
        throw 'Requested view is offscreen; scroll manually and retry.'
    }
    $bounds = $target.Current.BoundingRectangle
    if ([double]::IsNaN($bounds.X) -or
        [double]::IsInfinity($bounds.X) -or
        $bounds.Width -lt 20 -or $bounds.Height -lt 10) {
        throw 'Requested list/detail UIA bounds are invalid.'
    }
    if ($View -eq 'details' -and $detail.Current.Name -in @(
        'Выберите исследовательскую запись.',
        'Select a research claim.')) {
        throw 'Select a synthetic claim before capturing details.'
    }
}
Assert-CemViewer

Write-Host 'Switch focus to the open Dvinholm modal; no click or key input is automated.'
$deadline = [DateTime]::UtcNow.AddSeconds(30)
do {
    $foreground = [DvinholmCemDialogNative]::GetForegroundWindow()
    if ([DvinholmCemDialogNative]::GetAncestor($foreground,2) -eq $hwnd) {
        break
    }
    Start-Sleep -Milliseconds 150
} while ([DateTime]::UtcNow -lt $deadline)
if ([DvinholmCemDialogNative]::GetAncestor(
    [DvinholmCemDialogNative]::GetForegroundWindow(),2) -ne $hwnd) {
    throw 'Dvinholm is not foreground; no screenshot taken.'
}
Start-Sleep -Milliseconds 400
Assert-CemViewer

if (-not (Test-Path -LiteralPath $output -PathType Container)) {
    [void](New-Item -ItemType Directory -Path $output -Force)
}
if (@(Get-ChildItem -LiteralPath $output -Force).Count -ne 0) {
    throw 'Use a new, empty output directory for each capture.'
}
$filename = "Aurora-CemResearch-$Language-$View-1280x800-dip.png"
$destination = Join-Path $output $filename
$bitmap = [System.Drawing.Bitmap]::new($width,$height)
try {
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($outer.Left,$outer.Top,0,0,$bitmap.Size,
            [System.Drawing.CopyPixelOperation]::SourceCopy)
    }
    finally { $graphics.Dispose() }
    $bitmap.Save($destination,[System.Drawing.Imaging.ImageFormat]::Png)
}
finally { $bitmap.Dispose() }

$manifest = [ordered]@{
    schema = 'dvinholm.aurora.cem-research-private-visual.v1'
    screenshot = $filename
    screenshotSha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
    sourceHeadSha = $sha
    pilotRunNumber = [string]$evidence.pilotRunNumber
    verifiedExecutableSha256 = $evidence.executableSha256
    requestedLanguage = $Language
    expectedDialogTitle = $expectedTitle
    view = $View
    measuredDpi = $dpi
    measuredClientWidthPx = $clientW
    measuredClientHeightPx = $clientH
    measuredWidthDip = [Math]::Round($clientW * 96.0 / $dpi,2)
    measuredHeightDip = [Math]::Round($clientH * 96.0 / $dpi,2)
    capturedAtUtc = [DateTime]::UtcNow.ToString('o')
    note = 'Operator-confirmed synthetic private capture; human review required.'
}
$manifestPath = Join-Path $output "Aurora-CemResearch-$Language-$View-manifest.json"
ConvertTo-Json -InputObject $manifest -Depth 4 |
    Set-Content -LiteralPath $manifestPath -Encoding UTF8
Write-Host "Captured private synthetic modal screenshot and manifest to $output"
Write-Host 'Review pixel contents locally before sharing. Never commit private screenshots.'
