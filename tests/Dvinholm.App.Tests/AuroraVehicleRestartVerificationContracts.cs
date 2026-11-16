namespace Dvinholm.App.Tests;

/// <summary>
/// Static boundaries for the opt-in, real-desktop manual artwork restart check.
/// An offline CI contract is NOT a substitute for running it on Windows.
/// </summary>
public sealed class AuroraVehicleRestartVerificationContracts
{
    private static string Script()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "eng", "Verify-VolvoVehicleRestart.ps1");
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("Verify-VolvoVehicleRestart.ps1");
    }

    [Fact]
    public void RequiresOperatorConfirmedRealInteractiveWindowsRestart()
    {
        var script = Script();
        Assert.Contains("#requires -Version 5.1", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[switch]$IConfirmRestartedApplication", script);
        Assert.Contains("if (-not $IConfirmRestartedApplication)", script);
        Assert.Contains("$env:GITHUB_ACTIONS -eq 'true'", script);
        Assert.Contains("[Environment]::UserInteractive", script);
        Assert.Contains("$PSVersionTable.PSEdition -ne 'Desktop'", script);
    }

    [Fact]
    public void RequiresNineCanonicalModelOptionsAndReadOnlySavedFile()
    {
        var script = Script();
        foreach (var model in AuroraVehicleArtworkCatalog.Models)
        {
            Assert.Contains("'" + model.Id + "'", script);
            Assert.Contains("'" + model.Id + "'=", script);
        }
        Assert.Contains("vehicle-model.txt", script);
        Assert.Contains("$recorded -cne $ExpectedModelId", script);
        Assert.Contains("$file.Length -gt 64", script);
        Assert.Contains("[IO.FileAttributes]::ReparsePoint", script);
        Assert.Contains("$DataDirectory.StartsWith('\\\\')", script);
    }

    [Fact]
    public void RejectsSameProcessSelectionAndMultipleOpenWindows()
    {
        var script = Script();
        Assert.Contains("Get-Process -Name 'Dvinholm'", script);
        Assert.Contains("$windows.Count -ne 1", script);
        Assert.Contains("$window.StartTime.ToUniversalTime()", script);
        Assert.Contains("$file.LastWriteTimeUtc -gt $startedUtc", script);
        Assert.Contains("Preference was written AFTER Dvinholm launched", script);
    }

    [Fact]
    public void RequiresActualVisibleModelCaptionAndWinUiDecode()
    {
        var script = Script();
        Assert.Contains("DvinholmVehicleModelSelector", script);
        Assert.Contains("DvinholmVehicleArtworkCaption", script);
        Assert.Contains("SelectionPattern]::Pattern", script);
        Assert.Contains("$chosen.Count -ne 1", script);
        Assert.Contains("$caption.Current.Name -cne $expectedCaption", script);
        Assert.Contains("$caption.Current.HelpText -cne 'DvinholmVehicleAtlasLoaded'", script);
        Assert.Contains("$item.Current.IsOffscreen", script);
    }

    [Fact]
    public void NeverClaimsVisualApprovalOrTouchesVehicle()
    {
        var script = Script();
        Assert.Contains("visualModelAppearanceApproved = $false", script);
        Assert.Contains("physicalVehicleIdentityVerified = $false", script);
        foreach (var forbidden in new[] {
            "PassThru", "Connect_Click", "InvokePattern", "SelectionItemPattern",
            "SendKeys", "SendInput", "Set-Content", "WriteAllText",
            "WriteAllBytes", "File.Move", "Start-Process", "git push",
            "gh release", "CopyFromScreen", "Capture-OnlyVehicleModels"
        })
            Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);
    }
}
