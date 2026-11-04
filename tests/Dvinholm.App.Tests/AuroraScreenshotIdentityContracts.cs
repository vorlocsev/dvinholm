using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for identity-safe Aurora screenshot collection.
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
        Assert.Contains("CemConfiguration='Конфигурация CEM'; Dtc='Ошибки / DTC'; Ecu='Блоки ECU'", source);
        foreach (var title in new[] { "Подключение", "CEM PIN", "Конфигурация CEM",
            "Ошибки / DTC", "Блоки ECU" })
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
    public void ActualWorkspaceIsVerifiedAfterNavigationAndEveryResize()
    {
        var capture = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        Assert.Equal(2, capture.Split("Confirm-WorkspaceState $page").Length - 1);
        Assert.Contains("$pattern.Invoke()", capture);
        var nav = capture.IndexOf("function Invoke-OnlyNavigation(", StringComparison.Ordinal);
        var verify = capture.IndexOf("Confirm-WorkspaceState $page", nav, StringComparison.Ordinal);
        var resize = capture.IndexOf("MoveWindow(", verify, StringComparison.Ordinal);
        var again = capture.IndexOf("Confirm-WorkspaceState $page", resize, StringComparison.Ordinal);
        var pixels = capture.IndexOf("$bitmap.Save($dest", again, StringComparison.Ordinal);
        Assert.True(nav >= 0 && verify > nav && resize > verify &&
            again > resize && pixels > again);
        Assert.Contains("verifiedVisibleWorkspaceTitle", capture);
        Assert.Contains("verifiedSelectedLanguage", capture);
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
        Assert.Contains("Click its window once", helper);

        var confirmStart = capture.IndexOf("function Confirm-WorkspaceState", StringComparison.Ordinal);
        var confirmEnd = capture.IndexOf("function Invoke-OnlyNavigation", confirmStart, StringComparison.Ordinal);
        Assert.True(confirmStart >= 0 && confirmEnd > confirmStart);
        var confirm = capture[confirmStart..confirmEnd];
        Assert.True(confirm.IndexOf("Set-CaptureWindowForeground", StringComparison.Ordinal) <
                    confirm.IndexOf("$root.FindFirst", StringComparison.Ordinal));

        var resize = capture.IndexOf("MoveWindow(", StringComparison.Ordinal);
        var resizeForeground = capture.IndexOf("Set-CaptureWindowForeground", resize, StringComparison.Ordinal);
        var resizedConfirm = capture.IndexOf("Confirm-WorkspaceState $page", resizeForeground, StringComparison.Ordinal);
        var pixels = capture.IndexOf("$bitmap.Save($dest", resizedConfirm, StringComparison.Ordinal);
        Assert.True(resize >= 0 && resizeForeground > resize &&
                    resizedConfirm > resizeForeground && pixels > resizedConfirm);
    }

}
