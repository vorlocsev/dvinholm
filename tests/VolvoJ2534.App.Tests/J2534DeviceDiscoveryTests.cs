namespace VolvoJ2534.App.Tests;

public sealed class J2534DeviceDiscoveryTests
{
    [Fact]
    public void TryCreateDevice_UsesRegistryNameWhenDisplayNameIsMissing()
    {
        var created = VolvoJ2534.App.J2534DeviceDiscovery.TryCreateDevice(
            registryName: "VirtualAdapter",
            name: null,
            vendor: null,
            dllPath: @"C:\\Drivers\\j2534.dll",
            registryPath: @"SOFTWARE\PassThruSupport.04.04",
            out var device);

        Assert.True(created);
        Assert.Equal("VirtualAdapter", device.Name);
        Assert.Equal(string.Empty, device.Vendor);
        Assert.Equal(@"C:\\Drivers\\j2534.dll", device.DllPath);
        Assert.Equal(@"SOFTWARE\PassThruSupport.04.04\VirtualAdapter", device.RegistryKey);
        Assert.Equal("VirtualAdapter", device.Label);
    }

    [Fact]
    public void TryCreateDevice_IncludesVendorInLabelAndExpandsEnvironmentVariables()
    {
        var variable = "VOLVO_J2534_TEST_PATH";
        var previous = Environment.GetEnvironmentVariable(variable);
        var expectedPath = Path.Combine(Path.GetTempPath(), "adapter.dll");
        try
        {
            Environment.SetEnvironmentVariable(variable, expectedPath);
            var created = VolvoJ2534.App.J2534DeviceDiscovery.TryCreateDevice(
                "AdapterKey", "Mongoose", "Drew Technologies",
                $"%{variable}%", @"SOFTWARE\PassThruSupport.04.04", out var device);

            Assert.True(created);
            Assert.Equal(expectedPath, device.DllPath);
            Assert.Equal("Mongoose — Drew Technologies", device.Label);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreateDevice_RejectsMissingDllPath(string? path)
    {
        Assert.False(VolvoJ2534.App.J2534DeviceDiscovery.TryCreateDevice(
            "AdapterKey", "Adapter", "Vendor", path,
            @"SOFTWARE\PassThruSupport.04.04", out var device));
        Assert.Equal(string.Empty, device.DllPath);
    }
}
