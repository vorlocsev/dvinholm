namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline checks of the per-option research evidence
/// gates. No adapter, ECU, PIN, CAN, file import or configuration writer.
/// </summary>
public sealed class VolvoP2CemOptionEvidenceChecklistTests
{
    private static VolvoP2CemOptionEvidenceAssessment Check(string id) =>
        Assert.IsType<VolvoP2CemOptionEvidenceAssessment>(
            VolvoP2CemOptionEvidenceChecklist.Assess(id));

    [Fact]
    public void AllFourteenManifestLabelsRetainSixDistinctMissingProofGates()
    {
        Assert.Equal(14, VolvoP2CemConfigOptionManifest.Entries.Count);
        foreach (var option in VolvoP2CemConfigOptionManifest.Entries)
        {
            var review = Check(option.CanonicalId);
            Assert.Equal(6, review.BlockingRequirements.Count);
            Assert.Equal(6, review.BlockingRequirements.Select(x => x.Gap).Distinct().Count());
            Assert.All(review.BlockingRequirements, x =>
                Assert.False(string.IsNullOrWhiteSpace(x.MissingEvidence)));
            Assert.Equal(option.Provenance, review.SourceTier);
        }
    }

    [Fact]
    public void VendorAdvertisingCannotBecomeManufacturerApplicability()
    {
        var review = Check("p2.xc90.speed-policy");
        Assert.Equal(VolvoP2CemOptionProvenance.ThirdPartyAdvertisement, review.SourceTier);
        Assert.Contains("vendor-advertised category",
            review.BlockingRequirements[0].MissingEvidence);
        Assert.Contains("advertised option", review.FirstReviewAction);
        Assert.DoesNotContain("published service-menu", review.FirstReviewAction);
    }

    [Fact]
    public void VolvoMenuHeadingDoesNotBecomeVehicleSpecificMapping()
    {
        var review = Check("xc90.oem.cem.three-flash-indicator");
        Assert.Equal(VolvoP2CemOptionProvenance.VolvoPublishedCemServiceMenu, review.SourceTier);
        Assert.Contains("not a parameter mapping",
            review.BlockingRequirements[0].MissingEvidence);
        Assert.Contains("Volvo menu heading", review.FirstReviewAction);
        Assert.DoesNotContain("vendor-advertised", review.FirstReviewAction);
    }

    [Fact]
    public void AllPhysicalIdentityOwnershipMappingValueAndRecoveryGatesStayOpen()
    {
        foreach (var option in VolvoP2CemConfigOptionManifest.Entries)
        {
            var review = Check(option.CanonicalId);
            Assert.Equal(new[]
            {
                VolvoP2CemOptionEvidenceGap.IndependentApplicability,
                VolvoP2CemOptionEvidenceGap.AuthenticatedCemRevision,
                VolvoP2CemOptionEvidenceGap.PhysicalOwnerAndEquipment,
                VolvoP2CemOptionEvidenceGap.DocumentedReadOnlyMapping,
                VolvoP2CemOptionEvidenceGap.AllowedValuesAndDependencies,
                VolvoP2CemOptionEvidenceGap.BackupRecoveryAndBenchAcceptance
            }, review.BlockingRequirements.Select(x => x.Gap));
            Assert.Contains("empty catalogs mean unknown",
                review.BlockingRequirements[4].MissingEvidence);
        }
    }

    [Fact]
    public void UnverifiedOptionsNeverGainRealVehicleOrPinCapabilities()
    {
        foreach (var option in VolvoP2CemConfigOptionManifest.Entries)
        {
            var review = Check(option.CanonicalId);
            Assert.False(review.HasAuthenticatedPhysicalCem);
            Assert.False(review.HasVerifiedConfigurationRead);
            Assert.False(review.MayReadCemConfiguration);
            Assert.False(review.MayAcquirePin);
            Assert.False(review.MayPreviewOrWriteCemConfiguration);
            Assert.False(review.MayTransmitToVehicle);
        }
        Assert.Empty(VolvoP2CemSupportMatrix.ProductionEntries);
    }

    [Fact]
    public void UnknownCaseChangedAndPrivateIdentifiersDoNotEchoOrMatch()
    {
        const string privateText = "SENSITIVE-PRIVATE-PIN-INPUT";
        foreach (var invalid in new string?[]
        {
            null, "", " ", "P2.XC90.SPEED-POLICY", privateText,
            "secret\nline", new string('x', 129)
        })
        {
            Assert.Null(VolvoP2CemOptionEvidenceChecklist.Assess(invalid));
            var report = VolvoP2CemOptionEvidenceChecklist.BuildReadOnlyReport(invalid);
            Assert.Contains("Unknown CEM research label", report);
            Assert.DoesNotContain(privateText, report);
            Assert.DoesNotContain("secret", report);
        }
    }

    [Fact]
    public void RequirementsCannotBeChangedThroughReturnedCollection()
    {
        var first = Check("p2.xc90.speed-policy");
        var requirements = Assert.IsAssignableFrom<IList<VolvoP2CemOptionEvidenceRequirement>>(
            first.BlockingRequirements);
        Assert.Throws<NotSupportedException>(() =>
            requirements[0] = new(VolvoP2CemOptionEvidenceGap.IndependentApplicability, "verified"));
        Assert.Throws<NotSupportedException>(() => requirements.Clear());
        Assert.Contains("vendor-advertised category",
            Check("p2.xc90.speed-policy").BlockingRequirements[0].MissingEvidence);
    }

    [Fact]
    public void ExistingDetailCardShowsSourceSeparatedGatesWithoutSendingRequests()
    {
        var vendor = VolvoP2CemCandidateDetail.BuildReadOnlyReport(
            "p2.xc90.speed-policy", VolvoP2CemCatalogEvidenceTier.All);
        var oem = VolvoP2CemCandidateDetail.BuildReadOnlyReport(
            "xc90.oem.cem.three-flash-indicator", VolvoP2CemCatalogEvidenceTier.All);
        foreach (var report in new[] { vendor, oem })
        {
            Assert.Contains("OPTION-SPECIFIC EVIDENCE CHECKLIST (OFFLINE ONLY)", report);
            Assert.Equal(6, report.Split("[MISSING]", StringSplitOptions.None).Length - 1);
            Assert.Contains("No real configuration read, PIN access, transmission or write is permitted", report);
            Assert.Contains("Read CEM configuration: DISABLED", report);
            Assert.Contains("No CAN/J2534/UDS request or ECU change", report);
            Assert.DoesNotContain("0x27", report);
            Assert.DoesNotContain("7E0", report);
        }
        Assert.Contains("Source tier: ThirdPartyAdvertisement", vendor);
        Assert.Contains("Source tier: VolvoPublishedCemServiceMenu", oem);
    }
}
