namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic UDS error-privacy checks. No adapter, vehicle,
/// emulator, PIN operation or network request is involved.
/// </summary>
public sealed class UdsPrivateErrorActivityTests
{
    private const string PrivateVin = "YV1ABCDEF12345678";
    private const string PrivatePath = @"C:\private\garage\adapter.dll";

    [Fact]
    public void KnownNrcIncludesOnlyTrustedServiceCodeAndExplanation()
    {
        var error = new UdsNegativeResponseException(0x22, 0x31);
        var activity = UdsDiagnosticReadResultPresentation.Error(UdsReadResultKind.Did, error);

        Assert.Contains("Read DID rejected:", activity);
        Assert.Contains("UDS 0x22", activity);
        Assert.Contains("NRC 0x31", activity);
        Assert.Contains("request out of range or unsupported data identifier", activity);
        Assert.DoesNotContain(PrivateVin, activity);
    }

    [Fact]
    public void UnknownNrcIsReportedAsUnknownNotInterpretedAsSupported()
    {
        var error = new UdsNegativeResponseException(0x19, 0xE9);
        var activity = UdsDiagnosticReadResultPresentation.Error(UdsReadResultKind.Dtc, error);

        Assert.Contains("Read DTC rejected:", activity);
        Assert.Contains("NRC 0xE9", activity);
        Assert.Contains("unrecognized or manufacturer-specific", activity);
        Assert.DoesNotContain("supported configuration", activity);
    }

    [Fact]
    public void ExceptionDataCannotEscapeThroughNrcStatus()
    {
        var error = new UdsNegativeResponseException(0x22, 0x33);
        error.Data["raw ECU response"] = PrivateVin;
        error.Data["driver"] = PrivatePath;

        var activity = UdsDiagnosticReadResultPresentation.Error(UdsReadResultKind.Vin, error);

        Assert.Contains("security access denied", activity);
        Assert.DoesNotContain(PrivateVin, activity);
        Assert.DoesNotContain(PrivatePath, activity);
    }

    [Fact]
    public void TimeoutDoesNotSurfaceArbitraryTransportPayload()
    {
        var error = new TimeoutException($"Transport timeout: {PrivateVin} at {PrivatePath}");
        var activity = UdsDiagnosticReadResultPresentation.Error(UdsReadResultKind.Vin, error);

        Assert.Equal("Read VIN timeout. Check the adapter and selected ECU addresses.", activity);
        Assert.DoesNotContain(PrivateVin, activity);
        Assert.DoesNotContain(PrivatePath, activity);
    }

    [Fact]
    public void UnexpectedExceptionDoesNotSurfaceDriverMessageOrInnerException()
    {
        var error = new InvalidOperationException(
            $"Vendor error: {PrivateVin}",
            new IOException(PrivatePath));
        var activity = UdsDiagnosticReadResultPresentation.Error(UdsReadResultKind.Did, error);

        Assert.Equal("Read DID failed. Check the adapter connection and diagnostic settings.", activity);
        Assert.DoesNotContain(PrivateVin, activity);
        Assert.DoesNotContain(PrivatePath, activity);
    }

    [Fact]
    public void CancelledOperationDoesNotEchoUserOrAdapterData()
    {
        var error = new OperationCanceledException(PrivateVin + " " + PrivatePath);
        var activity = UdsDiagnosticReadResultPresentation.Error(UdsReadResultKind.Dtc, error);

        Assert.Equal("Read DTC cancelled.", activity);
        Assert.DoesNotContain(PrivateVin, activity);
        Assert.DoesNotContain(PrivatePath, activity);
    }

    [Fact]
    public void InvalidReadKindAndNullExceptionsFailClosed()
    {
        Assert.Throws<ArgumentNullException>(() =>
            UdsDiagnosticReadResultPresentation.Error(UdsReadResultKind.Vin, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UdsDiagnosticReadResultPresentation.Error(
                (UdsReadResultKind)255, new TimeoutException("unexpected")));
    }

    [Fact]
    public void RealUiGatesAllFourFailuresAndNeverPassesExceptionTextToActivity()
    {
        var source = MainWindowSource();
        var start = source.IndexOf("private async Task RunUdsOperationAsync(", StringComparison.Ordinal);
        var end = source.IndexOf("private void CancelUds_Click(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var region = source[start..end];
        var exceptions = new[]
        {
            "catch (OperationCanceledException ex)",
            "catch (UdsNegativeResponseException ex)",
            "catch (TimeoutException ex)",
            "catch (Exception ex)"
        };

        foreach (var marker in exceptions)
        {
            var startCatch = region.IndexOf(marker, StringComparison.Ordinal);
            var endCatch = region.IndexOf("}", startCatch, StringComparison.Ordinal);
            Assert.True(startCatch >= 0 && endCatch > startCatch, marker);
            var catchBody = region[startCatch..endCatch];
            Assert.Contains("_udsResultLifetime.CanDisplay(resultGeneration)", catchBody);
            Assert.Contains("SetStatus(UdsDiagnosticReadResultPresentation.Error(kind, ex))", catchBody);
            Assert.True(
                catchBody.IndexOf("CanDisplay(resultGeneration)", StringComparison.Ordinal) <
                catchBody.IndexOf("SetStatus(", StringComparison.Ordinal));
            Assert.DoesNotContain("ex.Message", catchBody);
            Assert.DoesNotContain("FormatSafeException", catchBody);
        }

        var presentation = PresentationSource();
        Assert.DoesNotContain("exception.Message", presentation.Replace("///", ""));
    }

    private static string MainWindowSource() =>
        Source("src/Dvinholm.App/MainWindow.xaml.cs");

    private static string PresentationSource() =>
        Source("src/Dvinholm.App/UdsDiagnosticReadResultPresentation.cs");

    private static string Source(string relativePath)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
        }

        throw new FileNotFoundException(relativePath);
    }
}
