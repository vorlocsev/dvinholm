namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic offline regression gates for an evidence collection
/// checklist, never a physical CEM support or security procedure.
/// </summary>
public sealed class VolvoP2CemRevisionEvidencePreflightTests
{
    [Fact]
    public void SevenIndependentGapsAlwaysRemainOpenForManualResearchArtifacts()
    {
        var review = Review();
        Assert.Equal(7, review.MissingEvidence.Count);
        Assert.Equal(7, review.MissingEvidence.Distinct().Count());
        Assert.False(review.HasAuthenticatedCemVariant);
        Assert.False(review.HasConfirmedTransport);
        Assert.False(review.CanReadLiveConfiguration);
        Assert.False(review.CanAcquirePin);
        Assert.False(review.CanWriteConfiguration);
        Assert.Contains("ALL 7/7 independent evidence gates remain OPEN", Report());
    }

    [Fact]
    public void Advertised2005ConfigurationAndImmobilizerMenusNeverGrantPermissions()
    {
        var review = Review(year: "2005");
        Assert.Contains(review.AdvertisedMenusForClaimedYear,
            x => x.Contains("Reading data for vehicle configuration", StringComparison.Ordinal));
        Assert.Contains(review.AdvertisedMenusForClaimedYear,
            x => x.Contains("Reading off parameters, immobilizer function", StringComparison.Ordinal));
        Assert.False(review.CanReadLiveConfiguration);
        Assert.False(review.CanAcquirePin);
        Assert.Contains("third-party advertising", Report(year: "2005"));
    }

    [Fact]
    public void For2007ProgrammedValuesAndCemHsRemainDistinct()
    {
        var review = Review(year: "2007");
        Assert.Contains(review.AdvertisedMenusForClaimedYear,
            x => x.StartsWith("CEM: Programmed values", StringComparison.Ordinal));
        Assert.Contains(review.AdvertisedMenusForClaimedYear,
            x => x.StartsWith("CEM-HS: ECU identification", StringComparison.Ordinal));
        Assert.DoesNotContain(review.AdvertisedMenusForClaimedYear,
            x => x.Contains("Reading data for vehicle configuration", StringComparison.Ordinal));
        Assert.Contains("NOT confirmed CAN bus IDs", Report(year: "2007"));
    }

    [Fact]
    public void For2008ListingsStayExactYearAndModuleLimited()
    {
        var review = Review(year: "2008");
        Assert.Equal(3, review.AdvertisedMenusForClaimedYear.Count);
        Assert.Contains(review.AdvertisedMenusForClaimedYear,
            x => x.Contains("PDF page 130", StringComparison.Ordinal));
        Assert.DoesNotContain(review.AdvertisedMenusForClaimedYear,
            x => x.Contains("immobilizer function", StringComparison.Ordinal));
        Assert.False(review.HasConfirmedTransport);
    }

    [Fact]
    public void UnlistedYearMeansUnknownCoverageNotUnsupportedVehicle()
    {
        var review = Review(year: "2012");
        Assert.Empty(review.AdvertisedMenusForClaimedYear);
        Assert.Contains("coverage UNKNOWN", Report(year: "2012"));
        Assert.Equal(7, review.MissingEvidence.Count);
    }

    [Fact]
    public void ClaimedVidaOriginAndVerifiedProfileCannotAuthenticatePhysicalCem()
    {
        var card = Card(year: "2007", vida: true);
        var snap = Snapshot() with { ProfileMarkedVerified = true };
        var review = VolvoP2CemRevisionEvidencePreflight.Assess(card, snap);
        Assert.False(review.HasAuthenticatedCemVariant);
        Assert.False(review.HasConfirmedTransport);
        Assert.Contains(VolvoP2CemRevisionEvidenceGap.ProvenSameModuleProvenance,
            review.MissingEvidence);
        Assert.Contains("do not prove", VolvoP2CemRevisionEvidencePreflight
            .BuildReadOnlyReport(card, snap));
    }

    [Fact]
    public void ForgedIdentityCapabilityAndNoncanonicalPartNumberFailClosed()
    {
        foreach (var bad in new[]
        {
            Card() with { MayAccessEcu = true },
            Card() with { IsRestorableBackup = true },
            Card() with { IsUnverifiedManualClaim = false },
            Card() with { ClaimedHardwarePartNumber = "hw123" },
            Card() with { ClaimedModelYear = 2099 }
        })
            Assert.Throws<InvalidDataException>(() =>
                VolvoP2CemRevisionEvidencePreflight.Assess(bad, Snapshot()));
    }

    [Fact]
    public void UnsafeSnapshotFailsAndValidReportNeverLeaksRawValues()
    {
        foreach (var bad in new[]
        {
            Snapshot() with { IsRestorableBackup = true },
            Snapshot() with { ContainsPotentiallySensitiveData = false },
            Snapshot() with { RequestCanId = "800" },
            Snapshot() with { Values = [new EcuDidReadback(0xF187, "BAD")] }
        })
            Assert.Throws<InvalidDataException>(() =>
                VolvoP2CemRevisionEvidencePreflight.BuildReadOnlyReport(Card(), bad));

        var report = Report();
        Assert.DoesNotContain("534543524554", report);
        Assert.DoesNotContain("PRIVATE_PROFILE", report);
        Assert.DoesNotContain("Confidential owner", report);
        Assert.Contains("No raw DID values, VIN, profile metadata", report);
        Assert.Contains("no live CAN, PIN or configuration action enabled", report);
    }

    private static VolvoP2CemRevisionEvidenceReview Review(string year = "2007") =>
        VolvoP2CemRevisionEvidencePreflight.Assess(Card(year), Snapshot());

    private static string Report(string year = "2007") =>
        VolvoP2CemRevisionEvidencePreflight.BuildReadOnlyReport(Card(year), Snapshot());

    private static VolvoP2CemIdentityResearchDocument Card(
        string year = "2007", bool vida = false) =>
        VolvoP2CemIdentityResearchExport.InspectJson(
            VolvoP2CemIdentityResearchExport.CreateJson(
                year, "HW123", "SW456", "DS789", vida));

    private static EcuConfigurationExportDocument Snapshot()
    {
        var original = new EcuConfigurationReadSnapshot(
            "PRIVATE_PROFILE", "Confidential owner", 0x7E0, 0x7E8,
            false, false, new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero),
            [new EcuDidReadback(0xF187, "534543524554")]);
        return EcuConfigurationSnapshotInspection.InspectJson(
            EcuConfigurationSnapshotExport.CreateJson(original, true));
    }
}
