namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline source-contract checks for the Lite pre-WinUI dependency UI.
/// None installs software, downloads binaries, probes a real host or launches WinUI.
/// </summary>
public sealed class LiteDependencyManagerContractTests
{
    private static string Read(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void LiteZipCopiesSourceMatchedManagerAndStartupLauncher()
    {
        var script = Read("eng/Package-WindowsDistribution.ps1");
        Assert.Contains("'Manage-Lite-Dependencies.ps1'", script);
        Assert.Contains("'Start-Dvinholm-Lite.cmd'", script);
        var gate = Read("eng/Test-LiteBundledPreflight.ps1");
        Assert.Contains("Repo = 'Manage-Lite-Dependencies.ps1'", gate);
        Assert.Contains("Repo = 'Start-Dvinholm-Lite.cmd'", gate);
    }

    [Fact]
    public void PreWinUiLaunchRequiresTheAuthoritativeReadOnlyInspector()
    {
        var launcher = Read("eng/Start-Dvinholm-Lite.cmd");
        Assert.Contains("Check-Lite-Prerequisites.ps1", launcher);
        Assert.Contains("Manage-Lite-Dependencies.ps1", launcher);
        Assert.Contains("-Mode Launch", launcher);
        var script = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("function Test-Ready", script);
        Assert.Contains("Check-Lite-Prerequisites.ps1", script);
        Assert.Contains("if ($Mode -eq 'Launch' -and $initial.Ready)", script);
        Assert.DoesNotContain("Install-Component -Name $Component; exit 0", launcher);
    }

    [Fact]
    public void OfficialWindowsAppSdkInstallerIsStableAndNativeOsArchitecture()
    {
        var source = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("https://aka.ms/windowsappsdk/2.5/2.5.1/", source);
        Assert.Contains("OSArchitecture", source);
        Assert.Contains("windowsappruntimeinstall-$suffix.exe", source);
        Assert.DoesNotContain("-experimental", source);
    }

    [Fact]
    public void DesktopRuntimeUsesTrustedMetadataAndSha512()
    {
        var source = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("dotnet/release-metadata/10.0/releases.json", source);
        Assert.Contains("windowsdesktop-runtime-win-x86.exe", source);
        Assert.Contains("Get-FileHash -LiteralPath $exe -Algorithm SHA512", source);
        Assert.Contains("latest-release", source);
        Assert.Contains("win-x86", source);
    }

    [Fact]
    public void DownloadCannotRunUnsignedOrModifiedInstaller()
    {
        var source = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("Get-AuthenticodeSignature -LiteralPath $exe", source);
        Assert.Contains("CN=Microsoft Corporation", source);
        Assert.Contains("SignerCertificate", source);
        Assert.Contains("$source.Hash", source);
        Assert.Contains("Downloaded installer size is outside allowed limits", source);
    }

    [Fact]
    public void ExplicitConsentAndVisibleVendorInstallerAreMandatory()
    {
        var source = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("[Windows.Forms.MessageBoxButtons]::YesNo", source);
        Assert.Contains("Start-Process -FilePath $exe -PassThru -Wait", source);
        Assert.DoesNotContain("-Quiet", source);
        Assert.DoesNotContain("-Silent", source);
        Assert.DoesNotContain("Start-Process -FilePath $exe -ArgumentList", source);
    }

    [Fact]
    public void OnLaunchAndOnCheckThereAreNoAutomaticInstalls()
    {
        var source = Read("eng/Manage-Lite-Dependencies.ps1");
        Assert.Contains("if ($Mode -eq 'Install')", source);
        Assert.Contains("Install-Component -Name $Component", source);
        Assert.Contains("$initial = Test-Ready", source);
        Assert.Contains("Update-View", source);
        Assert.Contains("Install / Update", source);
        Assert.Contains("Read-Host 'Press Enter to close'", source);
    }

    [Fact]
    public void WinUiReusesManagerWithoutBypassingAcceptanceOrUpdatingStable()
    {
        var ui = Read("src/Dvinholm.App/MainWindow.xaml");
        var code = Read("src/Dvinholm.App/MainWindow.xaml.cs");
        var launcher = Read("eng/Updater-Banner.ps1");
        Assert.DoesNotContain("ManageDependenciesButton", ui);
        Assert.DoesNotContain("CheckDependenciesButton", ui);
        Assert.DoesNotContain("CheckLiteDependenciesAsync()", code);
        Assert.Contains("DvinholmUserDataDirectory.IsAcceptanceLaunch", code);
        Assert.Contains("Manage-Lite-Dependencies.ps1", launcher);
        Assert.Contains("Start-Process -FilePath $powershell", launcher);
        Assert.DoesNotContain("Install-Component", code);
    }
}
