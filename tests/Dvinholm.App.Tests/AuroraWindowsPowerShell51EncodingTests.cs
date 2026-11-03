namespace Dvinholm.App.Tests;

/// <summary>
/// Eight static safety and compatibility checks for interactive Aurora
/// review scripts. Neither helper is executed and no GUI is captured.
/// </summary>
public sealed class AuroraWindowsPowerShell51EncodingTests
{
    [Theory]
    [InlineData("eng/Capture-UnifiedAuroraWinUI3.ps1")]
    [InlineData("eng/Review-UnifiedAuroraScreenshots.ps1")]
    public void OperatorScriptsRequireUtf8BomForWindowsPowerShell51(string path)
    {
        var bytes = File.ReadAllBytes(PathInRepo(path));
        Assert.True(bytes.Length >= 3, "PowerShell helper is empty.");
        Assert.Equal((byte)0xEF, bytes[0]);
        Assert.Equal((byte)0xBB, bytes[1]);
        Assert.Equal((byte)0xBF, bytes[2]);
    }

    [Fact]
    public void CaptureRetainsActualRussianNavigationAndPageTitleLiterals()
    {
        var text = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        foreach (var phrase in new[] {
            "Подключение", "Обновление", "Конфигурация CEM",
            "Ошибки (DTC)", "Ошибки / DTC",
            "Блоки (ECU)", "Блоки ECU", "Русский"
        })
            Assert.Contains(phrase, text);
        Assert.Contains("DvinholmWorkspaceTitle", text);
        Assert.Contains("DvinholmLanguageSelector", text);
    }

    [Fact]
    public void ReviewRetainsRussianTitlesAndDoesNotChangeDpiEvidence()
    {
        var text = Source("eng/Review-UnifiedAuroraScreenshots.ps1");
        foreach (var phrase in new[] {
            "Подключение", "Обновление", "Конфигурация CEM",
            "Ошибки / DTC", "Блоки ECU", "Русский"
        })
            Assert.Contains(phrase, text);
        Assert.Contains("actualClientWidthPx", text);
        Assert.Contains("windowDpi", text);
        Assert.Contains("DPI mismatch", text);
    }

    [Fact]
    public void BothHelpersExplicitlyTargetDesktopPowerShell51()
    {
        foreach (var path in new[] {
            "eng/Capture-UnifiedAuroraWinUI3.ps1",
            "eng/Review-UnifiedAuroraScreenshots.ps1"
        })
        {
            var text = Source(path);
            Assert.Contains("#Requires -Version 5.1", text);
            Assert.Contains("PSEdition -ne 'Desktop'", text);
        }
    }

    [Fact]
    public void NewX64PilotUsesRealWindowsPowerShell51ParserAndBytePreflight()
    {
        var workflow = Source(".github/workflows/winui3-unified-x64.yml");
        var start = workflow.IndexOf("- name: Parse Aurora desktop helpers in Windows PowerShell 5.1", StringComparison.Ordinal);
        var end = workflow.IndexOf("# New pipeline only:", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var guard = workflow[start..end];
        Assert.Contains("shell: powershell", guard);
        Assert.DoesNotContain("shell: pwsh", guard);
        Assert.Contains("$PSVersionTable.PSVersion.Major -ne 5", guard);
        Assert.Contains("$PSVersionTable.PSEdition -ne 'Desktop'", guard);
        Assert.Contains("$bytes[0] -ne 0xEF", guard);
        Assert.Contains("$bytes[1] -ne 0xBB", guard);
        Assert.Contains("$bytes[2] -ne 0xBF", guard);
        Assert.Contains("[System.Management.Automation.Language.Parser]::ParseFile(", guard);
        Assert.Contains("eng/Capture-UnifiedAuroraWinUI3.ps1", guard);
        Assert.Contains("eng/Review-UnifiedAuroraScreenshots.ps1", guard);
    }

    [Fact]
    public void PilotStillPackagesRealHelpersButDoesNotExecuteOrUploadScreenshots()
    {
        var workflow = Source(".github/workflows/winui3-unified-x64.yml");
        Assert.Contains("eng/Capture-UnifiedAuroraWinUI3.ps1", workflow);
        Assert.Contains("eng/Review-UnifiedAuroraScreenshots.ps1", workflow);
        Assert.Contains("CI must not execute either helper", workflow);
        Assert.DoesNotContain("powershell.exe -File eng/Capture-UnifiedAuroraWinUI3.ps1", workflow);
        Assert.DoesNotContain("powershell.exe -File eng/Review-UnifiedAuroraScreenshots.ps1", workflow);
        Assert.DoesNotContain("Aurora-ru-capture-manifest.json", workflow);
    }

    [Fact]
    public void RecoveryDoesNotRequireGlobalPolicyOrVehicleAccess()
    {
        var capture = Source("eng/Capture-UnifiedAuroraWinUI3.ps1");
        var review = Source("eng/Review-UnifiedAuroraScreenshots.ps1");
        Assert.Contains("[switch]$IConfirmOfflineNoSecrets", capture);
        Assert.Contains("[switch]$IConfirmPrivateOfflineReview", review);
        Assert.Contains("Screenshot output must be outside the Git repository", capture);
        Assert.Contains("The review directory must not be inside the Git repository", review);
        Assert.DoesNotContain("Set-ExecutionPolicy", capture);
        Assert.DoesNotContain("Set-ExecutionPolicy", review);
        Assert.DoesNotContain("PassThruWriteMsgs(", capture);
        Assert.DoesNotContain("Invoke-WebRequest", capture);
        Assert.DoesNotContain("Invoke-RestMethod", review);
    }

    private static string Source(string path) =>
        File.ReadAllText(PathInRepo(path));

    private static string PathInRepo(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException(relative);
    }
}
