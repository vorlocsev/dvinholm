namespace Dvinholm.App.Tests;

public sealed class CanRxDispatcherStopFromCallbackTests
{
    [Fact]
    public async Task StopFromReadErrorHandlerStopsCurrentWorkerAndAllowsRestart()
    {
        var adapter = new FakeJ2534Adapter();
        adapter.ThrowNextRead();
        using var dispatcher = new Dvinholm.App.CanRxDispatcher(adapter);
        using var stopReturned = new ManualResetEventSlim();

        dispatcher.ReadError += error =>
        {
            if (!error.Message.Contains("simulated adapter disconnect", StringComparison.Ordinal))
                return;

            // Stop is invoked on the receive worker itself. It must request
            // cancellation without synchronously joining the current worker.
            dispatcher.Stop();
            stopReturned.Set();
        };

        dispatcher.Start();

        Assert.True(stopReturned.Wait(TimeSpan.FromSeconds(2)),
            "Stop from the read-error callback did not return.");
        Assert.Equal(1, adapter.ReadCount);

        // The first worker needs to finish unwinding its error handler before
        // Start can safely create another native reader.
        var restarted = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                dispatcher.Start();
                restarted = true;
                break;
            }
            catch (InvalidOperationException)
            {
                await Task.Delay(10);
            }
        }

        Assert.True(restarted, "The stopped receive worker did not exit in time.");

        var readDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (adapter.ReadCount < 2 && DateTime.UtcNow < readDeadline)
            await Task.Delay(10);

        Assert.True(adapter.ReadCount >= 2, "The restarted worker did not read from the adapter.");
        dispatcher.Stop();
    }
}
