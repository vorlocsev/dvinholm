namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic synthetic checks for two separately validated
/// user-selected files. No adapter, car, ECU service, PIN or writing.
/// </summary>
public sealed class VolvoP2CemPairedResearchIntakeDisplayTests
{
    [Fact]
    public void TwoInspectedExportsComposeAnExplicitlyUncorrelatedReport()
    {
        var report = Pair(Card(), Snapshot());
        Assert.Contains("PAIRED OFFLINE RESEARCH INTAKE", report);
        Assert.Contains("Hardware P/N (claimed): HW123", report);
        Assert.Contains("Software P/N (claimed): SW456", report);
        Assert.Contains("Diagnostic software P/N (claimed): DS789", report);
        Assert.Contains("DID F187: 6 byte(s); value hidden", report);
        Assert.Contains("UNCORRELATED RESEARCH CLAIMS ONLY", report);
        Assert.Contains("NO AUTOMATIC MATCHING", report);
    }

    [Fact]
    public void NeverExposesDidPayloadOrPrivateSnapshotMetadata()
    {
        var report = Pair(Card(), Snapshot());
        Assert.DoesNotContain("534543524554", report);
        Assert.DoesNotContain("00AB", report);
        Assert.DoesNotContain("PRIVATE_PROFILE_ID", report);
        Assert.DoesNotContain("Private owner profile", report);
        Assert.Contains("Raw DID values, VIN, profile names", report);
    }

    [Fact]
    public void ClaimedVidaSourceNeverBecomesAuthenticatedEvidence()
    {
        var report = Pair(Card(vida: true), Snapshot());
        Assert.Contains("USER CLAIM ONLY, not independently authenticated", report);
        Assert.Contains("checksum authenticates a physical CEM", report);
        Assert.Contains("diagnostic snapshot might belong to another ECU", report);
    }

    [Fact]
    public void VerifiedProfileFlagNeverGrantsCemOrPinRights()
    {
        var report = Pair(Card(), Snapshot() with { ProfileMarkedVerified = true });
        Assert.Contains("NOT confirmed as an XC90 P2 CEM", report);
        Assert.Contains("service authorization", report);
        Assert.Contains("configuration read/write", report);
        Assert.Contains("No file changes, J2534/CAN request", report);
    }

    [Fact]
    public void DifferentClaimedYearCannotIdentifyTheSnapshotVehicle()
    {
        var report = Pair(Card(year: "2012"), Snapshot());
        Assert.Contains("XC90 model year (claimed): 2012", report);
        Assert.Contains("model-year match, transport support", report);
        Assert.Contains("UNCORRELATED RESEARCH CLAIMS ONLY", report);
    }

    [Fact]
    public void ExtendedCanAndDidLengthsStayMetadataOnly()
    {
        var snap = Snapshot() with
        {
            IsExtended = true,
            RequestCanId = "18DA10F1",
            ResponseCanId = "18DAF110"
        };
        var report = Pair(Card(), snap);
        Assert.Contains("CAN 29-bit; request 18DA10F1; response 18DAF110", report);
        Assert.Contains("value hidden", report);
        Assert.DoesNotContain("534543524554", report);
    }

    [Fact]
    public void UnsafeOrMalformedIdentityCardFailsClosed()
    {
        foreach (var item in new[]
        {
            Card() with { MayAccessEcu = true },
            Card() with { IsRestorableBackup = true },
            Card() with { IsUnverifiedManualClaim = false },
            Card() with { ClaimedModelYear = 2099 },
            Card() with { ClaimedHardwarePartNumber = "hw123" },
            Card() with { ClaimedSoftwarePartNumber = "unsafe whitespace" }
        })
            Assert.Throws<InvalidDataException>(() => Pair(item, Snapshot()));
    }

    [Fact]
    public void UnsafeOrMalformedDiagnosticRecordFailsClosed()
    {
        foreach (var item in new[]
        {
            Snapshot() with { IsRestorableBackup = true },
            Snapshot() with { ContainsPotentiallySensitiveData = false },
            Snapshot() with { SchemaVersion = 2 },
            Snapshot() with { RequestCanId = "800" },
            Snapshot() with { CapturedAtUtc = new DateTimeOffset(2026, 10, 27, 5, 0, 0, TimeSpan.FromHours(2)) },
            Snapshot() with { Values = [new EcuDidReadback(0xF187, "BAD")] },
            Snapshot() with
            {
                Values = [
                    new EcuDidReadback(0xF187, "00AB"),
                    new EcuDidReadback(0xF187, "0102")
                ]
            }
        })
            Assert.Throws<InvalidDataException>(() => Pair(Card(), item));
    }

    private static string Pair(
        VolvoP2CemIdentityResearchDocument claim,
        EcuConfigurationExportDocument snap) =>
        VolvoP2CemPairedResearchIntakeDisplay.Build(claim, snap);

    private static VolvoP2CemIdentityResearchDocument Card(
        string year = "2007", bool vida = false) =>
        VolvoP2CemIdentityResearchExport.InspectJson(
            VolvoP2CemIdentityResearchExport.CreateJson(
                year, "HW123", "SW456", "DS789", vida));

    private static EcuConfigurationExportDocument Snapshot()
    {
        var synthetic = new EcuConfigurationReadSnapshot(
            "PRIVATE_PROFILE_ID", "Private owner profile",
            0x7E0, 0x7E8, false, false,
            new DateTimeOffset(2026, 10, 27, 5, 0, 0, TimeSpan.Zero),
            [
                new EcuDidReadback(0xF187, "534543524554"),
                new EcuDidReadback(0xF189, "00AB")
            ]);
        var json = EcuConfigurationSnapshotExport.CreateJson(synthetic, true);
        return EcuConfigurationSnapshotInspection.InspectJson(json);
    }
}
