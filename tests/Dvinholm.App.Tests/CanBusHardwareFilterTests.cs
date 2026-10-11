namespace Dvinholm.App.Tests;

public sealed class CanBusHardwareFilterTests
{
    [Fact]
    public void StartAndStopCanIdFilter_TracksFilterLifecycle()
    {
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        Assert.True(bus.StartCanIdFilter(0x7E8, extended: false, out var filterId, out var error), error);
        Assert.NotEqual(0u, filterId);
        Assert.Equal(1, adapter.ActiveFilterCount);

        Assert.True(bus.StopCanFilter(filterId, out error), error);
        Assert.Equal(0, adapter.ActiveFilterCount);
    }

    [Theory]
    [InlineData(0x800u, false)]
    [InlineData(0x20000000u, true)]
    public void StartCanIdFilter_RejectsOutOfRangeIdWithoutDriverCall(uint id, bool extended)
    {
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        Assert.False(bus.StartCanIdFilter(id, extended, out var filterId, out var error));
        Assert.Equal(0u, filterId);
        Assert.Contains("out of range", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, adapter.ActiveFilterCount);
    }

    [Fact]
    public void StopCanFilter_RejectsUnknownFilterId()
    {
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>());
        using var bus = new Dvinholm.App.CanBus(adapter);
        bus.Start();

        Assert.False(bus.StopCanFilter(999, out var error));
        Assert.Contains("not active", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisposeWaitsForInFlightHardwareFilterCall()
    {
        using var filterEntered = new ManualResetEventSlim();
        using var allowFilterToFinish = new ManualResetEventSlim();
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>())
        {
            FilterCallEntered = filterEntered,
            AllowFilterCallToFinish = allowFilterToFinish
        };
        var bus = new Dvinholm.App.CanBus(adapter);

        var filterTask = Task.Run(() =>
            bus.StartCanIdFilter(0x7E8, extended: false, out _, out _));
        Task? disposeTask = null;

        try
        {
            Assert.True(filterEntered.Wait(TimeSpan.FromSeconds(1)),
                "The hardware filter call did not start.");
            disposeTask = Task.Run(bus.Dispose);

            await Task.Delay(100);
            Assert.False(disposeTask.IsCompleted,
                "CanBus.Dispose returned while a native filter call was active.");
        }
        finally
        {
            allowFilterToFinish.Set();
        }

        Assert.True(await filterTask);
        await (disposeTask ?? Task.Run(bus.Dispose)).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Throws<ObjectDisposedException>(() =>
            bus.StartCanIdFilter(0x7E8, false, out _, out _));
    }

    [Fact]
    public async Task DisposeWaitsForInFlightHardwareFilterRemoval()
    {
        using var stopFilterEntered = new ManualResetEventSlim();
        using var allowStopFilterToFinish = new ManualResetEventSlim();
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>())
        {
            StopFilterCallEntered = stopFilterEntered,
            AllowStopFilterCallToFinish = allowStopFilterToFinish
        };
        var bus = new Dvinholm.App.CanBus(adapter);
        Assert.True(bus.StartCanIdFilter(0x7E8, extended: false, out var filterId, out var error), error);

        var stopTask = Task.Run(() => bus.StopCanFilter(filterId, out _));
        Task? disposeTask = null;

        try
        {
            Assert.True(stopFilterEntered.Wait(TimeSpan.FromSeconds(1)),
                "The hardware filter removal call did not start.");
            disposeTask = Task.Run(bus.Dispose);

            await Task.Delay(100);
            Assert.False(disposeTask.IsCompleted,
                "CanBus.Dispose returned while a native filter removal call was active.");
        }
        finally
        {
            allowStopFilterToFinish.Set();
        }

        Assert.True(await stopTask);
        await (disposeTask ?? Task.Run(bus.Dispose)).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Throws<ObjectDisposedException>(() => bus.StopCanFilter(filterId, out _));
    }
    [Fact]
    public async Task DisposePreventsQueuedHardwareFilterCallAfterActiveCallCompletes()
    {
        using var filterEntered = new ManualResetEventSlim();
        using var allowFilterToFinish = new ManualResetEventSlim();
        using var secondCallAttempted = new ManualResetEventSlim();
        using var adapter = new VirtualJ2534Adapter(_ =>
            Array.Empty<Dvinholm.App.CanFrame>())
        {
            FilterCallEntered = filterEntered,
            AllowFilterCallToFinish = allowFilterToFinish
        };
        var bus = new Dvinholm.App.CanBus(adapter);

        var activeFilter = Task.Run(() =>
            bus.StartCanIdFilter(0x7E8, extended: false, out _, out _));
        Task<bool>? queuedFilter = null;
        Task? disposeTask = null;

        try
        {
            Assert.True(filterEntered.Wait(TimeSpan.FromSeconds(1)),
                "The first hardware filter call did not start.");

            queuedFilter = Task.Run(() =>
            {
                secondCallAttempted.Set();
                return bus.StartCanIdFilter(0x7E9, extended: false, out _, out _);
            });
            Assert.True(secondCallAttempted.Wait(TimeSpan.FromSeconds(1)),
                "The second filter operation did not start.");

            // Dispose marks the bus unavailable before waiting for the active
            // native call to leave the shared transmit/filter lock.
            disposeTask = Task.Run(bus.Dispose);
            await Task.Delay(100);
            Assert.False(disposeTask.IsCompleted,
                "Dispose returned while the active filter call was blocked.");
        }
        finally
        {
            allowFilterToFinish.Set();
        }

        Assert.True(await activeFilter.WaitAsync(TimeSpan.FromSeconds(2)));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await (queuedFilter ?? throw new InvalidOperationException("Queued filter was not started."))
                .WaitAsync(TimeSpan.FromSeconds(2)));
        await (disposeTask ?? Task.Run(bus.Dispose)).WaitAsync(TimeSpan.FromSeconds(2));

        // Only the first operation reached the adapter. The queued call must
        // re-check disposal after acquiring the lock, before entering native code.
        Assert.Equal(1, adapter.ActiveFilterCount);
    }

}
