using System.Net;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for download cancellation on window close and
/// prevention of unexpected replacement. No installed updater is launched.
/// </summary>
public sealed class UpdateDownloadCloseCancellationTests
{
    private static string WindowCode()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(file))
                return File.ReadAllText(file).Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        throw new FileNotFoundException("MainWindow.xaml.cs missing.");
    }

    private static string DownloadMethod()
    {
        var code = WindowCode();
        var start = code.IndexOf("private async Task DownloadAndLaunchLatestBuildAsync(", StringComparison.Ordinal);
        var end = code.IndexOf("private static string ProfileCatalogPath", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return code[start..end];
    }

    [Fact]
    public void WindowCloseCancelsBothLookupAndDownloadTokens()
    {
        var code = WindowCode();
        Assert.Contains("private CancellationTokenSource? _updateDownloadCts;", code);
        var start = code.IndexOf("Closed += (_, _) =>", StringComparison.Ordinal);
        var end = code.IndexOf("StopMonitor();", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var close = code[start..end];
        Assert.Contains("_closing = true;", close);
        Assert.Contains("_updateCheckCts?.Cancel();", close);
        Assert.Contains("_updateDownloadCts?.Cancel();", close);
    }

    [Fact]
    public void ClosedWindowCannotStartANewArchiveDownload()
    {
        var code = DownloadMethod();
        var closeGuard = code.IndexOf("if (_closing)\n            return;", StringComparison.Ordinal);
        var cts = code.IndexOf("using var downloadCts = new CancellationTokenSource();", StringComparison.Ordinal);
        var busy = code.IndexOf("_updaterBusy = true;", StringComparison.Ordinal);
        Assert.True(closeGuard >= 0 && cts > closeGuard && busy > cts);
    }

    [Fact]
    public void DownloadUsesActiveCloseCancellationToken()
    {
        var code = DownloadMethod();
        Assert.Contains("_updateDownloadCts = downloadCts;", code);
        Assert.Contains("DownloadAndPrepareAsync(\n                build, progress, downloadCts.Token)", code);
        Assert.DoesNotContain("DownloadAndPrepareAsync(build, progress);", code);
    }

    [Fact]
    public void WindowRechecksCancellationBeforeAnyReplacement()
    {
        var code = DownloadMethod();
        var download = code.IndexOf("stagedDirectory = await LatestBuildUpdater.DownloadAndPrepareAsync(", StringComparison.Ordinal);
        var guard = code.IndexOf("downloadCts.Token.ThrowIfCancellationRequested();", StringComparison.Ordinal);
        var closed = code.IndexOf("if (_closing)\n                throw new OperationCanceledException(downloadCts.Token);", StringComparison.Ordinal);
        var launch = code.IndexOf("LatestBuildUpdater.StartReplacement(", StringComparison.Ordinal);
        Assert.True(download >= 0 && guard > download && closed > guard && launch > closed);
    }

    [Fact]
    public void ClosedWindowDoesNotWriteFailureLogOrTouchUi()
    {
        var code = DownloadMethod();
        var closeCatch = code.IndexOf("catch (Exception) when (_closing)", StringComparison.Ordinal);
        var userCatch = code.IndexOf("catch (OperationCanceledException) when (!_closing", closeCatch, StringComparison.Ordinal);
        var normalCatch = code.IndexOf("catch (Exception ex)", userCatch, StringComparison.Ordinal);
        var logWrite = code.IndexOf("File.WriteAllText(logPath", StringComparison.Ordinal);
        Assert.True(closeCatch >= 0 && userCatch > closeCatch && normalCatch > userCatch && logWrite > normalCatch);
        // Only the close handler must stay UI-free: the separate user-cancel
        // branch intentionally updates retry controls on a still-open window.
        var canceledBranch = code[closeCatch..userCatch];
        Assert.DoesNotContain("UpdateStatusText.", canceledBranch);
        Assert.DoesNotContain("RunUpdateButton.", canceledBranch);
        Assert.DoesNotContain("SetStatus(", canceledBranch);
    }

    [Fact]
    public void LateCancellationDiscardsFullyPreparedPrivateStageOnly()
    {
        var code = DownloadMethod();
        var closeCatch = code.IndexOf("catch (Exception) when (_closing)", StringComparison.Ordinal);
        var normalCatch = code.IndexOf("catch (Exception ex)", closeCatch, StringComparison.Ordinal);
        Assert.True(closeCatch >= 0 && normalCatch > closeCatch);
        var branch = code[closeCatch..normalCatch];
        Assert.Contains("if (stagedDirectory is not null)", branch);
        Assert.Contains("LatestBuildUpdater.TryCleanup(() => Directory.Delete(stagedDirectory, recursive: true));", branch);
        Assert.DoesNotContain("AppContext.BaseDirectory", branch);
        Assert.DoesNotContain("StartReplacement", branch);
    }

    [Fact]
    public void CleanupClearsProgressAndTokenWithoutTouchingClosedControls()
    {
        var code = DownloadMethod();
        var end = code[code.IndexOf("finally\n", StringComparison.Ordinal)..];
        Assert.Contains("_updateProgressGate.Complete(progressEpoch);", end);
        Assert.Contains("_updateDownloadCts = null;", end);
        Assert.Contains("_updaterBusy = false;", end);
        Assert.Contains("if (!_closing)", end);
        Assert.Contains("RunUpdateButton.IsEnabled = true;", end);
        Assert.Contains("UpdateSource.IsEnabled = true;", end);
    }

    [Fact]
    public async Task InFlightDownloadHonorsCancellationOnRealHttpBoundary()
    {
        using var handler = new BlockingHandler();
        using var http = new HttpClient(handler);
        using var cts = new CancellationTokenSource();
        var build = new LatestBuildInfo(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 3, 3,
            "https://example.invalid/archive.zip", 1000, null,
            "2026-10-26T00:00:00Z");

        var task = LatestBuildUpdater.DownloadAndPrepareAsync(build, http, cancellationToken: cts.Token);
        await handler.Started.WaitAsync(TimeSpan.FromSeconds(10));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Started => _started.Task;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
