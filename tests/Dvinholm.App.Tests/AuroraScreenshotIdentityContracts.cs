using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Offline contracts for identity-safe Aurora screenshot collection.
/// They are source checks, not actual UI Automation or pixel acceptance.
/// </summary>
public sealed class AuroraScreenshotIdentityContracts
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }

        throw new FileNotFoundException(relative);
    }

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraScreenshotIdentityContracts).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml is not embedded.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(element => (string?)element.Attribute(X + "Name") == name);

    [Fact]
    public void WorkspaceTitleHasStableUiaIdWithoutASecondCompetingControl()
    {
        var doc = Layout();
        var title = Named(doc, "WorkspaceTitle");
        Assert.Equal("TextBlock", title.Name.LocalName);
        Assert.Equal("DvinholmWorkspaceTitle",
            (string?)title.Attribute("AutomationProperties.AutomationId"));
        Assert.Single(doc.Descendants(), element =>
            (string?)element.Attribute("AutomationProperties.AutomationId")
                == "DvinholmWorkspaceTitle");
        Assert.Contains(Named(doc, "WorkspaceHeaderGrid"), title.Ancestors());
    }

    [Fact]
    public void WorkspaceHeaderContainerExposesLocalizedUiaVisibilityAnchor()
    {
        var doc = Layout();
        var header = Named(doc, "WorkspaceHeaderCopy");
        Assert.Equal("DvinholmWorkspaceHeaderCopy",
            (string?)header.Attribute("AutomationProperties.AutomationId"));
        Assert.Equal("Control",
            (string?)header.Attribute("AutomationProperties.AccessibilityView"));

        var application = Source("src/Dvinholm.App/MainWindow.xaml.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        var titleAssignment = application.IndexOf("WorkspaceTitle.Text = section.Title;", StringComparison.Ordinal);
        var anchorName = application.IndexOf(
            "AutomationProperties.SetName(\n            WorkspaceHeaderCopy, section.Title);",
            titleAssignment, StringComparison.Ordinal);
        Assert.True(titleAssignment >= 0 && anchorName > titleAssignment);
    }

    [Fact]
    public void UpdatesAndAllLocalizedWorkspacesKeepUiaHeaderIdentityInSync()
    {
        var source = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        var routeStart = source.IndexOf("private void ApplyUnifiedWorkspaceSection(", StringComparison.Ordinal);
        var updatesStart = source.IndexOf("private void UpdateUnifiedWorkspaceHeader()", routeStart, StringComparison.Ordinal);
        var nextStart = source.IndexOf("private void SetUnifiedStatus(", updatesStart, StringComparison.Ordinal);
        Assert.True(routeStart >= 0 && updatesStart > routeStart && nextStart > updatesStart);

        const string synchronizedName =
            "AutomationProperties.SetName(WorkspaceHeaderCopy, WorkspaceTitle.Text);";
        var route = source[routeStart..updatesStart];
        var updates = source[updatesStart..nextStart];

        // Applying a page route and switching RU/EN must both publish the
        // actual visible title to the same UIA header checked by Capture.
        var routeTitle = route.IndexOf("WorkspaceTitle.Text = _unifiedEnglish ?", StringComparison.Ordinal);
        var routeAnchor = route.IndexOf(synchronizedName, StringComparison.Ordinal);
        var updateTitle = updates.IndexOf("WorkspaceTitle.Text = L(", StringComparison.Ordinal);
        var updateAnchor = updates.IndexOf(synchronizedName, StringComparison.Ordinal);
        Assert.True(routeTitle >= 0 && routeAnchor > routeTitle);
        Assert.True(updateTitle >= 0 && updateAnchor > updateTitle);
        Assert.Contains("if (_unifiedUpdatesActive)\n            UpdateUnifiedWorkspaceHeader();",
            source.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Contains("else if (_workspaceDestination is AuroraDestination current)\n            ApplyUnifiedWorkspaceSection(current);",
            source.Replace("\r\n", "\n", StringComparison.Ordinal));

        var capture = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        Assert.Contains("$headerName -cne $expectedTitle", capture);
        Assert.Contains("Workspace header identity mismatch", capture);
    }

    [Fact]
    public void LanguageSelectorHasUniqueUiaIdAndBothActualChoices()
    {
        var doc = Layout();
        var selector = Named(doc, "UnifiedLanguageSelector");
        Assert.Equal("ComboBox", selector.Name.LocalName);
        Assert.Equal("DvinholmLanguageSelector",
            (string?)selector.Attribute("AutomationProperties.AutomationId"));
        Assert.Single(doc.Descendants(), element =>
            (string?)element.Attribute("AutomationProperties.AutomationId")
                == "DvinholmLanguageSelector");
        Assert.Equal(new[] { "ru", "en" },
            selector.Elements().Select(e => (string?)e.Attribute("Tag")));
        Assert.Equal(new[] { "Русский", "English" },
            selector.Elements().Select(e => (string?)e.Attribute("Content")));
    }

    [Fact]
    public void ScreenshotMapsAllSixRussianWorkspaceTitlesNotSidebarAliases()
    {
        var source = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        var app = Source("src/Dvinholm.App/AuroraWorkspaceNavigation.cs");
        Assert.Contains("Connection='Подключение'; Updates='Обновление'; CemPin='CEM PIN'", source);
        Assert.Contains("CemConfiguration='Конфигурация CEM'; Dtc='Ошибки (DTC)'; Ecu='Блоки ECU'", source);
        foreach (var title in new[] { "Подключение", "CEM PIN", "Конфигурация CEM",
            "Ошибки (DTC)", "Блоки ECU" })
            Assert.Contains($"\"{title}\"", app);
        Assert.Contains("UpdateUnifiedWorkspaceHeader()", Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs"));
    }

    [Fact]
    public void ScreenshotMapsActualEnglishWorkspaceTitleTranslations()
    {
        var capture = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        var application = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        foreach (var title in new[] { "Connection", "CEM PIN", "CEM configuration",
            "Faults (DTC)", "Modules (ECU)", "Updates" })
        {
            Assert.Contains($"'{title}'", capture);
            Assert.Contains($"\"{title}\"", application);
        }
        Assert.Contains("WorkspaceTitle.Text = _unifiedEnglish ? destination switch", application);
    }

    [Fact]
    public void ActualSelectedLanguageMustBeReadFromUiaSelection()
    {
        var capture = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        Assert.Contains("[System.Windows.Automation.SelectionPattern]::Pattern", capture);
        Assert.Contains("$selection.Current.GetSelection()", capture);
        Assert.Contains("$selected.Count -ne 1", capture);
        Assert.Contains("$selected[0].Current.Name -cne $localeNames[$Language]", capture);
        Assert.Contains("Actual selected UI language does not match requested", capture);
        Assert.DoesNotContain("UnifiedLanguageSelector_SelectionChanged(", capture);
    }

    [Fact]
    public void OffscreenOrWrongWorkspaceTitleRefusesCapture()
    {
        var capture = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        Assert.Contains("[System.Windows.Automation.AutomationElement]::AutomationIdProperty", capture);
        Assert.Contains("'DvinholmWorkspaceTitle'", capture);
        Assert.Contains("$observedTitle -cne $expectedTitle", capture);
        Assert.Contains("$title.Current.IsOffscreen", capture);
        Assert.Contains("No screenshot saved", capture);
        Assert.Contains("refusing screenshot for $page", capture);
    }

    [Fact]
    public void ActualWorkspaceIsVerifiedAtOneFixedViewportWithoutResizing()
    {
        var capture = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        // Navigation always checks identity, and pixel capture always makes
        // its own visible-title check. The optional scroll probe may check
        // the same identity again without weakening either mandatory gate.
        var navStart = capture.IndexOf("function Invoke-OnlyNavigation(", StringComparison.Ordinal);
        var preflightStart = capture.IndexOf("# Preflight the fixed viewport", navStart, StringComparison.Ordinal);
        var loopStart = capture.IndexOf("foreach ($page in $Pages)", preflightStart, StringComparison.Ordinal);
        var reportStart = capture.IndexOf("$report = Join-Path $output", loopStart, StringComparison.Ordinal);
        Assert.True(navStart >= 0 && preflightStart > navStart &&
            loopStart > preflightStart && reportStart > loopStart);
        var navigation = capture[navStart..preflightStart];
        var captureLoop = capture[loopStart..reportStart];
        Assert.Equal(1, navigation.Split("Confirm-WorkspaceState $page").Length - 1);
        Assert.Contains("Confirm-WorkspaceState $page -RequireVisible", captureLoop);
        Assert.Contains("if ($VerifyWorkspaceScroll)", captureLoop);
        Assert.Contains("$fixedWidthDip = 1280", capture);
        Assert.Contains("$fixedHeightDip = 800", capture);
        Assert.Contains("requestedHeightDip = $fixedHeightDip", capture);
        Assert.DoesNotContain("MoveWindow(", capture);
        Assert.DoesNotContain("$WidthsDip", capture);
        Assert.Contains("$pattern.Invoke()", capture);
        var nav = capture.IndexOf("function Invoke-OnlyNavigation(", StringComparison.Ordinal);
        var verify = capture.IndexOf("Confirm-WorkspaceState $page", nav, StringComparison.Ordinal);
        var finalVerify = capture.IndexOf("Confirm-WorkspaceState $page -RequireVisible", verify, StringComparison.Ordinal);
        var pixels = capture.IndexOf("$bitmap.Save($dest", finalVerify, StringComparison.Ordinal);
        Assert.True(nav >= 0 && verify > nav && finalVerify > verify && pixels > finalVerify);
        Assert.Contains("verifiedVisibleWorkspaceTitle", capture);
        Assert.Contains("verifiedSelectedLanguage", capture);
    }

    [Fact]
    public void IdleStatusFooterChangesLanguageWithoutRewritingDiagnostics()
    {
        var updates = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        var application = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var xaml = Layout();
        Assert.Equal("{Binding Text, ElementName=StatusText}",
            (string?)Named(xaml, "ShellStatusText").Attribute("Text"));
        Assert.Contains("StatusText.Text = LocalizeAuroraIdleStatus(StatusText.Text);", updates);
        Assert.Contains("text is \"Disconnected\" or \"Не подключено\"", updates);
        Assert.Contains("? L(\"Не подключено\", \"Disconnected\") : text;", updates);
        Assert.Contains("StatusText.Text = LocalizeAuroraIdleStatus(text);", application);
        Assert.Contains("AuroraLanding.UpdateActivity(text);", application);
    }

    [Fact]
    public void ScreenshotCaptureParksAndRestoresCursorAroundVisiblePixelCapture()
    {
        var capture = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        var save = capture.IndexOf("$bitmap.Save($dest", StringComparison.Ordinal);
        var preserve = capture.IndexOf("GetCursorPos([ref]$savedCursor)", StringComparison.Ordinal);
        var bounds = capture.IndexOf("$clientBounds = Get-ClientScreenBounds", preserve, StringComparison.Ordinal);
        var park = capture.IndexOf("SetCursorPos($neutralX, $neutralY)", bounds, StringComparison.Ordinal);
        var parked = capture.IndexOf("GetCursorPos([ref]$parkedCursor)", park, StringComparison.Ordinal);
        var settle = capture.IndexOf("Start-Sleep -Milliseconds 1100", parked, StringComparison.Ordinal);
        var verify = capture.IndexOf("Confirm-WorkspaceState $page -RequireVisible", settle, StringComparison.Ordinal);
        var restore = capture.IndexOf("SetCursorPos($savedCursor.X, $savedCursor.Y)", StringComparison.Ordinal);
        Assert.True(preserve >= 0 && bounds > preserve && park > bounds &&
            parked > park && settle > parked && verify > settle &&
            save > verify && restore > save);
        Assert.Contains("blank, non-client title-bar area", capture);
        Assert.Contains("the app's client region", capture);
        Assert.Contains("Cursor did not remain outside the Dvinholm client area", capture);
        Assert.Contains("finally {", capture);
        Assert.DoesNotContain("status strip", capture);
        Assert.DoesNotContain("MoveWindow(", capture);
    }

    [Fact]
    public void MainWindowUsesFixedDpiAwareNonResizableAuroraViewport()
    {
        var application = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("FixedAuroraClientWidthDip = 1280", application);
        Assert.Contains("FixedAuroraClientHeightDip = 800", application);
        Assert.Contains("ConfigureFixedAuroraWindow();", application);
        Assert.Contains("presenter.IsResizable = false;", application);
        Assert.Contains("presenter.IsMaximizable = false;", application);
        Assert.Contains("presenter.IsMinimizable = true;", application);
        Assert.Contains("GetDpiForWindow(handle)", application);
        Assert.Contains("DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)", application);
        Assert.Contains("AppWindow.Resize(new Windows.Graphics.SizeInt32(outerWidth, outerHeight))", application);
        Assert.Contains("AppWindow.Move(new Windows.Graphics.PointInt32(", application);
        var initialize = application.IndexOf("InitializeComponent();", StringComparison.Ordinal);
        var configure = application.IndexOf("ConfigureFixedAuroraWindow();", initialize, StringComparison.Ordinal);
        var navigation = application.IndexOf("AuroraLanding.UseSharedNavigation();", configure, StringComparison.Ordinal);
        Assert.True(initialize >= 0 && configure > initialize && navigation > configure);
    }

    [Fact]
    public void ProofIsOnlyLocalVisiblePixelsAndNeverChangesVehicle()
    {
        var capture = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        var workflow = Source(".github/workflows/winui3-unified-x64.yml");
        Assert.Contains("$env:GITHUB_ACTIONS -eq 'true'", capture);
        Assert.Contains("IConfirmOfflineNoSecrets", capture);
        Assert.Contains("GetClientRect", capture);
        Assert.Contains("CopyFromScreen", capture);
        Assert.Contains("Screenshot output must be outside the Git repository", capture);
        Assert.DoesNotContain("ReadDtc_Click", capture);
        Assert.DoesNotContain("Connect_Click", capture);
        Assert.DoesNotContain("ReadCemPin", capture);
        Assert.DoesNotContain("Invoke-WebRequest", capture);
        Assert.Contains("Parse Aurora desktop helpers in Windows PowerShell 5.1 (never execute)", workflow);
        Assert.DoesNotContain("powershell.exe -NoProfile -File eng/Capture", workflow);
    }
    [Fact]
    public void CaptureRestoresAndForegroundsDvinholmBeforeCheckingOffscreenState()
    {
        var capture = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        Assert.Contains("public static extern bool ShowWindow", capture);
        Assert.Contains("public static extern bool IsIconic", capture);
        var helperStart = capture.IndexOf("function Set-CaptureWindowForeground", StringComparison.Ordinal);
        var helperEnd = capture.IndexOf("$titleCondition", helperStart, StringComparison.Ordinal);
        Assert.True(helperStart >= 0 && helperEnd > helperStart);
        var helper = capture[helperStart..helperEnd];
        Assert.Contains("IsIconic($handle)", helper);
        Assert.Contains("ShowWindow($handle, 9)", helper);
        Assert.Contains("SetForegroundWindow($handle)", helper);
        Assert.Contains("GetForegroundWindow()", helper);
        Assert.Contains("GetAncestor($foreground, 2)", helper);
        Assert.Contains("AddSeconds(30)", helper);
        Assert.Contains("click the Dvinholm window now", helper);
        Assert.Contains("Leave this script running", helper);
        Assert.Contains("click Dvinholm while the script is waiting", helper);

        Assert.Contains("function Confirm-WorkspaceState([string]$page, [switch]$RequireVisible)", capture);
        var confirmStart = capture.IndexOf("function Confirm-WorkspaceState", StringComparison.Ordinal);
        var confirmEnd = capture.IndexOf("function Invoke-OnlyNavigation", confirmStart, StringComparison.Ordinal);
        Assert.True(confirmStart >= 0 && confirmEnd > confirmStart);
        var confirm = capture[confirmStart..confirmEnd];
        Assert.True(confirm.IndexOf("Set-CaptureWindowForeground", StringComparison.Ordinal) <
                    confirm.IndexOf("$root.FindFirst", StringComparison.Ordinal));
        Assert.Contains("$titleOffscreen = $title.Current.IsOffscreen", confirm);
        Assert.Contains("($RequireVisible -and -not ($titleVisible -or $headerVisible))", confirm);
        Assert.Contains("$headerName -cne $expectedTitle", confirm);
        Assert.Contains("Test-BoundsIntersectClient $headerBounds $clientBounds", confirm);

        var navigationStart = confirmEnd;
        var navigationEnd = capture.IndexOf("$previewDpi", navigationStart, StringComparison.Ordinal);
        Assert.True(navigationEnd > navigationStart);
        var navigation = capture[navigationStart..navigationEnd];
        Assert.Contains("Confirm-WorkspaceState $page", navigation);
        Assert.DoesNotContain("Confirm-WorkspaceState $page -RequireVisible", navigation);

        var captureLoop = capture.IndexOf("foreach ($page in $Pages)", StringComparison.Ordinal);
        var foreground = capture.IndexOf("Set-CaptureWindowForeground", captureLoop, StringComparison.Ordinal);
        var visibleConfirm = capture.IndexOf("Confirm-WorkspaceState $page -RequireVisible", foreground, StringComparison.Ordinal);
        var pixels = capture.IndexOf("$bitmap.Save($dest", visibleConfirm, StringComparison.Ordinal);
        Assert.DoesNotContain("MoveWindow(", capture);
        Assert.True(captureLoop >= 0 && foreground > captureLoop &&
                    visibleConfirm > foreground && pixels > visibleConfirm);
    }

    [Fact]
    public void OffscreenRefusalReportsUiaAndWin32GeometryWithoutSavingImage()
    {
        var capture = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        Assert.Contains("public struct POINT", capture);
        Assert.Contains("public static extern bool ClientToScreen", capture);
        Assert.Contains("public static extern bool IsWindowVisible", capture);

        var diagnosticStart = capture.IndexOf("function Get-CaptureDiagnostics", StringComparison.Ordinal);
        var diagnosticEnd = capture.IndexOf("function Confirm-WorkspaceState", diagnosticStart, StringComparison.Ordinal);
        Assert.True(diagnosticStart >= 0 && diagnosticEnd > diagnosticStart);
        var diagnostic = capture[diagnosticStart..diagnosticEnd];
        Assert.Contains("GetWindowRect", diagnostic);
        Assert.Contains("GetClientRect", diagnostic);
        Assert.Contains("ClientToScreen", diagnostic);
        Assert.Contains("GetDpiForWindow", diagnostic);
        Assert.Contains("WorkingArea", diagnostic);
        Assert.Contains("TreeWalker]::ControlViewWalker", diagnostic);
        Assert.Contains("BoundingRectangle", diagnostic);
        Assert.Contains("IsOffscreen", diagnostic);
        Assert.Contains("foregroundIsDvinholm", diagnostic);
        Assert.Contains("headerAnchor name=", diagnostic);
        Assert.DoesNotContain("$bitmap.Save", diagnostic);

        var confirmStart = diagnosticEnd;
        var confirmEnd = capture.IndexOf("function Invoke-OnlyNavigation", confirmStart, StringComparison.Ordinal);
        Assert.True(confirmEnd > confirmStart);
        var confirm = capture[confirmStart..confirmEnd];
        var refusal = confirm.IndexOf("($RequireVisible -and -not ($titleVisible -or $headerVisible))", StringComparison.Ordinal);
        var report = confirm.IndexOf("Get-CaptureDiagnostics $title", StringComparison.Ordinal);
        var throwIndex = confirm.IndexOf("throw \"Expected visible workspace", StringComparison.Ordinal);
        Assert.True(refusal >= 0 && report > refusal && throwIndex > report);
        Assert.Contains("Capture geometry diagnostics (no screenshot was saved)", confirm);
    }

}
