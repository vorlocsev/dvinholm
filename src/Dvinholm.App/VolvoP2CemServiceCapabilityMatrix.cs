namespace Dvinholm.App;

/// <summary>
/// Research-only categories. These labels are NOT callable ECU services.
/// Existing generic read-only UDS functionality is separate from this gate.
/// </summary>
internal enum VolvoP2CemServiceOperation
{
    IdentifyCem,
    ReadConfiguration,
    ReviewPinAcquisition,
    AuthenticateConfigurationService,
    PreviewConfigurationChange,
    WriteConfiguration
}

internal enum VolvoP2ServiceCredentialScope
{
    Unspecified,
    ConfigurationService,
    ImmobilizerOrKeyMaterial
}

internal enum VolvoP2ServiceEvidenceGap
{
    ConfirmedXc90P2ModelYear,
    ExactCemHardwareSoftwareIdentity,
    IndependentlyDocumentedConfigurationMapping,
    IndependentlyDocumentedLegitimateServiceRoute,
    ConfigurationServiceCredentialScopeAndEntitlement,
    PhysicalJ2534CemAcceptance,
    TestedBackupAndRecovery,
    OperationalServiceNotImplemented
}

/// <summary>
/// Future-research references and classifications only. The strings must
/// never contain PINs, secrets, VINs, raw configuration bytes or tokens.
/// Claims are not authenticated by this model; only independent evidence
/// review and physical acceptance can validate their authenticity.
/// </summary>
internal sealed record VolvoP2CemServiceResearchContext(
    int? ModelYear,
    bool ModelYearIndependentlyConfirmed,
    VolvoP2CemSupportAssessment? Identity,
    string? ConfigurationMappingEvidenceReference,
    string? LegitimateServiceProcedureEvidenceReference,
    VolvoP2ServiceCredentialScope CredentialScope,
    bool ConfigurationServiceEntitlementConfirmed,
    string? PhysicalJ2534BenchRecordReference,
    string? BackupRecoveryBenchRecordReference);

internal sealed record VolvoP2CemServiceCapabilityAssessment(
    VolvoP2CemServiceOperation Operation,
    IReadOnlyList<VolvoP2ServiceEvidenceGap> BlockingRequirements,
    string Explanation)
{
    // There is no production implementation of P2-specific identification,
    // PIN or CEM configuration services. Research claims NEVER grant access.
    internal bool MaySendEcuRequest => false;
    internal bool MayAcquirePin => false;
    internal bool MayAuthenticateService => false;
    internal bool MayReadOrWriteConfiguration => false;
}

/// <summary>
/// Offline compatibility and entitlement gap analysis for a future P2 CEM
/// service workflow. This sends no J2534/UDS calls and accepts no secrets.
/// Any combination of submitted "evidence" still fails closed because
/// verified physical/protocol support and service code are not implemented.
/// </summary>
internal static class VolvoP2CemServiceCapabilityMatrix
{
    internal static IReadOnlyList<VolvoP2CemServiceOperation> ResearchOperations { get; } =
        Array.AsReadOnly(new[]
        {
            VolvoP2CemServiceOperation.IdentifyCem,
            VolvoP2CemServiceOperation.ReadConfiguration,
            VolvoP2CemServiceOperation.ReviewPinAcquisition,
            VolvoP2CemServiceOperation.AuthenticateConfigurationService,
            VolvoP2CemServiceOperation.PreviewConfigurationChange,
            VolvoP2CemServiceOperation.WriteConfiguration
        });

    internal static VolvoP2CemServiceCapabilityAssessment Assess(
        VolvoP2CemServiceOperation operation,
        VolvoP2CemServiceResearchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Reject undefined operation IDs rather than treating them as a
        // currently implemented/read-only operation.
        if (!ResearchOperations.Contains(operation))
            return Block(operation, [VolvoP2ServiceEvidenceGap.OperationalServiceNotImplemented],
                "Unrecognized P2/CEM service category. No operation is available.");

        var gaps = new List<VolvoP2ServiceEvidenceGap>();
        if (context.ModelYear is null ||
            !context.ModelYearIndependentlyConfirmed ||
            context.ModelYear.Value is < 2003 or > 2014)
            gaps.Add(VolvoP2ServiceEvidenceGap.ConfirmedXc90P2ModelYear);

        // A research matcher can label a synthetic DID fingerprint as
        // "verified" relative to user-supplied rules, but that does NOT
        // authenticate the physical CEM or the capture's provenance.
        // Keep this evidence gap until a separate, independently audited
        // physical identity/transport acceptance path actually exists.
        if (context.Identity is not {
                HasAuthenticatedPhysicalCem: true,
                Kind: VolvoP2CemSupportKind.VerifiedIdentityReadOnly
                    or VolvoP2CemSupportKind.DocumentedReadOnlyDidSet,
                VariantId: { Length: > 0 }
            })
            gaps.Add(VolvoP2ServiceEvidenceGap.ExactCemHardwareSoftwareIdentity);

        if (operation is VolvoP2CemServiceOperation.ReadConfiguration
            or VolvoP2CemServiceOperation.PreviewConfigurationChange
            or VolvoP2CemServiceOperation.WriteConfiguration)
        {
            if (!HasBoundedEvidenceReference(context.ConfigurationMappingEvidenceReference))
                gaps.Add(VolvoP2ServiceEvidenceGap.IndependentlyDocumentedConfigurationMapping);
        }

        if (operation is VolvoP2CemServiceOperation.ReviewPinAcquisition
            or VolvoP2CemServiceOperation.AuthenticateConfigurationService
            or VolvoP2CemServiceOperation.WriteConfiguration)
        {
            if (!HasBoundedEvidenceReference(context.LegitimateServiceProcedureEvidenceReference))
                gaps.Add(VolvoP2ServiceEvidenceGap.IndependentlyDocumentedLegitimateServiceRoute);
            if (context.CredentialScope != VolvoP2ServiceCredentialScope.ConfigurationService ||
                !context.ConfigurationServiceEntitlementConfirmed)
                gaps.Add(VolvoP2ServiceEvidenceGap.ConfigurationServiceCredentialScopeAndEntitlement);
        }

        if (operation is not VolvoP2CemServiceOperation.PreviewConfigurationChange &&
            !HasBoundedEvidenceReference(context.PhysicalJ2534BenchRecordReference))
            gaps.Add(VolvoP2ServiceEvidenceGap.PhysicalJ2534CemAcceptance);

        if (operation == VolvoP2CemServiceOperation.WriteConfiguration &&
            !HasBoundedEvidenceReference(context.BackupRecoveryBenchRecordReference))
            gaps.Add(VolvoP2ServiceEvidenceGap.TestedBackupAndRecovery);

        // Never omit this terminal blockade, even for apparently complete
        // user-supplied evidence. A review model is not an executable service.
        gaps.Add(VolvoP2ServiceEvidenceGap.OperationalServiceNotImplemented);
        return Block(operation, gaps, "RESEARCH ONLY: supplied references and " +
            "read-only identity claims are not independent ECU validation. " +
            "No PIN acquisition, service authentication or CEM configuration operation is enabled.");
    }

    private static VolvoP2CemServiceCapabilityAssessment Block(
        VolvoP2CemServiceOperation operation,
        IEnumerable<VolvoP2ServiceEvidenceGap> reasons,
        string explanation) =>
        new(operation, Array.AsReadOnly(reasons.Distinct().ToArray()), explanation);

    // Opaque references are NOT proof. Never echo them into the assessment,
    // status, telemetry or logs; a future trusted reviewer must inspect them.
    private static bool HasBoundedEvidenceReference(string? value) =>
        value is { Length: >= 8 and <= 256 } &&
        !string.IsNullOrWhiteSpace(value) &&
        value.All(c => !char.IsControl(c));
}
