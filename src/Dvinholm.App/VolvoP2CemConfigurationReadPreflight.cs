using System.Text;

namespace Dvinholm.App;

/// <summary>
/// An entirely offline explanation of why research file agreement CANNOT
/// produce a CEM configuration read procedure. No identifier allowlist,
/// CAN addressing, request plan, security step or session access exists.
/// </summary>
internal sealed record VolvoP2CemConfigurationReadPreflightAssessment(
    VolvoP2CemSourceSetKind SourceSetKind,
    IReadOnlyList<VolvoP2ServiceEvidenceGap> BlockingRequirements,
    bool SourceClaimsAreConflicting,
    bool SourceClaimsAreDuplicated)
{
    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool HasVerifiedReadMechanism => false;
    internal bool MayBuildDiagnosticRequest => false;
    internal bool MayReadLiveCemConfiguration => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteConfiguration => false;
}

/// <summary>
/// Reuses the existing bounded, strict source-set review and the established
/// P2 service-capability fail-closed gate. Part number agreement, including
/// multiple separately labelled files, NEVER becomes an authenticated
/// identity, independently documented read mapping or authorized bench test.
/// </summary>
internal static class VolvoP2CemConfigurationReadPreflight
{
    internal static VolvoP2CemConfigurationReadPreflightAssessment Assess(
        VolvoP2CemIdentityResearchDocument manualCard,
        IEnumerable<VolvoP2CemRevisionSourceClaim> sourceClaims)
    {
        ArgumentNullException.ThrowIfNull(manualCard);
        ArgumentNullException.ThrowIfNull(sourceClaims);
        var sourceReview = VolvoP2CemRevisionSourceSetReview.Assess(
            manualCard, sourceClaims);

        // Values in local, user-editable files do not corroborate the claimed
        // model year, hardware revision or configuration read semantics.
        // Null references correctly keep ALL physical evidence gates open.
        var research = new VolvoP2CemServiceResearchContext(
            manualCard.ClaimedModelYear,
            ModelYearIndependentlyConfirmed: false,
            Identity: null,
            ConfigurationMappingEvidenceReference: null,
            LegitimateServiceProcedureEvidenceReference: null,
            CredentialScope: VolvoP2ServiceCredentialScope.Unspecified,
            ConfigurationServiceEntitlementConfirmed: false,
            PhysicalJ2534BenchRecordReference: null,
            BackupRecoveryBenchRecordReference: null);

        var capability = VolvoP2CemServiceCapabilityMatrix.Assess(
            VolvoP2CemServiceOperation.ReadConfiguration, research);
        return new(
            sourceReview.Kind,
            capability.BlockingRequirements,
            sourceReview.Kind == VolvoP2CemSourceSetKind.ConflictingInScopeClaims,
            sourceReview.Kind == VolvoP2CemSourceSetKind.DuplicateSourceReference);
    }

    internal static string BuildReadOnlyReport(
        VolvoP2CemIdentityResearchDocument manualCard,
        IEnumerable<VolvoP2CemRevisionSourceClaim> sourceClaims)
    {
        var result = Assess(manualCard, sourceClaims);
        var report = new StringBuilder()
            .AppendLine("CEM CONFIGURATION READ ELIGIBILITY (OFFLINE RESEARCH ONLY)")
            .Append("Revision source status: ").AppendLine(result.SourceSetKind.ToString());

        if (result.SourceClaimsAreConflicting)
            report.AppendLine("CONFLICT: review contradictory year-scoped revision assertions before further research.");
        if (result.SourceClaimsAreDuplicated)
            report.AppendLine("DUPLICATE: repeated source reference cannot independently corroborate a CEM revision.");

        report.AppendLine("Required proof still MISSING for a CEM-specific configuration read:");
        foreach (var gap in result.BlockingRequirements)
            report.Append("  [MISSING] ").AppendLine(gap switch
            {
                VolvoP2ServiceEvidenceGap.ConfirmedXc90P2ModelYear =>
                    "Independent exact XC90 model-year confirmation (a manual claim is not proof).",
                VolvoP2ServiceEvidenceGap.ExactCemHardwareSoftwareIdentity =>
                    "Authenticated physical CEM hardware/software/diagnostic-software identity.",
                VolvoP2ServiceEvidenceGap.IndependentlyDocumentedConfigurationMapping =>
                    "Revision-scoped published read mechanism, value format, lengths and failure behavior.",
                VolvoP2ServiceEvidenceGap.PhysicalJ2534CemAcceptance =>
                    "Authorized DiCE/J2534 physical CEM transport and read-only bench acceptance.",
                VolvoP2ServiceEvidenceGap.OperationalServiceNotImplemented =>
                    "A reviewed CEM-specific read service is not implemented in Dvinholm.",
                _ => "Other prerequisite or unrecognized restriction; operation blocked."
            });

        return report
            .AppendLine("User-supplied files cannot close these independent verification gates.")
            .AppendLine("No CEM request/response ID, DID list, K-line route or PIN is inferred.")
            .AppendLine("Generic user-profile UDS DID reads are NOT verified CEM configuration.")
            .AppendLine("No request is sent; PIN, configuration reads and writes: DISABLED.")
            .ToString();
    }
}
