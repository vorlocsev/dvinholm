namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for late, queued updater progress events. There is no
/// network, physical J2534 device or interactive WinUI session involved.
/// </summary>
public sealed class UpdateProgressGateTests
{
    [Fact]
    public void NewGateRejectsUnstartedProgress()
    {
        var gate = new UpdateProgressGate();
        Assert.False(gate.Accepts(0));
        Assert.False(gate.Accepts(1));
    }

    [Fact]
    public void BeginningAttemptAcceptsOnlyItsEpoch()
    {
        var gate = new UpdateProgressGate();
        var epoch = gate.Begin();
        Assert.True(gate.Accepts(epoch));
        Assert.False(gate.Accepts(epoch + 1));
    }

    [Fact]
    public void CompletedAttemptRejectsItsQueuedMessages()
    {
        var gate = new UpdateProgressGate();
        var epoch = gate.Begin();
        gate.Complete(epoch);
        Assert.False(gate.Accepts(epoch));
    }

    [Fact]
    public void CompletionCanBeRepeatedAfterSuccessOrCatch()
    {
        var gate = new UpdateProgressGate();
        var epoch = gate.Begin();
        gate.Complete(epoch);
        gate.Complete(epoch);
        Assert.False(gate.Accepts(epoch));
    }

    [Fact]
    public void RestartRevokesAllPreviousDownloadEvents()
    {
        var gate = new UpdateProgressGate();
        var first = gate.Begin();
        var second = gate.Begin();
        Assert.NotEqual(first, second);
        Assert.False(gate.Accepts(first));
        Assert.True(gate.Accepts(second));
    }

    [Fact]
    public void LateOldCompletionDoesNotStopNewerUpdate()
    {
        var gate = new UpdateProgressGate();
        var old = gate.Begin();
        var current = gate.Begin();
        gate.Complete(old);
        Assert.True(gate.Accepts(current));
        gate.Complete(current);
        Assert.False(gate.Accepts(current));
    }

    [Fact]
    public void QueuedMessagesCannotOverwriteFailedOrNewerAttempt()
    {
        var gate = new UpdateProgressGate();
        var queued = new Queue<Action>();
        var shown = new List<string>();

        var failedEpoch = gate.Begin();
        queued.Enqueue(() => { if (gate.Accepts(failedEpoch)) shown.Add("old download"); });
        gate.Complete(failedEpoch);

        var retryEpoch = gate.Begin();
        queued.Enqueue(() => { if (gate.Accepts(retryEpoch)) shown.Add("new download"); });
        while (queued.TryDequeue(out var callback))
            callback();

        Assert.Equal(["new download"], shown);
        gate.Complete(retryEpoch);
    }

    [Fact]
    public void WindowChecksEpochOnDispatcherAndRevokesBeforeErrorOrReplacement()
    {
        var code = ReadWindowSource();
        var start = code.IndexOf("private async Task DownloadAndLaunchLatestBuildAsync(", StringComparison.Ordinal);
        var end = code.IndexOf("private static string ProfileCatalogPath", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = code[start..end];

        Assert.Contains("var progressEpoch = _updateProgressGate.Begin();", region);
        Assert.Contains("if (_closing || !_updateProgressGate.Accepts(progressEpoch)) return;", region);
        Assert.Contains("DispatcherQueue.TryEnqueue(() =>", region);

        var returned = region.IndexOf("await LatestBuildUpdater.DownloadAndPrepareAsync(build, progress)", StringComparison.Ordinal);
        var stopped = region.IndexOf("_updateProgressGate.Complete(progressEpoch);", returned, StringComparison.Ordinal);
        var replacement = region.IndexOf("LatestBuildUpdater.StartReplacement(", returned, StringComparison.Ordinal);
        Assert.True(returned >= 0 && stopped > returned && replacement > stopped);

        var caught = region.IndexOf("catch (Exception ex)", StringComparison.Ordinal);
        var stopOnFailure = region.IndexOf("_updateProgressGate.Complete(progressEpoch);", caught, StringComparison.Ordinal);
        var showFailure = region.IndexOf("FinishUpdateActivity($\"Update failed:", caught, StringComparison.Ordinal);
        Assert.True(caught >= 0 && stopOnFailure > caught && showFailure > stopOnFailure);
    }

    private static string ReadWindowSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(file))
                return File.ReadAllText(file);
        }

        throw new FileNotFoundException("MainWindow.xaml.cs not found for source contract test.");
    }
}
