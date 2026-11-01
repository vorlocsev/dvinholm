namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline gates for the real DTC status-mask selector. All choices
/// remain existing UDS 0x19/0x02 reads, never active probing or fault clear.
/// </summary>
public sealed class UdsDtcStatusMaskSelectionTests
{
    [Fact]
    public void AllStatusesIsExplicitDefault()
    {
        Assert.Equal((byte)0xFF, UdsDtcStatusMaskSelection.DefaultMask);
        Assert.True(UdsDtcStatusMaskSelection.TryParseTag("FF", out var value));
        Assert.Equal((byte)0xFF, value);
    }

    [Fact]
    public void StandardSingleBitFiltersAreExact()
    {
        var cases = new (string Tag, byte Expected)[]
        {
            ("01", 0x01),
            ("04", 0x04),
            ("08", 0x08),
            ("80", 0x80)
        };
        foreach (var (tag, expected) in cases)
        {
            Assert.True(UdsDtcStatusMaskSelection.TryParseTag(tag, out var parsed));
            Assert.Equal(expected, parsed);
            Assert.Equal(expected, (byte)(parsed & expected));
        }
    }

    [Fact]
    public void MissingMalformedAndUnofferedTagsFailClosed()
    {
        foreach (var tag in new string?[]
        {
            null, "", " ", "ff", "0xFF", "0X01", "F", "1",
            "00", "02", "03", "10", "20", "40", "7F", "AA", "100", "FF "
        })
        {
            Assert.False(UdsDtcStatusMaskSelection.TryParseTag(tag, out var parsed));
            Assert.Equal((byte)0, parsed);
        }
    }

    [Fact]
    public void EveryByteEitherMatchesAVisibleChoiceOrIsRejected()
    {
        var expected = new HashSet<byte> { 0xFF, 0x01, 0x04, 0x08, 0x80 };
        for (var n = 0; n <= byte.MaxValue; n++)
        {
            var tag = n.ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
            var accepted = UdsDtcStatusMaskSelection.TryParseTag(tag, out var value);
            Assert.Equal(expected.Contains((byte)n), accepted);
            if (accepted) Assert.Equal((byte)n, value);
            else Assert.Equal((byte)0, value);
        }
    }

    [Fact]
    public void XamlHasFiveNamedOptionsAndExplicitAllStatusesDefault()
    {
        var xaml = RepoFile("src", "Dvinholm.App", "MainWindow.xaml");
        var start = xaml.IndexOf("<ComboBox x:Name=\"DtcStatusMaskSelector\"",
            StringComparison.Ordinal);
        var end = xaml.IndexOf("</ComboBox>", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var selector = xaml[start..end];
        Assert.Contains("SelectedIndex=\"0\"", selector);
        Assert.Contains("All status bits\" Tag=\"FF\"", selector);
        Assert.Contains("Test failed\" Tag=\"01\"", selector);
        Assert.Contains("Pending DTC\" Tag=\"04\"", selector);
        Assert.Contains("Confirmed DTC\" Tag=\"08\"", selector);
        Assert.Contains("Warning indicator requested\" Tag=\"80\"", selector);
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(
            selector, "<ComboBoxItem ").Count);
        Assert.Contains("ReadDtc_Click", xaml);
        Assert.DoesNotContain("Click=\"ReadDtc_Click\"", selector);
    }

    [Fact]
    public void ReadButtonPassesChosenMaskToTheExistingSingleRequest()
    {
        var source = RepoFile("src", "Dvinholm.App", "MainWindow.xaml.cs");
        var start = source.IndexOf("private async void ReadDtc_Click(",
            StringComparison.Ordinal);
        var end = source.IndexOf("private bool TryParseDid(", start,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var handler = source[start..end];
        Assert.Contains("TryGetSelectedDtcStatusMask(out var statusMask)", handler);
        Assert.Contains("if (!TryGetSelectedDtcStatusMask", handler);
        Assert.Contains("return;", handler);
        Assert.Contains("client.ReadDtcResultByStatusMask(statusMask, token)", handler);
        Assert.Contains("FormatWithAvailability(result, statusMask)", handler);
        Assert.DoesNotContain("const byte statusMask = 0xFF", handler);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(
            handler, "ReadDtcResultByStatusMask\\(")
            .Cast<System.Text.RegularExpressions.Match>());
    }

    [Fact]
    public void FilterChangeInvalidatesInFlightDiagnosticsWithoutSendingCommands()
    {
        var source = RepoFile("src", "Dvinholm.App", "MainWindow.xaml.cs");
        var start = source.IndexOf("private void DtcStatusMask_SelectionChanged(",
            StringComparison.Ordinal);
        var end = source.IndexOf("private bool TryGetSelectedDtcStatusMask(",
            start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var handler = source[start..end];
        Assert.Contains("ClearDiagnosticReadResult();", handler);
        Assert.Contains("SetUdsEnabled(_connected && !_closing);", handler);
        Assert.DoesNotContain("RunUdsOperationAsync", handler);
        Assert.DoesNotContain("ReadDtcResultByStatusMask", handler);
        Assert.DoesNotContain("_session.Bus", handler);
        Assert.Contains("DtcStatusMaskSelector.SelectionChanged += DtcStatusMask_SelectionChanged;",
            source);
    }

    [Fact]
    public void MalformedSelectorDisablesReadButtonAndCannotBroadenToAll()
    {
        var source = RepoFile("src", "Dvinholm.App", "MainWindow.xaml.cs");
        Assert.Contains(
            "ReadDtcButton.IsEnabled = ready && TryGetSelectedDtcStatusMask(out _);",
            source);
        Assert.Contains("(DtcStatusMaskSelector?.SelectedItem as ComboBoxItem)?.Tag?.ToString()",
            source);
        Assert.Contains("Choose a valid Read DTC status filter", source);
        Assert.Contains("UdsDtcStatusMaskSelection.TryParseTag(", source);
        Assert.Contains("ClearDiagnosticReadResult();", source);
    }

    private static string RepoFile(params string[] parts)
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory);
             folder is not null; folder = folder.Parent)
        {
            var path = Path.Combine(new[] { folder.FullName }.Concat(parts).ToArray());
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(string.Join("/", parts));
    }
}
