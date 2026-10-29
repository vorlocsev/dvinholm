using System.Net;

namespace Dvinholm.App.Tests;

/// <summary>Retained transport-level cancellation regression. GUI update control now belongs to Launcher.</summary>
public sealed class UpdateDownloadCloseCancellationTests
{
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
