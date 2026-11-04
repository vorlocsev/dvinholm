#Requires -Version 5.1
<#
.SYNOPSIS
    Capture genuine, visible Dvinholm WinUI 3 screenshots from a signed-in
    interactive Windows desktop. This is not a CI/offscreen screenshot.
.EXAMPLE
    powershell.exe -NoProfile -File eng/Capture-UnifiedAuroraWinUI3.ps1 -Language ru -OutputDirectory C:\AuroraReview -IConfirmOfflineNoSecrets
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

    [ValidatePattern('^$|^[0-9a-fA-F]{40}$')]
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
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run with Windows PowerShell 5.1 (powershell.exe); desktop UI Automation assemblies are required.'
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

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    public static extern IntPtr GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    public static extern int GetAwarenessFromDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hwnd, int x, int y,
        int width, int height, bool repaint);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hwnd);

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
if (-not $setDpi -and
    [DvinholmWinUiCaptureNative]::GetAwarenessFromDpiAwarenessContext(
        [DvinholmWinUiCaptureNative]::GetThreadDpiAwarenessContext()) -ne 2) {
    throw 'Per-monitor DPI awareness is unavailable. Start a fresh PowerShell session; do not trust virtualized screenshot dimensions.'
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

# Workspaces have their own localized title, different from some sidebar names.
# Check both title and actual locale before every capture. If UIA cannot prove
# them, do not save a mislabelled screenshot.
$expectedTitles = @{
    ru = @{
        Connection='Подключение'; Updates='Обновление'; CemPin='CEM PIN';
        CemConfiguration='Конфигурация CEM'; Dtc='Ошибки / DTC'; Ecu='Блоки ECU'
    }
    en = @{
        Connection='Connection'; Updates='Updates'; CemPin='CEM PIN';
        CemConfiguration='CEM configuration'; Dtc='Faults (DTC)'; Ecu='Modules (ECU)'
    }
}
$localeNames = @{ ru='Русский'; en='English' }

# UI Automation reports IsOffscreen when another window (notably the
# PowerShell console running this script) covers the app. Bring Dvinholm to
# the foreground BEFORE using IsOffscreen as a screenshot-safety gate.
function Set-CaptureWindowForeground {
    if ([DvinholmWinUiCaptureNative]::IsIconic($handle)) {
        [void][DvinholmWinUiCaptureNative]::ShowWindow($handle, 9) # SW_RESTORE
    }
    [void][DvinholmWinUiCaptureNative]::SetForegroundWindow($handle)
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    $prompted = $false
    do {
        $foreground = [DvinholmWinUiCaptureNative]::GetForegroundWindow()
        if ([DvinholmWinUiCaptureNative]::GetAncestor($foreground, 2) -eq $handle) {
            Start-Sleep -Milliseconds 250
            return
        }
        if (-not $prompted) {
            Write-Host 'If PowerShell is covering Dvinholm, click the Dvinholm window now. Leave this script running; it will continue automatically.' -ForegroundColor Yellow
            $prompted = $true
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw 'Dvinholm did not become the foreground window within 30 seconds. No screenshot was saved; rerun and click Dvinholm while the script is waiting.'
}

$titleCondition = [System.Windows.Automation.PropertyCondition]::new(
    [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
    'DvinholmWorkspaceTitle')
$localeCondition = [System.Windows.Automation.PropertyCondition]::new(
    [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
    'DvinholmLanguageSelector')

function Format-UiaBounds($bounds) {
    if ($null -eq $bounds) { return 'unavailable' }
    return ('x={0}; y={1}; width={2}; height={3}' -f
        $bounds.X, $bounds.Y, $bounds.Width, $bounds.Height)
}

function Get-CaptureDiagnostics(
    [System.Windows.Automation.AutomationElement]$titleElement) {
    $lines = New-Object 'System.Collections.Generic.List[string]'
    $windowRect = New-Object DvinholmWinUiCaptureNative+RECT
    $clientRect = New-Object DvinholmWinUiCaptureNative+RECT
    $clientOrigin = New-Object DvinholmWinUiCaptureNative+POINT
    $gotWindow = [DvinholmWinUiCaptureNative]::GetWindowRect($handle, [ref]$windowRect)
    $gotClient = [DvinholmWinUiCaptureNative]::GetClientRect($handle, [ref]$clientRect)
    $clientOrigin.X = 0
    $clientOrigin.Y = 0
    $gotOrigin = [DvinholmWinUiCaptureNative]::ClientToScreen($handle, [ref]$clientOrigin)
    $foreground = [DvinholmWinUiCaptureNative]::GetForegroundWindow()
    $foregroundRoot = [DvinholmWinUiCaptureNative]::GetAncestor($foreground, 2)
    $dpi = [DvinholmWinUiCaptureNative]::GetDpiForWindow($handle)
    $screen = [System.Windows.Forms.Screen]::FromHandle($handle)
    $work = $screen.WorkingArea
    $monitor = $screen.Bounds

    $lines.Add(('HWND=0x{0:X}; visible={1}; minimized={2}; dpi={3}' -f
        $handle.ToInt64(), [DvinholmWinUiCaptureNative]::IsWindowVisible($handle),
        [DvinholmWinUiCaptureNative]::IsIconic($handle), $dpi))
    $lines.Add(('foreground=0x{0:X}; foregroundRoot=0x{1:X}; foregroundIsDvinholm={2}' -f
        $foreground.ToInt64(), $foregroundRoot.ToInt64(), ($foregroundRoot -eq $handle)))
    if ($gotWindow) {
        $lines.Add(('outerRect=left:{0},top:{1},right:{2},bottom:{3}' -f
            $windowRect.Left, $windowRect.Top, $windowRect.Right, $windowRect.Bottom))
    } else { $lines.Add('outerRect=unavailable') }
    if ($gotClient -and $gotOrigin) {
        $lines.Add(('clientScreenRect=left:{0},top:{1},right:{2},bottom:{3}' -f
            $clientOrigin.X, $clientOrigin.Y,
            ($clientOrigin.X + $clientRect.Right), ($clientOrigin.Y + $clientRect.Bottom)))
    } else { $lines.Add('clientScreenRect=unavailable') }
    $lines.Add(('monitorBounds=left:{0},top:{1},width:{2},height:{3}' -f
        $monitor.Left, $monitor.Top, $monitor.Width, $monitor.Height))
    $lines.Add(('workingArea=left:{0},top:{1},width:{2},height:{3}' -f
        $work.Left, $work.Top, $work.Width, $work.Height))

    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $node = $titleElement
    for ($depth = 0; $depth -lt 8 -and $null -ne $node; $depth++) {
        try {
            $current = $node.Current
            $name = ($current.Name -replace '[\r\n\t]', ' ').Trim()
            if ($name.Length -gt 80) { $name = $name.Substring(0, 80) }
            $bounds = $current.BoundingRectangle
            $lines.Add(('uia[{0}] type={1}; id={2}; name="{3}"; offscreen={4}; bounds=({5})' -f
                $depth, $current.ControlType.ProgrammaticName, $current.AutomationId,
                $name, $current.IsOffscreen, (Format-UiaBounds $bounds)))
            $node = $walker.GetParent($node)
        } catch {
            $lines.Add(('uia[{0}] unavailable: {1}' -f $depth, $_.Exception.Message))
            break
        }
    }
    return ($lines -join [Environment]::NewLine)
}

function Confirm-WorkspaceState([string]$page, [switch]$RequireVisible) {
    Set-CaptureWindowForeground
    $title = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        $titleCondition)
    if ($null -eq $title -or
        $title.Current.ControlType -ne [System.Windows.Automation.ControlType]::Text) {
        throw "Workspace title accessibility ID is not readable; refusing screenshot for $page."
    }
    $expectedTitle = $expectedTitles[$Language][$page]
    $observedTitle = $title.Current.Name
    # Navigation happens before the capture loop positions/resizes the window.
    # At that point the title may legitimately be outside the current viewport.
    # Enforce IsOffscreen only after MoveWindow has placed the final viewport.
    $titleOffscreen = $title.Current.IsOffscreen
    if ($observedTitle -cne $expectedTitle -or
        ($RequireVisible -and $titleOffscreen)) {
        $diagnostics = ''
        if ($RequireVisible -and $titleOffscreen) {
            $diagnostics = [Environment]::NewLine +
                "Capture geometry diagnostics (no screenshot was saved):" +
                [Environment]::NewLine + (Get-CaptureDiagnostics $title)
        }
        throw "Expected visible workspace '$expectedTitle', observed '$observedTitle' (offscreen: $titleOffscreen). No screenshot saved.$diagnostics"
    }

    $locale = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        $localeCondition)
    if ($null -eq $locale -or
        $locale.Current.ControlType -ne [System.Windows.Automation.ControlType]::ComboBox) {
        throw 'Language selector accessibility ID is unavailable; refusing screenshot.'
    }
    $selection = $locale.GetCurrentPattern(
        [System.Windows.Automation.SelectionPattern]::Pattern)
    if ($null -eq $selection) {
        throw 'Language selection cannot be verified by UI Automation; refusing screenshot.'
    }
    $selected = @($selection.Current.GetSelection())
    if ($selected.Count -ne 1 -or
        $selected[0].Current.Name -cne $localeNames[$Language]) {
        throw "Actual selected UI language does not match requested '$Language'; refusing screenshot."
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
    Confirm-WorkspaceState $page
}

# AdaptiveTrigger examines XAML/client width, not the outer window rectangle.
# Windows borders/title-bar pixels must be added when sizing the outer HWND.
# Preflight every selected width before any local image is created.
$previewDpi = [DvinholmWinUiCaptureNative]::GetDpiForWindow($handle)
$previewOuter = New-Object DvinholmWinUiCaptureNative+RECT
$previewClient = New-Object DvinholmWinUiCaptureNative+RECT
if ($previewDpi -lt 96 -or $previewDpi -gt 480 -or
    -not [DvinholmWinUiCaptureNative]::GetWindowRect($handle, [ref]$previewOuter) -or
    -not [DvinholmWinUiCaptureNative]::GetClientRect($handle, [ref]$previewClient)) {
    throw 'Unable to preflight physical DPI and client area; no screenshot was created.'
}
$borderWidth = ($previewOuter.Right - $previewOuter.Left) -
    ($previewClient.Right - $previewClient.Left)
$borderHeight = ($previewOuter.Bottom - $previewOuter.Top) -
    ($previewClient.Bottom - $previewClient.Top)
if ($borderWidth -lt 0 -or $borderHeight -lt 0 -or
    $borderWidth -gt 200 -or $borderHeight -gt 300) {
    throw 'Unexpected non-client frame; cannot guarantee responsive-layout width.'
}
$preflightWorkArea = [System.Windows.Forms.Screen]::FromHandle($handle).WorkingArea
foreach ($requestedDip in $WidthsDip) {
    $neededOuterWidth = [int][Math]::Round($requestedDip * $previewDpi / 96.0) + $borderWidth
    $neededOuterHeight = [int][Math]::Round($HeightDip * $previewDpi / 96.0) + $borderHeight
    if ($neededOuterWidth -gt $preflightWorkArea.Width - 24 -or
        $neededOuterHeight -gt $preflightWorkArea.Height - 24) {
        throw "Requested $requestedDip DIP client width cannot fit on this monitor at $previewDpi DPI. Use a larger monitor or a smaller -WidthsDip set."
    }
}

if (-not (Test-Path -LiteralPath $output -PathType Container)) {
    [void](New-Item -ItemType Directory -Path $output -Force)
}
if (@(Get-ChildItem -LiteralPath $output -Force).Count -ne 0) {
    throw 'Capture directory is not empty; choose a fresh folder to avoid mixing builds or languages.'
}
$results = @()
foreach ($page in $Pages) {
    Invoke-OnlyNavigation $page
    foreach ($dip in $WidthsDip) {
        $dpi = [DvinholmWinUiCaptureNative]::GetDpiForWindow($handle)
        if ($dpi -lt 96 -or $dpi -gt 480) {
            throw "Unexpected window DPI: $dpi. Refusing mislabelled screenshot."
        }
        $requestedClientWidthPx = [int][Math]::Round($dip * $dpi / 96.0)
        $requestedClientHeightPx = [int][Math]::Round($HeightDip * $dpi / 96.0)
        $outerWidthPx = $requestedClientWidthPx + $borderWidth
        $outerHeightPx = $requestedClientHeightPx + $borderHeight
        $work = [System.Windows.Forms.Screen]::FromHandle($handle).WorkingArea
        if ($outerWidthPx -gt $work.Width - 24 -or $outerHeightPx -gt $work.Height - 24) {
            throw "Requested $dip DIP client width at $dpi DPI cannot fit on the current monitor."
        }
        $left = $work.Left + 12
        $top = $work.Top + 12
        if (-not [DvinholmWinUiCaptureNative]::MoveWindow(
            $handle, $left, $top, $outerWidthPx, $outerHeightPx, $true)) {
            throw 'MoveWindow failed. Do not label this capture as a responsive-layout result.'
        }
        Set-CaptureWindowForeground
        Start-Sleep -Milliseconds 300
        $rect = New-Object DvinholmWinUiCaptureNative+RECT
        $client = New-Object DvinholmWinUiCaptureNative+RECT
        if (-not [DvinholmWinUiCaptureNative]::GetWindowRect($handle, [ref]$rect) -or
            -not [DvinholmWinUiCaptureNative]::GetClientRect($handle, [ref]$client)) {
            throw 'Could not read actual Dvinholm outer or client window bounds.'
        }
        $actualWidth = $rect.Right - $rect.Left
        $actualHeight = $rect.Bottom - $rect.Top
        $actualClientWidth = $client.Right - $client.Left
        $actualClientHeight = $client.Bottom - $client.Top
        if ([Math]::Abs($actualClientWidth-$requestedClientWidthPx) -gt 4 -or
            [Math]::Abs($actualClientHeight-$requestedClientHeightPx) -gt 4) {
            throw "The app/window manager did not accept $dip DIP client width at $dpi DPI; refusing misleading viewport screenshot."
        }
        $foreground = [DvinholmWinUiCaptureNative]::GetForegroundWindow()
        if ([DvinholmWinUiCaptureNative]::GetAncestor($foreground, 2) -ne $handle) {
            throw 'The Dvinholm window is not foreground. Close overlays and retry; capture refused.'
        }
        # Recheck after WinUI AdaptiveTrigger reflow before writing any pixels.
        Confirm-WorkspaceState $page -RequireVisible
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
            verifiedVisibleWorkspaceTitle = $expectedTitles[$Language][$page]
            verifiedSelectedLanguage = $localeNames[$Language]
            requestedWidthDip = $dip
            measuredWidthDip = [Math]::Round($actualClientWidth * 96.0 / $dpi, 1)
            measuredClientHeightDip = [Math]::Round($actualClientHeight * 96.0 / $dpi, 1)
            actualClientWidthPx = $actualClientWidth
            actualClientHeightPx = $actualClientHeight
            actualWidthPx = $actualWidth
            actualHeightPx = $actualHeight
            windowDpi = $dpi
            sourceCommitOperatorDeclared = $CommitSha
            screenshotUtc = [DateTime]::UtcNow.ToString('o')
            evidence = 'real desktop viewport only; manual privacy and visual review still required'
        }
        Write-Host "Captured $filename (client $actualClientWidth x $actualClientHeight; outer $actualWidth x $actualHeight px; $dpi DPI)"
    }
}
$report = Join-Path $output "Aurora-$Language-capture-manifest.json"
if (Test-Path -LiteralPath $report) {
    throw 'Capture manifest already exists. Select a new empty output directory.'
}
ConvertTo-Json -InputObject @($results) -Depth 5 |
    Set-Content -LiteralPath $report -Encoding UTF8
Write-Host 'Capture complete; no file was uploaded. Review/redact every screenshot before sharing.'
