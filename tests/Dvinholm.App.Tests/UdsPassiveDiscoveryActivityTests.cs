namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for passive discovery's public activity boundary.
/// No physical J2534, VirtualCar or ECU transmissions are involved.
/// </summary>
public sealed class UdsPassiveDiscoveryActivityTests
{
    private const string PrivateVin = "YV1ABCDEF12345678";
    private const string PrivatePath = @"C:\private\garage\adapter.dll";

    [Fact]
    public void CompletedScanReportsObservationNotAuthentication()
    {
        var activity = UdsPassiveDiscoveryActivity.Message(UdsPassiveDiscoveryOutcome.Completed);
        Assert.Contains("scan complete", activity);
        Assert.Contains("not verified module identities", activity);
        Assert.DoesNotContain("CEM detected", activity);
    }

    [Fact]
    public void CancelledScanRetainsOnlyNonSelectablePartialWarning()
    {
        var activity = UdsPassiveDiscoveryActivity.Message(
            UdsPassiveDiscoveryOutcome.CancelledWithObservations);
        Assert.Contains("cancelled", activity);
        Assert.Contains("not selectable", activity);
        Assert.Contains("no ECU identity verified", activity);
    }

    [Fact]
    public void ReceiveQueueOverflowDoesNotClaimACompleteScan()
    {
        var activity = UdsPassiveDiscoveryActivity.Message(
            UdsPassiveDiscoveryOutcome.IncompleteWithDrops);
        Assert.Contains("incomplete", activity);
        Assert.Contains("receive-queue drops", activity);
        Assert.Contains("not selectable", activity);
        Assert.DoesNotContain("complete.", activity);
    }

    [Fact]
    public void PreStartCancellationDoesNotInventObservations()
    {
        var activity = UdsPassiveDiscoveryActivity.Message(
            UdsPassiveDiscoveryOutcome.CancelledBeforeStart);
        Assert.Contains("cancelled before observations", activity);
        Assert.DoesNotContain("observed:", activity);
    }

    [Fact]
    public void UnexpectedTransportFailureHasFixedRecoveryHint()
    {
        var activity = UdsPassiveDiscoveryActivity.Message(UdsPassiveDiscoveryOutcome.Failed);
        Assert.Contains("scan failed", activity);
        Assert.Contains("Check the adapter connection", activity);
        Assert.Contains("no ECU identity verified", activity);
    }

    [Fact]
    public void EveryPublicOutcomeExcludesRawIdentifiersAndExceptionDetails()
    {
        foreach (var outcome in Enum.GetValues<UdsPassiveDiscoveryOutcome>())
        {
            var activity = UdsPassiveDiscoveryActivity.Message(outcome);
            Assert.DoesNotContain(PrivateVin, activity);
            Assert.DoesNotContain(PrivatePath, activity);
            Assert.DoesNotContain("Exception", activity);
            Assert.DoesNotContain("request 0x", activity);
            Assert.DoesNotContain("response 0x", activity);
            Assert.DoesNotContain("0x7E8", activity);
        }
    }

    [Fact]
    public void UnknownOutcomeFailsClosedInsteadOfReusingSuccessfulText()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UdsPassiveDiscoveryActivity.Message((UdsPassiveDiscoveryOutcome)255));
    }

    [Fact]
    public void RealDiscoveryHandlerUsesAllFixedOutcomesAndPreservesPartialIsolation()
    {
        var source = MainWindowSource();
        var start = source.IndexOf("private async void Discover_Click(", StringComparison.Ordinal);
        var end = source.IndexOf("private void ClearPassiveDiscoverySelection(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = source[start..end];

        foreach (var kind in Enum.GetValues<UdsPassiveDiscoveryOutcome>())
        {
            Assert.Contains(
                "SetStatus(UdsPassiveDiscoveryActivity.Message(UdsPassiveDiscoveryOutcome." +
                kind + "));", region);
        }

        Assert.DoesNotContain("SetStatus(ex.Message);", region);
        Assert.DoesNotContain("FormatSafeException(", region);
        Assert.DoesNotContain("catch (Exception ex)", region);
        Assert.Contains("ShowDiscoveredEcus(ex.Candidates, complete: false)", region);
        Assert.Contains("UdsPassiveEcuListPresentation.ScanSummary(ex.Candidates.Count, false)", region);
        Assert.Contains("ex.DroppedFrameCount", region);
        Assert.Contains("_udsGate.Release()", region);

        var selection = source.IndexOf("private void ShowDiscoveredEcus(", StringComparison.Ordinal);
        var selectionEnd = source.IndexOf("private void CancelDiscovery_Click(", selection,
            StringComparison.Ordinal);
        Assert.True(selection >= 0 && selectionEnd > selection);
        var selector = source[selection..selectionEnd];
        Assert.Contains("EcusView.IsEnabled = complete && found.Count > 0;", selector);
        Assert.Contains("EcuSelector.IsEnabled = complete && found.Count > 0;", selector);
    }

    private static string MainWindowSource()
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
