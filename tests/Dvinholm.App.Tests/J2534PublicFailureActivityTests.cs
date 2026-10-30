namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline failure-activity and real-handler contracts. No J2534 DLL,
/// real device, CAN TX, VirtualCar or physical ECU is used.
/// </summary>
public sealed class J2534PublicFailureActivityTests
{
    private const string PrivateVin = "YV1ABCDEF12345678";
    private const string PrivatePath = @"C:\private\garage\driver.dll";

    [Fact]
    public void ConnectionRejectionHasActionableFixedHint()
    {
        var text = J2534PublicFailureActivity.Message(J2534PublicFailure.ConnectionRejected);
        Assert.Contains("connection failed", text);
        Assert.Contains("32-bit DLL", text);
        Assert.Contains("CAN speed", text);
    }

    [Fact]
    public void UnexpectedConnectionFailureNeverNeedsAnExceptionMessage()
    {
        var text = J2534PublicFailureActivity.Message(J2534PublicFailure.ConnectionFault);
        Assert.Contains("unexpectedly", text);
        Assert.Contains("driver", text);
        Assert.DoesNotContain(PrivatePath, text);
    }

    [Fact]
    public void DisconnectAndMonitorTeardownMessagesAreDistinctAndPrivate()
    {
        var disconnect = J2534PublicFailureActivity.Message(J2534PublicFailure.DisconnectFault);
        var monitor = J2534PublicFailureActivity.Message(J2534PublicFailure.MonitorFaultDuringDisconnect);
        Assert.Contains("Session controls were reset", disconnect);
        Assert.Contains("previous CAN monitor", monitor);
        Assert.NotEqual(disconnect, monitor);
        Assert.DoesNotContain(PrivatePath, disconnect + monitor);
    }

    [Fact]
    public void MonitorStartFailureDoesNotClaimActiveCapture()
    {
        var text = J2534PublicFailureActivity.Message(J2534PublicFailure.MonitorStartFault);
        Assert.Contains("monitor could not start", text);
        Assert.DoesNotContain("Monitoring CAN...", text);
    }

    [Fact]
    public void FilterApplyRemoveAndUnexpectedFailuresHaveSeparateActions()
    {
        var apply = J2534PublicFailureActivity.Message(J2534PublicFailure.FilterApplyRejected);
        var remove = J2534PublicFailureActivity.Message(J2534PublicFailure.FilterRemoveRejected);
        var unexpected = J2534PublicFailureActivity.Message(J2534PublicFailure.FilterUnexpected);
        Assert.Contains("not applied", apply);
        Assert.Contains("could not be removed", remove);
        Assert.Contains("unexpectedly", unexpected);
        Assert.NotEqual(apply, remove);
        Assert.NotEqual(remove, unexpected);
    }

    [Fact]
    public void EveryFailureActivityIsPayloadFreeAndUnknownCasesThrow()
    {
        foreach (var failure in Enum.GetValues<J2534PublicFailure>())
        {
            var text = J2534PublicFailureActivity.Message(failure);
            Assert.NotEmpty(text);
            Assert.DoesNotContain(PrivateVin, text);
            Assert.DoesNotContain(PrivatePath, text);
            Assert.DoesNotContain("0x7E8", text);
            Assert.DoesNotContain("Exception", text);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            J2534PublicFailureActivity.Message((J2534PublicFailure)255));
    }

    [Fact]
    public void RealConnectionHandlerNeverForwardsVendorErrorsToAurora()
    {
        var section = Between(MainSource(), "private async void Connect_Click(",
            "private void Start_Click(");
        foreach (var kind in new[]
        {
            J2534PublicFailure.ConnectionRejected,
            J2534PublicFailure.ConnectionFault,
            J2534PublicFailure.DisconnectFault,
            J2534PublicFailure.MonitorFaultDuringDisconnect
        })
            Assert.Contains("J2534PublicFailureActivity.Message(J2534PublicFailure." +
                            kind + ")", section);
        Assert.DoesNotContain("result.error)", section);
        Assert.DoesNotContain("FormatSafeException(", section);
        Assert.DoesNotContain("monitorWarning", section);
        Assert.Contains("catch (Exception)", section);
        Assert.Contains("_connected = false;", section);
        Assert.Contains("StartButton.IsEnabled = false;", section);
        Assert.Contains("ConnectButton.Content = \"Connect\";", section);
    }

    [Fact]
    public void MonitorAndHardwareFilterGuardNativeFailuresWithoutArmingUi()
    {
        var source = MainSource();
        var start = Between(source, "private void StartMonitor()", "private void StopMonitor()");
        var createCts = start.IndexOf("var monitorCts = new CancellationTokenSource();",
            StringComparison.Ordinal);
        var subscribe = start.IndexOf("subscription = _session.Bus.Subscribe();",
            StringComparison.Ordinal);
        var disposal = start.IndexOf("monitorCts.Dispose();", StringComparison.Ordinal);
        var assignCts = start.IndexOf("_cts = monitorCts;", StringComparison.Ordinal);
        var arm = start.IndexOf("StartButton.Content = \"Stop monitor\";", StringComparison.Ordinal);
        Assert.True(createCts >= 0 && createCts < subscribe && subscribe < disposal &&
                    disposal < assignCts && assignCts < arm);
        Assert.Contains("catch (Exception)", start);
        Assert.Contains("J2534PublicFailure.MonitorStartFault", start);

        var hardware = Between(source, "private void HardwareFilter_Click(",
            "private void Filter_Changed(");
        Assert.Contains("if (!bus.StopCanFilter(activeFilterId, out _))", hardware);
        Assert.Contains("if (!bus.StartCanIdFilter(id, extended, out var filterId, out _))", hardware);
        Assert.Contains("J2534PublicFailure.FilterRemoveRejected", hardware);
        Assert.Contains("J2534PublicFailure.FilterApplyRejected", hardware);
        Assert.Contains("J2534PublicFailure.FilterUnexpected", hardware);
        Assert.Contains("catch (Exception)", hardware);
        Assert.DoesNotContain("stopError", hardware);
        Assert.DoesNotContain("startError", hardware);
        Assert.Contains("_activeHardwareFilterId = filterId;", hardware);
        Assert.Contains("_activeHardwareFilterId = null;", hardware);
    }

    private static string Between(string source, string first, string next)
    {
        var start = source.IndexOf(first, StringComparison.Ordinal);
        var end = source.IndexOf(next, Math.Max(0, start) + first.Length,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }

    private static string MainSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "Dvinholm.App",
                "MainWindow.xaml.cs");
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        throw new FileNotFoundException("src/Dvinholm.App/MainWindow.xaml.cs");
    }
}
