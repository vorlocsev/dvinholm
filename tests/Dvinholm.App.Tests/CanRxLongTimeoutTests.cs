namespace Dvinholm.App.Tests;

public sealed class CanRxLongTimeoutTests
{
    [Fact]
    public async Task Subscription_TryReadSupportsLongTimeoutAndStillHonorsCancellation()
    {
        using var adapter = new Dvinholm.App.J2534Native();
        using var dispatcher = new Dvinholm.App.CanRxDispatcher(adapter);
        using var subscription = dispatcher.Subscribe();
        using var cancellation = new CancellationTokenSource();

        var read = Task.Run(() =>
            subscription.TryRead(TimeSpan.FromDays(50), cancellation.Token, out _));

        // A timeout beyond the platform timer limit must not throw while
        // registering the wait, and caller cancellation must still wake it.
        await Task.Delay(50);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await read.WaitAsync(TimeSpan.FromSeconds(2)));
    }
}
