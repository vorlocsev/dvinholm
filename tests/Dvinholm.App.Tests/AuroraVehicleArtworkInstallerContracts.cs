namespace Dvinholm.App.Tests;

/// <summary>
/// Windows PowerShell 5.1 source contracts for opt-in local JPG replacement.
/// This never runs the script, touches real vehicles or claims GUI acceptance.
/// </summary>
public sealed class AuroraVehicleArtworkInstallerContracts
{
    private static string Script()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "eng",
                "Install-VolvoVehicleArtwork.ps1");
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("Install-VolvoVehicleArtwork.ps1");
    }

    [Fact]
    public void OnlyExplicitApplyChangesArtwork()
    {
        var script = Script();
        Assert.Contains("#requires -Version 5.1", script);
        Assert.Contains("[switch]$Apply", script);
        Assert.Contains("if (-not $Apply) { return }", script);
    }

    [Fact]
    public void AllowedModelsAreExactlyTheExistingNine()
    {
        var script = Script();
        foreach (var model in AuroraVehicleArtworkCatalog.Models)
            Assert.Contains("'" + model.Id + "'", script);
        Assert.Contains("if ($rows.Count -ne 9)", script);
        Assert.Contains("if ($matches.Count -ne 1", script);
    }

    [Fact]
    public void RejectsUntrustedPathsAndInPlaceOverwrite()
    {
        var script = Script();
        Assert.Contains("[IO.Path]::IsPathRooted($SourceJpg)", script);
        Assert.Contains("$SourceJpg.StartsWith('\\\\')", script);
        Assert.Contains("[IO.FileAttributes]::ReparsePoint", script);
        Assert.Contains("The source and destination JPG must differ", script);
    }

    [Fact]
    public void ChecksJpegDimensionsAndCompleteFile()
    {
        var script = Script();
        Assert.Contains("$bytes.Length -lt 20000", script);
        Assert.Contains("$bytes.Length -gt 2000000", script);
        Assert.Contains("$bytes[0] -ne 255", script);
        Assert.Contains("$image.RawFormat.Guid", script);
        Assert.Contains("$image.Width -ne 1280", script);
        Assert.Contains("$image.Height -ne 510", script);
    }

    [Fact]
    public void RewritesOnlyMatchingManifestFileAndDigest()
    {
        var script = Script();
        Assert.Contains("assets\\vehicles\\manifest.json", script);
        Assert.Contains("$entry.sha256 = $hash", script);
        Assert.Contains("$entry.bytes = $bytes.Length", script);
        Assert.Contains("$entry.width = 1280", script);
        Assert.Contains("$entry.height = 510", script);
        Assert.Contains("ConvertTo-Json -InputObject $rows -Depth 6", script);
    }

    [Fact]
    public void StagesBothFilesAndUsesAtomicReplace()
    {
        var script = Script();
        Assert.Contains("$stagedJpg = $destination +", script);
        Assert.Contains("$stagedManifest = $manifestPath +", script);
        Assert.Contains("[IO.File]::Replace($stagedJpg, $destination, $jpgBackup)", script);
        Assert.Contains("[IO.File]::Replace($stagedManifest, $manifestPath, $manifestBackup)", script);
    }

    [Fact]
    public void RollsBackOnFailureAndVerifiesResult()
    {
        var script = Script();
        Assert.Contains("[IO.File]::Replace($manifestBackup, $manifestPath, $null)", script);
        Assert.Contains("[IO.File]::Replace($jpgBackup, $destination, $null)", script);
        Assert.Contains("Post-replacement JPG digest mismatch", script);
        Assert.Contains("if ($completed)", script);
    }

    [Fact]
    public void NeverDownloadsConnectsOrReleases()
    {
        var script = Script();
        foreach (var forbidden in new[]
        {
            "Invoke-WebRequest", "Invoke-RestMethod", "PassThru", "ReadDtc",
            "Start-Process", "git push", "gh release", "Dvinholm.exe"
        })
            Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);
    }
}
