namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic read-only result-lifetime acceptance checks.
/// No VirtualCar, native J2534 adapter, ECU or CAN transmission is used.
/// </summary>
public sealed class UdsLateCompletionCancellationTests
{
    [Fact]
    public void ExplicitCancellationInvalidatesSuccessfulLateResponse()
    {
        var lifetime = new UdsReadResultLifetime();
        var request = lifetime.Invalidate();
        Assert.True(lifetime.CanDisplay(request));

        lifetime.Invalidate(); // Cancel clicked while the worker is still running.
        Assert.False(lifetime.CanDisplay(request));
    }

    [Fact]
    public void ExplicitCancellationInvalidatesLateFailureActivity()
    {
        var lifetime = new UdsReadResultLifetime();
        var request = lifetime.Invalidate();
        lifetime.Invalidate();

        // The same token guards success and exception status.
        Assert.False(lifetime.CanDisplay(request));
        Assert.True(lifetime.CanDisplay(lifetime.Generation));
    }

    [Fact]
    public void ASubsequentReadCanPublishWithoutRevivingCancelledOne()
    {
        var lifetime = new UdsReadResultLifetime();
        var cancelled = lifetime.Invalidate();
        lifetime.Invalidate();
        var newer = lifetime.Invalidate();

        Assert.False(lifetime.CanDisplay(cancelled));
        Assert.True(lifetime.CanDisplay(newer));
        Assert.Equal(3, newer);
    }

    [Fact]
    public void CancelHandlerInvalidatesBeforeRequestingCooperativeCancellation()
    {
        var source = MainWindowSource();
        var handler = Between(source, "private void CancelUds_Click(", "private async void Connect_Click(");
        var invalidate = handler.IndexOf("ClearDiagnosticReadResult();", StringComparison.Ordinal);
        var cancel = handler.IndexOf("cts.Cancel();", StringComparison.Ordinal);

        Assert.True(invalidate >= 0 && cancel > invalidate);
        Assert.Contains("if (_udsCts is not { } cts)", handler);
        Assert.Contains("late output will be discarded", handler);
        Assert.DoesNotContain("DiagnosticResultText.Text =", handler);
    }

    [Fact]
    public void InFlightContextEditsPublishOnlySafeNonPayloadActivity()
    {
        var source = MainWindowSource();
        var clear = Between(source, "private long ClearDiagnosticReadResult()", "private void ClearDiagnosticResult_Click(");
        Assert.Contains("_udsResultLifetime.Invalidate();", clear);
        Assert.Contains("if (_udsCts is not null)", clear);
        Assert.Contains("SetStatus(\"Diagnostic context changed; pending UDS output will be discarded.\");", clear);
        Assert.DoesNotContain("DiagnosticResultText.Text = data", clear);
    }

    [Fact]
    public void StaleSuccessfulReadCannotRestorePayloadOrOverwriteActivity()
    {
        var source = MainWindowSource();
        var read = ReadOperation(source);
        var awaitIndex = read.IndexOf("var data = await Task.Run(", StringComparison.Ordinal);
        var guardIndex = read.IndexOf("if (!_udsResultLifetime.CanDisplay(resultGeneration))", StringComparison.Ordinal);
        var returnIndex = read.IndexOf("return;", guardIndex, StringComparison.Ordinal);
        var revealIndex = read.IndexOf("DiagnosticResultText.Text = presentation.Details", StringComparison.Ordinal);
        var activityIndex = read.IndexOf("SetStatus(presentation.Activity)", StringComparison.Ordinal);

        Assert.True(awaitIndex >= 0 && awaitIndex < guardIndex);
        Assert.True(guardIndex < returnIndex && returnIndex < revealIndex);
        Assert.True(revealIndex < activityIndex);
        Assert.DoesNotContain("SetStatus(", read[guardIndex..revealIndex]);
    }

    [Fact]
    public void EveryLateFailureAndCancellationStatusChecksItsGeneration()
    {
        var read = ReadOperation(MainWindowSource());
        var errors = new[]
        {
            "catch (OperationCanceledException ex)",
            "catch (UdsNegativeResponseException ex)",
            "catch (TimeoutException ex)",
            "catch (Exception ex)",
            "finally"
        };

        for (var i = 0; i < errors.Length - 1; i++)
        {
            var section = Between(read, errors[i], errors[i + 1]);
            Assert.Contains("if (_udsResultLifetime.CanDisplay(resultGeneration))", section);
            Assert.Contains("SetStatus(", section);
            Assert.True(section.IndexOf("CanDisplay(resultGeneration)", StringComparison.Ordinal) <
                        section.IndexOf("SetStatus(", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void GenerationSurvivesTryAndFinallyRestoresControls()
    {
        var read = ReadOperation(MainWindowSource());
        var declaration = read.IndexOf("long resultGeneration = 0;", StringComparison.Ordinal);
        var startTry = read.IndexOf("try", declaration, StringComparison.Ordinal);
        var assign = read.IndexOf("resultGeneration = ClearDiagnosticReadResult();", StringComparison.Ordinal);

        Assert.True(declaration >= 0 && declaration < startTry && startTry < assign);
        var last = Between(read, "finally", "\n    }");
        Assert.Contains("_udsCts = null;", last);
        Assert.Contains("_udsGate.Release();", last);
        Assert.Contains("SetUdsEnabled(_connected);", last);
    }

    private static string ReadOperation(string source) =>
        Between(source, "private async Task RunUdsOperationAsync(", "private void CancelUds_Click(");

    private static string Between(string source, string first, string next)
    {
        var start = source.IndexOf(first, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing beginning: {first}");
        var end = source.IndexOf(next, start + first.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing end: {next}");
        return source[start..end];
    }

    private static string MainWindowSource()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
        }

        throw new FileNotFoundException("src/Dvinholm.App/MainWindow.xaml.cs");
    }
}
