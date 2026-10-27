namespace Dvinholm.App;

/// <summary>
/// A researcher's assertion about a future display parameter, NOT an ECU
/// parameter mapping or validated configuration value.
/// </summary>
internal enum VolvoP2ParameterReviewNotation
{
    Proposed,
    HumanReviewedClaim
}

internal enum VolvoP2ParameterEvidenceKind
{
    UnknownCandidate,
    UnsupportedResearchYear,
    MissingEvidence,
    InvalidEvidenceCatalog,
    NoAuthenticatedRevision,
    NoMatchingResearchRecord,
    ProposedResearchOnly,
    ReviewedClaimResearchOnly
}

internal enum VolvoP2ParameterEvidenceProblem
{
    ExcessiveRecords,
    NullRecord,
    UnknownCandidateId,
    InvalidXc90ModelYearRange,
    MissingVariantOrRevision,
    InvalidSourceReference,
    InvalidMeaningReference,
    InvalidReviewNotation,
    OverlappingRevisionClaim
}

/// <summary>
/// Metadata only. No CAN ID, DID, memory address, checksum, PIN, raw value,
/// allowed value encoding, credentials or executable service data.
/// A citation and a review notation do not prove any assertion is true.
/// </summary>
internal sealed record VolvoP2CemParameterResearchRecord(
    string CandidateId,
    string VariantId,
    int ModelYearFrom,
    int ModelYearThrough,
    string CemHardwareRevision,
    string CemSoftwareRevision,
    string EvidenceReference,
    string? ValueMeaningEvidenceReference,
    VolvoP2ParameterReviewNotation ReviewNotation);

internal sealed record VolvoP2ParameterEvidenceAssessment(
    VolvoP2ParameterEvidenceKind Kind,
    int MatchingResearchRecords,
    IReadOnlyList<VolvoP2ParameterEvidenceProblem> Problems,
    string Explanation)
{
    internal bool HasVerifiedPhysicalCemIdentity => false;
    internal bool HasVerifiedParameterMapping => false;
    internal bool MayReadVehicleConfiguration => false;
    internal bool MayPrepareConfigurationEdit => false;
    internal bool MayFindOrAuthorizePin => false;
    internal bool MayTransmitOrWriteToEcu => false;
}

/// <summary>
/// Bounded offline preflight. The entire proposed catalog is validated before
/// considering any record; there is deliberately no production parameter
/// catalog and no way to grant live vehicle capabilities from this class.
/// </summary>
internal static class VolvoP2CemParameterEvidenceCatalog
{
    internal const int MaximumResearchRecords = 64;
    internal const int MaximumEvidenceReferenceLength = 256;

    // Never synthesize real records from option advertisements or the
    // identity-only CEM support matrix.
    internal static IReadOnlyList<VolvoP2CemParameterResearchRecord> ProductionRecords { get; } =
        Array.Empty<VolvoP2CemParameterResearchRecord>();

    internal static VolvoP2ParameterEvidenceAssessment Assess(
        string? candidateId,
        int? modelYear,
        bool yearIndependentlyConfirmed,
        VolvoP2CemSupportAssessment? cemIdentity,
        string? claimedHardwareRevision,
        string? claimedSoftwareRevision,
        IEnumerable<VolvoP2CemParameterResearchRecord?>? records)
    {
        var candidate = VolvoP2ConfigOptionCatalog.Assess(
            candidateId, modelYear, yearIndependentlyConfirmed);
        if (candidate.Kind == VolvoP2ConfigCandidateAssessmentKind.NotListed)
            return Assessment(VolvoP2ParameterEvidenceKind.UnknownCandidate, 0, [],
                "Unknown candidate identifier. No CEM parameter is identified.");

        if (candidate.Kind != VolvoP2ConfigCandidateAssessmentKind.VendorAdvertisedOnly)
            return Assessment(VolvoP2ParameterEvidenceKind.UnsupportedResearchYear, 0, [],
                "The independently confirmed model year is not inside this advertising scope.");

        if (records is null)
            return Assessment(VolvoP2ParameterEvidenceKind.MissingEvidence, 0, [],
                "No independent candidate/revision research catalog was supplied.");

        // At most one more than the bound is enumerated, even for infinite input.
        var bounded = records.Take(MaximumResearchRecords + 1).ToArray();
        if (bounded.Length == 0)
            return Assessment(VolvoP2ParameterEvidenceKind.MissingEvidence, 0, [],
                "No real XC90 P2/CEM parameter mapping is in the production catalog.");

        var problems = CheckEntireCatalog(bounded);
        if (problems.Length != 0)
            return Assessment(VolvoP2ParameterEvidenceKind.InvalidEvidenceCatalog, 0,
                problems, "Candidate research catalog contains inconsistent assertions.");

        if (cemIdentity is not {
                Kind: VolvoP2CemSupportKind.VerifiedIdentityReadOnly
                  or VolvoP2CemSupportKind.DocumentedReadOnlyDidSet,
                VariantId: { Length: > 0 }
            } ||
            !SafeRevision(claimedHardwareRevision) ||
            !SafeRevision(claimedSoftwareRevision))
            return Assessment(VolvoP2ParameterEvidenceKind.NoAuthenticatedRevision, 0, [],
                "No independently authenticated CEM hardware/software revision is available.");

        // This exact-string join is a metadata comparison, not evidence
        // authentication. Even a matching *synthetic* fingerprint remains
        // prohibited from enabling reading, editing or authorization.
        var matches = bounded.Where(record =>
            string.Equals(record!.CandidateId, candidateId, StringComparison.Ordinal) &&
            string.Equals(record.VariantId, cemIdentity.VariantId, StringComparison.Ordinal) &&
            string.Equals(record.CemHardwareRevision, claimedHardwareRevision, StringComparison.Ordinal) &&
            string.Equals(record.CemSoftwareRevision, claimedSoftwareRevision, StringComparison.Ordinal) &&
            modelYear >= record.ModelYearFrom && modelYear <= record.ModelYearThrough).ToArray();

        if (matches.Length == 0)
            return Assessment(VolvoP2ParameterEvidenceKind.NoMatchingResearchRecord, 0, [],
                "No research metadata matches the submitted candidate, year and revision claims.");

        // Validation also prohibits overlapping revision scopes, so there is
        // no permitted first-match selection on ambiguity.
        if (matches.Length != 1)
            return Assessment(VolvoP2ParameterEvidenceKind.InvalidEvidenceCatalog, 0,
                [VolvoP2ParameterEvidenceProblem.OverlappingRevisionClaim],
                "Ambiguous candidate/revision records must not be selected.");

        return Assessment(
            matches[0]!.ReviewNotation == VolvoP2ParameterReviewNotation.HumanReviewedClaim
                ? VolvoP2ParameterEvidenceKind.ReviewedClaimResearchOnly
                : VolvoP2ParameterEvidenceKind.ProposedResearchOnly,
            1, [], "A claimed revision-specific research record matches. " +
                   "Its sources, CEM identity and parameter meaning are NOT authenticated. " +
                   "No vehicle request, PIN access or configuration edit is enabled.");
    }

    private static VolvoP2ParameterEvidenceProblem[] CheckEntireCatalog(
        IReadOnlyList<VolvoP2CemParameterResearchRecord?> records)
    {
        if (records.Count > MaximumResearchRecords)
            return [VolvoP2ParameterEvidenceProblem.ExcessiveRecords];

        var known = VolvoP2ConfigOptionCatalog.Entries
            .Select(entry => entry.CanonicalId).ToHashSet(StringComparer.Ordinal);
        var errors = new HashSet<VolvoP2ParameterEvidenceProblem>();
        var valid = new List<VolvoP2CemParameterResearchRecord>();

        foreach (var record in records)
        {
            if (record is null)
            {
                errors.Add(VolvoP2ParameterEvidenceProblem.NullRecord);
                continue;
            }

            if (record.CandidateId is null || !known.Contains(record.CandidateId))
                errors.Add(VolvoP2ParameterEvidenceProblem.UnknownCandidateId);

            if (record.ModelYearFrom < VolvoP2ConfigOptionCatalog.VendorAdvertisedEarliestP2ModelYear ||
                record.ModelYearThrough > VolvoP2ConfigOptionCatalog.LastFirstGenerationModelYear ||
                record.ModelYearThrough < record.ModelYearFrom)
                errors.Add(VolvoP2ParameterEvidenceProblem.InvalidXc90ModelYearRange);

            if (!SafeRevision(record.VariantId) ||
                !SafeRevision(record.CemHardwareRevision) ||
                !SafeRevision(record.CemSoftwareRevision))
                errors.Add(VolvoP2ParameterEvidenceProblem.MissingVariantOrRevision);

            if (!SafeReference(record.EvidenceReference))
                errors.Add(VolvoP2ParameterEvidenceProblem.InvalidSourceReference);

            if (record.ReviewNotation == VolvoP2ParameterReviewNotation.HumanReviewedClaim &&
                !SafeReference(record.ValueMeaningEvidenceReference))
                errors.Add(VolvoP2ParameterEvidenceProblem.InvalidMeaningReference);

            if (!Enum.IsDefined(record.ReviewNotation))
                errors.Add(VolvoP2ParameterEvidenceProblem.InvalidReviewNotation);

            valid.Add(record);
        }

        foreach (var group in valid.GroupBy(record =>
                     (record.CandidateId, record.VariantId,
                      record.CemHardwareRevision, record.CemSoftwareRevision)))
        {
            var ordered = group.OrderBy(entry => entry.ModelYearFrom)
                .ThenBy(entry => entry.ModelYearThrough).ToArray();
            for (var i = 1; i < ordered.Length; i++)
                if (ordered[i].ModelYearFrom <= ordered[i - 1].ModelYearThrough)
                    errors.Add(VolvoP2ParameterEvidenceProblem.OverlappingRevisionClaim);
        }

        return errors.OrderBy(error => error).ToArray();
    }

    private static bool SafeRevision(string? value) =>
        value is { Length: >= 2 and <= 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'Z'
            or >= 'a' and <= 'z' or '_' or '-' or '.');

    private static bool SafeReference(string? value) =>
        value is { Length: >= 12 and <= MaximumEvidenceReferenceLength } &&
        !string.IsNullOrWhiteSpace(value) &&
        !value.Any(char.IsControl);

    private static VolvoP2ParameterEvidenceAssessment Assessment(
        VolvoP2ParameterEvidenceKind kind, int count,
        VolvoP2ParameterEvidenceProblem[] problems, string explanation) =>
        new(kind, count, Array.AsReadOnly(problems), explanation);
}
