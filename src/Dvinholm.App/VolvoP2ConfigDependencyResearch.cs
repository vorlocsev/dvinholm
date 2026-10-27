namespace Dvinholm.App;

/// <summary>
/// Research assertions about possible relationships between high-level
/// configuration candidate names. These are NOT validated ECU dependencies,
/// factory parameter IDs, firmware mappings or executable edit plans.
/// </summary>
internal enum VolvoP2ConfigDependencyRelation
{
    Requires,
    PotentialConflict,
    ReviewTogether
}

internal enum VolvoP2ConfigDependencyReviewKind
{
    UnknownCandidate,
    ModelYearUnconfirmedOrOutsideResearchScope,
    InvalidResearchClaims,
    NoResearchClaims,
    UnverifiedResearchClaimsOnly
}

internal enum VolvoP2ConfigDependencyIssue
{
    MissingClaimSet,
    TooManyClaims,
    UnknownCandidateId,
    SelfReference,
    UndefinedRelation,
    DuplicateRelationship,
    MissingOrUnsafeSource,
    CyclicRequirements
}

/// <summary>
/// A bounded, user/researcher-supplied proposition. SourceNote is an opaque
/// research reference, not independently authenticated proof. Never display
/// or log the note from the resulting assessment.
/// </summary>
internal sealed record VolvoP2ConfigDependencyClaim(
    string CandidateId,
    string RelatedCandidateId,
    VolvoP2ConfigDependencyRelation Relation,
    string SourceNote);

internal sealed record VolvoP2ConfigDependencyReview(
    VolvoP2ConfigDependencyReviewKind Kind,
    int RelatedClaimCount,
    IReadOnlyList<VolvoP2ConfigDependencyIssue> Issues,
    string Explanation)
{
    // A structurally consistent graph is not an ECU configuration plan.
    internal bool CanIssueVehicleRequest => false;
    internal bool CanReadConfiguration => false;
    internal bool CanPrepareVehicleEdit => false;
    internal bool CanAuthenticateOrFindPin => false;
    internal bool CanWriteConfiguration => false;
}

/// <summary>
/// Offline-only, fail-closed preflight for candidate-to-candidate dependency
/// *propositions*. It checks graph shape and declared research provenance,
/// not whether the relationships are true for any XC90/CEM revision.
/// </summary>
internal static class VolvoP2ConfigDependencyResearch
{
    internal const int MaximumClaims = 64;
    internal const int MaximumSourceNoteLength = 256;

    internal static VolvoP2ConfigDependencyReview Assess(
        string? candidateId,
        int? modelYear,
        bool modelYearIndependentlyConfirmed,
        IEnumerable<VolvoP2ConfigDependencyClaim?>? claims)
    {
        var catalogResult = VolvoP2ConfigOptionCatalog.Assess(
            candidateId, modelYear, modelYearIndependentlyConfirmed);

        if (catalogResult.Kind == VolvoP2ConfigCandidateAssessmentKind.NotListed)
            return Result(VolvoP2ConfigDependencyReviewKind.UnknownCandidate, 0,
                Array.Empty<VolvoP2ConfigDependencyIssue>(),
                "Unknown Dvinholm research candidate. No vehicle option is identified.");

        if (catalogResult.Kind != VolvoP2ConfigCandidateAssessmentKind.VendorAdvertisedOnly)
            return Result(
                VolvoP2ConfigDependencyReviewKind.ModelYearUnconfirmedOrOutsideResearchScope,
                0, Array.Empty<VolvoP2ConfigDependencyIssue>(),
                "Model year is not confirmed within the vendor-advertised research scope.");

        if (claims is null)
            return Invalid(VolvoP2ConfigDependencyIssue.MissingClaimSet);

        // Take is bounded even for a lazily generated or infinite input.
        var supplied = claims.Take(MaximumClaims + 1).ToArray();
        if (supplied.Length > MaximumClaims)
            return Invalid(VolvoP2ConfigDependencyIssue.TooManyClaims);

        var knownIds = VolvoP2ConfigOptionCatalog.Entries
            .Select(entry => entry.CanonicalId)
            .ToHashSet(StringComparer.Ordinal);

        var issues = new HashSet<VolvoP2ConfigDependencyIssue>();
        var relationships = new HashSet<(string Left, string Right)>();
        foreach (var claim in supplied)
        {
            if (claim is null)
            {
                issues.Add(VolvoP2ConfigDependencyIssue.UnknownCandidateId);
                continue;
            }

            bool leftKnown = claim.CandidateId is not null &&
                knownIds.Contains(claim.CandidateId);
            bool rightKnown = claim.RelatedCandidateId is not null &&
                knownIds.Contains(claim.RelatedCandidateId);

            if (!leftKnown || !rightKnown)
                issues.Add(VolvoP2ConfigDependencyIssue.UnknownCandidateId);

            if (leftKnown && rightKnown &&
                string.Equals(claim.CandidateId, claim.RelatedCandidateId,
                    StringComparison.Ordinal))
                issues.Add(VolvoP2ConfigDependencyIssue.SelfReference);

            bool relationKnown = Enum.IsDefined(claim.Relation);
            if (!relationKnown)
                issues.Add(VolvoP2ConfigDependencyIssue.UndefinedRelation);

            if (string.IsNullOrWhiteSpace(claim.SourceNote) ||
                claim.SourceNote.Length > MaximumSourceNoteLength ||
                claim.SourceNote.Any(char.IsControl))
                issues.Add(VolvoP2ConfigDependencyIssue.MissingOrUnsafeSource);

            // For PotentialConflict, reverse references describe the same
            // undirected assertion; duplicate relationship kinds also fail.
            if (leftKnown && rightKnown && relationKnown)
            {
                var left = claim.CandidateId;
                var right = claim.RelatedCandidateId;
                if (claim.Relation == VolvoP2ConfigDependencyRelation.PotentialConflict &&
                    string.CompareOrdinal(left, right) > 0)
                    (left, right) = (right, left);

                if (!relationships.Add((left, right)))
                    issues.Add(VolvoP2ConfigDependencyIssue.DuplicateRelationship);
            }
        }

        if (issues.Count == 0 && HasRequirementCycle(supplied!))
            issues.Add(VolvoP2ConfigDependencyIssue.CyclicRequirements);

        if (issues.Count != 0)
            return Result(VolvoP2ConfigDependencyReviewKind.InvalidResearchClaims, 0,
                issues.OrderBy(issue => issue).ToArray(),
                "Research relationship declarations failed structural preflight.");

        int related = supplied.Count(claim =>
            string.Equals(claim!.CandidateId, candidateId, StringComparison.Ordinal) ||
            string.Equals(claim.RelatedCandidateId, candidateId, StringComparison.Ordinal));

        return Result(
            supplied.Length == 0
                ? VolvoP2ConfigDependencyReviewKind.NoResearchClaims
                : VolvoP2ConfigDependencyReviewKind.UnverifiedResearchClaimsOnly,
            related, Array.Empty<VolvoP2ConfigDependencyIssue>(),
            supplied.Length == 0
                ? "No researched option relationships are registered."
                : "Relationship claims are unverified; their sources and graph shape " +
                  "do not establish a firmware-specific dependency or write permission.");
    }

    private static bool HasRequirementCycle(
        IReadOnlyList<VolvoP2ConfigDependencyClaim?> claims)
    {
        var graph = claims
            .Where(claim => claim!.Relation == VolvoP2ConfigDependencyRelation.Requires)
            .GroupBy(claim => claim!.CandidateId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => group.Select(claim => claim!.RelatedCandidateId).ToArray(),
                StringComparer.Ordinal);

        var entered = new HashSet<string>(StringComparer.Ordinal);
        var finished = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(string id)
        {
            if (entered.Contains(id))
                return true;
            if (finished.Contains(id))
                return false;

            entered.Add(id);
            if (graph.TryGetValue(id, out var targets))
            {
                foreach (string target in targets)
                    if (Visit(target))
                        return true;
            }
            entered.Remove(id);
            finished.Add(id);
            return false;
        }

        return graph.Keys.Any(Visit);
    }

    private static VolvoP2ConfigDependencyReview Invalid(
        VolvoP2ConfigDependencyIssue issue) =>
        Result(VolvoP2ConfigDependencyReviewKind.InvalidResearchClaims, 0,
            new[] { issue }, "Research relationship declarations failed structural preflight.");

    private static VolvoP2ConfigDependencyReview Result(
        VolvoP2ConfigDependencyReviewKind kind,
        int count,
        VolvoP2ConfigDependencyIssue[] issues,
        string explanation) =>
        new(kind, count, Array.AsReadOnly(issues), explanation);
}
