using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for the user-approved Northern Launcher mark.
/// Vector artwork is original, but physical Windows screenshot approval remains separate.
/// </summary>
public sealed class NorthernLauncherEmblemContractTests
{
    private static string Read(string path)
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var candidate = Path.Combine(folder.FullName,
                path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }
        throw new FileNotFoundException(path);
    }

    [Fact]
    public void CanonicalNorthernEmblemIsAStandaloneLocalSvg()
    {
        var doc = XDocument.Parse(Read("assets/launcher-northern-emblem.svg"));
        XNamespace svg = "http://www.w3.org/2000/svg";
        Assert.Equal(svg + "svg", doc.Root?.Name);
        Assert.Equal("0 0 256 256", (string?)doc.Root?.Attribute("viewBox"));
        Assert.Single(doc.Descendants(svg + "title"));
        Assert.Single(doc.Descendants(svg + "desc"));
        Assert.Empty(doc.Descendants().Where(e => e.Name.LocalName is "script" or "foreignObject"));
        Assert.Empty(doc.Descendants().Attributes().Where(a => a.Name.LocalName == "href"));
    }

    [Fact]
    public void IdentityCombinesGullGearRingArrowAndWhiteSeaWaves()
    {
        var source = Read("assets/launcher-northern-emblem.svg");
        Assert.Contains("gull", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gear", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ring and arrow", source);
        Assert.Contains("White Sea waves", source);
        Assert.Contains("M53 122", source);
        Assert.Contains("M184 71", source);
        Assert.Contains("M67 159", source);
    }

    [Fact]
    public void EmblemKeepsAuroraColorsAndNoOfficialVolvoWordmark()
    {
        var source = Read("assets/launcher-northern-emblem.svg");
        Assert.Contains("#061323", source);
        Assert.Contains("#F0F8FF", source);
        Assert.Contains("#41D7ED", source);
        Assert.Contains("#27F0C1", source);
        Assert.DoesNotContain(">VOLVO<", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:", source);
    }

    [Fact]
    public void NativeLauncherIconRendersItsOwnEmblemAtSevenSizes()
    {
        var script = Read("eng/New-LauncherIcon.ps1");
        Assert.Contains("@(16,24,32,48,64,128,256)", script);
        Assert.Contains("[Drawing.Bitmap]::new(", script);
        Assert.Contains("Drawing.Imaging.ImageFormat]::Png", script);
        Assert.Contains("DrawEllipse($silver", script);
        Assert.Contains("FillPolygon($bird", script);
        Assert.Contains("DrawBezier($wave", script);
        Assert.Contains("DrawLine($silver,184,71,234,19)", script);
        Assert.Contains("Write([uint16]$sizes.Count)", script);
    }

    [Fact]
    public void SmallUpdateBadgeDistinguishesLauncherFromDvinholmApp()
    {
        var script = Read("eng/New-LauncherIcon.ps1");
        Assert.Contains("FillEllipse($badgeBg,178,178,58,58)", script);
        Assert.Contains("DrawEllipse($badgeBorder,178,178,58,58)", script);
        Assert.Contains("DrawArc($badgeStroke,189,189,36,36,45,285)", script);
        var build = Read("eng/Build-Launchers.ps1");
        Assert.Contains("New-LauncherIcon.ps1", build);
        Assert.Contains("src\\Dvinholm.App\\Assets\\Dvinholm.ico", build);
        Assert.Contains("Dvinholm-Launcher.ico", build);
        Assert.Contains("Icon=$brandedLauncherIcon", build);
        Assert.Contains("Icon=$brandedAppIcon", build);
    }

    [Fact]
    public void ExistingWindowsCiVerifiesEmbeddedExeNotLooseIco()
    {
        var script = Read("eng/Test-NativeLauncherIcons.ps1");
        Assert.Contains("RT_GROUP_ICON", script);
        Assert.Contains("count -lt 7", script);
        Assert.Contains("ExtractAssociatedIcon($file)", script);
        Assert.Contains("Both EXEs have an identical icon", script);
        Assert.Contains("Launcher.exe", script);
        Assert.Contains("Dvinholm.exe", script);
    }

    [Fact]
    public void TheApprovedNorthernHomeUsesMatchingGullGearAndWaves()
    {
        var cockpit = Read("eng/Launcher-ServiceCockpit.ps1");
        Assert.Contains("Northern identity", cockpit);
        Assert.Contains("Gear + ring/arrow + white-gull silhouette", cockpit);
        Assert.Contains("$g.DrawArc($silver", cockpit);
        Assert.Contains("$g.FillPolygon($bird", cockpit);
        Assert.Contains("$g.DrawArc($cyan", cockpit);
        Assert.Contains("Show-NorthernUpdates", cockpit);
        Assert.Contains("Update-NorthernCompactSnapshot", cockpit);
    }

    [Fact]
    public void NativeX86LauncherAndReleaseIntegrityRemainIndependent()
    {
        var build = Read("eng/Build-Launchers.ps1");
        var manifest = Read("eng/Package-Updater-Banner.ps1");
        var validator = Read("eng/Assert-LauncherSelfUpdatePackage.ps1");
        Assert.Contains("SUBSYSTEM:WINDOWS", build);
        Assert.Contains("MACHINE:X86", build);
        Assert.Contains("launcher-service-cockpit-banner.jpg", manifest);
        Assert.Contains("750KB", manifest);
        Assert.Contains("Launcher.exe", validator);
        Assert.Contains("Get-FileHash", Read("eng/Updater-Banner.ps1"));
        Assert.DoesNotContain("dotnet.exe", Read("eng/native/launcher.cpp"));
    }
}
