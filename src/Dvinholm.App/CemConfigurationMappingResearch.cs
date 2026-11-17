namespace Dvinholm.App;

/// <summary>
/// Research-only claim: an untrusted source associates a one-based ordinal
/// with a textual meaning for one exact, claimed hardware/software pair.
/// This is NOT a verified Volvo catalog entry, diagnostic address or command.
/// </summary>
internal sealed record CemConfigurationMappingResearchClaim(
    CemConfigurationIndexedFormat Format,
    int Index1Based,
    string HardwarePartNumber,
    string SoftwareRevision,
    string ClaimedMeaning,
    string EvidenceReference);

internal enum CemConfigurationMappingResearchKind
{
    MissingEvidence,
    InvalidEvidence,
    InvalidSnapshotOrIndex,
    MissingClaimedRevision,
    NoExactResearchMatch,
    UnverifiedResearchMatch
}

internal enum CemConfigurationMappingResearchProblem
{
    TooManyRecords,
    NullRecord,
    InvalidFormat,
    IndexOutsideFormat,
    InvalidHardwareOrSoftwareRevision,
    InvalidMeaning,
    InvalidEvidenceReference,
    RepeatedOrConflictingScope
}

internal sealed record CemConfigurationMappingResearchAssessment(
    CemConfigurationMappingResearchKind Kind,
    string? UnverifiedRawValueHex,
    int MatchingResearchRecords,
    IReadOnlyList<CemConfigurationMappingResearchProblem> Problems)
{
    // Neither a provided citation nor an exact textual match authenticates
    // a physical ECU, a Volvo option, a supported service or service rights.
    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool HasVerifiedParameterMapping => false;
    internal bool MayReadLiveConfiguration => false;
    internal bool MayPrepareOrWriteConfiguration => false;
    internal bool MayAcquirePin => false;
}

/// <summary>
/// Strict offline intake gate for unverified P2/P3 index research.
/// A complete catalog is checked before exact metadata matching.
/// No production mapping is created from this input.
/// </summary>
internal static class CemConfigurationMappingResearch
{
    internal const int MaximumRecords = 64;

    internal static IReadOnlyList<CemConfigurationMappingResearchClaim> ProductionMappings { get; } =
        Array.Empty<CemConfigurationMappingResearchClaim>();

    internal static CemConfigurationMappingResearchAssessment Assess(
        CemConfigurationIndexedSnapshot snapshot,
        int index1Based,
        string? claimedHardwarePartNumber,
        string? claimedSoftwareRevision,
        IEnumerable<CemConfigurationMappingResearchClaim?>? proposedClaims)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var count = ExpectedCount(snapshot.Format);
        if (count == 0 || index1Based < 1 || index1Based > count ||
            snapshot.Values is null || snapshot.Values.Count != count ||
            snapshot.Values.Where((value, index) => value.Index1Based != index + 1).Any() ||
            snapshot.BodySha256 is not { Length: 64 } ||
            snapshot.BodySha256.Any(c => c is not (>= '0' and <= '9' or >= 'A' and <= 'F')))
            return Result(CemConfigurationMappingResearchKind.InvalidSnapshotOrIndex);

        if (proposedClaims is null)
            return Result(CemConfigurationMappingResearchKind.MissingEvidence);

        // One-pass bounded enumeration: never drain an untrusted unbounded iterator.
        var bounded = proposedClaims.Take(MaximumRecords + 1).ToArray();
        if (bounded.Length == 0)
            return Result(CemConfigurationMappingResearchKind.MissingEvidence);

        var problems = Validate(bounded);
        if (problems.Count != 0)
            return new(CemConfigurationMappingResearchKind.InvalidEvidence,
                null, 0, problems);

        if (!ValidPartNumber(claimedHardwarePartNumber) ||
            !ValidSoftwareRevision(claimedSoftwareRevision))
            return Result(CemConfigurationMappingResearchKind.MissingClaimedRevision);

        // An exact string join means the user-provided claims are internally
        // consistent. It is never evidence that the hardware or meaning is real.
        var matches = bounded.Where(claim =>
            claim!.Format == snapshot.Format &&
            claim.Index1Based == index1Based &&
            string.Equals(claim.HardwarePartNumber,
                claimedHardwarePartNumber, StringComparison.Ordinal) &&
            string.Equals(claim.SoftwareRevision,
                claimedSoftwareRevision, StringComparison.Ordinal)).ToArray();

        if (matches.Length == 0)
            return Result(CemConfigurationMappingResearchKind.NoExactResearchMatch);

        // Validate() rejects duplicate scopes; never silently choose first.
        if (matches.Length != 1)
            return new(CemConfigurationMappingResearchKind.InvalidEvidence, null, 0,
                Array.AsReadOnly(new[]
                {
                    CemConfigurationMappingResearchProblem.RepeatedOrConflictingScope
                }));

        return new(CemConfigurationMappingResearchKind.UnverifiedResearchMatch,
            snapshot.Values[index1Based - 1].ValueHex,
            1, Array.AsReadOnly(Array.Empty<CemConfigurationMappingResearchProblem>()));
    }

    // Shared by strict local JSON import; no validation rule is duplicated.
    internal static IReadOnlyList<CemConfigurationMappingResearchProblem> Validate(
        IReadOnlyList<CemConfigurationMappingResearchClaim?> claims)
    {
        var problems = new HashSet<CemConfigurationMappingResearchProblem>();
        if (claims.Count > MaximumRecords)
            problems.Add(CemConfigurationMappingResearchProblem.TooManyRecords);

        var scopes = new HashSet<(CemConfigurationIndexedFormat, int, string, string)>();
        foreach (var claim in claims)
        {
            if (claim is null)
            {
                problems.Add(CemConfigurationMappingResearchProblem.NullRecord);
                continue;
            }

            if (ExpectedCount(claim.Format) == 0)
                problems.Add(CemConfigurationMappingResearchProblem.InvalidFormat);
            else if (claim.Index1Based < 1 || claim.Index1Based > ExpectedCount(claim.Format))
                problems.Add(CemConfigurationMappingResearchProblem.IndexOutsideFormat);

            if (!ValidPartNumber(claim.HardwarePartNumber) ||
                !ValidSoftwareRevision(claim.SoftwareRevision))
                problems.Add(CemConfigurationMappingResearchProblem.InvalidHardwareOrSoftwareRevision);

            if (claim.ClaimedMeaning is not { Length: >= 3 and <= 100 } ||
                claim.ClaimedMeaning.Any(c => char.IsControl(c) || c is '<' or '>'))
                problems.Add(CemConfigurationMappingResearchProblem.InvalidMeaning);

            if (claim.EvidenceReference is not { Length: >= 12 and <= 256 } ||
                claim.EvidenceReference.Any(char.IsControl))
                problems.Add(CemConfigurationMappingResearchProblem.InvalidEvidenceReference);

            var key = (claim.Format, claim.Index1Based,
                claim.HardwarePartNumber, claim.SoftwareRevision);
            if (!scopes.Add(key))
                problems.Add(CemConfigurationMappingResearchProblem.RepeatedOrConflictingScope);
        }

        return Array.AsReadOnly(problems.OrderBy(problem => problem).ToArray());
    }

    private static int ExpectedCount(CemConfigurationIndexedFormat format) => format switch
    {
        CemConfigurationIndexedFormat.P2B9Fc => CemConfigurationBlockDecoder.P2ValueCount,
        CemConfigurationIndexedFormat.P3F106 => CemConfigurationBlockDecoder.P3ValueCount,
        _ => 0
    };

    // Shared with offline revision triage; remains research-only.
    internal static bool ValidPartNumber(string? value) =>
        value is { Length: >= 6 and <= 20 } &&
        value.All(c => c is >= '0' and <= '9');

    internal static bool ValidSoftwareRevision(string? value) =>
        value is { Length: >= 2 and <= 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'Z' or
            >= 'a' and <= 'z' or '_' or '-' or '.');

    private static CemConfigurationMappingResearchAssessment Result(
        CemConfigurationMappingResearchKind kind) =>
        new(kind, null, 0,
            Array.AsReadOnly(Array.Empty<CemConfigurationMappingResearchProblem>()));
}
