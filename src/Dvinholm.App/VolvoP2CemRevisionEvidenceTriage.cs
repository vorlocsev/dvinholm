using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Offline classification of TWO unverified part-number claims plus a
/// community hardware P/N mention. Text consistency is not physical proof.
/// </summary>
internal enum VolvoP2CemRevisionEvidenceTriageKind
{
    OutsideClaimedYearScope,
    DifferentHardwareClaims,
    DifferentApplicationSoftwareClaims,
    DifferentDiagnosticSoftwareClaims,
    SameTextWithAmbiguousCommunityCategory,
    SameTextWithSingleCommunityMention,
    SameTextWithNoSelectedCommunityMention
}

internal sealed record VolvoP2CemRevisionEvidenceTriageAssessment(
    VolvoP2CemRevisionEvidenceTriageKind Kind,
    bool HardwareTextMatches,
    bool ApplicationSoftwareTextMatches,
    bool DiagnosticSoftwareTextMatches)
{
    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool HasIndependentSourceVerification => false;
    internal bool HasVerifiedHardwareFirmwareCompatibility => false;
    internal bool HasVerifiedTransport => false;
    internal bool MayReadConfiguration => false;
    internal bool MayRequestPin => false;
    internal bool MayWriteConfiguration => false;
}

/// <summary>
/// Makes revision/version discrepancies visible in the EXISTING two-file
/// research preview. Never infers compatibility from a community P/N alone,
/// or from any textual match between user-authored research files.
/// </summary>
internal static class VolvoP2CemRevisionEvidenceTriage
{
    internal static VolvoP2CemRevisionEvidenceTriageAssessment Assess(
        VolvoP2CemIdentityResearchDocument manualCard,
        VolvoP2CemRevisionSourceClaim sourceClaim)
    {
        // This validates both complete strict schemas and never interprets
        // caller-supplied access flags or invalid part-number strings.
        var match = VolvoP2CemRevisionSourceComparison.Compare(manualCard, sourceClaim);

        if (match.Kind == VolvoP2CemRevisionSourceMatchKind.YearOutsideResearchScope)
            return Result(VolvoP2CemRevisionEvidenceTriageKind.OutsideClaimedYearScope, match);

        // A hardware claim mismatch blocks *all* version identification,
        // even if both numbers happen to share a community source category.
        if (!match.HardwareClaimMatches)
            return Result(VolvoP2CemRevisionEvidenceTriageKind.DifferentHardwareClaims, match);

        // An identical hardware P/N does not imply identical firmware.
        // Prioritize the application/software identity, then diagnostic SW.
        if (!match.SoftwareClaimMatches)
            return Result(VolvoP2CemRevisionEvidenceTriageKind.DifferentApplicationSoftwareClaims, match);
        if (!match.DiagnosticSoftwareClaimMatches)
            return Result(VolvoP2CemRevisionEvidenceTriageKind.DifferentDiagnosticSoftwareClaims, match);

        // Only after all three exact text claims match may the community P/N
        // provide a contextual label. It still cannot authenticate an ECU,
        // select a vehicle protocol/baud rate or enable service permissions.
        var mention = VolvoP2CemCommunityPartNumberResearch.Assess(
            manualCard.ClaimedHardwarePartNumber);
        var classification = mention.Kind switch
        {
            VolvoP2CemCommunityPartNumberKind.ConflictingCommunityCategories =>
                VolvoP2CemRevisionEvidenceTriageKind.SameTextWithAmbiguousCommunityCategory,
            VolvoP2CemCommunityPartNumberKind.ReportedBrickCem or
                VolvoP2CemCommunityPartNumberKind.ReportedLCem or
                VolvoP2CemCommunityPartNumberKind.ReportedHCem =>
                VolvoP2CemRevisionEvidenceTriageKind.SameTextWithSingleCommunityMention,
            _ => VolvoP2CemRevisionEvidenceTriageKind.SameTextWithNoSelectedCommunityMention
        };
        return Result(classification, match);
    }

    internal static string BuildReadOnlyReport(
        VolvoP2CemIdentityResearchDocument manualCard,
        VolvoP2CemRevisionSourceClaim sourceClaim)
    {
        var result = Assess(manualCard, sourceClaim);
        var message = result.Kind switch
        {
            VolvoP2CemRevisionEvidenceTriageKind.OutsideClaimedYearScope =>
                "Claimed model year is outside the source's declared scope. No revision comparison accepted.",
            VolvoP2CemRevisionEvidenceTriageKind.DifferentHardwareClaims =>
                "Hardware part-number claims differ. Cannot infer the same physical CEM or firmware.",
            VolvoP2CemRevisionEvidenceTriageKind.DifferentApplicationSoftwareClaims =>
                "Hardware text matches but application/software part-number claims differ. Revision remains unresolved.",
            VolvoP2CemRevisionEvidenceTriageKind.DifferentDiagnosticSoftwareClaims =>
                "Hardware/application text matches but diagnostic-software claims differ. Revision remains unresolved.",
            VolvoP2CemRevisionEvidenceTriageKind.SameTextWithAmbiguousCommunityCategory =>
                "All three strings agree, but the community hardware number occurs in multiple CEM categories.",
            VolvoP2CemRevisionEvidenceTriageKind.SameTextWithSingleCommunityMention =>
                "All three strings agree and a hardware number has one selected community category mention ONLY.",
            _ =>
                "All three strings agree, but the hardware number has no mention in the selected non-exhaustive community sample."
        };

        return new StringBuilder()
            .AppendLine("CEM HARDWARE / APPLICATION SW / DIAGNOSTIC SW TRIAGE (OFFLINE)")
            .Append("Research outcome: ").AppendLine(result.Kind.ToString())
            .AppendLine(message)
            .Append("Hardware text comparison: ")
            .AppendLine(result.HardwareTextMatches ? "SAME" : "DIFFERENT / WITHHELD")
            .Append("Application software text comparison: ")
            .AppendLine(result.ApplicationSoftwareTextMatches ? "SAME" : "DIFFERENT / WITHHELD")
            .Append("Diagnostic software text comparison: ")
            .AppendLine(result.DiagnosticSoftwareTextMatches ? "SAME" : "DIFFERENT / WITHHELD")
            .AppendLine("Both inputs are user-editable CLAIMS. Community mentions are unverified, incomplete and not an XC90 fitment map.")
            .AppendLine("Missing evidence: physical CEM hardware/firmware, independent source authenticity, exact transport and authorized readback.")
            .AppendLine("No real ECU identification, CAN address, bus speed, PIN authorization, backup or configuration write is established.")
            .AppendLine("No raw part numbers, PIN, VIN, file paths, source labels or raw diagnostic bytes are included here.")
            .AppendLine("No J2534 access, requests or writes; all live CEM capabilities DISABLED.")
            .ToString();
    }

    private static VolvoP2CemRevisionEvidenceTriageAssessment Result(
        VolvoP2CemRevisionEvidenceTriageKind kind,
        VolvoP2CemRevisionSourceMatch match) =>
        new(kind, match.HardwareClaimMatches, match.SoftwareClaimMatches,
            match.DiagnosticSoftwareClaimMatches);
}
