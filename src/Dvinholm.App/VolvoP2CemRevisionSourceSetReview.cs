using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Compares source-SCOPED text claims only. Never authenticates provenance,
/// a real module, CAN addressing, configuration maps or permissions.
/// </summary>
internal enum VolvoP2CemSourceSetKind
{
    NoInScopeClaims,
    OneMatchingUnverifiedClaim,
    MultipleMatchingUnverifiedClaims,
    ConflictingInScopeClaims,
    DuplicateSourceReference
}

internal sealed record VolvoP2CemSourceSetAssessment(
    VolvoP2CemSourceSetKind Kind,
    int TotalClaims,
    int YearScopedClaims,
    int MatchingClaims,
    int ContradictingClaims,
    int OutsideYearScopeClaims)
{
    internal bool HasAuthenticatedCemIdentity => false;
    internal bool HasIndependentSourceAuthentication => false;
    internal bool HasVerifiedCemTransport => false;
    internal bool CanReadConfiguration => false;
    internal bool CanAcquirePin => false;
    internal bool CanWriteConfiguration => false;
}

/// <summary>
/// Fail-closed, bounded research-only review of a manually entered VIDA card
/// against up to 16 separately selected revision-source claim files.
/// Reuses strict existing validators; source label uniqueness is NOT proof
/// the underlying publisher or physical ECU is independent.
/// </summary>
internal static class VolvoP2CemRevisionSourceSetReview
{
    internal const int MaximumSources = 16;

    internal static VolvoP2CemSourceSetAssessment Assess(
        VolvoP2CemIdentityResearchDocument manualCard,
        IEnumerable<VolvoP2CemRevisionSourceClaim> sources)
    {
        ArgumentNullException.ThrowIfNull(manualCard);
        ArgumentNullException.ThrowIfNull(sources);
        // A limited enumerator also rejects untrusted or infinite inputs.
        var claims = sources.Take(MaximumSources + 1).ToArray();
        if (claims.Length is 0 or > MaximumSources)
            throw new InvalidDataException("Select 1..16 CEM revision source claims.");

        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicated = false;
        var scoped = 0;
        var matches = 0;
        var differences = 0;
        foreach (var claim in claims)
        {
            // Compare validates BOTH strict unverified schemas before a result
            // is counted. Do not trust user-provided permission flags.
            var comparison = VolvoP2CemRevisionSourceComparison.Compare(
                manualCard, claim);
            if (!references.Add(claim.SourceReference))
                duplicated = true;
            if (comparison.Kind == VolvoP2CemRevisionSourceMatchKind.YearOutsideResearchScope)
                continue;
            scoped++;
            if (comparison.Kind == VolvoP2CemRevisionSourceMatchKind.SameUnverifiedPartNumberClaims)
                matches++;
            else
                differences++;
        }

        var kind = duplicated
            ? VolvoP2CemSourceSetKind.DuplicateSourceReference
            : scoped == 0
                ? VolvoP2CemSourceSetKind.NoInScopeClaims
                : differences > 0
                    ? VolvoP2CemSourceSetKind.ConflictingInScopeClaims
                    : matches == 1
                        ? VolvoP2CemSourceSetKind.OneMatchingUnverifiedClaim
                        : VolvoP2CemSourceSetKind.MultipleMatchingUnverifiedClaims;

        return new(kind, claims.Length, scoped, matches, differences,
            claims.Length - scoped);
    }

    internal static string BuildReadOnlyReport(
        VolvoP2CemIdentityResearchDocument manualCard,
        IEnumerable<VolvoP2CemRevisionSourceClaim> sources)
    {
        var assessment = Assess(manualCard, sources);
        return new StringBuilder()
            .AppendLine("XC90 P2/CEM MULTI-SOURCE REVISION TRIAGE (OFFLINE)")
            .AppendLine("UNVERIFIED USER-SELECTED RECORDS ONLY; NO SOURCE AUTHENTICATION.")
            .Append("Outcome: ").AppendLine(assessment.Kind.ToString())
            .Append("Files reviewed: ").AppendLine(
                assessment.TotalClaims.ToString(CultureInfo.InvariantCulture))
            .Append("Within claimed model-year range: ").AppendLine(
                assessment.YearScopedClaims.ToString(CultureInfo.InvariantCulture))
            .Append("Same three part-number strings: ").AppendLine(
                assessment.MatchingClaims.ToString(CultureInfo.InvariantCulture))
            .Append("Contradicting in-scope strings: ").AppendLine(
                assessment.ContradictingClaims.ToString(CultureInfo.InvariantCulture))
            .Append("Outside claimed year range: ").AppendLine(
                assessment.OutsideYearScopeClaims.ToString(CultureInfo.InvariantCulture))
            .AppendLine("Repeated source labels are NOT independent corroboration.")
            .AppendLine("Even distinct labels can refer to one original, unverified source.")
            .AppendLine("Identical text does NOT authenticate a physical CEM or prove")
            .AppendLine("diagnostic transport, configuration semantics, PIN rights or recovery.")
            .AppendLine("Part numbers, citations, raw DID bytes, VIN and file paths are omitted.")
            .AppendLine("No file, ECU profile, adapter setting, CAN/J2534 command or PIN changed.")
            .AppendLine("Live CEM configuration read/write and authorization: DISABLED.")
            .ToString();
    }
}
