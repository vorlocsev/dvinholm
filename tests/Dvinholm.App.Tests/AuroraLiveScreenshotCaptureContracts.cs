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
        Assert.Contains("function Assert-WorkspaceScrollToEnd([string]$page)", script);
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
}
