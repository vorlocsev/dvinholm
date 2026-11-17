namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts separating J2534 vendor metadata and read errors
/// from Aurora home activity. No DLL, device, CAN frame or ECU is accessed.
/// </summary>
public sealed class J2534ConnectionPresentationTests
{
    private const string PrivateVin = "YV1ABCDEF12345678";
    private const string PrivatePath = @"C:\Users\DriverUser\Documents\adapter.dll";

    [Fact]
    public void ConnectedActivityIsConstantAndDoesNotIncludeVendorMetadata()
    {
        var text = J2534ConnectionPresentation.ConnectedActivity;
        Assert.Contains("Connected to J2534 CAN", text);
        Assert.Contains("local adapter workspace", text);
        Assert.DoesNotContain(PrivateVin, text);
        Assert.DoesNotContain("API 04.04", text);
        Assert.DoesNotContain(PrivatePath, text);
    }

    [Fact]
    public void ReadWorkerErrorActivityNeverNeedsVendorExceptionText()
    {
        var text = J2534ConnectionPresentation.ReadErrorActivity;
        Assert.Contains("CAN read failed", text);
        Assert.Contains("Check the adapter", text);
        Assert.DoesNotContain(PrivatePath, text);
        Assert.DoesNotContain(PrivateVin, text);
        Assert.DoesNotContain("Exception", text);
    }

    [Fact]
    public void LocalAdapterDetailsPreserveOrdinaryThreePartVersion()
    {
        var version = new J2534Native.AdapterVersionInfo("FW3.5", "DLL2.6", "API04.04");
        var details = J2534ConnectionPresentation.LocalDetails(version, null);
        Assert.Contains("Driver-reported (unverified; local workspace only)", details);
        Assert.Contains("API API04.04", details);
        Assert.Contains("DLL DLL2.6", details);
        Assert.Contains("FW FW3.5", details);
        Assert.Contains("capabilities not reported", details);
    }

    [Fact]
    public void MissingVersionAndCapabilityDataNeverPretendToBeVerified()
    {
        var details = J2534ConnectionPresentation.LocalDetails(null, null);
        Assert.Contains("version not reported", details);
        Assert.Contains("capabilities not reported", details);
        Assert.DoesNotContain("API 04.04", details);
    }

    [Fact]
    public void HardwareFilterAndVersionQueryCapabilitiesRemainVisibleLocally()
    {
        var version = new J2534Native.AdapterVersionInfo("fw", "dll", "api");
        var supported = new J2534Native.AdapterCapabilities(true, true, false);
        var unsupported = new J2534Native.AdapterCapabilities(false, false, true);
        var yes = J2534ConnectionPresentation.LocalDetails(version, supported);
        var no = J2534ConnectionPresentation.LocalDetails(version, unsupported);
        Assert.Contains("HW filters available", yes);
        Assert.Contains("version query available", yes);
        Assert.Contains("HW filters unavailable", no);
        Assert.Contains("version query unavailable", no);
    }

    [Fact]
    public void UntrustedLocalVersionTextIsRedactedAndBounded()
    {
        var version = new J2534Native.AdapterVersionInfo(
            PrivatePath, "driver\ninfo", new string('Z', 150));
        var details = J2534ConnectionPresentation.LocalDetails(version, null);
        Assert.DoesNotContain(PrivatePath, details);
        Assert.DoesNotContain("driver\ninfo", details);
        Assert.Contains("[local path redacted]", details);
        Assert.DoesNotContain(new string('Z', 150), details);
        Assert.Contains("…", details);
    }

    [Fact]
    public void XamlKeepsMetadataInTheLocalWorkspaceOutsideAuroraHome()
    {
        var xaml = MainXaml();
        Assert.Contains("x:Name=\"AdapterMetadataText\"", xaml);
        Assert.Contains("Text=\"J2534 adapter details: disconnected.\"", xaml);
        Assert.Contains("local diagnostic details", xaml, StringComparison.OrdinalIgnoreCase);
        var home = xaml.IndexOf("<local:AuroraHome", StringComparison.Ordinal);
        var workspace = xaml.IndexOf("<Grid x:Name=\"AuroraWorkspaceHost\"", StringComparison.Ordinal);
        var metadata = xaml.IndexOf("x:Name=\"AdapterMetadataText\"", StringComparison.Ordinal);
        Assert.True(home >= 0 && workspace > home && metadata > workspace);
    }

    [Fact]
    public void RealUiRoutesReadAndConnectionActivityThroughTrustedTextAndResetsMetadata()
    {
        var source = MainWindowSource();
        var readEventStart = source.IndexOf("_session.ReadErrorEpoch += errorEpoch =>", StringComparison.Ordinal);
        Assert.True(readEventStart >= 0, "WinUI must use the originating CAN-bus error epoch.");
        var readEventEnd = source.IndexOf("        };", readEventStart, StringComparison.Ordinal);
        Assert.True(readEventEnd > readEventStart);
        var readEvent = source[readEventStart..readEventEnd];
        var guard = readEvent.IndexOf(
            "if (_closing || _session.ConnectionEpoch != errorEpoch)", StringComparison.Ordinal);
        var clear = readEvent.IndexOf("ClearDiagnosticReadResult();", StringComparison.Ordinal);
        var activity = readEvent.IndexOf(
            "SetStatus(J2534ConnectionPresentation.ReadErrorActivity);", StringComparison.Ordinal);
        Assert.Contains("DispatcherQueue.TryEnqueue(", readEvent);
        Assert.True(guard > 0 && clear > guard && activity > clear,
            "A delayed error cannot clear new-connection results or publish stale Aurora activity.");
        Assert.Contains("if (_connected)", readEvent);
        Assert.DoesNotContain("ex.Message", readEvent);
        Assert.DoesNotContain("SanitizeDriverDiagnostic", readEvent);

        var connectStart = source.IndexOf("private async void Connect_Click(", StringComparison.Ordinal);
        var connectEnd = source.IndexOf("private void Start_Click(", connectStart, StringComparison.Ordinal);
        Assert.True(connectStart >= 0 && connectEnd > connectStart);
        var connect = source[connectStart..connectEnd];
        Assert.Contains("J2534ConnectionPresentation.LocalDetails(", connect);
        Assert.Contains("SetStatus(J2534ConnectionPresentation.ConnectedActivity);", connect);
        Assert.True(connect.Split(
            "J2534ConnectionPresentation.DisconnectedDetailsForLanguage(_unifiedEnglish);").Length >= 3);
        Assert.Contains("_adapterMetadataConnected = false;", connect);
        Assert.Contains("_adapterMetadataConnected = true;", connect);
        Assert.Contains("_session.AdapterVersion, _session.AdapterCapabilities, _unifiedEnglish", connect);
        Assert.DoesNotContain("versionText", connect);
        Assert.DoesNotContain("capabilityText", connect);
        Assert.DoesNotContain("SetStatus(\"Connected · J2534 · CAN · \"", connect);
        Assert.DoesNotContain("_session.AdapterVersionStatus", connect);
        Assert.Contains("ConnectButton.Content = AuroraUiText(\"Отключить\", \"Disconnect\");", connect);
    }

    private static string MainXaml() => ReadSource("MainWindow.xaml");

    private static string MainWindowSource() => ReadSource("MainWindow.xaml.cs");

    private static string ReadSource(string filename)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Dvinholm.App", filename);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
        }

        throw new FileNotFoundException("Missing source: " + filename);
    }
}
