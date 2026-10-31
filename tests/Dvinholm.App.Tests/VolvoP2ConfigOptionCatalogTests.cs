namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline gates for vendor-advertised XC90 P2 *names*.
/// No protocol, ECU, PIN, J2534 connection or vehicle write is exercised.
/// </summary>
public sealed class VolvoP2ConfigOptionCatalogTests
{
    [Fact]
    public void CatalogHasEightDistinctLocallyNamespacedIdentifiers()
    {
        var entries = VolvoP2ConfigOptionCatalog.Entries;
        Assert.Equal(8, entries.Count);
        Assert.All(entries, e => Assert.StartsWith("p2.xc90.", e.CanonicalId));
        Assert.Equal(entries.Count, entries.Select(e => e.CanonicalId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.DisplayName)));
    }

    [Fact]
    public void EveryCandidateDisclosesVendorOnlyEvidenceAndYearScope()
    {
        Assert.All(VolvoP2ConfigOptionCatalog.Entries, e =>
        {
            Assert.Equal(2006, e.AdvertisedEarliestModelYear);
            Assert.Equal(2014, e.ScopeLastModelYear);
            Assert.Contains("vendor advertisement", e.Source);
            Assert.Contains("not independently verified", e.Source);
            Assert.Contains("user-provided screenshots", e.Source);
        });
    }

    [Fact]
    public void EveryCandidateDisablesVehicleReadWriteAndPin()
    {
        Assert.All(VolvoP2ConfigOptionCatalog.Entries, e =>
        {
            Assert.False(e.ModuleOwnershipVerified);
            Assert.False(e.FirmwareApplicabilityVerified);
            Assert.False(e.HasVerifiedReadMapping);
            Assert.False(e.HasVerifiedWriteMapping);
            Assert.False(e.CanReadFromVehicle);
            Assert.False(e.CanWriteToVehicle);
            Assert.False(e.CanRetrieveOrAuthorizePin);
        });
    }

    [Fact]
    public void ReturnedCatalogCannotBeMutatedThroughListInterface()
    {
        var list = (IList<VolvoP2ConfigOptionCandidate>)VolvoP2ConfigOptionCatalog.Entries;
        var original = list[0];
        Assert.Throws<NotSupportedException>(() => list[0] = original with
        {
            DisplayName = "Never alter live catalog"
        });
        Assert.Same(original, VolvoP2ConfigOptionCatalog.Entries[0]);
    }

    [Fact]
    public void MissingOrUnconfirmedModelYearNeverClaimsApplicability()
    {
        foreach (var assessment in new[]
        {
            Assess("p2.xc90.wheel-size", null, true),
            Assess("p2.xc90.wheel-size", 2007, false)
        })
        {
            Assert.Equal(VolvoP2ConfigCandidateAssessmentKind.ModelYearUnconfirmed, assessment.Kind);
            Assert.False(assessment.CanIssueVehicleRequest);
            Assert.False(assessment.CanEditVehicle);
        }
    }

    [Fact]
    public void OutOfScopeAndPre2006ModelYearsRemainUnsupportedClaims()
    {
        foreach (var year in new[] { 2002, 2015 })
            Assert.Equal(
                VolvoP2ConfigCandidateAssessmentKind.OutsideFirstGenerationXc90Scope,
                Assess("p2.xc90.auxiliary-heater", year, true).Kind);
        foreach (var year in new[] { 2003, 2004, 2005 })
            Assert.Equal(
                VolvoP2ConfigCandidateAssessmentKind.OutsideVendorAdvertisedModelYears,
                Assess("p2.xc90.auxiliary-heater", year, true).Kind);
    }

    [Fact]
    public void MatchingAdvertisedYearNeverAuthorizesPhysicalCapability()
    {
        foreach (var year in new[] { 2006, 2007, 2014 })
        {
            var assessment = Assess("p2.xc90.speed-policy", year, true);
            Assert.Equal(VolvoP2ConfigCandidateAssessmentKind.VendorAdvertisedOnly, assessment.Kind);
            Assert.NotNull(assessment.Candidate);
            Assert.Contains("not verified", assessment.Explanation);
            Assert.False(assessment.CanIssueVehicleRequest);
            Assert.False(assessment.CanEditVehicle);
            Assert.False(assessment.CanAuthorizeServiceOrPin);
        }
    }

    [Fact]
    public void UnknownIdsAreCaseSensitiveAndSafetyFlagsAreExplicit()
    {
        foreach (var id in new string?[] { null, "", "p2.xc90.SPEED-POLICY", "vendor.option.42" })
            Assert.Equal(VolvoP2ConfigCandidateAssessmentKind.NotListed,
                Assess(id, 2007, true).Kind);

        var entries = VolvoP2ConfigOptionCatalog.Entries;
        Assert.True(entries.Single(e => e.CanonicalId == "p2.xc90.speed-policy")
            .SafetyOrComplianceReviewRequired);
        Assert.True(entries.Single(e => e.CanonicalId == "p2.xc90.automatic-locking")
            .SafetyOrComplianceReviewRequired);
        Assert.False(entries.Single(e => e.CanonicalId == "p2.xc90.service-interval")
            .SafetyOrComplianceReviewRequired);
    }

    private static VolvoP2ConfigCandidateAssessment Assess(
        string? id, int? year, bool confirmed) =>
        VolvoP2ConfigOptionCatalog.Assess(id, year, confirmed);
}
