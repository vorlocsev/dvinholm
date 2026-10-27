namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic, fully offline checks for unverified candidate
/// relationship evidence. There is no physical CEM, J2534, PIN or write path.
/// </summary>
public sealed class VolvoP2ConfigDependencyResearchTests
{
    private const string Heater = "p2.xc90.auxiliary-heater";
    private const string Washers = "p2.xc90.headlamp-washers";
    private const string Region = "p2.xc90.market-region";
    private const string Parking = "p2.xc90.parking-assistance";

    [Fact]
    public void EmptyResearchGraphIsNotAValidatedConfigurationPlan()
    {
        var result = Review(Heater, 2007, true, Array.Empty<VolvoP2ConfigDependencyClaim>());
        Assert.Equal(VolvoP2ConfigDependencyReviewKind.NoResearchClaims, result.Kind);
        Assert.Empty(result.Issues);
        Assert.Equal(0, result.RelatedClaimCount);
        AssertNoOperations(result);
    }

    [Fact]
    public void WellFormedResearchClaimsRemainUnverifiedAndNeverEnableOperations()
    {
        var research = new[]
        {
            Claim(Heater, Washers, VolvoP2ConfigDependencyRelation.Requires),
            Claim(Washers, Region, VolvoP2ConfigDependencyRelation.ReviewTogether),
            Claim(Region, Parking, VolvoP2ConfigDependencyRelation.PotentialConflict)
        };
        var a = Review(Heater, 2006, true, research);
        var b = Review(Washers, 2014, true, research);
        Assert.Equal(VolvoP2ConfigDependencyReviewKind.UnverifiedResearchClaimsOnly, a.Kind);
        Assert.Equal(1, a.RelatedClaimCount);
        Assert.Equal(2, b.RelatedClaimCount);
        Assert.Contains("unverified", b.Explanation);
        Assert.Empty(b.Issues);
        AssertNoOperations(a);
        AssertNoOperations(b);
    }

    [Fact]
    public void UnknownOptionAndUnconfirmedOrOutOfRangeYearsFailClosed()
    {
        var empty = Array.Empty<VolvoP2ConfigDependencyClaim>();
        var unknown = Review("p2.xc90.MARKET-REGION", 2008, true, empty);
        Assert.Equal(VolvoP2ConfigDependencyReviewKind.UnknownCandidate, unknown.Kind);
        AssertNoOperations(unknown);

        foreach (var (year, confirmed) in new (int?, bool)[]
        {
            (null, true), (2007, false), (2003, true),
            (2005, true), (2015, true)
        })
        {
            var review = Review(Region, year, confirmed, empty);
            Assert.Equal(
                VolvoP2ConfigDependencyReviewKind.ModelYearUnconfirmedOrOutsideResearchScope,
                review.Kind);
            AssertNoOperations(review);
        }
    }

    [Fact]
    public void NullClaimsAndUnknownOrSelfReferencedCandidatesAreRejected()
    {
        var noSet = Review(Heater, 2008, true, null);
        Assert.Contains(VolvoP2ConfigDependencyIssue.MissingClaimSet, noSet.Issues);

        var unknown = Review(Heater, 2008, true, new[]
        {
            Claim(Heater, "unknown.vendor.id"),
            Claim(Region, Region)
        });
        Assert.Equal(VolvoP2ConfigDependencyReviewKind.InvalidResearchClaims, unknown.Kind);
        Assert.Contains(VolvoP2ConfigDependencyIssue.UnknownCandidateId, unknown.Issues);
        Assert.Contains(VolvoP2ConfigDependencyIssue.SelfReference, unknown.Issues);
        Assert.Equal(0, unknown.RelatedClaimCount);
        AssertNoOperations(unknown);
    }

    [Fact]
    public void UndefinedRelationshipsAndDuplicatePairsAreRejected()
    {
        var invalidRelation = Review(Heater, 2008, true,
            new[] { Claim(Heater, Washers, (VolvoP2ConfigDependencyRelation)100) });
        Assert.Contains(VolvoP2ConfigDependencyIssue.UndefinedRelation,
            invalidRelation.Issues);

        var duplicates = Review(Heater, 2008, true, new[]
        {
            Claim(Heater, Washers),
            Claim(Heater, Washers)
        });
        Assert.Contains(VolvoP2ConfigDependencyIssue.DuplicateRelationship,
            duplicates.Issues);

        var reverseConflict = Review(Region, 2008, true, new[]
        {
            Claim(Region, Parking, VolvoP2ConfigDependencyRelation.PotentialConflict),
            Claim(Parking, Region, VolvoP2ConfigDependencyRelation.PotentialConflict)
        });
        Assert.Contains(VolvoP2ConfigDependencyIssue.DuplicateRelationship,
            reverseConflict.Issues);
        AssertNoOperations(reverseConflict);
    }

    [Fact]
    public void CyclicResearchRequirementsNeverProduceAnEditPlan()
    {
        var result = Review(Heater, 2008, true, new[]
        {
            Claim(Heater, Washers, VolvoP2ConfigDependencyRelation.Requires),
            Claim(Washers, Region, VolvoP2ConfigDependencyRelation.Requires),
            Claim(Region, Heater, VolvoP2ConfigDependencyRelation.Requires)
        });
        Assert.Equal(VolvoP2ConfigDependencyReviewKind.InvalidResearchClaims, result.Kind);
        Assert.Contains(VolvoP2ConfigDependencyIssue.CyclicRequirements, result.Issues);
        AssertNoOperations(result);
    }

    [Fact]
    public void MissingOversizedOrControlCharacterEvidenceIsRejected()
    {
        foreach (string source in new[]
        {
            "", "   ", new string('X', VolvoP2ConfigDependencyResearch.MaximumSourceNoteLength + 1),
            "not-safe\nsecond line"
        })
        {
            var result = Review(Heater, 2008, true, new[]
            {
                Claim(Heater, Washers, source: source)
            });
            Assert.Contains(VolvoP2ConfigDependencyIssue.MissingOrUnsafeSource,
                result.Issues);
            AssertNoOperations(result);
        }
    }

    [Fact]
    public void BoundariesRedactionAndImmutableDiagnosticsAreEnforced()
    {
        var excessive = Enumerable.Repeat(Claim(Heater, Washers),
            VolvoP2ConfigDependencyResearch.MaximumClaims + 1);
        var tooMany = Review(Heater, 2008, true, excessive);
        Assert.Contains(VolvoP2ConfigDependencyIssue.TooManyClaims, tooMany.Issues);

        var secretMarker = "DO-NOT-ECHO-PRIVATE-TOKEN";
        var invalid = Review(Heater, 2008, true, new[]
        {
            Claim(Heater, "invalid.id", source: secretMarker)
        });
        Assert.DoesNotContain(secretMarker, invalid.Explanation);
        Assert.DoesNotContain(secretMarker, string.Join(",", invalid.Issues));
        var issues = (IList<VolvoP2ConfigDependencyIssue>)invalid.Issues;
        Assert.Throws<NotSupportedException>(() =>
            issues[0] = VolvoP2ConfigDependencyIssue.MissingClaimSet);
        AssertNoOperations(invalid);
        AssertNoOperations(tooMany);
    }

    private static VolvoP2ConfigDependencyClaim Claim(
        string left,
        string right,
        VolvoP2ConfigDependencyRelation relation =
            VolvoP2ConfigDependencyRelation.ReviewTogether,
        string source = "Research note, unverified, no operational mapping") =>
        new(left, right, relation, source);

    private static VolvoP2ConfigDependencyReview Review(
        string? id, int? year, bool confirmed,
        IEnumerable<VolvoP2ConfigDependencyClaim>? claims) =>
        VolvoP2ConfigDependencyResearch.Assess(id, year, confirmed, claims);

    private static void AssertNoOperations(VolvoP2ConfigDependencyReview result)
    {
        Assert.False(result.CanIssueVehicleRequest);
        Assert.False(result.CanReadConfiguration);
        Assert.False(result.CanPrepareVehicleEdit);
        Assert.False(result.CanAuthenticateOrFindPin);
        Assert.False(result.CanWriteConfiguration);
    }
}
