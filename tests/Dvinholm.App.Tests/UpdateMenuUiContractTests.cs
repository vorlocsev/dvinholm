using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// The diagnostic executable must never offer its former updater flyout.
/// Updates and runtime management are exclusively launcher-owned.
/// </summary>
public sealed class UpdateMenuUiContractTests
{
    private static string Read(string relative)
    {
        for (DirectoryInfo? root = new(AppContext.BaseDirectory); root is not null; root = root.Parent)
        {
            var path = Path.Combine(root.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void DiagnosticXamlHasNoUpdateButtonOrFlyout()
    {
        var xaml = XDocument.Parse(Read("src/Dvinholm.App/MainWindow.xaml"));
        Assert.DoesNotContain(xaml.Descendants(), node =>
            node.Attributes().Any(attr => attr.Name.LocalName == "Name" &&
                (attr.Value is "UpdateButton" or "UpdateFlyout" or "RunUpdateButton")));
        Assert.DoesNotContain("Check update", xaml.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindowDoesNotContainUpdateOrRuntimeActions()
    {
        var source = Read("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.DoesNotContain("CheckLatestBuildAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateButton_Click", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ManageDependenciesButton_Click", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DownloadAndLaunchLatestBuildAsync", source, StringComparison.Ordinal);
        Assert.Contains("LatestBuildUpdater.CurrentWindowTitle", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LauncherStillOwnsChannelAndPrerequisiteManagement()
    {
        var launcher = Read("eng/Updater-Banner.ps1");
        Assert.Contains("Development", launcher, StringComparison.Ordinal);
        Assert.Contains("Stable", launcher, StringComparison.Ordinal);
        Assert.Contains("Manage-Lite-Dependencies.ps1", launcher, StringComparison.Ordinal);
    }
}
