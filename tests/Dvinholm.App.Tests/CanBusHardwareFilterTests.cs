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
}
