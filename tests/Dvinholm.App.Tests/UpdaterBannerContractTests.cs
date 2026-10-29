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
    public void AuroraDashboardFitsWorkingAreaAndPreservesBranding()
    {
        var theme = Read("eng/Launcher-AuroraTheme.ps1");
        Assert.Contains("function Set-AuroraCompactLayout", theme);
        Assert.Contains("function Resize-AuroraControlTree", theme);
        Assert.Contains("Screen]::FromControl($form).WorkingArea", theme);
        Assert.Contains("[Math]::Min(0.80,$fit)", theme);
        Assert.Contains("$form.AutoScroll=", theme);
        Assert.Contains("$form.ClientSize=[Drawing.Size]::new(", theme);
        Assert.Contains("$g.ScaleTransform(", theme);
        Assert.Contains("$heroHeight=182", theme);
        Assert.Contains("$verticalSaving=232-$heroHeight", theme);
        Assert.Contains("$referenceHeight=790-$verticalSaving-50", theme);
        Assert.Contains("$sidebarFooter.SetBounds(22,392,180,58)", theme);
        Assert.Contains("$form.AutoScrollMinSize=", theme);
        Assert.Contains("$brandTitle.Text='DVINHOLM'", theme);
        Assert.Contains("dvinholm-banner.webp", theme);
        Assert.Contains("$script:heroBackdrop", theme);
    }

    [Fact]
    public void LauncherScalesAllActionsBeforeShowingWindow()
    {
        var banner = Read("eng/Updater-Banner.ps1");
        var button = banner.IndexOf("$versionsButton.BringToFront()", StringComparison.Ordinal);
        var scale = banner.IndexOf("Set-AuroraCompactLayout", StringComparison.Ordinal);
        var smoke = banner.IndexOf("if($Mode -eq 'UiSmokeTest')", StringComparison.Ordinal);
        Assert.True(button >= 0 && scale > button && smoke > scale);
        var cockpit = Read("eng/Launcher-ServiceCockpit.ps1");
        Assert.Contains("Service Cockpit 8/8 Windows WinForms layout and action smoke checks passed.", banner);
        Assert.Contains("Screen]::FromControl($form).WorkingArea", cockpit);
        Assert.Contains("Service Cockpit controls extend past the display boundary.", banner);
        Assert.Contains("$hero.Height -ne [int][Math]::Round(184*$script:auroraScale)", banner);
        Assert.Contains("$script:serviceCardDrivers.Bottom -gt $contentHeight", banner);
        Assert.Contains("$form.AutoScroll=$needsScroll", cockpit);
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

    [Fact]
    public void LauncherOnlyClosesOnVerifiedApplicationProcessHandoff()
    {
        var banner = Read("eng/Updater-Banner.ps1");
        var manager = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("$script:pendingAction=$action", banner);
        Assert.Contains("$finishedAction -ceq 'Launch'", banner);
        Assert.Contains("$snapshot.Done -eq $true", banner);
        Assert.Contains("$snapshot.Failed -eq $false", banner);
        Assert.Contains("if($launchSucceeded)", banner);
        Assert.Contains("$form.Close()", banner);
        Assert.Contains("$helper=if($testHelper){$testHelper}else{$manager}", banner);
        Assert.Contains("$script:appProcessStarted = $true", manager);
        Assert.Contains("if ($Mode -eq 'Launch' -and -not $script:appProcessStarted) { exit 41 }", manager);
    }

    [Fact]
    public void AuroraNavigationHasActionsAndPersistedBilingualSettings()
    {
        var theme = Read("eng/Launcher-AuroraTheme.ps1");
        var banner = Read("eng/Updater-Banner.ps1");
        Assert.Contains("Begin-Action 'Launch'", theme);
        Assert.Contains("Begin-Action 'Check'", theme);
        Assert.Contains("Begin-Action 'Probe'", theme);
        Assert.Contains("Show-LauncherSettings", theme);
        Assert.Contains("languageChoice", theme);
        Assert.Contains("language-preference.txt", banner);
        Assert.Contains("Save-LauncherLanguage", banner);
        Assert.Contains("Apply-LauncherLanguage", banner);
        Assert.Contains("RU=@", banner);
        Assert.Contains("EN=@", banner);
        Assert.Contains("Begin-Action 'RepairInstall'", banner);
        Assert.Contains("$rollback.Visible=$false", theme);
        Assert.Contains("dvinholm-banner.webp", Read("eng/Package-Updater-Banner.ps1"));
        Assert.Contains("Launcher-ServiceCockpit.ps1", Read("eng/Package-Updater-Banner.ps1"));
    }
    [Fact]
    public void ApprovedServiceCockpitHasRealOperationsAndBilingualLayout()
    {
        var service = Read("eng/Launcher-ServiceCockpit.ps1");
        var banner = Read("eng/Updater-Banner.ps1");
        Assert.Contains("New-ServiceCard 14 194 405 482", service);
        Assert.Contains("New-ServiceCard 430 194 736 155", service);
        Assert.Contains("New-ServiceCard 430 357 736 155", service);
        Assert.Contains("New-ServiceCard 430 520 736 156", service);
        Assert.Contains("SetBounds(0,0,$referenceWidth,$heroHeight)", service);
        Assert.Contains("$heroHeight=184", service);
        Assert.Contains("$windowsQuick.Add_Click({Begin-Action 'Probe'})", service);
        Assert.Contains("$driverQuick.Add_Click({Show-J2534Drivers})", service);
        Assert.Contains("$driverScanQuick.Add_Click({Update-ServiceDriverInventory})", service);
        Assert.Contains("$settingsQuick.Add_Click({Show-LauncherSettings})", service);
        Assert.Contains("$depsInstallQuick.Add_Click({Show-MicrosoftComponents})", service);
        Assert.Contains("function Show-MicrosoftComponents", banner);
        Assert.Contains("function Start-MicrosoftComponent", banner);
        Assert.Contains("Service Cockpit 8/8 Windows WinForms layout and action smoke checks passed.", banner);
        Assert.Contains("componentsTitle='Microsoft x86 prerequisites'", banner);
        Assert.Contains("componentsTitle='Компоненты Microsoft x86'", banner);
    }

    [Fact]
    public void ServiceCockpitInstallActionAndRecoveryLabelStaySafe()
    {
        var banner = Read("eng/Updater-Banner.ps1");
        var cockpit = Read("eng/Launcher-ServiceCockpit.ps1");

        Assert.Contains("function Test-CockpitUpdateAvailable", banner);
        Assert.Contains("Service Cockpit update buttons 8/8 offline state checks passed.", banner);
        Assert.Contains("Test-CockpitUpdateAvailable $script:lastCurrent $script:lastAvailable", banner);
        Assert.Contains("if($action -ceq 'Install' -and", banner);
        Assert.Contains("Service Cockpit Start button overlaps its recovery or help labels.", banner);
        Assert.Contains("$recovery.SetBounds(35,586,365,24)", cockpit);
        Assert.Contains("$launch.SetBounds(35,528,365,53)", cockpit);
    }

    [Fact]
    public void LauncherLocalizesWorkerStatusWithoutChangingInternalState()
    {
        var banner = Read("eng/Updater-Banner.ps1");
        Assert.Contains("function Format-LauncherDetail", banner);
        Assert.Contains("function Format-LauncherRecovery", banner);
        Assert.Contains("function Set-LauncherDetail", banner);
        Assert.Contains("$script:lastRawStatus=$raw", banner);
        Assert.Contains("Set-LauncherDetail ([string]$snapshot.Detail)", banner);
        Assert.Contains("$status.Text=Format-LauncherDetail $script:lastRawStatus", banner);
        Assert.Contains("Service Cockpit 8/8 offline RU/EN status translation checks passed.", banner);
        Assert.Contains("default {return $raw}", banner);
    }

    [Fact]
    public void NativeLauncherAndAppStartersEmbedDistinctBrandedIcons()
    {
        var build = Read("eng/Build-Launchers.ps1");
        var generator = Read("eng/New-LauncherIcon.ps1");
        var smoke = Read("eng/Test-NativeLauncherIcons.ps1");
        var workflow = Read(".github/workflows/ci.yml");
        Assert.Contains("Get-Command 'rc.exe'", build);
        Assert.Contains("'Launcher.exe';Define='DVINHOLM_UPDATER';Icon=$brandedLauncherIcon", build);
        Assert.Contains("'Dvinholm.exe';Define='DVINHOLM_DIRECT_LAUNCH';Icon=$brandedAppIcon", build);
        Assert.Contains("1 ICON", build);
        Assert.Contains("'assets\\dvinholm-icon.png'", build);
        Assert.Contains("foreach($size in $sizes)", generator);
        Assert.Contains("DrawArc", generator);
        Assert.Contains("RT_GROUP_ICON", smoke);
        Assert.Contains("Native EXE icons 3/3 offline checks passed.", smoke);
        Assert.Contains("Verify distinct embedded native EXE icons", workflow);
        var banner = Read("eng/Updater-Banner.ps1");
        Assert.Contains("ExtractAssociatedIcon($nativeLauncherPath)", banner);
        Assert.Contains("$form.Icon=$script:nativeLauncherIcon", banner);
    }

}
