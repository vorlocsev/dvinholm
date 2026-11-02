using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Offline checks of the user-triggered WinUI stage-and-verify controls.
/// These do not simulate GitHub connectivity, publisher signature or an install.
/// </summary>
public sealed class UnifiedX64StagingUiTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var name = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(name)) return File.ReadAllText(name);
        }
        throw new FileNotFoundException(relative);
    }

    private static XDocument Layout()
    {
        using var stream = typeof(UnifiedX64StagingUiTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("WinUI XAML resource not embedded.");
        return XDocument.Load(stream);
    }

    private static XElement Control(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    [Fact]
    public void StageAndCancelBeginDisabledUntilReleaseIsSelected()
    {
        var doc = Layout();
        Assert.Equal("False", (string?)Control(doc, "UnifiedStageUpdateButton").Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Control(doc, "UnifiedCancelStageButton").Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Control(doc, "UnifiedPreflightButton").Attribute("IsEnabled"));
        Assert.Equal("Collapsed", (string?)Control(doc, "UnifiedStageProgress").Attribute("Visibility"));
    }

    [Fact]
    public void ReleaseDiscoveryRetainsOnlyAnExplicitCandidate()
    {
        var code = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("_unifiedCandidate = candidate;", code);
        Assert.Contains("InvalidateUnifiedCandidate();", code);
        Assert.Contains("UnifiedX64ReleaseFeed.CheckAsync(channel, cts.Token)", code);
        Assert.DoesNotContain("UnifiedX64StageDownloader.StageAsync(", code);
    }

    [Fact]
    public void DownloadRequiresASelectedTrustedCandidateAndNoConcurrentOperation()
    {
        var code = Source("src/Dvinholm.App/MainWindow.UnifiedDownloads.cs");
        Assert.Contains("!UnifiedNetworkAllowed || _unifiedCheckCts is not null", code);
        Assert.Contains("_unifiedStageCts is not null || _unifiedStagedPackage is not null", code);
        Assert.Contains("!UnifiedX64StageDownloader.ValidateCandidate(candidate)", code);
        Assert.Contains("UnifiedStageUpdateButton.IsEnabled = UnifiedNetworkAllowed", code);
    }

    [Fact]
    public void StageUsesPrivateDirectoryAndNeverLaunchesOrExtractsAnything()
    {
        var code = Source("src/Dvinholm.App/MainWindow.UnifiedDownloads.cs");
        Assert.Contains("UnifiedX64StoragePaths.StagingRoot", code);
        Assert.Contains("UnifiedX64StoragePaths.BackupRoot", code);
        Assert.Contains("await UnifiedX64StageDownloader.StageAsync(", code);
        Assert.DoesNotContain("Process.Start(", code);
        Assert.DoesNotContain("ZipFile.Extract", code);
        Assert.DoesNotContain("File.Replace(", code);
        Assert.DoesNotContain("InstallUpdate", code);
    }

    [Fact]
    public void ChannelChangesCancellationAndCloseInvalidateStagedBytes()
    {
        var updates = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        var main = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("_unifiedCheckCts?.Cancel();\n        InvalidateUnifiedCandidate();", updates);
        Assert.Contains("_unifiedStageCts?.Cancel();\n            DeleteStagedArchive();", main);
        Assert.Contains("if (_unifiedUpdatesActive)\n            _unifiedStageCts?.Cancel();", main);
        Assert.Contains("cts.IsCancellationRequested || _closing", Source(
            "src/Dvinholm.App/MainWindow.UnifiedDownloads.cs"));
    }

    [Fact]
    public void UserMayCancelAndProgressCannotBeMisrepresentedAsInstall()
    {
        var doc = Layout();
        Assert.Equal("UnifiedStageUpdate_Click",
            (string?)Control(doc, "UnifiedStageUpdateButton").Attribute("Click"));
        Assert.Equal("UnifiedCancelStage_Click",
            (string?)Control(doc, "UnifiedCancelStageButton").Attribute("Click"));
        var source = Source("src/Dvinholm.App/MainWindow.UnifiedDownloads.cs");
        Assert.Contains("_unifiedStageCts?.Cancel();", source);
        Assert.Contains("publisher", source, StringComparison.OrdinalIgnoreCase);
        var page = Control(doc, "UnifiedUpdatesPage");
        Assert.DoesNotContain(page.Descendants(), e => e.Name.LocalName == "Button" &&
            (((string?)e.Attribute("Content"))?.Contains("Установить", StringComparison.OrdinalIgnoreCase) == true ||
             ((string?)e.Attribute("Content"))?.Equals("Install", StringComparison.OrdinalIgnoreCase) == true));
    }
}
