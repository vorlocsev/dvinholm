namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline guards for read-only snapshot context changes and private
/// failure activity. No vehicle, VirtualCar, J2534 adapter or filesystem write.
/// </summary>
public sealed class EcuSnapshotOperationContextTests
{
    private static EcuProfile Profile() =>
        new("test.ecm", "Synthetic ECU", 0x7E0, 0x7E8, false,
            new ushort[] { 0xF187, 0xF190 }, "Offline test only");

    [Fact]
    public void SameProfileAndUnchangedGenerationCanContinue()
    {
        var profile = Profile();
        Assert.True(EcuSnapshotOperationContext.IsCurrent(
            profile, profile, 7, 7, addressAndSessionStillValid: true));
    }

    [Fact]
    public void AnotherSelectedProfileWithIdenticalCanIdsInvalidatesCapture()
    {
        var oldProfile = Profile();
        var newProfile = Profile(); // Same ID and values but a different selection object.
        Assert.False(EcuSnapshotOperationContext.IsCurrent(
            oldProfile, newProfile, 7, 7, addressAndSessionStillValid: true));
    }

    [Fact]
    public void RequestOrDidEditInvalidatesThePendingGeneration()
    {
        var profile = Profile();
        var lifetime = new UdsReadResultLifetime();
        var captured = lifetime.Invalidate();
        lifetime.Invalidate();

        Assert.False(EcuSnapshotOperationContext.IsCurrent(
            profile, profile, captured, lifetime.Generation, true));
    }

    [Fact]
    public void InitialOrInvalidGenerationCannotBePresentedAsValid()
    {
        var profile = Profile();
        Assert.False(EcuSnapshotOperationContext.IsCurrent(profile, profile, 0, 0, true));
        Assert.False(EcuSnapshotOperationContext.IsCurrent(profile, profile, -1, -1, true));
    }

    [Fact]
    public void NoSessionOrChangedAddressesFailClosed()
    {
        var profile = Profile();
        Assert.False(EcuSnapshotOperationContext.IsCurrent(profile, profile, 8, 8, false));
        Assert.True(EcuSnapshotOperationContext.IsCurrent(profile, profile, 8, 8, true));
    }

    [Fact]
    public void MissingOrClearedSelectionCannotPreserveSnapshot()
    {
        var profile = Profile();
        Assert.False(EcuSnapshotOperationContext.IsCurrent(null, profile, 1, 1, true));
        Assert.False(EcuSnapshotOperationContext.IsCurrent(profile, null, 1, 1, true));
        Assert.False(EcuSnapshotOperationContext.IsCurrent(null, null, 1, 1, true));
    }

    [Fact]
    public void RealUiChecksContextAfterConsentAfterReadAndBeforeWriting()
    {
        var source = MainWindowSource();
        var helper = Between(source,
            "private bool IsSnapshotContextCurrent(", "private async void CaptureSnapshot_Click(");
        Assert.Contains("EcuSnapshotOperationContext.IsCurrent(", helper);
        Assert.Contains("ProfileSelector.SelectedItem as EcuProfile", helper);
        Assert.Contains("_udsResultLifetime.Generation", helper);
        Assert.Contains("CanCaptureSelectedSnapshot(startedProfile, busy: false)", helper);

        var handler = SnapshotHandler(source);
        var generation = handler.IndexOf("captureGeneration = ClearDiagnosticReadResult();",
            StringComparison.Ordinal);
        var consent = handler.IndexOf("await consent.ShowAsync()", StringComparison.Ordinal);
        var capture = handler.IndexOf("EcuConfigurationReadback.Capture(", StringComparison.Ordinal);
        var picker = handler.IndexOf("ReportSavePathPicker.PickPath(", StringComparison.Ordinal);
        var save = handler.IndexOf("await EcuConfigurationSnapshotExport.SaveToUserSelectedPathAsync(",
            StringComparison.Ordinal);
        Assert.True(generation >= 0 && generation < consent);
        Assert.True(consent < capture && capture < picker && picker < save);
        Assert.Contains("!IsSnapshotContextCurrent(profile, captureGeneration)",
            handler[consent..capture]);
        Assert.Contains("!IsSnapshotContextCurrent(profile, captureGeneration)",
            handler[capture..picker]);
        Assert.Contains("!IsSnapshotContextCurrent(profile, captureGeneration)",
            handler[picker..save]);
        Assert.Contains("cts.Token.ThrowIfCancellationRequested();", handler[picker..save]);
    }

    [Fact]
    public void RealUiGuardsLateErrorsAndDoesNotEchoExceptionOrFilePath()
    {
        var handler = SnapshotHandler(MainWindowSource());
        Assert.Contains("if (IsSnapshotContextCurrent(profile, captureGeneration))", handler);
        Assert.Contains("if (_udsResultLifetime.CanDisplay(captureGeneration))", handler);
        Assert.Contains("UdsDiagnosticReadResultPresentation.Error(UdsReadResultKind.Did, ex)", handler);
        Assert.Contains("Check the selected file if saving had started", handler);
        Assert.DoesNotContain("SetStatus(\"ECU DID snapshot rejected: \" + ex.Message)", handler);
        Assert.DoesNotContain("FormatSafeException(ex,", handler);
        Assert.DoesNotContain("Path.GetFileName(destinationPath)", handler);
        Assert.Contains("saved a sensitive diagnostic snapshot to the selected file", handler);

        var save = handler.IndexOf(
            "await EcuConfigurationSnapshotExport.SaveToUserSelectedPathAsync(",
            StringComparison.Ordinal);
        var completed = handler.IndexOf("SetStatus($\"Read {snapshot.Values.Count}", save,
            StringComparison.Ordinal);
        Assert.True(save >= 0 && completed > save);
        Assert.Contains("if (IsSnapshotContextCurrent(profile, captureGeneration))",
            handler[save..completed]);
    }

    private static string SnapshotHandler(string source) =>
        Between(source, "private async void CaptureSnapshot_Click(",
            "private async void ReadDid_Click(");

    private static string Between(string source, string first, string next)
    {
        var start = source.IndexOf(first, StringComparison.Ordinal);
        var end = source.IndexOf(next, Math.Max(0, start) + first.Length,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }

    private static string MainWindowSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "src", "Dvinholm.App",
                "MainWindow.xaml.cs");
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException("src/Dvinholm.App/MainWindow.xaml.cs");
    }
}
