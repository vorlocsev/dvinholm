namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline-only cases using invented research labels and invented
/// hardware/software revisions. None are physical Volvo CEM data.
/// </summary>
public sealed class VolvoP2CemParameterEvidenceCatalogTests
{
    private const string Heater = "p2.xc90.auxiliary-heater";
    private const string Washers = "p2.xc90.headlamp-washers";

    [Fact]
    public void ProductionParameterMappingsAreDeliberatelyEmpty()
    {
        Assert.Empty(VolvoP2CemParameterEvidenceCatalog.ProductionRecords);
        var result = Assess(Heater, 2007, true, Identity(), "TEST_HW", "TEST_SW", []);
        Assert.Equal(VolvoP2ParameterEvidenceKind.MissingEvidence, result.Kind);
        AssertNoPermissions(result);
    }

    [Fact]
    public void ProposedRevisionSpecificRecordIsOnlyAResearchClaim()
    {
        var result = Assess(Heater, 2007, true, Identity(), "TEST_HW", "TEST_SW",
            [Entry()]);
        Assert.Equal(VolvoP2ParameterEvidenceKind.ProposedResearchOnly, result.Kind);
        Assert.Equal(1, result.MatchingResearchRecords);
        Assert.Empty(result.Problems);
        Assert.Contains("NOT authenticated", result.Explanation);
        AssertNoPermissions(result);
    }

    [Fact]
    public void HumanReviewNotationNeverAuthenticatesFirmwareOrGrantsAccess()
    {
        var evidence = Entry() with
        {
            ReviewNotation = VolvoP2ParameterReviewNotation.HumanReviewedClaim,
            ValueMeaningEvidenceReference = "Independent synthetic value study"
        };
        var result = Assess(Heater, 2008, true, Identity(), "TEST_HW", "TEST_SW",
            [evidence]);
        Assert.Equal(VolvoP2ParameterEvidenceKind.ReviewedClaimResearchOnly, result.Kind);
        Assert.Equal(1, result.MatchingResearchRecords);
        AssertNoPermissions(result);
    }

    [Fact]
    public void CandidateNamesAndModelYearBoundariesMustMatchResearchScope()
    {
        foreach (var candidate in new[] { "", "p2.xc90.AUXILIARY-HEATER", "vendor.option" })
        {
            var result = Assess(candidate, 2007, true, Identity(), "TEST_HW", "TEST_SW", [Entry()]);
            Assert.Equal(VolvoP2ParameterEvidenceKind.UnknownCandidate, result.Kind);
            AssertNoPermissions(result);
        }
        foreach (var (year, confirmed) in new (int?, bool)[]
        {
            (null, true), (2007, false), (2005, true), (2015, true)
        })
        {
            var result = Assess(Heater, year, confirmed, Identity(), "TEST_HW", "TEST_SW",
                [Entry()]);
            Assert.Equal(VolvoP2ParameterEvidenceKind.UnsupportedResearchYear, result.Kind);
            AssertNoPermissions(result);
        }
    }

    [Fact]
    public void WrongAmbiguousIdentityOrHardwareSoftwareRevisionFailClosed()
    {
        foreach (var identity in new[]
        {
            (VolvoP2CemSupportAssessment?)null,
            Identity(VolvoP2CemSupportKind.Unknown),
            Identity(VolvoP2CemSupportKind.Ambiguous)
        })
        {
            var result = Assess(Heater, 2008, true, identity, "TEST_HW", "TEST_SW", [Entry()]);
            Assert.Equal(VolvoP2ParameterEvidenceKind.NoAuthenticatedRevision, result.Kind);
            AssertNoPermissions(result);
        }
        foreach (var (hw, sw) in new[]
        {
            ("OTHER_HW", "TEST_SW"), ("TEST_HW", "OTHER_SW")
        })
        {
            var result = Assess(Heater, 2008, true, Identity(), hw, sw, [Entry()]);
            Assert.Equal(VolvoP2ParameterEvidenceKind.NoMatchingResearchRecord, result.Kind);
            AssertNoPermissions(result);
        }
    }

    [Fact]
    public void AnyMalformedRecordRejectsEntireResearchCatalog()
    {
        var invalid = new VolvoP2CemParameterResearchRecord?[]
        {
            Entry() with { CandidateId = "invalid.vendor.id" },
            Entry() with { CemSoftwareRevision = "" },
            Entry() with { EvidenceReference = "has\ncontrol" },
            Entry() with { ModelYearFrom = 2004 },
            Entry() with { ReviewNotation = (VolvoP2ParameterReviewNotation)42 },
            Entry() with
            {
                ReviewNotation = VolvoP2ParameterReviewNotation.HumanReviewedClaim,
                ValueMeaningEvidenceReference = null
            },
            null
        };
        var result = Assess(Heater, 2008, true, Identity(), "TEST_HW", "TEST_SW",
            invalid);
        Assert.Equal(VolvoP2ParameterEvidenceKind.InvalidEvidenceCatalog, result.Kind);
        Assert.Contains(VolvoP2ParameterEvidenceProblem.UnknownCandidateId, result.Problems);
        Assert.Contains(VolvoP2ParameterEvidenceProblem.MissingVariantOrRevision, result.Problems);
        Assert.Contains(VolvoP2ParameterEvidenceProblem.InvalidSourceReference, result.Problems);
        Assert.Contains(VolvoP2ParameterEvidenceProblem.InvalidXc90ModelYearRange, result.Problems);
        Assert.Contains(VolvoP2ParameterEvidenceProblem.InvalidReviewNotation, result.Problems);
        Assert.Contains(VolvoP2ParameterEvidenceProblem.InvalidMeaningReference, result.Problems);
        Assert.Contains(VolvoP2ParameterEvidenceProblem.NullRecord, result.Problems);
        Assert.Equal(0, result.MatchingResearchRecords);
        AssertNoPermissions(result);
    }

    [Fact]
    public void OverlappingExactRevisionRangesRejectEveryPotentialMatch()
    {
        var baseline = Entry();
        var conflict = baseline with
        {
            ModelYearFrom = 2007,
            ModelYearThrough = 2009,
            EvidenceReference = "Conflicting synthetic research note"
        };
        var result = Assess(Heater, 2008, true, Identity(), "TEST_HW", "TEST_SW",
            [baseline, conflict]);
        Assert.Equal(VolvoP2ParameterEvidenceKind.InvalidEvidenceCatalog, result.Kind);
        Assert.Contains(VolvoP2ParameterEvidenceProblem.OverlappingRevisionClaim,
            result.Problems);
        AssertNoPermissions(result);

        // Other candidates can coexist without claiming real mappings.
        var distinct = Assess(Washers, 2008, true, Identity(), "TEST_HW", "TEST_SW",
            [baseline, Entry() with { CandidateId = Washers }]);
        Assert.Equal(VolvoP2ParameterEvidenceKind.ProposedResearchOnly, distinct.Kind);
        AssertNoPermissions(distinct);
    }

    [Fact]
    public void BoundedAndRedactedImmutableResultsCannotBeModified()
    {
        var oversized = Enumerable.Repeat(Entry(),
            VolvoP2CemParameterEvidenceCatalog.MaximumResearchRecords + 1);
        var exceeded = Assess(Heater, 2008, true, Identity(), "TEST_HW", "TEST_SW",
            oversized);
        Assert.Contains(VolvoP2ParameterEvidenceProblem.ExcessiveRecords,
            exceeded.Problems);
        AssertNoPermissions(exceeded);

        const string secret = "SENSITIVE-SYNTHETIC-NOTE-DO-NOT-ECHO";
        var bad = Assess(Heater, 2008, true, Identity(), "TEST_HW", "TEST_SW",
            [Entry() with { EvidenceReference = secret + "\n" }]);
        Assert.DoesNotContain(secret, bad.Explanation);
        Assert.DoesNotContain(secret, string.Join(",", bad.Problems));
        var issues = Assert.IsAssignableFrom<IList<VolvoP2ParameterEvidenceProblem>>(
            bad.Problems);
        Assert.Throws<NotSupportedException>(() =>
            issues[0] = VolvoP2ParameterEvidenceProblem.ExcessiveRecords);
        AssertNoPermissions(bad);
    }

    private static VolvoP2CemSupportAssessment Identity(
        VolvoP2CemSupportKind kind = VolvoP2CemSupportKind.VerifiedIdentityReadOnly) =>
        new(kind,
            kind == VolvoP2CemSupportKind.Unknown ? null : "SYNTHETIC_CEM_VARIANT",
            Array.Empty<ushort>(), "Synthetic read-only identity fixture, not Volvo evidence");

    private static VolvoP2CemParameterResearchRecord Entry() =>
        new(Heater, "SYNTHETIC_CEM_VARIANT", 2006, 2010,
            "TEST_HW", "TEST_SW", "Synthetic research only; never confirmed",
            null, VolvoP2ParameterReviewNotation.Proposed);

    private static VolvoP2ParameterEvidenceAssessment Assess(
        string? id, int? year, bool confirmed,
        VolvoP2CemSupportAssessment? identity,
        string? hardware, string? software,
        IEnumerable<VolvoP2CemParameterResearchRecord?>? records) =>
        VolvoP2CemParameterEvidenceCatalog.Assess(
            id, year, confirmed, identity, hardware, software, records);

    private static void AssertNoPermissions(VolvoP2ParameterEvidenceAssessment result)
    {
        Assert.False(result.HasVerifiedPhysicalCemIdentity);
        Assert.False(result.HasVerifiedParameterMapping);
        Assert.False(result.MayReadVehicleConfiguration);
        Assert.False(result.MayPrepareConfigurationEdit);
        Assert.False(result.MayFindOrAuthorizePin);
        Assert.False(result.MayTransmitOrWriteToEcu);
    }
}
