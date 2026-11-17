namespace Dvinholm.App.Tests;

/// <summary>
/// Read-only guardrails for a local visual triptych. Static source checks do
/// not establish that screenshots exist or that a human approved imagery.
/// </summary>
public sealed class AuroraVehicleScreenshotTriptychContracts
{
    private static string Source(string path)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName,
                path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }
        throw new FileNotFoundException(path);
    }

    [Fact]
    public void SourceJpgsAreVerifiedAgainstManifestBeforeReportConstruction()
    {
        var script = Source("eng/Review-VolvoVehicleScreenshots.ps1");
        var sourceCheck = script.IndexOf("$sourceImages = @{}", StringComparison.Ordinal);
        var reportBegin = script.IndexOf("$sb = [System.Collections.Generic.List[string]]::new()",
            StringComparison.Ordinal);
        Assert.True(sourceCheck > 0 && reportBegin > sourceCheck);
        foreach (var required in new[]
        {
            "$file.Length -ne [int]$model.bytes",
            "$file.Attributes -band [IO.FileAttributes]::ReparsePoint",
            "$sha.ComputeHash($jpgBytes)",
            "$actualSha -cne [string]$model.sha256",
            "[Drawing.Image]::FromStream($stream, $false, $true)",
            "$image.RawFormat.Guid",
            "$image.Width -ne 1280",
            "$image.Height -ne 510"
        })
        {
            Assert.Contains(required, script);
            Assert.True(script.IndexOf(required, StringComparison.Ordinal) < reportBegin);
        }
    }

    [Fact]
    public void ReviewRendersNineSourceRuEnTriptychsWithoutRemoteImages()
    {
        var script = Source("eng/Review-VolvoVehicleScreenshots.ps1");
        Assert.Contains("grid-template-columns:repeat(3,minmax(0,1fr))", script);
        Assert.Contains("'data:image/jpeg;base64,' + [Convert]::ToBase64String($jpgBytes)", script);
        Assert.Contains("$sourceImage = $sourceImages[$id]", script);
        Assert.Contains("REFERENCE JPG — source bytes verified", script);
        Assert.Contains("foreach ($lang in @('ru','en'))", script);
        Assert.Contains("src=\"' + $sourceImage.dataUri", script);
        Assert.DoesNotContain("https://", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScreenshotEvidenceStillRequiresRealModelDecodeAndPixelDigests()
    {
        var script = Source("eng/Review-VolvoVehicleScreenshots.ps1");
        foreach (var required in new[]
        {
            "jpgDecodedByWinUiImageOpened",
            "screenshotSha256",
            "artworkViewportSha256",
            "artworkViewportRectPx",
            "CaptureSessionId", // no serialized property case guarantee
            "Get-FileHash -LiteralPath $png -Algorithm SHA256",
            "$actualCropHash -cne $pixelHash",
            "actualWidthPx",
            "sourceCommitOperatorDeclared",
        })
        {
            Assert.Contains(required, script, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RejectsMissingManifestsAndDoesNotProduceVisualApproval()
    {
        var script = Source("eng/Review-VolvoVehicleScreenshots.ps1");
        Assert.Contains("visual review NOT RUN", script);
        Assert.Contains("$rows.Count -ne 9", script);
        Assert.Contains("Image geometry", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT APPROVED", script);
        Assert.Contains("IConfirmPrivateOfflineReview", script);
        Assert.Contains("Private screenshot review is disabled in CI", script);
        Assert.Contains("Keep private screenshot output outside the repository.", script);
        Assert.DoesNotContain("Invoke-WebRequest", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoke-RestMethod", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("git push", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentationMakesHumanReviewMandatory()
    {
        var text = Source("docs/design/aurora-approved/VEHICLE-MODEL-REVIEW.md");
        Assert.Contains("три колонки", text);
        Assert.Contains("SHA-256", text);
        Assert.Contains("визуальную приёмку", text);
        Assert.Contains("не доказывает геометрическую точность Volvo", text);
    }
}
