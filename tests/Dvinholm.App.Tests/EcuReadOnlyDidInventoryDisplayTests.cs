namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline tests for the real post-read Diagnostics
/// inventory. No adapter, programming, authentication or file export.
/// </summary>
public sealed class EcuReadOnlyDidInventoryDisplayTests
{
    [Fact]
    public void CompletedReadShowsDidsInAscendingOrderAndOnlyLengths()
    {
        var text = EcuReadOnlyDidInventoryDisplay.Build(Snapshot(
            (0xF190, "593132"), (0xF187, "AA"), (0xF189, "0102")));
        Assert.Contains("Observed DIDs: 3", text);
        Assert.Contains("DID 0xF187: 1 byte(s); value hidden", text);
        Assert.Contains("DID 0xF189: 2 byte(s); value hidden", text);
        Assert.Contains("DID 0xF190: 3 byte(s); value hidden", text);
        Assert.True(text.IndexOf("DID 0xF187", StringComparison.Ordinal) <
                    text.IndexOf("DID 0xF189", StringComparison.Ordinal));
        Assert.True(text.IndexOf("DID 0xF189", StringComparison.Ordinal) <
                    text.IndexOf("DID 0xF190", StringComparison.Ordinal));
    }

    [Fact]
    public void SensitiveDidPayloadAndProfileIdentityAreNotDisplayed()
    {
        var snapshot = Snapshot((0xF190, "56494E2D534543524554"),
            (0xF187, "DEADBEEF")) with
        {
            ProfileId = "PRIVATE-PROFILE-ALPHA",
            ProfileName = "PRIVATE-OWNER-NAME"
        };
        var text = EcuReadOnlyDidInventoryDisplay.Build(snapshot);
        foreach (var secret in new[] {
            "56494E2D534543524554", "DEADBEEF", "PRIVATE-PROFILE",
            "PRIVATE-OWNER", "7E0", "7E8" })
            Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Values, VIN and profile metadata are hidden.", text);
    }

    [Fact]
    public void InventoryCannotBeMistakenForVerifiedConfigurationOrPin()
    {
        var text = EcuReadOnlyDidInventoryDisplay.Build(
            Snapshot((0xF187, "BB")) with { ProfileMarkedVerified = true });
        Assert.Contains("do not verify physical CEM identity", text);
        Assert.Contains("Not a restorable configuration backup", text);
        Assert.Contains("No PIN or configuration write is supported", text);
    }

    [Fact]
    public void MissingDuplicateOrMalformedDidsAreRejectedAsWholeInventory()
    {
        var bad = new[] {
            Snapshot(),
            Snapshot((0xF187, "AA"), (0xF187, "BB")),
            Snapshot((0xF187, "")),
            Snapshot((0xF187, "A")),
            Snapshot((0xF187, "GG")),
            Snapshot((0xF187, "aabb")),
            Snapshot((0xF187, new string('A', 8186)))
        };
        foreach (var value in bad)
            Assert.Throws<InvalidDataException>(() =>
                EcuReadOnlyDidInventoryDisplay.Build(value));
    }

    [Fact]
    public void StandardAndExtendedCanIdsAreValidatedBeforeDisplay()
    {
        var good = Snapshot((0xF187, "AA"));
        foreach (var bad in new[] {
            good with { RequestId = 0x800 },
            good with { ResponseId = 0x800 },
            good with { ResponseId = good.RequestId },
            good with { IsExtended = true, RequestId = 0x20000000u }
        })
            Assert.Throws<InvalidDataException>(() =>
                EcuReadOnlyDidInventoryDisplay.Build(bad));

        var extended = good with {
            IsExtended = true,
            RequestId = 0x1FFFFFFEu,
            ResponseId = 0x1FFFFFFFu
        };
        Assert.Contains("DID 0xF187", EcuReadOnlyDidInventoryDisplay.Build(extended));
    }

    [Fact]
    public void DefaultOrNonUtcCaptureIsRejected()
    {
        var good = Snapshot((0xF187, "AA"));
        foreach (var candidate in new[] {
            good with { CapturedAtUtc = default },
            good with { CapturedAtUtc = good.CapturedAtUtc.ToOffset(TimeSpan.FromHours(2)) }
        })
            Assert.Throws<InvalidDataException>(() =>
                EcuReadOnlyDidInventoryDisplay.Build(candidate));
    }

    [Fact]
    public void ClassicCanMaximumBytesAndMaximumDidCountAreBounded()
    {
        var maxValue = new string('A', EcuConfigurationReadback.MaximumDidValueBytes * 2);
        var maxDids = Enumerable.Range(0, EcuConfigurationReadback.MaximumDataIdentifiers)
            .Select(i => ((ushort)(0xE000 + i), "AA")).ToArray();
        Assert.Contains("4092 byte(s)",
            EcuReadOnlyDidInventoryDisplay.Build(Snapshot((0xF187, maxValue))));
        Assert.Contains("Observed DIDs: 128",
            EcuReadOnlyDidInventoryDisplay.Build(Snapshot(maxDids)));
        Assert.Throws<InvalidDataException>(() =>
            EcuReadOnlyDidInventoryDisplay.Build(Snapshot(
                Enumerable.Range(0, 129).Select(i =>
                    ((ushort)(0xE000 + i), "AA")).ToArray())));
    }

    [Fact]
    public void ExistingCaptureHandlerShowsInventoryBeforeOptionalSaveAsAndClearsStaleResults()
    {
        var source = MainWindowSource();
        var start = source.IndexOf("private async void CaptureSnapshot_Click(",
            StringComparison.Ordinal);
        var end = source.IndexOf("private async void ReadDid_Click(", start,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var handler = source[start..end];
        var inventory = handler.IndexOf(
            "EcuReadOnlyDidInventoryDisplay.Build(snapshot)", StringComparison.Ordinal);
        var save = handler.IndexOf(
            "ReportSavePathPicker.PickPath(", StringComparison.Ordinal);
        Assert.True(inventory >= 0 && save > inventory);
        Assert.Contains("IsSnapshotContextCurrent(profile, captureGeneration)", handler);
        Assert.Contains("DiagnosticResultExpander.IsExpanded = true", handler);
        Assert.Contains("destinationPath is null", handler);
        Assert.Contains("No file was saved", handler);
        Assert.DoesNotContain("SetStatus(EcuReadOnlyDidInventoryDisplay.Build", handler);
        Assert.Contains("ClearDiagnosticReadResult();",
            source[source.IndexOf("private void CancelUds_Click(", StringComparison.Ordinal)..]);
    }

    private static EcuConfigurationReadSnapshot Snapshot(
        params (ushort Did, string ValueHex)[] items) =>
        new("synthetic-profile", "Synthetic profile", 0x7E0u, 0x7E8u,
            false, false, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            items.Select(x => new EcuDidReadback(x.Did, x.ValueHex)).ToArray());

    private static string MainWindowSource()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory);
             folder is not null; folder = folder.Parent)
        {
            var file = Path.Combine(folder.FullName,
                "src", "Dvinholm.App", "MainWindow.xaml.cs");
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException("MainWindow.xaml.cs");
    }
}
