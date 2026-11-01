namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline progress contracts for the existing opt-in UDS 0x22 snapshot.
/// All reads are injected; no J2534, VIN disclosure, PIN or ECU writes.
/// </summary>
public sealed class EcuSnapshotReadProgressTests
{
    [Fact]
    public void SuccessfulMultiDidCaptureReportsOrderedCountsWithoutChangingValues()
    {
        var updates = new List<(int Done, int Total)>();
        var calls = new List<ushort>();
        var snapshot = EcuConfigurationReadback.Capture(
            Profile(0xF190, 0xF187, 0xF189), (did, _) =>
            {
                calls.Add(did);
                return Response(did, 0xAA);
            }, reportProgress: (done, total) => updates.Add((done, total)));

        Assert.Equal(new ushort[] { 0xF187, 0xF189, 0xF190 }, calls);
        Assert.Equal(new[] { (1, 3), (2, 3), (3, 3) }, updates);
        Assert.Equal(new ushort[] { 0xF187, 0xF189, 0xF190 },
            snapshot.Values.Select(x => x.DataIdentifier));
        Assert.All(snapshot.Values, value => Assert.Equal("AA", value.ValueHex));
    }

    [Fact]
    public void ProgressReceivesNoDidNumbersHexPayloadOrProfileMetadata()
    {
        var progressData = new List<string>();
        var profile = new EcuProfile("SENSITIVE-PROFILE", "SENSITIVE-NAME",
            0x7E0, 0x7E8, false, [0xF190], "SENSITIVE-SOURCE");
        var result = EcuConfigurationReadback.Capture(profile,
            (did, _) => Response(did, 0x59, 0x56, 0x31),
            reportProgress: (done, total) =>
                progressData.Add($"{done}/{total}"));
        Assert.Equal(new[] { "1/1" }, progressData);
        Assert.Equal("595631", Assert.Single(result.Values).ValueHex);
        Assert.DoesNotContain("F190", progressData[0]);
        Assert.DoesNotContain("595631", progressData[0]);
        Assert.DoesNotContain("SENSITIVE", progressData[0]);
    }

    [Fact]
    public void MalformedFirstDidResponseNeverReportsProgress()
    {
        var calls = 0;
        Assert.Throws<InvalidDataException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187),
                (_, _) => [0xF1, 0x90, 0xAA],
                reportProgress: (_, _) => calls++));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void SecondReadFailureNeverReportsFalseCompletion()
    {
        var updates = new List<(int, int)>();
        var reads = 0;
        Assert.Throws<IOException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187, 0xF189),
                (did, _) =>
                {
                    reads++;
                    if (did == 0xF189) throw new IOException("Synthetic failure");
                    return Response(did, 0xAA);
                },
                reportProgress: (done, total) => updates.Add((done, total))));
        Assert.Equal(2, reads);
        Assert.Equal(new[] { (1, 2) }, updates);
    }

    [Fact]
    public void AlreadyCancelledCaptureNeverReadsOrReportsProgress()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var reads = 0;
        var updates = 0;
        Assert.ThrowsAny<OperationCanceledException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187),
                (did, _) =>
                {
                    reads++;
                    return Response(did, 0xAA);
                }, cts.Token,
                reportProgress: (_, _) => updates++));
        Assert.Equal(0, reads);
        Assert.Equal(0, updates);
    }

    [Fact]
    public void CancellationAfterFirstProgressPreventsNextDidRequest()
    {
        using var cts = new CancellationTokenSource();
        var calls = 0;
        var updates = new List<(int, int)>();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187, 0xF189),
                (did, _) =>
                {
                    calls++;
                    return Response(did, 0xBB);
                }, cts.Token, (done, total) =>
                {
                    updates.Add((done, total));
                    cts.Cancel();
                }));
        Assert.Equal(1, calls);
        Assert.Equal(new[] { (1, 2) }, updates);
    }

    [Fact]
    public void CancellationDuringResponseDiscardsProgressAndPartialRead()
    {
        using var cts = new CancellationTokenSource();
        var updates = 0;
        Assert.ThrowsAny<OperationCanceledException>(() =>
            EcuConfigurationReadback.Capture(Profile(0xF187),
                (did, _) =>
                {
                    cts.Cancel();
                    return Response(did, 0x11);
                }, cts.Token, (_, _) => updates++));
        Assert.Equal(0, updates);
    }

    [Fact]
    public void OptionalCallbackAndRealWinUiGatesKeepConsentAndLateProgressSafe()
    {
        var result = EcuConfigurationReadback.Capture(Profile(0xF187),
            (did, _) => Response(did, 0x00));
        Assert.Equal("00", Assert.Single(result.Values).ValueHex);

        var source = MainWindowSource();
        var start = source.IndexOf("private async void CaptureSnapshot_Click(",
            StringComparison.Ordinal);
        var end = source.IndexOf("private async void ReadDid_Click(", start,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var handler = source[start..end];
        Assert.Contains("Selected profile contains {profile!.ReadableDataIdentifiers.Count}", handler);
        Assert.Contains("await consent.ShowAsync()", handler);
        Assert.Contains("reportProgress: (completed, total)", handler);
        Assert.Contains("DispatcherQueue.TryEnqueue(", handler);
        Assert.Contains("readInProgress && !_closing", handler);
        Assert.Contains("!cts.IsCancellationRequested", handler);
        Assert.Contains("IsSnapshotContextCurrent(profile, captureGeneration)", handler);
        Assert.Contains("readInProgress = false;", handler);
        Assert.Contains("Not a complete snapshot until every DID succeeds.", handler);
        Assert.DoesNotContain("SetStatus(snapshot.Values", handler);
    }

    private static byte[] Response(ushort did, params byte[] data) =>
        [(byte)(did >> 8), (byte)did, .. data];

    private static EcuProfile Profile(params ushort[] dids) =>
        new("synthetic.ecu", "Synthetic ECU", 0x7E0, 0x7E8,
            false, dids, "Offline test only", false);

    private static string MainWindowSource()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory);
             folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(folder.FullName, "src",
                "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("src/Dvinholm.App/MainWindow.xaml.cs");
    }
}
