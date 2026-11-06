using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Static offline checks for the opt-in, fixed-viewport interactive Windows capture
/// helper. Parser validation happens in Windows CI; none of these tests claim
/// that a WinUI window actually rendered, or that a screenshot was produced.
/// </summary>
public sealed class AuroraLiveScreenshotCaptureContracts
{
    private static string Source(string relative)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var full = Path.Combine(directory.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full)) return File.ReadAllText(full);
        }
        throw new FileNotFoundException(relative);
    }

    private static string Capture() =>
        Source("eng/Capture-UnifiedAuroraWinUI3.ps1");

    [Fact]
    public void PrCaptureRejectsMismatchedRunningExeBeforeFirstScreenshot()
    {
        var capture = Capture();
        var workflow = Source(".github/workflows/winui3-unified-x64.yml");

        Assert.Contains("artifacts/Aurora-x64-build-evidence.json", capture);
        Assert.Contains("dvinholm.aurora.x64.provenance.v1", capture);
        Assert.Contains("$evidence.sourceHeadCommitSha -cne $CommitSha.ToLowerInvariant()", capture);
        Assert.Contains("$window.Path", capture);
        Assert.Contains("Get-FileHash -LiteralPath $runningExe -Algorithm SHA256", capture);
        Assert.Contains("$runningSha256 -cne $evidence.executableSha256", capture);
        Assert.Contains("STALE DVINHOLM.EXE", capture);
        Assert.Contains("refusing build-labelled screenshots", capture);
        Assert.Contains("Verified running x64 Dvinholm.exe against PR artifact", capture);
        var integrityGate = capture.IndexOf("if (-not [string]::IsNullOrWhiteSpace($CommitSha))",
            StringComparison.Ordinal);
        var firstNavigation = capture.IndexOf("Invoke-OnlyNavigation $page", StringComparison.Ordinal);
        var firstImageSave = capture.IndexOf("$bitmap.Save($dest", StringComparison.Ordinal);
        Assert.True(integrityGate >= 0 && firstNavigation > integrityGate &&
            firstImageSave > firstNavigation);
        Assert.Contains("Write and verify Aurora GUI executable provenance", workflow);
        Assert.Contains("AURORA_PR_HEAD_SHA: ${{ github.event.pull_request.head.sha }}", workflow);
        Assert.Contains("$env:GITHUB_SHA.ToLowerInvariant()", workflow);
        Assert.Contains("Get-FileHash -LiteralPath $exe -Algorithm SHA256", workflow);
        Assert.Contains("$archive.GetEntry('Dvinholm.exe')", workflow);
        Assert.Contains("if ($packedHash -cne $exeSha)", workflow);
        Assert.Contains("artifacts/Aurora-x64-build-evidence.json", workflow);
        Assert.Contains("            artifacts/Aurora-x64-build-evidence.json", workflow);
    }

    [Fact]
    public void ScriptIsExplicitDesktopPowerShellRatherThanCiScreenshotClaim()
    {
        var script = Capture();
        Assert.Contains("#Requires -Version 5.1", script);
        Assert.Contains("Set-StrictMode -Version Latest", script);
        Assert.Contains("PSEdition -ne 'Desktop'", script);
        Assert.Contains("UIAutomationClient", script);
        Assert.Contains("real desktop viewport only", script);
    }

    [Fact]
    public void PrivacyConfirmationAndInteractiveSessionAreRequired()
    {
        var script = Capture();
        Assert.Contains("[switch]$IConfirmOfflineNoSecrets", script);
        Assert.Contains("if (-not $IConfirmOfflineNoSecrets)", script);
        Assert.Contains("[Environment]::UserInteractive", script);
        Assert.Contains("$env:CI -eq 'true'", script);
        Assert.Contains("$env:GITHUB_ACTIONS -eq 'true'", script);
        Assert.Contains("no file was uploaded", script);
    }

    [Fact]
    public void RepositoryScreenshotsAndAlreadyPopulatedOutputAreRejected()
    {
        var script = Capture();
        Assert.Contains("$repoRoot + [IO.Path]::DirectorySeparatorChar", script);
        Assert.Contains("Screenshot output must be outside the Git repository", script);
        Assert.Contains("Get-ChildItem -LiteralPath $output -Force", script);
        Assert.Contains("Capture directory is not empty", script);
        Assert.Contains("File already exists", script);
    }

    [Fact]
    public void AllSixRoutesUseOnlyUserApprovedNavigationButtons()
    {
        var script = Capture();
        foreach (var route in new[] { "Connection", "Updates", "CemPin",
            "CemConfiguration", "Dtc", "Ecu" })
            Assert.Contains(route, script);
        foreach (var name in new[] { "Подключение", "Обновление", "CEM PIN",
            "Конфигурация CEM", "Ошибки (DTC)", "Блоки (ECU)",
            "Faults (DTC)", "Modules (ECU)" })
            Assert.Contains(name, script);
        Assert.Contains("[System.Windows.Automation.InvokePattern]::Pattern", script);
        Assert.Contains("Invoke-OnlyNavigation $page", script);
        Assert.DoesNotContain("Click='Connect'", script);
    }

    [Fact]
    public void CapturesRealForegroundPixelsAtMeasuredWindowDpi()
    {
        var script = Capture();
        foreach (var symbol in new[] {
            "GetDpiForWindow", "GetWindowRect", "GetClientRect",
            "GetForegroundWindow", "GetAncestor", "CopyFromScreen",
            "SetProcessDpiAwarenessContext", "GetThreadDpiAwarenessContext",
            "GetWindowDpiAwarenessContext", "AreDpiAwarenessContextsEqual"
        })
            Assert.Contains(symbol, script);
        Assert.Contains("The Dvinholm window is not foreground", script);
        Assert.Contains("requestedWidthDip", script);
        Assert.Contains("measuredWidthDip", script);
        Assert.Contains("actualWidthPx", script);
        Assert.Contains("actualClientWidthPx", script);
        Assert.Contains("Confirm-WorkspaceState $page", script);
        Assert.Contains("verifiedVisibleWorkspaceTitle", script);
        Assert.Contains("verifiedSelectedLanguage", script);
        Assert.Contains("$fixedWidthDip = 1280", script);
        Assert.Contains("$fixedHeightDip = 800", script);
        Assert.Contains("$requestedClientWidthPx = [int][Math]::Round($fixedWidthDip * $previewDpi / 96.0)", script);
        Assert.Contains("[Math]::Abs($actualClientWidth-$requestedClientWidthPx) -gt 4", script);
        Assert.Contains("fixed viewport 1280x800 DIP", script);
        Assert.Contains("Per-Monitor V2 DPI awareness", script);
        Assert.Contains("$windowDpiContext", script);
        Assert.Contains("[IntPtr](-4)", script);
        Assert.DoesNotContain("MoveWindow", script);
    }

    [Fact]
    public void CaptureUsesOneFixedViewportAndBothLanguages()
    {
        var script = Capture();
        Assert.Contains("ValidateSet('ru','en')", script);
        Assert.Contains("$fixedWidthDip = 1280", script);
        Assert.Contains("$fixedHeightDip = 800", script);
        Assert.DoesNotContain("WidthsDip", script);
        Assert.DoesNotContain("[int]$HeightDip", script);
        Assert.DoesNotContain("MoveWindow", script);
        Assert.Contains("requestedHeightDip", script);
        Assert.Contains("Cannot fit", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("operatorSelectedLanguage", script);
        Assert.Contains("expectedTitles", script);
        Assert.Contains("DvinholmWorkspaceTitle", script);
        Assert.Contains("DvinholmLanguageSelector", script);
        Assert.Contains("sourceCommitOperatorDeclared", script);
    }

    [Fact]
    public void OptionalBottomViewsRequireUiAutomationAndPreserveSixPrimaryScreenshots()
    {
        var script = Capture();
        Assert.Contains("[switch]$CaptureBottomViews", script);
        Assert.Contains("if ($CaptureBottomViews -and -not $VerifyWorkspaceScroll)", script);
        Assert.Contains("-CaptureBottomViews requires -VerifyWorkspaceScroll", script);
        Assert.Contains("$page -in @('CemPin','CemConfiguration','Dtc','Ecu')", script);
        Assert.Contains("Aurora-$page-$Language-$fixedWidthDip-dip-bottom.png", script);
        Assert.Contains("Aurora-$page-$Language-$fixedWidthDip-dip.png", script);
        Assert.Contains("$bottomResults = @()", script);
        Assert.Contains("$bottomResults += [ordered]@{", script);
        Assert.Contains("scrollPosition = 'bottom'", script);
        Assert.Contains("verifiedOuterScrollPercentAtLeast98 = $true", script);
        Assert.Contains("Aurora-$Language-bottom-capture-manifest.json", script);
        Assert.Contains("Aurora-$Language-capture-manifest.json", script);
        Assert.Contains("ConvertTo-Json -InputObject @($results) -Depth 5", script);
        Assert.Contains("ConvertTo-Json -InputObject @($bottomResults) -Depth 5", script);
    }

    [Fact]
    public void BottomPixelsAreTakenAtVerifiedEndBeforeOriginalScrollIsRestored()
    {
        var script = Capture();
        var start = script.IndexOf("function Assert-WorkspaceScrollToEnd(", StringComparison.Ordinal);
        var end = script.IndexOf("function Invoke-OnlyNavigation(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var section = script[start..end];
        var toBottom = section.IndexOf("$scroll.SetScrollPercent($noScroll, [double]100)",
            StringComparison.Ordinal);
        var checkedPercent = section.IndexOf("$atBottom -lt 98", toBottom,
            StringComparison.Ordinal);
        var checkedTitle = section.IndexOf("Confirm-WorkspaceState $page -RequireVisible",
            checkedPercent, StringComparison.Ordinal);
        var recheck = section.IndexOf("$captureScrollPercent = [double]$scroll.Current.VerticalScrollPercent",
            checkedTitle, StringComparison.Ordinal);
        var snapshot = section.IndexOf("$bottomBitmap.Save($bottomDestination",
            recheck, StringComparison.Ordinal);
        var restore = section.IndexOf("$scroll.SetScrollPercent($noScroll, $originalPercent)",
            snapshot, StringComparison.Ordinal);
        Assert.True(toBottom >= 0 && checkedPercent > toBottom &&
            checkedTitle > checkedPercent && recheck > checkedTitle &&
            snapshot > recheck && restore > snapshot);
        Assert.Contains("$captureScrollPercent -lt 98", section);
        Assert.Contains("[Math]::Abs($captureScrollPercent - $atBottom) -gt 1", section);
        Assert.Contains("Test-Path -LiteralPath $bottomDestination", section);
        Assert.Contains("Dvinholm lost foreground before bottom screenshot", section);
        Assert.Contains("CopyFromScreen($captureLeft, $captureTop, 0, 0,", section);
        Assert.Contains("finally { $bottomBitmap.Dispose() }", section);
        Assert.Contains("finally {", section);
        Assert.DoesNotContain("ReadDtc_Click(", section);
        Assert.DoesNotContain("PassThruOpen(", section);
    }

    [Fact]
    public void CaptureParksCursorOutsideClientAreaToDismissHoverTooltips()
    {
        var script = Capture();
        var loop = script.IndexOf("foreach ($page in $Pages)", StringComparison.Ordinal);
        var report = script.IndexOf("$report = Join-Path", loop, StringComparison.Ordinal);
        Assert.True(loop >= 0 && report > loop);
        var section = script[loop..report];

        var park = section.IndexOf(
            "$neutralX = $rect.Left + [int][Math]::Round(180 * $dpi / 96.0)",
            StringComparison.Ordinal);
        var bounds = section.IndexOf("$clientBounds = Get-ClientScreenBounds",
            park, StringComparison.Ordinal);
        var move = section.IndexOf("SetCursorPos($neutralX, $neutralY)",
            bounds, StringComparison.Ordinal);
        var verify = section.IndexOf("GetCursorPos([ref]$parkedCursor)",
            move, StringComparison.Ordinal);
        var wait = section.IndexOf("Start-Sleep -Milliseconds 1100",
            verify, StringComparison.Ordinal);
        var bottom = section.IndexOf("Assert-WorkspaceScrollToEnd $page",
            wait, StringComparison.Ordinal);
        var topPixels = section.IndexOf("$graphics.CopyFromScreen($rect.Left",
            bottom, StringComparison.Ordinal);
        var restore = section.IndexOf(
            "SetCursorPos($savedCursor.X, $savedCursor.Y)",
            topPixels, StringComparison.Ordinal);

        Assert.True(park >= 0 && bounds > park && move > bounds &&
            verify > move && wait > verify && bottom > wait &&
            topPixels > bottom && restore > topPixels);
        Assert.Contains("blank, non-client title-bar area", section);
        Assert.Contains("the app's client region", section);
        Assert.Contains("Cursor did not remain outside the Dvinholm client area", section);
        Assert.Contains("finally", section);
        Assert.DoesNotContain("neutral status strip", section);
    }

    [Fact]
    public void CaptureNeverPublishesImagesOrReadsCarData()
    {
        var script = Capture();
        Assert.DoesNotContain("Invoke-WebRequest", script);
        Assert.DoesNotContain("Invoke-RestMethod", script);
        Assert.DoesNotContain("Upload-Artifact", script);
        Assert.DoesNotContain("git push", script);
        Assert.DoesNotContain("ReadDtc_Click", script);
        Assert.DoesNotContain("PassThru", script);
        Assert.DoesNotContain("ReadCemPin", script);
        Assert.DoesNotContain("ClearDiagnosticInformation", script);
        Assert.Contains("sourceCommitOperatorDeclared", script);
        Assert.Contains("manual privacy and visual review still required", script);
    }

    [Fact]
    public void OptionalScrollProbeChecksOuterWorkspaceAndRestoresPositionWithoutBusActions()
    {
        var script = Capture();
        var xaml = XDocument.Parse(Source("src/Dvinholm.App/MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var scroll = xaml.Descendants().Single(e =>
            (string?)e.Attribute(x + "Name") == "WorkspaceScroll");
        var footer = xaml.Descendants().Single(e =>
            (string?)e.Attribute(x + "Name") == "ShellStatusBar");
        Assert.Equal("DvinholmWorkspaceScroll",
            (string?)scroll.Attribute("AutomationProperties.AutomationId"));
        Assert.Equal("DvinholmShellStatusBar",
            (string?)footer.Attribute("AutomationProperties.AutomationId"));
        Assert.Equal("Control",
            (string?)footer.Attribute("AutomationProperties.AccessibilityView"));
        foreach (var (controlName, id) in new[] {
            ("ShellStatusText", "DvinholmFooterStatusText"),
            ("StatusFooterVersion", "DvinholmFooterVersionText")
        })
        {
            var footerText = xaml.Descendants().Single(e =>
                (string?)e.Attribute(x + "Name") == controlName);
            Assert.Equal("TextBlock", footerText.Name.LocalName);
            Assert.Contains(footer, footerText.Ancestors());
            Assert.Equal(id,
                (string?)footerText.Attribute("AutomationProperties.AutomationId"));
            Assert.Equal("Control",
                (string?)footerText.Attribute("AutomationProperties.AccessibilityView"));
        }

        Assert.Contains("[switch]$VerifyWorkspaceScroll", script);
        Assert.Contains("if ($VerifyWorkspaceScroll)", script);
        Assert.Contains("function Assert-WorkspaceScrollToEnd(", script);
        Assert.Contains("[string]$page,", script);
        Assert.Contains("if ($page -notin @('CemPin','CemConfiguration','Dtc','Ecu'))", script);
        Assert.Contains("TryGetCurrentPattern(", script);
        Assert.Contains("[System.Windows.Automation.ScrollPattern]::Pattern", script);
        Assert.Contains("DvinholmShellStatusBar", script);
        Assert.Contains("DvinholmFooterStatusText", script);
        Assert.Contains("DvinholmFooterVersionText", script);
        var statusIndex = script.IndexOf("'DvinholmFooterStatusText'", StringComparison.Ordinal);
        var versionIndex = script.IndexOf("'DvinholmFooterVersionText'", StringComparison.Ordinal);
        var borderIndex = script.IndexOf("'DvinholmShellStatusBar'", StringComparison.Ordinal);
        Assert.True(statusIndex >= 0 && versionIndex > statusIndex &&
            borderIndex > versionIndex);
        Assert.Contains("-not $candidate.Current.IsOffscreen", script);
        Assert.Contains("Test-ValidUiaBounds $candidate.Current.BoundingRectangle", script);
        Assert.Contains("$footerAvailable = ($null -ne $footer)", script);
        Assert.Contains("Footer UIA evidence NOT RUN for ${page}:", script);
        Assert.Contains("footer NOT RUN.", script);
        Assert.Contains("fixed header (UIA)", script);
        Assert.DoesNotContain("No visible footer UIA peer found for $page", script);
        Assert.Contains("$scroll.SetScrollPercent($noScroll, [double]100)", script);
        Assert.Contains("$atBottom -lt 98", script);
        Assert.Contains("if ($footerAvailable)", script);
        Assert.Contains("Fixed footer moved or became invisible", script);
        Assert.Contains("Confirm-WorkspaceState $page -RequireVisible", script);
        Assert.Contains("$scroll.SetScrollPercent($noScroll, $originalPercent)", script);
        Assert.DoesNotContain("PassThruOpen(", script);
        Assert.DoesNotContain("ReadDtc_Click(", script);
        Assert.DoesNotContain("SetFocus(", script);
    }

    [Fact]
    public void UnpackagedWinUi3ManifestDeclaresPerMonitorV2DpiAwareness()
    {
        var project = XDocument.Parse(Source("src/Dvinholm.App/Dvinholm.App.csproj"));
        Assert.Equal("app.manifest",
            project.Root?.Elements("PropertyGroup")
                .Elements("ApplicationManifest").Single().Value);

        var manifest = XDocument.Parse(Source("src/Dvinholm.App/app.manifest"));
        XNamespace asmV3 = "urn:schemas-microsoft-com:asm.v3";
        XNamespace windowsSettings2005 = "http://schemas.microsoft.com/SMI/2005/WindowsSettings";
        XNamespace windowsSettings2016 = "http://schemas.microsoft.com/SMI/2016/WindowsSettings";
        var settings = manifest.Root?.Element(asmV3 + "application")
            ?.Element(asmV3 + "windowsSettings");

        Assert.NotNull(settings);
        Assert.Equal("true/pm", settings!.Element(windowsSettings2005 + "dpiAware")?.Value);
        Assert.Equal("PerMonitorV2",
            settings.Element(windowsSettings2016 + "dpiAwareness")?.Value);
    }

    [Theory]
    [InlineData(96, 1280, 800)]
    [InlineData(120, 1600, 1000)]
    [InlineData(144, 1920, 1200)]
    [InlineData(192, 2560, 1600)]
    public void FixedAuroraViewportConvertsDipToPhysicalPixelsAtDisplayDpi(
        int dpi, int expectedWidthPx, int expectedHeightPx)
    {
        var actualWidthPx = (int)Math.Round(1280 * dpi / 96.0);
        var actualHeightPx = (int)Math.Round(800 * dpi / 96.0);

        Assert.Equal(expectedWidthPx, actualWidthPx);
        Assert.Equal(expectedHeightPx, actualHeightPx);
        Assert.Equal(1280, (int)Math.Round(actualWidthPx * 96.0 / dpi));
        Assert.Equal(800, (int)Math.Round(actualHeightPx * 96.0 / dpi));
    }

    [Fact]
    public void RealAppStillHasExactlySixSidebarRoutesAndNoSettings()
    {
        var main = Source("src/Dvinholm.App/MainWindow.xaml");
        var xaml = XDocument.Parse(main);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var sidebar = xaml.Descendants().Single(e =>
            (string?)e.Attribute(x + "Name") == "SidebarNavPanel");
        var buttons = sidebar.Elements().Where(e => e.Name.LocalName == "Button").ToArray();
        Assert.Equal(6, buttons.Length);
        Assert.Equal(5, buttons.Count(e => (string?)e.Attribute("Click") == "WorkspaceNavigate_Click"));
        Assert.Equal("UnifiedUpdatesNavigate_Click",
            (string?)buttons.Single(e => (string?)e.Attribute(x + "Name") == "SidebarUpdatesButton").Attribute("Click"));
        Assert.DoesNotContain(xaml.Descendants(), e =>
            (string?)e.Attribute(x + "Name") == "SettingsPage");

        // Stage-40 spacing correction: long RU/EN action labels must wrap,
        // while their existing Click and disabled-state contracts remain.
        var actionStyle = xaml.Descendants().Single(e =>
            e.Name.LocalName == "Style" &&
            (string?)e.Attribute(x + "Key") == "AuroraActionCaptionButton");
        Assert.Contains(actionStyle.Descendants(), e =>
            e.Name.LocalName == "Setter" &&
            (string?)e.Attribute("Property") == "ContentTemplate" &&
            (string?)e.Attribute("Value") == "{StaticResource AuroraWrapCaption}");
        foreach (var name in new[] {
            "P2ConfigCatalogButton", "CemConfigReadButton", "CemConfigBackupButton",
            "CemConfigCompareButton", "CemConfigPrepareButton", "CemConfigWriteButton",
            "EcuDiagnosticsNavigateButton", "EcuMonitorNavigateButton",
            "EcuProfilesNavigateButton", "ReadDtcButton", "DtcClearButton"
        })
        {
            var control = xaml.Descendants().Single(e =>
                (string?)e.Attribute(x + "Name") == name);
            Assert.Equal("{StaticResource AuroraActionCaptionButton}",
                (string?)control.Attribute("Style"));
        }
    }

    [Fact]
    public void OnePerInvocationCaptureTokenIsWrittenToBothTopAndBottomManifests()
    {
        var script = Capture();
        Assert.Contains("$captureSessionId = [guid]::NewGuid().ToString('D')", script);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(
            script, @"captureSessionId = \$captureSessionId").Count);
        var tokenCreation = script.IndexOf("$captureSessionId = [guid]::NewGuid().ToString('D')",
            StringComparison.Ordinal);
        var pageLoop = script.IndexOf("foreach ($page in $Pages)", StringComparison.Ordinal);
        var bottom = script.IndexOf("scrollPosition = 'bottom'", pageLoop,
            StringComparison.Ordinal);
        var top = script.IndexOf("measuredWidthDip =", bottom, StringComparison.Ordinal);
        Assert.True(tokenCreation >= 0 && pageLoop > tokenCreation &&
            bottom > pageLoop && top > bottom);
    }

    [Fact]
    public void CaptureSessionTokenDoesNotAuthorizeDiagnosticActions()
    {
        var script = Capture();
        Assert.Contains("not signed identity evidence, authentication", script);
        Assert.DoesNotContain("PassThruOpen(", script);
        Assert.DoesNotContain("ReadCemPin(", script);
        Assert.DoesNotContain("Invoke-WebRequest", script);
    }


    // Stage 543: genuine first-fold checks, not a visual acceptance claim.
    [Fact]
    public void OptionalTopFoldProbeReadsOnlyOuterWorkspaceScrollPattern()
    {
        var script = Capture();
        var begin = script.IndexOf("function Assert-WorkspaceTopBeforePixels(", StringComparison.Ordinal);
        var end = script.IndexOf("function Invoke-OnlyNavigation(", begin, StringComparison.Ordinal);
        Assert.True(begin >= 0 && end > begin);
        var probe = script[begin..end];
        Assert.Contains("'DvinholmWorkspaceScroll'", probe);
        Assert.Contains("[System.Windows.Automation.ScrollPattern]::Pattern", probe);
        Assert.Contains("$scroll.Current.VerticallyScrollable", probe);
        Assert.Contains("$scroll.Current.HorizontallyScrollable", probe);
        Assert.Contains("$atTop -gt 2", probe);
        Assert.Contains("$afterIdentity -gt 2", probe);
        Assert.Contains("Confirm-WorkspaceState $page -RequireVisible", probe);
        Assert.DoesNotContain("SetScrollPercent(", probe);
        Assert.DoesNotContain("PassThru", probe);
    }

    [Fact]
    public void TopFoldIsVerifiedAfterBottomRestorationButBeforeTopPngSave()
    {
        var script = Capture();
        var loop = script.IndexOf("foreach ($page in $Pages)", StringComparison.Ordinal);
        var bottom = script.IndexOf("Assert-WorkspaceScrollToEnd $page", loop, StringComparison.Ordinal);
        var topCheck = script.IndexOf("$topFoldVerified = Assert-WorkspaceTopBeforePixels $page",
            bottom, StringComparison.Ordinal);
        var png = script.IndexOf("$bitmap.Save($dest", topCheck, StringComparison.Ordinal);
        var manifest = script.IndexOf("verifiedOuterScrollPercentAtMost2 = $topFoldVerified",
            png, StringComparison.Ordinal);
        Assert.True(loop >= 0 && bottom > loop && topCheck > bottom &&
            png > topCheck && manifest > png);
        Assert.Contains("if ($VerifyWorkspaceScroll -and", script);
        Assert.Contains("$topFoldVerified = $false", script);
    }

    [Fact]
    public void TopFoldProbeNeverChangesUserScrollPositionOrUsesVehicleActions()
    {
        var script = Capture();
        var begin = script.IndexOf("function Assert-WorkspaceTopBeforePixels(", StringComparison.Ordinal);
        var end = script.IndexOf("function Invoke-OnlyNavigation(", begin, StringComparison.Ordinal);
        Assert.True(begin >= 0 && end > begin);
        var probe = script[begin..end];
        Assert.DoesNotContain("SetScrollPercent", probe);
        Assert.DoesNotContain("ChangeView(", probe);
        Assert.DoesNotContain("ReadDtc_Click", probe);
        Assert.DoesNotContain("ReadCemPin", probe);
        Assert.DoesNotContain("SetFocus(", probe);
        Assert.DoesNotContain("InvokePattern", probe);
        Assert.Contains("return $true", probe);
    }

}
