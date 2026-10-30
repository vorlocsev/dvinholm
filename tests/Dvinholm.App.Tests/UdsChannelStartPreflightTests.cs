namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline contracts for explicit UDS TX/RX validation
/// and private channel-opening errors. Does not instantiate a CAN adapter.
/// </summary>
public sealed class UdsChannelStartPreflightTests
{
    [Fact]
    public void DisconnectedSessionsCannotPrepareEvenValidAddresses()
    {
        var ok = Prepare(false, 0, "7E0", "7E8", out var request, out var response,
            out var extended, out var error);

        Assert.False(ok);
        Assert.Equal("Connect to J2534 first.", error);
        Assert.Equal(0u, request);
        Assert.Equal(0u, response);
        Assert.False(extended);
    }

    [Fact]
    public void InvalidOrMissingFormatCannotPrepareChannel()
    {
        foreach (var index in new[] { -1, 2, 12 })
        {
            var ok = Prepare(true, index, "7E0", "7E8", out var request,
                out var response, out var extended, out var error);
            Assert.False(ok);
            Assert.Contains("Select 11-bit or 29-bit", error);
            Assert.Equal(0u, request);
            Assert.Equal(0u, response);
            Assert.False(extended);
        }
    }

    [Fact]
    public void EmptyOrMissingTxRxNeverUsesConventionalDefaults()
    {
        foreach (var (tx, rx) in new (string?, string?)[]
                 { (null, "7E8"), ("7E0", null), ("", "7E8"), ("7E0", ""),
                   ("  ", "7E8"), ("7E0", "  ") })
        {
            Assert.False(Prepare(true, 0, tx, rx, out var request, out var response,
                out _, out var error));
            Assert.Equal(0u, request);
            Assert.Equal(0u, response);
            Assert.Contains("both request and response CAN IDs", error);
        }
    }

    [Fact]
    public void MalformedAndOutOfRangeValuesAreRejectedBeforeChannelCreation()
    {
        foreach (var (format, tx, rx) in new[]
                 { (0, "GHI", "7E8"), (0, "7E0", "800"),
                   (1, "20000000", "18DAF110"), (1, "18DA10F1", "none") })
        {
            Assert.False(Prepare(true, format, tx, rx, out var request, out var response,
                out _, out var error));
            Assert.Equal(0u, request);
            Assert.Equal(0u, response);
            Assert.Contains("CAN ID must be a hexadecimal value", error);
        }
    }

    [Fact]
    public void EqualTxRxValuesFailClosedEvenWithDifferentTextFormatting()
    {
        foreach (var (format, tx, rx) in new[]
                 { (0, "7e8", "0x7E8"), (0, "007E8", "7E8"),
                   (1, "18DAF110", "0x18daf110") })
        {
            Assert.False(Prepare(true, format, tx, rx, out var request, out var response,
                out _, out var error));
            Assert.Equal(0u, request);
            Assert.Equal(0u, response);
            Assert.Equal("Request and response CAN IDs must be different.", error);
        }
    }

    [Fact]
    public void DistinctStandardIdsKeepTheirExactParsedValues()
    {
        Assert.True(Prepare(true, 0, "0x7e0", " 7E8 ", out var request,
            out var response, out var extended, out var error));
        Assert.Equal(0x7E0u, request);
        Assert.Equal(0x7E8u, response);
        Assert.False(extended);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void DistinctExtendedIdsKeepTheirExactParsedValues()
    {
        Assert.True(Prepare(true, 1, "18DA10F1", "0x18daf110", out var request,
            out var response, out var extended, out var error));
        Assert.Equal(0x18DA10F1u, request);
        Assert.Equal(0x18DAF110u, response);
        Assert.True(extended);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void RealUiValidatesBeforeOpeningChannelAndNeverPublishesVendorException()
    {
        var source = MainWindowSource();
        var start = source.IndexOf("private bool TryGetUdsClient(", StringComparison.Ordinal);
        var end = source.IndexOf("internal static bool TryParseCanId(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var channel = source[start..end];

        var preflight = channel.IndexOf("UdsChannelStartPreflight.TryPrepare(", StringComparison.Ordinal);
        var subscription = channel.IndexOf("new IsoTpChannel(", StringComparison.Ordinal);
        Assert.True(preflight >= 0 && subscription > preflight);
        Assert.Contains("out var requestId, out var responseId, out var canExtendedId", channel);
        Assert.Contains("error = UdsChannelStartPreflight.ChannelFailureActivity;", channel);
        Assert.DoesNotContain("FormatSafeException(", channel);
        Assert.DoesNotContain("ex.Message", channel);
        Assert.Contains("catch (Exception)", channel);

        var readStart = source.IndexOf("private async Task RunUdsOperationAsync(", StringComparison.Ordinal);
        var readEnd = source.IndexOf("private void CancelUds_Click(", readStart, StringComparison.Ordinal);
        Assert.True(readStart >= 0 && readEnd > readStart);
        var read = source[readStart..readEnd];
        Assert.Contains("if (!TryGetUdsClient(out var client, out var error)", read);
        Assert.Contains("SetStatus(error);", read);
        Assert.DoesNotContain("FormatSafeException(", read);
        Assert.DoesNotContain("ex.Message", read);
        Assert.DoesNotContain("YV1", UdsChannelStartPreflight.ChannelFailureActivity);
    }

    private static bool Prepare(
        bool connected, int format, string? tx, string? rx,
        out uint request, out uint response, out bool extended, out string error) =>
        UdsChannelStartPreflight.TryPrepare(connected, format, tx, rx,
            out request, out response, out extended, out error);

    private static string MainWindowSource()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "Dvinholm.App",
                "MainWindow.xaml.cs");
            if (File.Exists(path))
                return File.ReadAllText(path);
        }

        throw new FileNotFoundException("src/Dvinholm.App/MainWindow.xaml.cs");
    }
}
