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
    public void OnlySixApprovedPagesUseOneFixedClientViewport()
    {
        var script = Script();
        Assert.Contains("$pages = @('Connection','Updates','CemPin','CemConfiguration','Dtc','Ecu')", script);
        Assert.Contains("$fixedWidthDip = 1280", script);
        Assert.Contains("$fixedHeightDip = 800", script);
        Assert.Contains("$widths = @($fixedWidthDip)", script);
        Assert.Contains("foreach ($lang in @('ru','en'))", script);
        Assert.Contains("$pages -cnotcontains $page", script);
        Assert.Contains("$widths -notcontains $width", script);
        Assert.Contains("Unexpected page or fixed viewport dimensions", script);
    }

    [Fact]
    public void WindowsPowerShell51JsonArrayIsUnwrappedBeforeRecordFieldCasts()
    {
        var script = Script();
        const string read = "$decoded = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json";
        const string enumerate = "$rows = @($decoded)";
        const string cast = "$width = [int]$entry.requestedWidthDip";
        var readAt = script.IndexOf(read, StringComparison.Ordinal);
        var arrayGuardAt = script.IndexOf("$decoded -isnot [System.Array]", StringComparison.Ordinal);
        var enumerateAt = script.IndexOf(enumerate, StringComparison.Ordinal);
        var castAt = script.IndexOf(cast, StringComparison.Ordinal);

        Assert.True(readAt >= 0 && arrayGuardAt > readAt &&
            enumerateAt > arrayGuardAt && castAt > enumerateAt);
        Assert.DoesNotContain("$rows = @(Get-Content", script);
        Assert.Contains("$entry -isnot [pscustomobject]", script);
        Assert.Contains("capture manifest contains a non-record JSON value.", script);
    }

    [Fact]
    public void MissingOrDuplicateManifestEvidenceIsRejected()
    {
        var script = Script();
        Assert.Contains("Aurora-{0}-capture-manifest.json", script);
        Assert.Contains("The $lang capture manifest is missing.", script);
        Assert.Contains("if ($rows.Count -gt 6)", script);
        Assert.Contains("$seen.ContainsKey($key)", script);
        Assert.Contains("Duplicate $lang screenshot entry", script);
        Assert.Contains("Unexpected extra records in fixed-viewport screenshot manifest.", script);
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
        Assert.Contains("[int]$entry.requestedHeightDip -ne $fixedHeightDip", script);
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
        Assert.Contains("[Math]::Abs([double]$entry.measuredClientHeightDip - $fixedHeightDip) -gt 5", script);
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
        Assert.Contains(" / 12. Missing is NOT RUN; present is NOT visual PASS.", script);
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

    // Stage 541: seven source guards for optional real-desktop bottom-fold review.
    // These are NOT a claim that any screenshot was visually accepted.
    [Fact]
    public void BottomReviewIsOptionalAndNeverInventsMissingScreenEvidence()
    {
        var script = Script();
        Assert.Contains("Aurora-{0}-bottom-capture-manifest.json", script);
        Assert.Contains("if (Test-Path -LiteralPath $bottomManifestPath -PathType Leaf)", script);
        Assert.Contains("$bottomData[$lang] = $seenBottom", script);
        Assert.Contains("NOT RUN (missing bottom screenshot)", script);
        Assert.DoesNotContain("visual PASS</figcaption>", script);
    }

    [Fact]
    public void BottomManifestCanOnlyContainFourLongPagesWithMatchingTopCaptures()
    {
        var script = Script();
        Assert.Contains("$bottomPages = @('CemPin','CemConfiguration','Dtc','Ecu')", script);
        Assert.Contains("if ($bottomRows.Count -gt 4)", script);
        Assert.Contains("$bottomPages -cnotcontains $page", script);
        Assert.Contains("$seenBottom.ContainsKey($key)", script);
        Assert.Contains("$topEntries[$lang].ContainsKey($key)", script);
        Assert.Contains("Bottom screenshot has no verified matching top page:", script);
        Assert.Contains("Duplicate bottom screenshot:", script);
    }

    [Fact]
    public void BottomManifestRequiresActualScrollPatternEvidenceAndLocalizedIdentity()
    {
        var script = Script();
        Assert.Contains("$bottomEntry.operatorSelectedLanguage -cne $lang", script);
        Assert.Contains("$bottomEntry.verifiedVisibleWorkspaceTitle -cne", script);
        Assert.Contains("$bottomEntry.scrollPosition -cne 'bottom'", script);
        Assert.Contains("$bottomEntry.verifiedOuterScrollPercentAtLeast98 -isnot [bool]", script);
        Assert.Contains("-not $bottomEntry.verifiedOuterScrollPercentAtLeast98", script);
        Assert.Contains("Bottom fold is not verified by capture UI Automation:", script);
    }

    [Fact]
    public void BottomEvidenceMustMatchItsTopViewportDpiAndOperatorCommit()
    {
        var script = Script();
        Assert.Contains("$topEntries[$lang][$key]", script);
        Assert.Contains("'requestedWidthDip','requestedHeightDip','actualWidthPx','actualHeightPx'", script);
        Assert.Contains("'actualClientWidthPx','actualClientHeightPx','windowDpi'", script);
        Assert.Contains("$bottomEntry.PSObject.Properties[$field]", script);
        Assert.Contains("$topEntry.PSObject.Properties[$field].Value", script);
        Assert.Contains("Bottom/top capture geometry or DPI differs:", script);
        Assert.Contains("$bottomEntry.sourceCommitOperatorDeclared -cne", script);
        Assert.Contains("Bottom/top operator-declared source commit differs:", script);
    }

    [Fact]
    public void BottomPngMustExistAndAgreeWithManifestPixelDimensions()
    {
        var script = Script();
        Assert.Contains("Aurora-$page-$lang-$fixedWidthDip-dip-bottom.png", script);
        Assert.Contains("Unsafe or mismatched bottom screenshot filename:", script);
        Assert.Contains("Test-Path -LiteralPath $bottomPng -PathType Leaf", script);
        Assert.Contains("$bottomInfo.Length -gt 30000000", script);
        Assert.Contains("$bottomSignature = @(137,80,78,71,13,10,26,10)", script);
        Assert.Contains("Missing bottom PNG IHDR:", script);
        Assert.Contains("$bottomPixelWidth -ne [int]$bottomEntry.actualWidthPx", script);
        Assert.Contains("$bottomPixelHeight -ne [int]$bottomEntry.actualHeightPx", script);
    }

    [Fact]
    public void ReportPairsEightOptionalBottomViewsWithoutImplyingVisualApproval()
    {
        var script = Script();
        Assert.Contains("$bottomPresent = 0", script);
        Assert.Contains("$bottomData[$lang].ContainsKey($key)", script);
        Assert.Contains("Outer workspace bottom | separately captured and unreviewed", script);
        Assert.Contains("bottom UIA verified | ", script);
        Assert.Contains("Validated bottom-fold PNG/manifest pairs:", script);
        Assert.Contains(" / 8. Missing is NOT RUN; present is NOT visual PASS.", script);
        Assert.Contains("Bottom folds: last CEM PIN, configuration, DTC and ECU controls", script);
    }

    [Fact]
    public void OptionalBottomReviewNeverUploadsImagesOrTouchesJ2534()
    {
        var script = Script();
        Assert.Contains("The review directory must not be inside the Git repository.", script);
        Assert.Contains("$lang + '/' + $bottomName", script);
        Assert.DoesNotContain("Invoke-WebRequest", script);
        Assert.DoesNotContain("Invoke-RestMethod", script);
        Assert.DoesNotContain("Copy-Item", script);
        Assert.DoesNotContain("data:image/", script);
        Assert.DoesNotContain("PassThruOpen", script);
        Assert.DoesNotContain("ReadCemPin", script);
    }

}
