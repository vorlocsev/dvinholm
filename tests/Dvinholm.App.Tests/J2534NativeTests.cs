namespace Dvinholm.App.Tests;

public sealed class J2534NativeTests
{
    [Theory]
    [InlineData(@"Vendor failure at C:\Users\Example\adapter.dll", "[local path redacted]")]
    [InlineData(@"Vendor failure at \\workstation\share\adapter.dll", "[local path redacted]")]
    [InlineData("Vendor failure at /home/example/adapter.so", "[local path redacted]")]
    public void SanitizeDriverDiagnostic_RedactsLocalPaths(string detail, string expected)
    {
        var sanitized = Dvinholm.App.J2534Native.SanitizeDriverDiagnostic(detail);

        Assert.Contains(expected, sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\Users\Example", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/home/example", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("workstation", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeDriverDiagnostic_PreservesUsefulTextAndBoundsLength()
    {
        Assert.Equal("Device is busy; retry later",
            Dvinholm.App.J2534Native.SanitizeDriverDiagnostic("Device is busy; retry later"));

        var longDetail = new string('x', 300);
        var sanitized = Dvinholm.App.J2534Native.SanitizeDriverDiagnostic(longDetail);
        Assert.Equal(241, sanitized.Length);
        Assert.EndsWith("…", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RejectsEmptyPathAndLeavesWrapperUnloaded()
    {
        using var native = new Dvinholm.App.J2534Native();

        Assert.False(native.Load("  ", out var loadError));
        Assert.Contains("path is empty", loadError, StringComparison.OrdinalIgnoreCase);

        Assert.False(native.Open(out var openError));
        Assert.Contains("not loaded", openError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_RejectsMissingLibraryAndCanBeRetried()
    {
        using var native = new Dvinholm.App.J2534Native();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-j2534.dll");

        Assert.False(native.Load(missingPath, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.DoesNotContain(missingPath, error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("selected J2534 DLL", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(native.Open(out var openError));
        Assert.Contains("not loaded", openError, StringComparison.OrdinalIgnoreCase);

        // A failed load must not poison the wrapper's state.
        Assert.False(native.Load(missingPath, out var retryError));
        Assert.False(string.IsNullOrWhiteSpace(retryError));
        Assert.DoesNotContain(missingPath, retryError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConnectWithoutLoadedDeviceReturnsDiagnosticInsteadOfThrowing()
    {
        using var native = new Dvinholm.App.J2534Native();

        Assert.False(native.Connect(500000, out var error));
        Assert.Contains("not open", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadAndWriteWithoutConnectedChannelReturnDiagnostic()
    {
        using var native = new Dvinholm.App.J2534Native();

        Assert.False(native.Read(out _, 100, out var readError));
        Assert.Contains("not connected", readError, StringComparison.OrdinalIgnoreCase);

        var message = default(Dvinholm.App.J2534Native.PassthruMsg);
        Assert.False(native.Write(in message, 100, out var writeError));
        Assert.Contains("not connected", writeError, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class J2534SessionFailureTests
{
    [Fact]
    public void ConnectWithEmptyPathLeavesSessionDisconnected()
    {
        using var session = new Dvinholm.App.J2534Session();

        Assert.False(session.Connect(" ", 500000, out var error));
        Assert.Contains("path is empty", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(session.IsConnected);
        Assert.Throws<InvalidOperationException>(() => _ = session.Bus);
    }


    [Fact]
    public void ConnectRejectsZeroBaudRateBeforeLoadingDriver()
    {
        using var session = new Dvinholm.App.J2534Session();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-j2534.dll");

        Assert.False(session.Connect(missingPath, 0, out var error));
        Assert.Contains("baud rate", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(session.IsConnected);
    }

    [Fact]
    public void FailedConnectionCanBeRetriedAndRemainsDisconnected()
    {
        using var session = new Dvinholm.App.J2534Session();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-j2534.dll");

        Assert.False(session.Connect(missingPath, 500000, out var firstError));
        Assert.False(session.IsConnected);
        Assert.False(string.IsNullOrWhiteSpace(firstError));

        Assert.False(session.Connect(missingPath, 500000, out var secondError));
        Assert.False(session.IsConnected);
        Assert.False(string.IsNullOrWhiteSpace(secondError));
    }
}
