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
        Assert.Contains("ValidateSet('Launch','Manage','Install','Probe','VersionReport','SelfTest')", manager);
        Assert.Contains("if ($Mode -eq 'SelfTest')", manager);
        Assert.Contains("Select-DotNetInstaller $index", manager);
        Assert.Contains("if ($Mode -eq 'Probe')", manager);
        Assert.Contains("Get-AuthenticodeSignature -LiteralPath $exe", manager);
        Assert.Contains("Get-FileHash -LiteralPath $exe -Algorithm SHA512", manager);
        Assert.Contains("Start-Process -FilePath $exe -PassThru -Wait", manager);
    }

    [Fact]
    public void RuntimeVersionReportKeepsLocalProbeOfflineAndUsesVerifiedSources()
    {
        var manager = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("VersionReport", manager);
        Assert.Contains("Get-InstalledComponentVersions", manager);
        Assert.Contains("Get-OfficialLatestVersions", manager);
        Assert.Contains("dotnet/release-metadata/10.0/releases.json", manager);
        Assert.Contains("microsoft/WindowsAppSDK/releases/latest", manager);
        Assert.Contains("VCUpdateNote", manager);
        Assert.Contains("Latest version not verified", manager);
        Assert.Contains("Same SDK line; patch status unknown", manager);
        Assert.Contains("version decisions 8/8 offline checks passed.", manager);
        var probe = manager.IndexOf("if ($Mode -eq 'Probe')", StringComparison.Ordinal);
        var report = manager.IndexOf("if($Mode -eq 'VersionReport')", StringComparison.Ordinal);
        Assert.True(probe > 0 && report > 0);
        Assert.DoesNotContain("Invoke-WebRequest -Uri $source.Url", manager[..probe]);
    }

    [Fact]
    public void LauncherOffersExplicitNonInstallingRuntimeVersionCheck()
    {
        var banner = Read("eng/Updater-Banner.ps1");
        Assert.Contains("Button 'Check runtime versions'", banner);
        Assert.Contains("Begin-Action 'Versions'", banner);
        Assert.Contains("-Mode VersionReport", banner);
        Assert.Contains("DependencyReport", banner);
        Assert.Contains("WinApp 2.5.1 is the tested installer baseline", banner);
        Assert.Contains("$refresh.Add_Click({Begin-Action 'Probe'})", banner);
        Assert.Contains("$versionsButton.Enabled=-not $busy", banner);
        Assert.DoesNotContain("-Mode Install -Component Auto", banner);
    }

    [Fact]
    public void MicrosoftInstallerRestartCodesPropagateThroughHelper()
    {
        var manager = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("function Get-MicrosoftInstallerOutcome", manager);
        Assert.Contains("if($exitCode -in @(3010,1641))", manager);
        Assert.Contains("Microsoft installer exit classification 6/6 offline checks passed.", manager);
        Assert.Contains("return [int]$installer.ExitCode", manager);
        Assert.Contains("exit $exitCode", manager);
        Assert.Contains("Restart Windows before verifying or launching Dvinholm.", manager);
    }

    [Fact]
    public void LauncherReportsMicrosoftRebootWithoutMislabelingFailureOrRescanning()
    {
        var banner = Read("eng/Updater-Banner.ps1");
        Assert.Contains("}elseif($exitCode -in @(3010,1641)){", banner);
        Assert.Contains("Restart Windows before rechecking prerequisites", banner);
        Assert.Contains("Remove-Item -LiteralPath $script:statefile -Force", banner);
        Assert.Contains("Begin-Action 'Probe'", banner);
    }

    [Fact]
    public void RuntimeProbeRequiresBothDesktopAndCoreX86Frameworks()
    {
        var manager = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("function Get-DotNet10RuntimeInventory", manager);
        Assert.Contains("Microsoft\\.WindowsDesktop\\.App", manager);
        Assert.Contains("Microsoft\\.NETCore\\.App", manager);
        Assert.Contains("$runtimeInventory.Ready", manager);
        Assert.Contains("Test-DotNet10RuntimeInventory", manager);
        Assert.Contains("Desktop/Core x86 inventory 8/8 offline checks passed.", manager);
        Assert.Contains("DotNetCore=$components.DotNetCore", manager);
        Assert.Contains("function ConvertTo-DependencyVersionReport", manager);
        Assert.Contains("Lite dependency version report 8/8 offline checks passed.", manager);
    }

    [Fact]
    public void VersionDialogShowsDesktopAndCoreSeparately()
    {
        var banner = Read("eng/Updater-Banner.ps1");
        Assert.Contains("'DotNetCore'{'.NET Core x86'}", banner);
        Assert.Contains("foreach($key in @('DotNet','DotNetCore','WinApp','VC'))", banner);
        Assert.Contains("$part=$report.$key", banner);
        Assert.Contains("latest SDK line", banner);
        Assert.Contains("strict launch preflight still applies", banner);
        Assert.Contains("Begin-Action 'Versions'", banner);
    }

    [Fact]
    public void VersionReportSeparatesFastInventoryFromStrictRuntimeconfigPreflight()
    {
        var manager = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("function Get-StrictCompatibilityReport", manager);
        Assert.Contains("function Get-StrictPreflightDisposition", manager);
        Assert.Contains("Lite strict preflight disposition 8/8 offline checks passed.", manager);
        Assert.Contains("Test-StrictPreflightDisposition", manager);
        Assert.Contains("Check-Lite-Prerequisites.ps1", manager);
        Assert.Contains("-WindowsAppSdkLine '2.5'", manager);
        Assert.Contains("StrictPreflight", manager);
        Assert.Contains("No complete application build selected", manager);
        Assert.Contains("Strict runtimeconfig, Windows App Runtime and VC++ x86 preflight passed", manager);
    }

    [Fact]
    public void VersionDialogAuditsSelectedVerifiedBuildWithoutRunningChannelScripts()
    {
        var banner = Read("eng/Updater-Banner.ps1");
        Assert.Contains("Read-ManagedVersion $current", banner);
        Assert.Contains("$versionTarget=$current", banner);
        Assert.Contains("$versionTarget=$PSScriptRoot", banner);
        Assert.Contains("Strict compatibility [", banner);
        Assert.Contains("$strictTargetValid=($script:state.Recovery -ceq 'Ready')", banner);
        Assert.Contains("could not be validated", banner);

        var manager = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("Join-Path $PSScriptRoot 'Check-Lite-Prerequisites.ps1'", manager);
        Assert.DoesNotContain("Join-Path $TargetDirectory 'Check-Lite-Prerequisites.ps1'", manager);
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
