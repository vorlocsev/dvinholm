using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline UI/handler wiring checks. These do not claim interactive
/// Windows acceptance or real update download tests.
/// </summary>
public sealed class UpdateMenuProgressTests
{
    private static string Read(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
            dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }

        throw new FileNotFoundException("Update menu source missing.", relative);
    }

    private static XDocument Xaml => XDocument.Parse(Read("src/Dvinholm.App/MainWindow.xaml"));
    private static string Code => Read("src/Dvinholm.App/MainWindow.xaml.cs");

    private static XElement Named(XDocument document, string type, string id) =>
        Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == type &&
            element.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == id));

    private static string Region(string startMarker, string nextMarker)
    {
        var code = Code;
        var start = code.IndexOf(startMarker, StringComparison.Ordinal);
        var end = code.IndexOf(nextMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return code[start..end];
    }

    [Fact]
    public void IndeterminateProgressIsInsideTheUpdaterFlyoutAndHiddenInitially()
    {
        var doc = Xaml;
        var flyout = Named(doc, "Flyout", "UpdateFlyout");
        var bar = Named(doc, "ProgressBar", "UpdateProgressBar");
        Assert.Contains(flyout, bar.Ancestors());
        Assert.Equal("True", (string?)bar.Attribute("IsIndeterminate"));
        Assert.Equal("Collapsed", (string?)bar.Attribute("Visibility"));
    }

    [Fact]
    public void ProgressDetailsAreAccessibleWrappedAndInitiallyHidden()
    {
        var doc = Xaml;
        var flyout = Named(doc, "Flyout", "UpdateFlyout");
        var bar = Named(doc, "ProgressBar", "UpdateProgressBar");
        var details = Named(doc, "TextBlock", "UpdateProgressText");
        Assert.Contains(flyout, details.Ancestors());
        Assert.Equal("Collapsed", (string?)details.Attribute("Visibility"));
        Assert.Equal("Wrap", (string?)details.Attribute("TextWrapping"));
        Assert.Equal("Update progress", (string?)bar.Attributes().Single(a => a.Name.LocalName == "Name" && a.Name.NamespaceName.EndsWith("AutomationProperties", StringComparison.Ordinal)).Value);
    }

    [Fact]
    public void HelperShowsActivityAndHidesItWithoutLeavingStaleDetails()
    {
        var region = Region("private void ShowUpdateActivity(", "private bool UseStableUpdateSource");
        Assert.Contains("UpdateProgressText.Text = text;", region);
        Assert.Contains("UpdateProgressBar.Visibility = Visibility.Visible;", region);
        Assert.Contains("UpdateProgressBar.Visibility = Visibility.Collapsed;", region);
        Assert.Contains("message is null ? Visibility.Collapsed : Visibility.Visible", region);
    }

    [Fact]
    public void ChannelSwitchClearsProgressAndInvalidatesOldOffer()
    {
        var region = Region("private void UpdateSource_SelectionChanged(", "private async void UpdateFlyout_Opened");
        Assert.Contains("FinishUpdateActivity();", region);
        Assert.Contains("_latestBuild = null;", region);
        Assert.Contains("_updateAvailable = false;", region);
        Assert.Contains("UpdateSourcePreferenceStore(UpdateSourceSettingsFile).Save(", region);
    }

    [Fact]
    public void LookupShowsActivityAndClearsItOnSuccess()
    {
        var region = Region("private async Task CheckLatestBuildAsync()", "private async Task DownloadAndLaunchLatestBuildAsync");
        Assert.Contains("ShowUpdateActivity(\"Checking selected update channel…\");", region);
        Assert.Contains("RunUpdateButton.IsEnabled = false;", region);
        Assert.Contains("RunUpdateButton.Content = _updateAvailable ?", region);
        Assert.Contains("FinishUpdateActivity();", region);
    }

    [Fact]
    public void LookupErrorKeepsUsefulRetryInformationInsideMenu()
    {
        var region = Region("private async Task CheckLatestBuildAsync()", "private async Task DownloadAndLaunchLatestBuildAsync");
        Assert.Contains("RunUpdateButton.Content = \"Retry update check\";", region);
        Assert.Contains("FinishUpdateActivity(\"Update check unavailable. Select Retry update check.\");", region);
        Assert.Contains("RunUpdateButton.IsEnabled = true;", region);
    }

    [Fact]
    public void DownloadReportsProgressInMenuAndStatusBarOnUiQueue()
    {
        var region = Region("private async Task DownloadAndLaunchLatestBuildAsync(", "private static string ProfileCatalogPath");
        Assert.Contains("ShowUpdateActivity(\"Preparing the selected update…\");", region);
        Assert.Contains("DispatcherQueue.TryEnqueue(() =>", region);
        Assert.Contains("if (_closing) return;", region);
        Assert.Contains("SetStatus(message);", region);
        Assert.Contains("ShowUpdateActivity(message);", region);
        Assert.Contains("ShowUpdateActivity($\"Replacing application files with", region);
    }

    [Fact]
    public void DownloadErrorStopsSpinnerButRetainsRetryAndDiagnostic()
    {
        var region = Region("private async Task DownloadAndLaunchLatestBuildAsync(", "private static string ProfileCatalogPath");
        Assert.Contains("RunUpdateButton.Content = \"Retry update\";", region);
        Assert.Contains("FinishUpdateActivity($\"Update failed: {detail} See update.log for details.\");", region);
        Assert.Contains("_updateAvailable = true;", region);
        Assert.Contains("RunUpdateButton.IsEnabled = true;", region);
        Assert.Contains("UpdateSource.IsEnabled = true;", region);
    }
}
