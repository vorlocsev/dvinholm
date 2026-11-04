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
            "SetProcessDpiAwarenessContext", "GetThreadDpiAwarenessContext"
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
        Assert.DoesNotContain("HeightDip", script);
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
