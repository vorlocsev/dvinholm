using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for the selected Techno-North No. 9 Launcher mark.
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
        Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName is "script" or "foreignObject");
        Assert.DoesNotContain(doc.Descendants().Attributes(), a => a.Name.LocalName == "href");
    }

    [Fact]
    public void IdentityCombinesGullGearRingArrowAndWhiteSeaWaves()
    {
        var source = Read("assets/launcher-northern-emblem.svg");
        Assert.Contains("gull", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gear", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ring", source);
        Assert.Contains("White Sea waves", source);
        Assert.Contains("M36 119", source);
        Assert.Contains("M171 87", source);
        Assert.Contains("M41 162", source);
        Assert.Contains("riveted", source);
    }

    [Fact]
    public void EmblemKeepsAuroraColorsAndNoOfficialVolvoWordmark()
    {
        var source = Read("assets/launcher-northern-emblem.svg");
        Assert.Contains("#061323", source);
        Assert.Contains("#F0F8FF", source);
        Assert.Contains("#A6C3D9", source);
        Assert.Contains("#93B8C9", source);
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
        Assert.Contains("DrawEllipse($steel", script);
        Assert.Contains("FillPolygon($bird", script);
        Assert.Contains("DrawBezier($sea", script);
        Assert.Contains("FillPolygon($silver", script);
        Assert.Contains("247,14", script);
        Assert.Contains("Write([uint16]$sizes.Count)", script);
    }

    [Fact]
    public void NoAdditionalUpdateBadgeAndMainApplicationIconRemainsDistinct()
    {
        var svg = Read("assets/launcher-northern-emblem.svg");
        var script = Read("eng/New-LauncherIcon.ps1");
        Assert.Contains("No separate update badge", svg);
        Assert.DoesNotContain("cx=\"207\" cy=\"207\"", svg);
        Assert.DoesNotContain("$badgeBg=", script);
        Assert.DoesNotContain("$badgeBorder=", script);
        Assert.Contains("FillPolygon($silver", script);
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
        Assert.Contains("Validate-Asset $zip[0]", Read("eng/Updater-Banner.ps1"));
        Assert.DoesNotContain("dotnet.exe", Read("eng/native/launcher.cpp"));
    }
}
