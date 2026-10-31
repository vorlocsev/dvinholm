namespace Dvinholm.App;

/// <summary>
/// A single next evidence-gathering checkpoint for XC90 P2/CEM research.
/// This is an offline worksheet prompt, not a live readiness decision.
/// </summary>
internal enum VolvoP2CemNextEvidenceKind
{
    VerifyModelYear,
    OutsideFirstGenerationScope,
    ResolveUnsupportedVidaInterface,
    IdentifyPhysicalNetwork,
    DocumentOtherNetwork,
    SpecifyHistoricalHsCanRate,
    ResolveHsCanRateConflict,
    QualifyDiagnosticInterface,
    CorroborateCemRevision
}

internal sealed record VolvoP2CemNextEvidenceCheckpoint(
    VolvoP2CemNextEvidenceKind Kind, string Action)
{
    internal bool CanOpenCemSession => false;
    internal bool CanReadConfiguration => false;
    internal bool CanAcquirePin => false;
    internal bool CanWriteConfiguration => false;
}

/// <summary>
/// Ranks the EXISTING compatibility matrix's unverified evidence gaps.
/// All output is fixed text with no user-supplied identifiers, CAN commands,
/// firmware assumptions or platform-specific PIN procedures.
/// </summary>
internal static class VolvoP2CemNextEvidencePlanner
{
    internal static VolvoP2CemNextEvidenceCheckpoint Plan(
        int? year, bool independentlyCheckedYear, VolvoP2NetworkKind network,
        uint? proposedBaudRate, VolvoP2CemDiagnosticInterface adapter)
    {
        var review = VolvoP2CemReadinessMatrix.Assess(year,
            independentlyCheckedYear, network, proposedBaudRate, adapter);
        var yearStatus = review.Rows.Single(row =>
            row.Dimension == VolvoP2CemReadinessDimension.Xc90P2ModelYear).Status;
        var speedStatus = review.Rows.Single(row =>
            row.Dimension == VolvoP2CemReadinessDimension.HighSpeedCanReference).Status;

        if (yearStatus == VolvoP2CemReadinessStatus.Unconfirmed)
            return new(VolvoP2CemNextEvidenceKind.VerifyModelYear,
                "Independently verify the XC90 model year before interpreting network guidance.");
        if (yearStatus == VolvoP2CemReadinessStatus.OutsideFirstGenerationScope)
            return new(VolvoP2CemNextEvidenceKind.OutsideFirstGenerationScope,
                "STOP: this model-year claim falls outside the XC90 P2 reference scope.");

        if (adapter == VolvoP2CemDiagnosticInterface.J2534_1)
            return new(VolvoP2CemNextEvidenceKind.ResolveUnsupportedVidaInterface,
                "STOP: J2534-1 is not a VIDA diagnostics interface. Verify an authorized supported diagnostic tool.");

        if (network == VolvoP2NetworkKind.Unknown)
            return new(VolvoP2CemNextEvidenceKind.IdentifyPhysicalNetwork,
                "Identify the physical network from authoritative service evidence; do not guess a bus or CAN ID.");

        if (network != VolvoP2NetworkKind.HighSpeedCan)
            return new(VolvoP2CemNextEvidenceKind.DocumentOtherNetwork,
                "Collect revision-scoped documentation for this non-HS-CAN network; HS-CAN rates do not apply.");

        if (proposedBaudRate is null or 0)
            return new(VolvoP2CemNextEvidenceKind.SpecifyHistoricalHsCanRate,
                "Review the published model-year HS-CAN rate separately from CEM diagnostic transport.");

        if (speedStatus == VolvoP2CemReadinessStatus.ConflictsWithPublishedReference)
            return new(VolvoP2CemNextEvidenceKind.ResolveHsCanRateConflict,
                "STOP: the proposed HS-CAN rate conflicts with published model-year guidance; check the source.");

        if (adapter != VolvoP2CemDiagnosticInterface.DiCE)
            return new(VolvoP2CemNextEvidenceKind.QualifyDiagnosticInterface,
                "Document authorized physical CEM diagnostic support for this interface; software download claims are insufficient.");

        return new(VolvoP2CemNextEvidenceKind.CorroborateCemRevision,
            "Obtain independently corroborated CEM hardware, application software and diagnostic software revisions.");
    }
}
