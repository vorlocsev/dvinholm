using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline tests for user-initiated cancellation from the update flyout.
/// These check wiring/guard ordering, not actual interactive Windows acceptance.
/// </summary>
public sealed class UpdateDownloadCancelButtonTests
{
    private static string Read(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file))
                return File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        throw new FileNotFoundException("Required updater source not found.", relative);
    }

    private static string Window => Read("src/Dvinholm.App/MainWindow.xaml.cs");
    private static string Download => Region("private async Task DownloadAndLaunchLatestBuildAsync(",
        "private static string ProfileCatalogPath");

    private static string Region(string startMarker, string endMarker)
    {
        var source = Window;
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }

    [Fact]
    public void CancelActionBelongsToUpdateFlyoutAndIsInitiallyHiddenDisabled()
    {
        var doc = XDocument.Parse(Read("src/Dvinholm.App/MainWindow.xaml"));
        var button = Assert.Single(doc.Descendants(), e =>
            e.Name.LocalName == "Button" &&
            e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value == "CancelUpdateButton"));
        Assert.Contains(button.Ancestors(), e => e.Name.LocalName == "Flyout");
        Assert.Equal("Collapsed", (string?)button.Attribute("Visibility"));
        Assert.Equal("False", (string?)button.Attribute("IsEnabled"));
        Assert.Equal("CancelUpdateButton_Click", (string?)button.Attribute("Click"));
        Assert.Equal("Cancel update download", (string?)button.Attribute("AutomationProperties.Name"));
    }

    [Fact]
    public void CancelHandlerRefusesInactiveOrClosingWindow()
    {
        var region = Region("private void CancelUpdateButton_Click(", "private async Task DownloadAndLaunchLatestBuildAsync(");
        Assert.Contains("if (_closing || !_updaterBusy ||", region);
        Assert.Contains("_updateDownloadCts is not { IsCancellationRequested: false } downloadCts", region);
    }

    [Fact]
    public void CancelHandlerOnlyCancelsCurrentTransferAndDisablesRepeatClicks()
    {
        var region = Region("private void CancelUpdateButton_Click(", "private async Task DownloadAndLaunchLatestBuildAsync(");
        var disabled = region.IndexOf("CancelUpdateButton.IsEnabled = false;", StringComparison.Ordinal);
        var message = region.IndexOf("UpdateProgressText.Text = \"Cancelling download…\";", StringComparison.Ordinal);
        var cancel = region.IndexOf("downloadCts.Cancel();", StringComparison.Ordinal);
        Assert.True(disabled >= 0 && message > disabled && cancel > message);
        Assert.DoesNotContain("StartReplacement(", region);
        Assert.DoesNotContain("Close();", region);
    }

    [Fact]
    public void CancelOnlyAppearsDuringActiveArchivePreparation()
    {
        var region = Download;
        var token = region.IndexOf("_updateDownloadCts = downloadCts;", StringComparison.Ordinal);
        var enabled = region.IndexOf("CancelUpdateButton.IsEnabled = true;", StringComparison.Ordinal);
        var shown = region.IndexOf("CancelUpdateButton.Visibility = Visibility.Visible;", StringComparison.Ordinal);
        var completed = region.IndexOf("downloadCts.Token.ThrowIfCancellationRequested();", StringComparison.Ordinal);
        var hide = region.IndexOf("CancelUpdateButton.Visibility = Visibility.Collapsed;", StringComparison.Ordinal);
        var replacement = region.IndexOf("LatestBuildUpdater.StartReplacement(", StringComparison.Ordinal);
        Assert.True(token >= 0 && enabled > token && shown > enabled);
        Assert.True(completed > shown && hide > completed && replacement > hide);
    }

    [Fact]
    public void LateProgressCannotOverwriteCancellationMessage()
    {
        var region = Download;
        Assert.Contains("if (_closing || downloadCts.IsCancellationRequested ||", region);
        Assert.Contains("!_updateProgressGate.Accepts(progressEpoch)) return;", region);
        Assert.Contains("DispatcherQueue.TryEnqueue(() =>", region);
        Assert.Contains("SetStatus(message);", region);
    }

    [Fact]
    public void UserCancellationIsHandledBeforeOrdinaryFailureAndNeverLogsError()
    {
        var region = Download;
        var closed = region.IndexOf("catch (Exception) when (_closing)", StringComparison.Ordinal);
        var user = region.IndexOf("catch (OperationCanceledException) when (!_closing && downloadCts.IsCancellationRequested)", StringComparison.Ordinal);
        var other = region.IndexOf("catch (Exception ex)", StringComparison.Ordinal);
        Assert.True(closed >= 0 && user > closed && other > user);
        var userCancel = region[user..other];
        Assert.DoesNotContain("update.log", userCancel);
        Assert.DoesNotContain("File.WriteAllText(", userCancel);
        Assert.DoesNotContain("StartReplacement(", userCancel);
    }

    [Fact]
    public void CancellationPreservesOfferAndProvidesRetryWithoutClosingWindow()
    {
        var region = Download;
        var start = region.IndexOf("catch (OperationCanceledException) when (!_closing && downloadCts.IsCancellationRequested)", StringComparison.Ordinal);
        var end = region.IndexOf("catch (Exception ex)", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var canceled = region[start..end];
        Assert.Contains("_updateProgressGate.Complete(progressEpoch);", canceled);
        Assert.Contains("RunUpdateButton.Content = \"Retry update\";", canceled);
        Assert.Contains("FinishUpdateActivity(\"Download cancelled.", canceled);
        Assert.Contains("_updateAvailable = true;", canceled);
        Assert.DoesNotContain("_latestBuild = null;", canceled);
        Assert.DoesNotContain("Close();", canceled);
    }

    [Fact]
    public void CancelAndUpdateControlsAreRestoredOnlyWhileWindowIsOpen()
    {
        var region = Download;
        var finallyIndex = region.LastIndexOf("finally\n", StringComparison.Ordinal);
        Assert.True(finallyIndex > 0);
        var cleanup = region[finallyIndex..];
        Assert.Contains("_updateDownloadCts = null;", cleanup);
        Assert.Contains("_updaterBusy = false;", cleanup);
        Assert.Contains("if (!_closing)", cleanup);
        Assert.Contains("CancelUpdateButton.IsEnabled = false;", cleanup);
        Assert.Contains("CancelUpdateButton.Visibility = Visibility.Collapsed;", cleanup);
        Assert.Contains("RunUpdateButton.IsEnabled = true;", cleanup);
        Assert.Contains("UpdateSource.IsEnabled = true;", cleanup);
    }
}
