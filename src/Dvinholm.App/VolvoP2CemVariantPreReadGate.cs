using System.Text;

namespace Dvinholm.App;

/// <summary>
/// An offline decision card for one manually claimed XC90 P2 CEM revision.
/// A matching user-supplied source is evidence of matching TEXT, never
/// a verified physical CEM, protocol, or authorization for any ECU request.
/// </summary>
internal enum VolvoP2CemPreReadRequirement
{
    ModelYearSourceScope,
    HardwareSoftwareClaimConsistency,
    IndependentlyAuthenticatedCemIdentity,
    VerifiedDiagnosticTransportAndAddresses,
    RevisionSpecificConfigurationReadSemantics,
    PhysicalReadOnlyJ2534Acceptance,
    ReviewedCemReadImplementation
}

internal enum VolvoP2CemPreReadFinding
{
    SourceEvidenceMissing,
    UnverifiedClaimOnly,
    ConflictingSourceClaims,
    IndependentlyVerifiedEvidenceMissing,
    NotImplemented
}

internal sealed record VolvoP2CemPreReadCheck(
    VolvoP2CemPreReadRequirement Requirement,
    VolvoP2CemPreReadFinding Finding);

internal sealed record VolvoP2CemVariantPreReadAssessment(
    VolvoP2CemSourceSetKind SourceKind,
    IReadOnlyList<VolvoP2CemPreReadCheck> Checks,
    string NextResearchAction)
{
    // A report cannot authorize the generic DID path or any CEM-specific
    // protocol. Only independently verified procedures and future code can.
    internal bool MaySendDiagnosticRequest => false;
    internal bool MayReadLiveConfiguration => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteConfiguration => false;
}

/// <summary>
/// Condenses existing bounded source and read-capability assessments into
/// seven actionable gates. No new trust input, identifier, address or
/// service definition is introduced by this read-only adapter.
/// </summary>
internal static class VolvoP2CemVariantPreReadGate
{
    internal static VolvoP2CemVariantPreReadAssessment Assess(
        VolvoP2CemIdentityResearchDocument manualCard,
        IEnumerable<VolvoP2CemRevisionSourceClaim> sources)
    {
        ArgumentNullException.ThrowIfNull(manualCard);
        ArgumentNullException.ThrowIfNull(sources);

        // One materialization through the already bounded validator. This
        // rejects empty, oversized, inconsistent and malicious claim lists.
        var claims = sources.Take(VolvoP2CemRevisionSourceSetReview.MaximumSources + 1)
            .ToArray();
        var source = VolvoP2CemRevisionSourceSetReview.Assess(manualCard, claims);
        var preflight = VolvoP2CemConfigurationReadPreflight.Assess(manualCard, claims);
        if (!preflight.BlockingRequirements.Contains(
                VolvoP2ServiceEvidenceGap.OperationalServiceNotImplemented))
            throw new InvalidOperationException("CEM pre-read operation gate is missing.");

        var yearFinding = source.YearScopedClaims == 0
            ? VolvoP2CemPreReadFinding.SourceEvidenceMissing
            : VolvoP2CemPreReadFinding.UnverifiedClaimOnly;
        var revisionFinding = source.Kind switch
        {
            VolvoP2CemSourceSetKind.NoInScopeClaims =>
                VolvoP2CemPreReadFinding.SourceEvidenceMissing,
            VolvoP2CemSourceSetKind.ConflictingInScopeClaims or
                VolvoP2CemSourceSetKind.DuplicateSourceReference =>
                VolvoP2CemPreReadFinding.ConflictingSourceClaims,
            _ => VolvoP2CemPreReadFinding.UnverifiedClaimOnly
        };

        var checks = new[]
        {
            new VolvoP2CemPreReadCheck(
                VolvoP2CemPreReadRequirement.ModelYearSourceScope, yearFinding),
            new VolvoP2CemPreReadCheck(
                VolvoP2CemPreReadRequirement.HardwareSoftwareClaimConsistency,
                revisionFinding),
            new VolvoP2CemPreReadCheck(
                VolvoP2CemPreReadRequirement.IndependentlyAuthenticatedCemIdentity,
                VolvoP2CemPreReadFinding.IndependentlyVerifiedEvidenceMissing),
            new VolvoP2CemPreReadCheck(
                VolvoP2CemPreReadRequirement.VerifiedDiagnosticTransportAndAddresses,
                VolvoP2CemPreReadFinding.IndependentlyVerifiedEvidenceMissing),
            new VolvoP2CemPreReadCheck(
                VolvoP2CemPreReadRequirement.RevisionSpecificConfigurationReadSemantics,
                VolvoP2CemPreReadFinding.IndependentlyVerifiedEvidenceMissing),
            new VolvoP2CemPreReadCheck(
                VolvoP2CemPreReadRequirement.PhysicalReadOnlyJ2534Acceptance,
                VolvoP2CemPreReadFinding.IndependentlyVerifiedEvidenceMissing),
            new VolvoP2CemPreReadCheck(
                VolvoP2CemPreReadRequirement.ReviewedCemReadImplementation,
                VolvoP2CemPreReadFinding.NotImplemented)
        };

        var next = source.Kind switch
        {
            VolvoP2CemSourceSetKind.NoInScopeClaims =>
                "Find a documented source explicitly covering the claimed model year.",
            VolvoP2CemSourceSetKind.ConflictingInScopeClaims =>
                "Resolve contradictory hardware/software revision claims before selecting a variant.",
            VolvoP2CemSourceSetKind.DuplicateSourceReference =>
                "Replace repeated source references with distinct, independently reviewable evidence.",
            _ =>
                "Authenticate exact CEM hardware/software revisions and source a revision-specific read specification."
        };

        return new(source.Kind, Array.AsReadOnly(checks), next);
    }

    internal static string BuildReadOnlyReport(
        VolvoP2CemIdentityResearchDocument manualCard,
        IEnumerable<VolvoP2CemRevisionSourceClaim> sources)
    {
        var review = Assess(manualCard, sources);
        var text = new StringBuilder()
            .AppendLine("XC90 P2/CEM VARIANT PRE-READ CHECK (OFFLINE)")
            .Append("Source consistency: ").AppendLine(review.SourceKind.ToString())
            .AppendLine("This is a research checklist, NOT a vehicle compatibility verdict.");
        foreach (var check in review.Checks)
            text.Append("  [").Append(check.Finding).Append("] ")
                .AppendLine(check.Requirement.ToString());

        return text.Append("Next research step: ").AppendLine(review.NextResearchAction)
            .AppendLine("Part numbers, source labels, VIN, PIN and raw DID bytes are withheld.")
            .AppendLine("No CEM diagnostic request or automatic adapter selection is available.")
            .AppendLine("CEM configuration read, PIN acquisition and writes: BLOCKED.")
            .ToString();
    }
}
