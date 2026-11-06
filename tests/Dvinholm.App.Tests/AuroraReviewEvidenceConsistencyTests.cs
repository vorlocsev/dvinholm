using System.Text.RegularExpressions;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts that cross-check genuine screenshot capture metadata
/// against the private RU/EN review. No GUI pixels are generated or accepted.
/// </summary>
public sealed class AuroraReviewEvidenceConsistencyTests
{
    private static readonly string[] Pages =
    {
        "Connection", "Updates", "CemPin", "CemConfiguration", "Dtc", "Ecu"
    };

    private static string Source(string relative)
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory);
             folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    private static string Review() => Source("eng/Review-UnifiedAuroraScreenshots.ps1");
    private static string Capture() => Source("eng/Capture-UnifiedAuroraWinUI3.ps1");

    private static Dictionary<string, string> PageTitles(string script, string language)
    {
        var expected = script.IndexOf("$expectedTitles = @{", StringComparison.Ordinal);
        Assert.True(expected >= 0);
        var langStart = script.IndexOf(language + " = @{", expected, StringComparison.Ordinal);
        Assert.True(langStart > expected);
        var langEnd = script.IndexOf("\n    }", langStart, StringComparison.Ordinal);
        Assert.True(langEnd > langStart);
        var section = script.Substring(langStart, langEnd - langStart);
        return Regex.Matches(section, @"(Connection|Updates|CemPin|CemConfiguration|Dtc|Ecu)='([^']*)'")
            .Cast<Match>()
            .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value,
                StringComparer.Ordinal);
    }

    [Fact]
    public void RussianReviewExpectedTitlesMatchRealCaptureTitlesExactly()
    {
        var titles = PageTitles(Review(), "ru");
        Assert.Equal(Pages.Length, titles.Count);
        Assert.Equal(PageTitles(Capture(), "ru"), titles);
        Assert.Equal("Ошибки (DTC)", titles["Dtc"]);
        Assert.Equal("Блоки ECU", titles["Ecu"]);
    }

    [Fact]
    public void EnglishReviewExpectedTitlesMatchRealCaptureTitlesExactly()
    {
        var titles = PageTitles(Review(), "en");
        Assert.Equal(Pages.Length, titles.Count);
        Assert.Equal(PageTitles(Capture(), "en"), titles);
        Assert.Equal("Faults (DTC)", titles["Dtc"]);
        Assert.Equal("Modules (ECU)", titles["Ecu"]);
    }

    [Fact]
    public void WrongPageTitlesAreRejectedEvenIfNonempty()
    {
        var review = Review();
        Assert.Contains("$expectedTitle = [string]$expectedTitles[$lang][$page]", review);
        Assert.Contains("[string]$entry.verifiedVisibleWorkspaceTitle -cne $expectedTitle", review);
        Assert.Contains("Page identity is not verified for $lang $key.", review);
        Assert.Contains("Expected the exact localized workspace title.", review);
        Assert.Contains("$pages -cnotcontains $page", review);
    }

    [Fact]
    public void ReviewCrossChecksActualHeightDipWithPhysicalPixelsAndDpi()
    {
        var review = Review();
        Assert.Contains("[int]$entry.actualClientHeightPx -lt 100", review);
        Assert.Contains("[int]$entry.actualClientHeightPx -gt [int]$entry.actualHeightPx", review);
        Assert.Contains("([int]$entry.actualClientHeightPx * 96.0 / [int]$entry.windowDpi)", review);
        Assert.Contains("$entry.measuredClientHeightDip", review);
        Assert.Contains("[int]$entry.windowDpi -lt 96", review);
        Assert.Contains("[int]$entry.windowDpi -gt 480", review);
    }

    [Fact]
    public void ReviewCrossChecksActualWidthDipWithPhysicalPixelsAndDpi()
    {
        var review = Review();
        Assert.Contains("[int]$entry.actualClientWidthPx -lt 100", review);
        Assert.Contains("[int]$entry.actualClientWidthPx -gt [int]$entry.actualWidthPx", review);
        Assert.Contains("([int]$entry.actualClientWidthPx * 96.0 / [int]$entry.windowDpi)", review);
        Assert.Contains("[Math]::Abs([double]$entry.measuredWidthDip - $width) -gt 5", review);
        Assert.Contains("$pixelWidth -ne [int]$entry.actualWidthPx", review);
        Assert.Contains("$pixelHeight -ne [int]$entry.actualHeightPx", review);
    }

    [Fact]
    public void SourceCaptureActuallyRecordsEveryEvidenceFieldReadByReview()
    {
        var capture = Capture();
        foreach (var field in new[] {
            "verifiedVisibleWorkspaceTitle", "verifiedSelectedLanguage",
            "operatorSelectedLanguage", "sourceCommitOperatorDeclared",
            "requestedWidthDip", "measuredWidthDip", "measuredClientHeightDip",
            "actualClientWidthPx", "actualClientHeightPx",
            "actualWidthPx", "actualHeightPx", "windowDpi" })
            Assert.Contains(field + " =", capture);
        Assert.Contains("Confirm-WorkspaceState $page", capture);
        Assert.Contains("GetClientRect", capture);
    }

    [Fact]
    public void ReviewCannotSilentlyDeclareVisualAcceptanceOrPublishFiles()
    {
        var review = Review();
        Assert.Contains("Missing is NOT RUN; present is NOT visual PASS.", review);
        Assert.Contains("against the ORIGINAL approved six-page image", review);
        Assert.DoesNotContain("PASS (pixel", review);
        Assert.DoesNotContain("Invoke-WebRequest", review);
        Assert.DoesNotContain("Copy-Item", review);
        Assert.DoesNotContain("Start-BitsTransfer", review);
        Assert.DoesNotContain("data:image/", review);
        Assert.Contains("The review directory must not be inside the Git repository.", review);
    }

    [Fact]
    public void WindowsCiParsesButCannotExecuteOrUploadReviewScreenshots()
    {
        var workflow = Source(".github/workflows/winui3-unified-x64.yml");
        var review = Review();
        Assert.Contains("Parse Aurora desktop helpers in Windows PowerShell 5.1 (never execute)", workflow);
        Assert.Contains("'eng/Review-UnifiedAuroraScreenshots.ps1'", workflow);
        Assert.Contains("'FullyQualifiedName~AuroraReviewEvidenceConsistencyTests'", workflow);
        Assert.Contains("$env:GITHUB_ACTIONS -eq 'true'", review);
        Assert.DoesNotContain("powershell.exe -NoProfile -File eng/Review-", workflow);
        Assert.Contains("eng/Review-UnifiedAuroraScreenshots.ps1", workflow);
        Assert.DoesNotContain("Aurora-local-review.html", workflow);
    }
}
