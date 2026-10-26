using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline guards for viewing the updater flyout safely from a
/// receipt-isolated Windows acceptance copy. They do not launch WinUI.
/// </summary>
public sealed class UpdateMenuAcceptanceIsolationTests
{
    private static string ReadSource(string path)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file))
                return File.ReadAllText(file);
        }

        throw new FileNotFoundException("Acceptance source missing", path);
    }

    private static string Code => ReadSource("src/Dvinholm.App/MainWindow.xaml.cs");

    private static string AcceptanceStartup
    {
        get
        {
            var code = Code;
            var start = code.IndexOf("if (DvinholmUserDataDirectory.IsAcceptanceLaunch)", StringComparison.Ordinal);
            var end = code.IndexOf("_session.ReadError +=", start, StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);
            return code[start..end];
        }
    }

    private static string Handler(string signature, string next)
    {
        var code = Code;
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        var end = code.IndexOf(next, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return code[start..end];
    }

    [Fact]
    public void CheckUpdateEntryOpensMenuWithoutInvokingInstaller()
    {
        var doc = XDocument.Parse(ReadSource("src/Dvinholm.App/MainWindow.xaml"));
        var button = Assert.Single(doc.Descendants(), e =>
            e.Name.LocalName == "Button" &&
            e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "UpdateButton"));
        Assert.Null(button.Attribute("Click"));
        var flyout = Assert.Single(button.Descendants(), e => e.Name.LocalName == "Flyout");
        Assert.Equal("UpdateFlyout_Opened", (string?)flyout.Attribute("Opened"));
    }

    [Fact]
    public void AcceptanceLeavesOnlyMenuEntryAvailable()
    {
        Assert.DoesNotContain("UpdateButton.IsEnabled = false;", AcceptanceStartup.Split('\n').Select(line => line.Trim()));
        Assert.Contains("RunUpdateButton.IsEnabled = false;", AcceptanceStartup.Split('\n').Select(line => line.Trim()));
    }

    [Fact]
    public void AcceptanceKeepsChannelChoiceEnabledForIsolatedPreference()
    {
        Assert.DoesNotContain("UpdateSource.IsEnabled = false;", AcceptanceStartup);
        Assert.Contains("LoadSavedUpdateSource();", Code);
        Assert.Contains("UpdateSourcePreferenceStore(UpdateSourceSettingsFile).Save(", Code);
    }

    [Fact]
    public void AcceptanceDisplayShowsCurrentVersionAndNoNetworkGuarantee()
    {
        var section = AcceptanceStartup;
        Assert.Contains("Current: {LatestBuildUpdater.CurrentDisplayVersion}", section);
        Assert.Contains("Offline acceptance: update checks and installation disabled", section);
    }

    [Fact]
    public void ChannelChangeRetainsOfflineWarningWithoutCheckingForUpdates()
    {
        var change = Handler("private void UpdateSource_SelectionChanged",
            "private async void UpdateFlyout_Opened");
        Assert.Contains("DvinholmUserDataDirectory.IsAcceptanceLaunch", change);
        Assert.Contains("Offline acceptance: update checks and installation disabled", change);
        Assert.Contains("_latestBuild = null;", change);
        Assert.Contains("_updateAvailable = false;", change);
        Assert.DoesNotContain("GetLatestStableReleaseAsync", change);
        Assert.DoesNotContain("GetLatestSuccessfulBuildAsync", change);
    }

    [Fact]
    public void FlyoutAutoCheckReturnsWithoutNetworkInAcceptance()
    {
        var opened = Handler("private async void UpdateFlyout_Opened",
            "private async void UpdateButton_Click");
        Assert.Contains("DvinholmUserDataDirectory.IsAcceptanceLaunch", opened);
        Assert.Contains("return;", opened);
        Assert.Contains("await CheckLatestBuildAsync();", opened);
        Assert.True(opened.IndexOf("DvinholmUserDataDirectory.IsAcceptanceLaunch", StringComparison.Ordinal) <
            opened.IndexOf("await CheckLatestBuildAsync();", StringComparison.Ordinal));
    }

    [Fact]
    public void InstallerClickAndHardwareConnectionStayBlocked()
    {
        var click = Handler("private async void UpdateButton_Click",
            "private async Task CheckLatestBuildAsync");
        Assert.Contains("if (DvinholmUserDataDirectory.IsAcceptanceLaunch)", click);
        Assert.True(click.IndexOf("if (DvinholmUserDataDirectory.IsAcceptanceLaunch)", StringComparison.Ordinal) <
            click.IndexOf("await DownloadAndLaunchLatestBuildAsync", StringComparison.Ordinal));
        Assert.Contains("ConnectButton.IsEnabled = false;", AcceptanceStartup);
    }

    [Fact]
    public void SavedChannelInAcceptanceIsPrivateFromInstalledStable()
    {
        var root = Path.Combine(Path.GetTempPath(), "DvinholmUpdateMenuAcceptance", Guid.NewGuid().ToString("N"));
        try
        {
            var app = Path.Combine(root, "verified-copy");
            var local = Path.Combine(root, "normal-local-appdata");
            var privateData = DvinholmUserDataDirectory.Resolve(app, local, null, hasVerifiedReceipt: true);
            var installedData = DvinholmUserDataDirectory.Resolve(app, local, null, hasVerifiedReceipt: false);
            Assert.NotEqual(privateData, installedData);

            var preference = Path.Combine(privateData, UpdateSourcePreferenceStore.FileName);
            new UpdateSourcePreferenceStore(preference).Save(UpdateSourceChannel.Stable);
            Assert.Equal(UpdateSourceChannel.Stable,
                new UpdateSourcePreferenceStore(preference).Load(UpdateSourceChannel.Development));
            Assert.False(File.Exists(Path.Combine(installedData, UpdateSourcePreferenceStore.FileName)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
