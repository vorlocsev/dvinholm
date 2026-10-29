using System;
using System.IO;
using Xunit;

namespace Dvinholm.App.Tests;

/// <summary>
/// Offline contract tests for the pre-.NET J2534 driver manager. No hardware,
/// downloads, installer execution, or privileged changes during tests.
/// </summary>
public sealed class J2534LauncherDriversContractTests
{
    private static string Read(string path)
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            var file = Path.Combine(folder.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(path);
    }

    [Fact]
    public void ScansOnlyExplicit32BitPassThruRegistrationAndChecksDllArchitecture()
    {
        var script = Read("eng/J2534-DriverManager.ps1");
        Assert.Contains("RegistryView]::Registry32", script);
        Assert.Contains("PassThruSupport.04.04", script);
        Assert.Contains("PassThruSupport.02.02", script);
        Assert.Contains("Test-X86PE", script);
    }

    [Fact]
    public void MongooseJlrIsNotReportedReadyFromUsbAlone()
    {
        var script = Read("eng/J2534-DriverManager.ps1");
        Assert.Contains("MongooseJlr=", script);
        Assert.Contains("mongoose.*jlr|jlr.*mongoose", script);
        Assert.Contains("RegisteredX86Dll=($mongoose.Count -gt 0)", script);
        Assert.Contains("hardware untested", script);
    }

    [Fact]
    public void VolvoDiceIsNotClaimedToBeJ2534Compatible()
    {
        var script = Read("eng/J2534-DriverManager.ps1");
        Assert.Contains("VolvoDice=", script);
        Assert.Contains("J2534Supported=$false", script);
        Assert.Contains("Get-PnpDevice -PresentOnly", script);
        Assert.Contains("Get-CimInstance -ClassName Win32_PnPEntity", script);
        Assert.Contains("Get-DiceUsbClassification", script);
        Assert.Contains("DriverStatus=$dice.DriverStatus", script);
        Assert.Contains("USB DiCE found; Windows reports a driver/device problem", script);
    }

    [Fact]
    public void SignedVendorInstallerNeedsConsentAndManufacturerMatching()
    {
        var script = Read("eng/J2534-DriverManager.ps1");
        Assert.Contains("Get-AuthenticodeSignature", script);
        Assert.Contains("signature.Status -ne 'Valid'", script);
        Assert.Contains("Drew Technologies|OPUS IVS", script);
        Assert.Contains("if(-not $Approved)", script);
        Assert.Contains("Start-Process", script);
        Assert.DoesNotContain("Invoke-WebRequest", script);
    }

    [Fact]
    public void OfficialVendorPagesAreShownInsteadOfArbitraryBinaryDownloads()
    {
        var script = Read("eng/J2534-DriverUI.ps1");
        Assert.Contains("https://opusivs.com/support/product-downloads/", script);
        Assert.Contains("https://www.volvospecialtools.com/index.cfm?event=info.dice", script);
        Assert.Contains("Mode Verify", script);
        Assert.Contains("Show-J2534Drivers", script);
    }

    [Fact]
    public void LauncherOffersDriversWithoutRequiringDotnet()
    {
        var launcher = Read("eng/Updater-Banner.ps1");
        var skin = Read("eng/J2534-DriverUI.ps1");
        Assert.Contains("J2534-DriverUI.ps1", launcher);
        Assert.Contains("J2534-DriverManager.ps1", skin);
        Assert.Contains("Драйверы адаптеров", skin);
        Assert.DoesNotContain("Dvinholm.App.exe", skin);
        Assert.Contains("$script:driverRescan=$scan", skin);
        Assert.Contains("$script:driverInstallerPending=$true", skin);
        Assert.Contains("if($script:driverRescan)", launcher);
        Assert.Contains("& $script:driverRescan", launcher);
        Assert.Contains("if($fromDriverInstaller)", launcher);
    }

    [Fact]
    public void MainArchiveHasCanonicalNameWithoutLite()
    {
        var workflow = Read(".github/workflows/ci.yml");
        Assert.Contains("Dvinholm-Windows-x86.zip", workflow);
        Assert.Contains("Upload main Dvinholm distribution", workflow);
        Assert.DoesNotContain("Dvinholm-Windows-x86-Launcher-Lite.zip", workflow);
    }

    [Fact]
    public void CompatibleLegacyUpdateFeedIsNotRemovedAbruptly()
    {
        var workflow = Read(".github/workflows/ci.yml");
        var updater = Read("eng/Updater-Banner.ps1");
        Assert.Contains("Dvinholm-Windows-x86-Lite.zip", workflow);
        Assert.Contains("Dvinholm-Windows-x86-Lite.zip", updater);
    }
}
