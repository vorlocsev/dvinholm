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

        Assert.Equal(new[] { "new download" }, shown);
        gate.Complete(retryEpoch);
    }
}
