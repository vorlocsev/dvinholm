namespace Dvinholm.App.Tests;

/// <summary>
/// Eight source-based controls for the private on-device RU/EN screenshot
/// review report. These are NOT an assertion of native UIA or visual PASS.
/// </summary>
public sealed class AuroraPrivateReviewReportContracts
{
    private static string Source(string relative)
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory);
             folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    private static string Script() =>
        Source("eng/Review-UnifiedAuroraScreenshots.ps1");

    [Fact]
    public void ReportRequiresExplicitPrivateOfflineOperatorConfirmation()
    {
        var script = Script();
        Assert.Contains("#Requires -Version 5.1", script);
        Assert.Contains("IConfirmPrivateOfflineReview", script);
        Assert.Contains("$env:GITHUB_ACTIONS -eq 'true'", script);
        Assert.Contains("Run with Windows PowerShell 5.1", script);
        Assert.Contains("The review directory must not be inside the Git repository.", script);
        Assert.DoesNotContain("Invoke-WebRequest", script);
        Assert.DoesNotContain("Upload-Artifact", script);
    }

    [Fact]
    public void OnlySixApprovedPagesAndFiveRecordedClientDipWidthsAppear()
    {
        var script = Script();
        Assert.Contains("$pages = @('Connection','Updates','CemPin','CemConfiguration','Dtc','Ecu')", script);
        Assert.Contains("$widths = @(640,800,980,1280,1440)", script);
        Assert.Contains("foreach ($lang in @('ru','en'))", script);
        Assert.Contains("$pages -cnotcontains $page", script);
        Assert.Contains("$widths -notcontains $width", script);
        Assert.Contains("Unexpected page or DIP width", script);
    }

    [Fact]
    public void MissingOrDuplicateManifestEvidenceIsRejected()
    {
        var script = Script();
        Assert.Contains("Aurora-{0}-capture-manifest.json", script);
        Assert.Contains("The $lang capture manifest is missing.", script);
        Assert.Contains("if ($rows.Count -gt 30)", script);
        Assert.Contains("$seen.ContainsKey($key)", script);
        Assert.Contains("Duplicate $lang screenshot entry", script);
        Assert.Contains("Unexpected extra records in screenshot manifest.", script);
    }

    [Fact]
    public void LanguageAndActuallyVerifiedWorkspaceMustAgree()
    {
        var script = Script();
        Assert.Contains("operatorSelectedLanguage", script);
        Assert.Contains("verifiedSelectedLanguage", script);
        Assert.Contains("verifiedVisibleWorkspaceTitle", script);
        Assert.Contains("Unverified language label for $lang $key", script);
        Assert.Contains("Page identity is not verified", script);
        Assert.Contains("Aurora-$page-$lang-$width-dip.png", script);
        Assert.Contains("Screenshot filename differs from the expected safe name", script);
    }

    [Fact]
    public void PngMustExistAndHaveValidSignatureAndCorrectPixelDimensions()
    {
        var script = Script();
        Assert.Contains("Test-Path -LiteralPath $png -PathType Leaf", script);
        Assert.Contains("30000000", script);
        Assert.Contains("$signature = @(137,80,78,71,13,10,26,10)", script);
        Assert.Contains("$buffer[12] -ne 73", script);
        Assert.Contains("PNG IHDR", script);
        Assert.Contains("$pixelWidth -ne [int]$entry.actualWidthPx", script);
        Assert.Contains("$pixelHeight -ne [int]$entry.actualHeightPx", script);
        Assert.Contains("PNG dimensions do not match", script);
        Assert.Contains("[int]$entry.windowDpi -lt 96", script);
        Assert.Contains("[Math]::Abs([double]$entry.measuredWidthDip - $width) -gt 5", script);
    }

    [Fact]
    public void VersionEvidenceIsOperatorDeclaredOnlyAndMixedCommitsRefuse()
    {
        var script = Script();
        Assert.Contains("sourceCommitOperatorDeclared", script);
        Assert.Contains("^[0-9a-fA-F]{40}$", script);
        Assert.Contains("$declaredSha -cne $sha", script);
        Assert.Contains("RU and EN captures refer to different operator-declared commits.", script);
        Assert.Contains("Some screenshots lack the operator-declared commit", script);
        Assert.Contains("Operator-declared source commit (not authenticated)", script);
        Assert.Contains("No source commit authenticated or operator-declared.", script);
    }

    [Fact]
    public void ReportShowsMissingPicturesAsNotRunNeverVisualPass()
    {
        var script = Script();
        Assert.Contains("Validated local PNG/manifest pairs:", script);
        Assert.Contains("$present +", script);
        Assert.Contains(" / 60. Missing is NOT RUN; present is NOT visual PASS.", script);
        Assert.Contains("NOT RUN (missing screenshot)", script);
        Assert.Contains("NOT a visual PASS, not a vehicle/J2534 test.", script);
        Assert.Contains("against the ORIGINAL approved six-page image", script);
        Assert.DoesNotContain("PASS (pixel", script);
    }

    [Fact]
    public void ReportIsStaticLocalRelativeHtmlAndNotPartOfProductionPackage()
    {
        var script = Script();
        var workflow = Source(".github/workflows/winui3-unified-x64.yml");
        Assert.Contains("Aurora-local-review.html", script);
        Assert.Contains("src=\"'", script);
        Assert.Contains("$lang + '/' + $name", script);
        Assert.DoesNotContain("data:image/", script);
        Assert.DoesNotContain("Copy-Item", script);
        Assert.DoesNotContain("http://", script);
        Assert.DoesNotContain("https://", script);
        Assert.Contains("Parse Aurora desktop helpers in Windows PowerShell 5.1 (never execute)", workflow);
        Assert.Contains("eng/Review-UnifiedAuroraScreenshots.ps1", workflow);
        Assert.DoesNotContain("powershell.exe -NoProfile -File eng/Review-", workflow);
    }

    // Stage 68: six manual rubric evidence checks and two DPI provenance gates.
    // These only validate the offline review script, not actual screen pixels.
    [Theory]
    [InlineData("Banner / emblem / Aurora colors versus approved image")]
    [InlineData("Exactly six navigation routes and sidebar alignment")]
    [InlineData("Connection: real adapter, CAN speed and session state")]
    [InlineData("Updates: real version, channel and safe non-installable actions")]
    [InlineData("CEM PIN/configuration: honest evidence and disabled write actions")]
    [InlineData("DTC/ECU: table captions, schematic disclaimers and wrapping")]
    public void ReviewChecklistIncludesAllPrimaryVisualRiskAreas(string criterion)
    {
        var script = Script();
        Assert.Contains(criterion, script);
        Assert.Contains("Manual visual acceptance checklist (all unreviewed)", script);
        Assert.Contains("NOT RUN until human comparison", script);
        Assert.DoesNotContain("visual PASS</li>", script);
    }

    [Fact]
    public void MixedWindowDpiWithinOneLanguageMustFailBeforeReportCreation()
    {
        var script = Script();
        Assert.Contains("$observedDpi = $null", script);
        Assert.Contains("$observedDpi -ne $dpi", script);
        Assert.Contains("Mixed window DPI within $lang captures.", script);
        Assert.Contains("$manifestDpi[$lang] = $dpis", script);
        Assert.True(script.IndexOf("Mixed window DPI within $lang captures.",
                StringComparison.Ordinal) <
            script.IndexOf("$html = New-Object", StringComparison.Ordinal));
    }

    [Fact]
    public void CrossLanguageDpiMismatchRefusesAndCaptionsShowMeasuredScale()
    {
        var script = Script();
        Assert.Contains("$manifestDpi['ru'].ContainsKey($key)", script);
        Assert.Contains("$manifestDpi['en'].ContainsKey($key)", script);
        Assert.Contains("$manifestDpi['ru'][$key] -ne $manifestDpi['en'][$key]", script);
        Assert.Contains("RU/EN window DPI mismatch for $key.", script);
        Assert.Contains("$dpi = $manifestDpi[$lang][$key]", script);
        Assert.Contains("$scale = [Math]::Round($dpi * 100.0 / 96.0)", script);
        Assert.Contains("Recorded DPI describes the actual Dvinholm window", script);
        Assert.True(script.IndexOf("RU/EN window DPI mismatch for $key.",
                StringComparison.Ordinal) <
            script.IndexOf("[IO.File]::WriteAllText($output", StringComparison.Ordinal));
        Assert.DoesNotContain("Invoke-WebRequest", script);
        Assert.DoesNotContain("Copy-Item", script);
    }
}
