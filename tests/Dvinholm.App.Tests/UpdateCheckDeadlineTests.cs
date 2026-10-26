using System.Net;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for bounded update checks and shutdown cancellation.
/// No installed updater, hardware adapter, or GitHub request is used.
/// </summary>
public sealed class UpdateCheckDeadlineTests
{
    private static string WindowSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(path))
                return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        throw new FileNotFoundException("MainWindow.xaml.cs not found.");
    }

    private static string LookupCode()
    {
        var code = WindowSource();
        var start = code.IndexOf("private async Task CheckLatestBuildAsync()", StringComparison.Ordinal);
        var end = code.IndexOf("private async Task DownloadAndLaunchLatestBuildAsync(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return code[start..end];
    }

    [Fact]
    public void LookupHasSingleBoundedDeadlineAndDoesNotStartDuringClose()
    {
        var s = LookupCode();
        Assert.Contains("if (_updaterBusy || _closing)", s);
        Assert.Contains("new CancellationTokenSource(TimeSpan.FromSeconds(60))", s);
        Assert.Contains("_updateCheckCts = lookupCts;", s);
        Assert.True(s.IndexOf("_updateCheckCts = lookupCts;", StringComparison.Ordinal) <
            s.IndexOf("try\n", StringComparison.Ordinal));
    }

    [Fact]
    public void BothChannelsUseSameTotalLookupDeadline()
    {
        var s = LookupCode();
        Assert.Contains("GetLatestStableReleaseAsync(lookupCts.Token)", s);
        Assert.Contains("GetLatestSuccessfulBuildAsync(lookupCts.Token)", s);
        Assert.DoesNotContain("GetLatestStableReleaseAsync()", s);
        Assert.DoesNotContain("GetLatestSuccessfulBuildAsync()", s);
    }

    [Fact]
    public void TimeoutProvidesRetryAndNeverRetainsOffer()
    {
        var s = LookupCode();
        var start = s.IndexOf("catch (OperationCanceledException) when (lookupCts.IsCancellationRequested)", StringComparison.Ordinal);
        var end = s.IndexOf("catch (Exception)", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var timedOut = s[start..end];
        Assert.Contains("_latestBuild = null;", timedOut);
        Assert.Contains("_updateAvailable = false;", timedOut);
        Assert.Contains("Update check timed out.", timedOut);
        Assert.Contains("RunUpdateButton.Content = \"Retry update check\";", timedOut);
        Assert.Contains("FinishUpdateActivity(", timedOut);
    }

    [Fact]
    public void ClosingStopsInFlightLookupWithoutWritingToUnloadedControls()
    {
        var src = WindowSource();
        var closed = src.IndexOf("Closed += (_, _) =>", StringComparison.Ordinal);
        var beforeHandler = src.IndexOf("StopMonitor();", closed, StringComparison.Ordinal);
        Assert.True(closed >= 0 && beforeHandler > closed);
        var shutdown = src[closed..beforeHandler];
        Assert.Contains("_closing = true;", shutdown);
        Assert.Contains("_updateCheckCts?.Cancel();", shutdown);

        var check = LookupCode();
        Assert.Contains("if (_closing)\n                return;", check);
        Assert.Contains("catch (Exception) when (_closing)", check);
    }

    [Fact]
    public void ClosingTransportErrorIsCaughtBeforeTimeoutAndGenericError()
    {
        var s = LookupCode();
        var closed = s.IndexOf("catch (Exception) when (_closing)", StringComparison.Ordinal);
        var timedOut = s.IndexOf("catch (OperationCanceledException) when (lookupCts.IsCancellationRequested)", StringComparison.Ordinal);
        var unexpected = s.IndexOf("catch (Exception)\n", StringComparison.Ordinal);
        Assert.True(closed >= 0 && timedOut > closed && unexpected > timedOut);
    }

    [Fact]
    public void CompletionReleasesLifetimeAndOnlyReenablesLiveWindow()
    {
        var s = LookupCode();
        var cleanup = s.IndexOf("finally", StringComparison.Ordinal);
        Assert.True(cleanup >= 0);
        var finalBlock = s[cleanup..];
        Assert.Contains("_updateCheckCts = null;", finalBlock);
        Assert.Contains("_updaterBusy = false;", finalBlock);
        Assert.Contains("if (!_closing)", finalBlock);
        Assert.Contains("RunUpdateButton.IsEnabled = true;", finalBlock);
        Assert.Contains("UpdateSource.IsEnabled = true;", finalBlock);
    }

    [Fact]
    public async Task StableReleaseLookupObservesCallerCancellation()
    {
        using var http = new HttpClient(new CancellationOnlyHandler());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await LatestBuildUpdater.GetLatestStableReleaseAsync(http, cts.Token));
    }

    [Fact]
    public async Task DevelopmentLookupObservesCallerCancellation()
    {
        using var http = new HttpClient(new CancellationOnlyHandler());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(http, cts.Token));
    }

    private sealed class CancellationOnlyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("No live HTTP requests allowed.");
        }
    }
}
