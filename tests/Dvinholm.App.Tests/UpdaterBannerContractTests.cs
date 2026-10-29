namespace Dvinholm.App.Tests;

/// <summary>
/// Eight source-contract checks for the independent Windows 5.1 banner.
/// No GitHub, hardware, real registry, download or installer is accessed.
/// </summary>
public sealed class UpdaterBannerContractTests
{
    private static string Read(string path)
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(p)) return File.ReadAllText(p);
        }
        throw new FileNotFoundException(path);
    }

    [Fact]
    public void IndependentLauncherDoesNotRequireDvinholmOrDotNetTen()
    {
        var source = Read("eng/native/launcher.cpp");
        Assert.Contains("CreateProcessW", source);
        Assert.Contains("Updater-Banner.ps1", source);
        Assert.Contains("Binaries", source);
        Assert.DoesNotContain("Dvinholm.App.exe", source);
        Assert.DoesNotContain("dotnet.exe", source);
        Assert.DoesNotContain("WinAppRuntime", source);
    }

    [Fact]
    public void StableAndDevelopmentUseOfficialReleaseMetadataOnly()
    {
        var source = Read("eng/Updater-Banner.ps1");
        Assert.Contains("releases/latest", source);
        Assert.Contains("releases?per_page=40", source);
        Assert.Contains("tag_name", source);
        Assert.Contains("target_commitish", source);
        Assert.Contains("prerelease", source);
    }

    [Fact]
    public void EveryUpdateChecksBothGitHubDigestsAndChecksumManifest()
    {
        var source = Read("eng/Updater-Banner.ps1");
        Assert.Contains("Validate-Asset $zip[0]", source);
        Assert.Contains("Validate-Asset $sums[0]", source);
        Assert.Contains("GetHash", source.Replace("(Hash ", "GetHash", StringComparison.Ordinal));
        Assert.Contains("Release checksum manifest mismatch.", source);
    }

    [Fact]
    public void DangerousArchivePathsAndX86MismatchAreRejected()
    {
        var source = Read("eng/Updater-Banner.ps1");
        Assert.Contains("Duplicate ZIP entry.", source);
        Assert.Contains("ZIP links forbidden.", source);
        Assert.Contains("ZIP size budget exceeded.", source);
        Assert.Contains("ZIP path escaped staging.", source);
        Assert.Contains("Invalid x86 app PE.", source);
    }

    [Fact]
    public void DifferentChannelsUseIsolatedUserWritableDirectories()
    {
        var source = Read("eng/Updater-Banner.ps1");
        Assert.Contains("LocalApplicationData", source);
        Assert.Contains("Dvinholm\\UpdaterBanner", source);
        Assert.Contains("$channelRoot", source);
        Assert.Contains("'current'", source);
        Assert.Contains("'previous'", source);
        Assert.DoesNotContain("Program Files\\Dvinholm", source);
    }

    [Fact]
    public void PrerequisiteCheckPrecedesBinaryPromotionAndInstallIsConfirmed()
    {
        var source = Read("eng/Updater-Banner.ps1");
        var preflight = source.IndexOf("Check-Lite-Prerequisites.ps1') -LiteDirectory",
            StringComparison.Ordinal);
        var promote = source.IndexOf("Move-Item -LiteralPath $pending -Destination $current", StringComparison.Ordinal);
        Assert.True(preflight >= 0 && promote > preflight);
        Assert.Contains("MessageBoxButtons]::YesNo", source);
        Assert.DoesNotContain("/quiet", source);
        Assert.DoesNotContain("-Mode Install -Component Auto", source);
    }

    [Fact]
    public void IndependentRuntimeInventoryAndVendorInstallerRemainExplicit()
    {
        var manager = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("ValidateSet('Launch','Manage','Install','Probe','SelfTest')", manager);
        Assert.Contains("if ($Mode -eq 'SelfTest')", manager);
        Assert.Contains("Select-DotNetInstaller $index", manager);
        Assert.Contains("if ($Mode -eq 'Probe')", manager);
        Assert.Contains("Get-AuthenticodeSignature -LiteralPath $exe", manager);
        Assert.Contains("Get-FileHash -LiteralPath $exe -Algorithm SHA512", manager);
        Assert.Contains("Start-Process -FilePath $exe -PassThru -Wait", manager);
    }

    [Fact]
    public void NativeLauncherPackageContainsOnlyWindowsInboxDependencies()
    {
        var script = Read("eng/Package-Updater-Banner.ps1");
        Assert.Contains("Launcher.exe", script);
        Assert.Contains("Binaries/", script);
        Assert.Contains("Dvinholm-icon.png", script);
        Assert.Contains("Updater-Banner.ps1", script);
        Assert.Contains("Manage-Lite-Dependencies.ps1", script);
        Assert.Contains("750KB", script);
        Assert.DoesNotContain("Dvinholm.App.exe", script);
        Assert.DoesNotContain("coreclr.dll", script);
        Assert.DoesNotContain("WindowsAppRuntime.dll", script);
    }
}
