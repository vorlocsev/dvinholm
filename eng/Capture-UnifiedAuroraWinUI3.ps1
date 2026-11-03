#Requires -Version 5.1
<#
.SYNOPSIS
    Capture genuine, visible Dvinholm WinUI 3 screenshots from a signed-in
    interactive Windows desktop. This is not a CI/offscreen screenshot.
.EXAMPLE
    pwsh -File eng/Capture-UnifiedAuroraWinUI3.ps1 -Language ru -OutputDirectory C:\AuroraReview -IConfirmOfflineNoSecrets
.NOTES
    Open the reviewed x64 Dvinholm.exe and choose RU or EN in the app first.
    Disconnect all real J2534 devices, clear private VIN/PIN/path data, and
    review images locally before sharing. Never store captures in the repo.
    This script invokes only six existing navigation buttons via UI Automation.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('ru','en')]
    [string]$Language,

    [Parameter(Mandatory=$true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory=$true)]
    [switch]$IConfirmOfflineNoSecrets,

    [ValidateSet('Connection','Updates','CemPin','CemConfiguration','Dtc','Ecu')]
    [string[]]$Pages = @('Connection','Updates','CemPin','CemConfiguration','Dtc','Ecu'),

    [ValidateSet(640,800,980,1280,1440)]
    [int[]]$WidthsDip = @(640,800,980,1280,1440),

    [ValidateRange(480,900)]
    [int]$HeightDip = 700,

    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$CommitSha = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $IConfirmOfflineNoSecrets) {
    throw 'Explicit privacy/offline confirmation is required.'
}
if ($env:CI -eq 'true' -or $env:GITHUB_ACTIONS -eq 'true' -or
    -not [Environment]::UserInteractive) {
    throw 'Real Aurora screenshots require a signed-in interactive Windows desktop. CI is not a GUI acceptance environment.'
}
if (-not $IsWindows -and $PSVersionTable.PSVersion.Major -ge 6) {
    throw 'Windows desktop required.'
}
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Windows desktop required.'
}

$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\','/')
$output = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\','/')
if ($output.Equals($repoRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $output.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Screenshot output must be outside the Git repository to avoid accidental publication.'
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class DvinholmWinUiCaptureNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hwnd, int x, int y,
        int width, int height, bool repaint);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
}
'@

# This PowerShell process must use physical screen coordinates. An existing
# DPI context may reject this call: that is safe only if a reviewed desktop
# session can establish its actual DPI context; otherwise refuse capture.
$setDpi = [DvinholmWinUiCaptureNative]::SetProcessDpiAwarenessContext([IntPtr](-4))
if (-not $setDpi) {
    throw 'Cannot set per-monitor-v2 DPI awareness for trustworthy screenshot dimensions. Start in a fresh PowerShell session.'
}

$windows = @(Get-Process -Name 'Dvinholm' -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero })
if ($windows.Count -ne 1) {
    throw "Expected exactly one visible Dvinholm.exe window, found $($windows.Count). Launch only the reviewed x64 build."
}
$window = $windows[0]
$handle = $window.MainWindowHandle
$root = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
if ($null -eq $root) {
    throw 'Windows UI Automation cannot inspect the Dvinholm window.'
}

$labels = @{
    ru = @{
        Connection='Подключение'; Updates='Обновление'; CemPin='CEM PIN';
        CemConfiguration='Конфигурация CEM'; Dtc='Ошибки (DTC)'; Ecu='Блоки (ECU)'
    }
    en = @{
        Connection='Connection'; Updates='Updates'; CemPin='CEM PIN';
        CemConfiguration='CEM configuration'; Dtc='Faults (DTC)'; Ecu='Modules (ECU)'
    }
}

function Invoke-OnlyNavigation([string]$page) {
    $name = $labels[$Language][$page]
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $button = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        $condition)
    if ($null -eq $button -or
        $button.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) {
        throw "Navigation button '$name' was not found. Choose the indicated language in the app first."
    }
    $pattern = $button.GetCurrentPattern(
        [System.Windows.Automation.InvokePattern]::Pattern)
    if ($null -eq $pattern) {
        throw "Navigation button '$name' has no InvokePattern; no screenshot was taken."
    }
    $pattern.Invoke()
    Start-Sleep -Milliseconds 500
}

if (-not (Test-Path -LiteralPath $output -PathType Container)) {
    [void](New-Item -ItemType Directory -Path $output -Force)
}
$results = @()
foreach ($page in $Pages) {
    Invoke-OnlyNavigation $page
    foreach ($dip in $WidthsDip) {
        $dpi = [DvinholmWinUiCaptureNative]::GetDpiForWindow($handle)
        if ($dpi -lt 96 -or $dpi -gt 480) {
            throw "Unexpected window DPI: $dpi. Refusing mislabelled screenshot."
        }
        $widthPx = [int][Math]::Round($dip * $dpi / 96.0)
        $heightPx = [int][Math]::Round($HeightDip * $dpi / 96.0)
        $work = [System.Windows.Forms.Screen]::FromHandle($handle).WorkingArea
        if ($widthPx -gt $work.Width - 24 -or $heightPx -gt $work.Height - 24) {
            throw "Requested $dip DIP at $dpi DPI cannot fit on this monitor. Reduce HeightsDip or choose a larger monitor."
        }
        $left = $work.Left + 12
        $top = $work.Top + 12
        if (-not [DvinholmWinUiCaptureNative]::MoveWindow(
            $handle, $left, $top, $widthPx, $heightPx, $true)) {
            throw 'MoveWindow failed. Do not label this capture as a responsive-layout result.'
        }
        [void][DvinholmWinUiCaptureNative]::SetForegroundWindow($handle)
        Start-Sleep -Milliseconds 550
        $rect = New-Object DvinholmWinUiCaptureNative+RECT
        if (-not [DvinholmWinUiCaptureNative]::GetWindowRect($handle, [ref]$rect)) {
            throw 'Could not read actual Dvinholm window bounds.'
        }
        $actualWidth = $rect.Right - $rect.Left
        $actualHeight = $rect.Bottom - $rect.Top
        if ([Math]::Abs($actualWidth-$widthPx) -gt 24 -or
            [Math]::Abs($actualHeight-$heightPx) -gt 24) {
            throw "The app/window manager did not accept $dip DIP at $dpi DPI; no misleading screenshot will be saved."
        }
        $foreground = [DvinholmWinUiCaptureNative]::GetForegroundWindow()
        if ([DvinholmWinUiCaptureNative]::GetAncestor($foreground, 2) -ne $handle) {
            throw 'The Dvinholm window is not foreground. Close overlays and retry; capture refused.'
        }
        $filename = "Aurora-$page-$Language-$dip-dip.png"
        $dest = Join-Path $output $filename
        if (Test-Path -LiteralPath $dest) {
            throw "File already exists: $filename. Select a new empty output directory."
        }
        $bitmap = [System.Drawing.Bitmap]::new($actualWidth, $actualHeight)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size,
                    [System.Drawing.CopyPixelOperation]::SourceCopy)
            }
            finally { $graphics.Dispose() }
            $bitmap.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $bitmap.Dispose() }
        $results += [ordered]@{
            file = $filename
            page = $page
            operatorSelectedLanguage = $Language
            requestedWidthDip = $dip
            measuredWidthDip = [Math]::Round($actualWidth * 96.0 / $dpi, 1)
            actualWidthPx = $actualWidth
            actualHeightPx = $actualHeight
            windowDpi = $dpi
            sourceCommitOperatorDeclared = $CommitSha
            screenshotUtc = [DateTime]::UtcNow.ToString('o')
            evidence = 'real desktop viewport only; manual privacy and visual review still required'
        }
        Write-Host "Captured $filename ($actualWidth x $actualHeight physical px at $dpi DPI)"
    }
}
$report = Join-Path $output "Aurora-$Language-capture-manifest.json"
if (Test-Path -LiteralPath $report) {
    throw 'Capture manifest already exists. Select a new empty output directory.'
}
ConvertTo-Json -InputObject @($results) -Depth 5 |
    Set-Content -LiteralPath $report -Encoding UTF8
Write-Host 'Capture complete; no file was uploaded. Review/redact every screenshot before sharing.'
