namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for user-directed, privacy-reduced CEM research
/// summaries. These fixtures are synthetic and not actual Volvo data.
/// </summary>
public sealed class EcuCemResearchIntakeDisplayTests
{
    [Fact]
    public void ReportsHistoricalTransportCaptureTimeAndDidLengthsOnly()
    {
        var summary = EcuCemResearchIntakeDisplay.Build(Document());
        Assert.Contains("2026-10-27 05:00:00", summary);
        Assert.Contains("CAN 11-bit; request 7E0; response 7E8", summary);
        Assert.Contains("DID F187: 6 byte(s); value hidden", summary);
        Assert.Contains("DID F189: 2 byte(s); value hidden", summary);
        Assert.Contains("2 identifier(s)", summary);
    }

    [Fact]
    public void NeverEchoesActualDidValueBytesOrDecodedIdentifiers()
    {
        var summary = EcuCemResearchIntakeDisplay.Build(Document());
        Assert.DoesNotContain("534543524554", summary);
        Assert.DoesNotContain("00AB", summary);
        Assert.Contains("values omitted", summary);
        Assert.Contains("value hidden", summary);
    }

    [Fact]
    public void DoesNotLeakProfileNameIdOrSecretFilePath()
    {
        var historical = Document() with
        {
            ProfileId = "OWNER_PRIVATE_IDENTIFIER",
            ProfileName = "Person PRIVATE NAME"
        };
        var result = EcuCemResearchIntakeDisplay.Build(historical);
        Assert.DoesNotContain("OWNER_PRIVATE_IDENTIFIER", result);
        Assert.DoesNotContain("Person PRIVATE NAME", result);
        Assert.DoesNotContain("C:\\private", result);
        Assert.Contains("No VIN, profile ID/name", result);
    }

    [Fact]
    public void NeverClaimsRealCemPinMappingServiceOrWriteCapability()
    {
        var report = EcuCemResearchIntakeDisplay.Build(Document() with
        {
            ProfileMarkedVerified = true
        });
        Assert.Contains("NOT confirmed as an XC90 P2 CEM", report);
        Assert.Contains("checksum is not source authentication", report);
        Assert.Contains("cannot authorize reads, writes or PIN acquisition", report);
        Assert.Contains("NOT a configuration backup", report);
        Assert.Contains("Only share after reviewing", report);
    }

    [Fact]
    public void DisplaysExtendedCanFrameWithoutConfusingWithStandard()
    {
        var data = Document() with
        {
            IsExtended = true,
            RequestCanId = "18DA10F1",
            ResponseCanId = "18DAF110"
        };
        var summary = EcuCemResearchIntakeDisplay.Build(data);
        Assert.Contains("CAN 29-bit; request 18DA10F1; response 18DAF110", summary);
        Assert.DoesNotContain("CAN 11-bit", summary);
    }

    [Fact]
    public void RefusesUnsupportedSchemaRestoreFlagAndNonUtcTimestamp()
    {
        var bad = new[]
        {
            Document() with { SchemaVersion = 2 },
            Document() with { IsRestorableBackup = true },
            Document() with { ContainsPotentiallySensitiveData = false },
            Document() with
            {
                CapturedAtUtc = new DateTimeOffset(2026, 10, 27, 5, 0, 0,
                    TimeSpan.FromHours(2))
            }
        };
        foreach (var entry in bad)
            Assert.Throws<InvalidDataException>(() => EcuCemResearchIntakeDisplay.Build(entry));
        Assert.Throws<ArgumentNullException>(() => EcuCemResearchIntakeDisplay.Build(null!));
    }

    [Fact]
    public void RefusesUnsafeCanAndDidMetadata()
    {
        var bad = new[]
        {
            Document() with { RequestCanId = "800" },
            Document() with { ResponseCanId = "7E0" },
            Document() with { Values = [] },
            Document() with
            {
                Values = [
                    new EcuDidReadback(0xF187, "00AB"),
                    new EcuDidReadback(0xF187, "CCDD")
                ]
            },
            Document() with
            {
                Values = [
                    new EcuDidReadback(0xF189, "00AB"),
                    new EcuDidReadback(0xF187, "CCDD")
                ]
            },
            Document() with { Values = [new EcuDidReadback(0xF187, "ab")] }
        };
        foreach (var entry in bad)
            Assert.Throws<InvalidDataException>(() => EcuCemResearchIntakeDisplay.Build(entry));
    }

    [Fact]
    public void RefusesExcessiveValuesAndNonByteAlignedOrMissingHex()
    {
        var entries = Enumerable.Range(0, EcuConfigurationReadback.MaximumDataIdentifiers + 1)
            .Select(i => new EcuDidReadback((ushort)(0xF100 + i), "AA")).ToArray();
        foreach (var bad in new[]
        {
            Document() with { Values = entries },
            Document() with { Values = [new EcuDidReadback(0xF187, "")] },
            Document() with { Values = [new EcuDidReadback(0xF187, "A")] },
            Document() with { Values = [new EcuDidReadback(0xF187, "ABCDG0")] },
            Document() with
            {
                Values = [new EcuDidReadback(0xF187,
                    new string('A', EcuConfigurationSnapshotExport.MaximumValueHexCharacters + 2))]
            }
        })
            Assert.Throws<InvalidDataException>(() => EcuCemResearchIntakeDisplay.Build(bad));
    }

    private static EcuConfigurationExportDocument Document() =>
        new(1, false, true, "PRIVATE_PROFILE_ID", "Sensitive profile metadata",
            "7E0", "7E8", false, false,
            new DateTimeOffset(2026, 10, 27, 5, 0, 0, TimeSpan.Zero),
            [
                new EcuDidReadback(0xF187, "534543524554"),
                new EcuDidReadback(0xF189, "00AB")
            ]);
}
