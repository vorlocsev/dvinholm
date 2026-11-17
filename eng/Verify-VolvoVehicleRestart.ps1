#requires -Version 5.1
<#
.SYNOPSIS
    Read-only post-restart proof for a manually selected Volvo reference JPG.
.DESCRIPTION
    Checks a single real, visible Dvinholm WinUI 3 window on the interactive
    Windows desktop. Does not launch/restart Dvinholm, change the selection,
    read VIN, access CAN/J2534, alter user settings or capture screenshots.
    Use BEFORE the nine-model screenshot capture, which changes selections.
.EXAMPLE
    .\eng\Verify-VolvoVehicleRestart.ps1 -ExpectedModelId s80-p2 -Language ru -IConfirmRestartedApplication
.EXAMPLE
    .\eng\Verify-VolvoVehicleRestart.ps1 -ExpectedModelId xc70-p2 -Language en -DataDirectory 'C:\Dvinholm-Review\acceptance-user-data' -IConfirmRestartedApplication
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('xc90-p2','xc70-p2','v70-p2','s60-p2','s80-p2',
                 'xc90-spa','xc70-p3','v70-p3','s80-p3')]
    [string]$ExpectedModelId,
    [Parameter(Mandatory=$true)]
    [ValidateSet('ru','en')]
    [string]$Language,
    [string]$DataDirectory = '',
    [switch]$IConfirmRestartedApplication
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IConfirmRestartedApplication) {
    throw 'Explicit confirmation that Dvinholm was closed and relaunched is required.'
}
if ($env:CI -eq 'true' -or $env:GITHUB_ACTIONS -eq 'true' -or
    -not [Environment]::UserInteractive -or
    [Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or
    $PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Requires the actual interactive Windows PowerShell 5.1 desktop; CI cannot prove restart.'
}
if ([string]::IsNullOrWhiteSpace($DataDirectory)) {
    $DataDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Dvinholm'
}
if (-not [IO.Path]::IsPathRooted($DataDirectory) -or
    $DataDirectory.StartsWith('\\')) {
    throw 'DataDirectory must be an absolute local path, never UNC.'
}
$data = [IO.Path]::GetFullPath($DataDirectory)
$preference = Join-Path $data 'vehicle-model.txt'
if (-not (Test-Path -LiteralPath $preference -PathType Leaf)) {
    throw 'No saved manual vehicle preference exists. Select a model, close and restart the app.'
}
$file = Get-Item -LiteralPath $preference -Force
if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
    $file.Length -lt 1 -or $file.Length -gt 64) {
    throw 'Saved vehicle preference is linked, empty or oversized; refusing restart proof.'
}
$recorded = [IO.File]::ReadAllText($preference).Trim()
if ($recorded -cne $ExpectedModelId) {
    throw 'Saved manual model ID does not equal the expected model. Restart proof failed.'
}

$windows = @(Get-Process -Name 'Dvinholm' -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero })
if ($windows.Count -ne 1) {
    throw 'Expected exactly one visible Dvinholm.exe window; close stale copies.'
}
$window = $windows[0]
$window.Refresh()
if (-not $window.MainWindowTitle.StartsWith('Dvinholm', [StringComparison]::Ordinal)) {
    throw 'The visible window is not identified as Dvinholm.'
}
# Time ordering excludes merely choosing a model in the currently running
# process. The recorded preference must predate this process startup.
$startedUtc = $window.StartTime.ToUniversalTime()
if ($file.LastWriteTimeUtc -gt $startedUtc) {
    throw 'Preference was written AFTER Dvinholm launched. Close and restart before verifying.'
}
if ($startedUtc -gt [DateTime]::UtcNow.AddMinutes(2)) {
    throw 'Process start time is invalid; refusing restart proof.'
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::FromHandle($window.MainWindowHandle)
if ($null -eq $root) {
    throw 'Unable to inspect the real Dvinholm window with UI Automation.'
}
function Find-OnlyUiaAnchor([string]$automationId) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)
    $item = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -eq $item -or $item.Current.IsOffscreen) {
        throw "Required visible UI Automation anchor is missing: $automationId. Open Modules (ECU)."
    }
    return $item
}
$names = @{
    'xc90-p2'='XC90 I · P2'; 'xc70-p2'='XC70 II · P2'
    'v70-p2'='V70 II · P2'; 's60-p2'='S60 I · P2'
    's80-p2'='S80 I · P2'; 'xc90-spa'='XC90 II · SPA'
    'xc70-p3'='XC70 III · P3'; 'v70-p3'='V70 III · P3'
    's80-p3'='S80 II · P3'
}
$selector = Find-OnlyUiaAnchor 'DvinholmVehicleModelSelector'
if ($selector.Current.ControlType -ne [System.Windows.Automation.ControlType]::ComboBox) {
    throw 'Vehicle selector is not a real UIA ComboBox.'
}
$selection = $selector.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)
if ($null -eq $selection) { throw 'Vehicle selector has no UIA SelectionPattern.' }
$chosen = @($selection.Current.GetSelection())
if ($chosen.Count -ne 1 -or $chosen[0].Current.Name -cne $names[$ExpectedModelId]) {
    throw 'Displayed manual model differs from the saved model after process restart.'
}
$caption = Find-OnlyUiaAnchor 'DvinholmVehicleArtworkCaption'
$suffix = if ($Language -eq 'ru') {
    ' · справочный рендер, выбран вручную'
} else {
    ' · reference render, selected manually'
}
$expectedCaption = 'Volvo ' + $names[$ExpectedModelId] + $suffix
if ($caption.Current.Name -cne $expectedCaption) {
    throw 'Visible artwork caption does not match the selected model and language.'
}
if ($caption.Current.HelpText -cne 'DvinholmVehicleAtlasLoaded') {
    throw 'The JPG is not confirmed decoded by WinUI ImageOpened.'
}
# Public output contains only the canonical, manually selected ID and process
# timing facts. Never output VIN, user paths, J2534 metadata or PIN data.
[pscustomobject]@{
    proof = 'local-visible-ui-post-restart'
    modelId = $ExpectedModelId
    language = $Language
    processStartedAfterPreferenceSave = $true
    manualSelectionVerifiedByUia = $true
    jpgDecodedByWinUi = $true
    visualModelAppearanceApproved = $false
    physicalVehicleIdentityVerified = $false
}
