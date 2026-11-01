using System.Text;

namespace Dvinholm.App;

/// <summary>
/// The six independent evidence gaps between a static P2/CEM option label
/// and even a documented read-only configuration mapping. This research
/// checklist cannot grant live or offline write capabilities.
/// </summary>
internal enum VolvoP2CemOptionEvidenceGap
{
    IndependentApplicability,
    AuthenticatedCemRevision,
    PhysicalOwnerAndEquipment,
    DocumentedReadOnlyMapping,
    AllowedValuesAndDependencies,
    BackupRecoveryAndBenchAcceptance
}

internal sealed record VolvoP2CemOptionEvidenceRequirement(
    VolvoP2CemOptionEvidenceGap Gap, string MissingEvidence);

internal sealed record VolvoP2CemOptionEvidenceAssessment(
    VolvoP2CemOptionProvenance SourceTier,
    IReadOnlyList<VolvoP2CemOptionEvidenceRequirement> BlockingRequirements,
    string FirstReviewAction,
    bool AdditionalSafetyOrEquipmentReviewRequired)
{
    // This is a description of missing proof, not a capability assessment.
    // No user-entered text or synthetic record may unlock an ECU operation.
    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool HasVerifiedConfigurationRead => false;
    internal bool MayReadCemConfiguration => false;
    internal bool MayAcquirePin => false;
    internal bool MayPreviewOrWriteCemConfiguration => false;
    internal bool MayTransmitToVehicle => false;
}

/// <summary>
/// Source-separated, fully offline evidence checklist over only the immutable
/// built-in CEM research manifest. No user-supplied "verified" bits, identifiers,
/// ECU response bytes, CAN addressing or service procedures are consulted.
/// </summary>
internal static class VolvoP2CemOptionEvidenceChecklist
{
    internal static VolvoP2CemOptionEvidenceAssessment? Assess(string? canonicalId)
    {
        var option = VolvoP2CemConfigOptionManifest.Find(canonicalId);
        if (option is null) return null;

        var isAdvertisement = option.Provenance ==
            VolvoP2CemOptionProvenance.ThirdPartyAdvertisement;
        var first = isAdvertisement
            ? "Independent model- and CEM-revision-specific applicability of the vendor-advertised category."
            : "Independent physical CEM revision applicability of the published service-menu heading (not a parameter mapping).";

        return new VolvoP2CemOptionEvidenceAssessment(
            option.Provenance,
            Array.AsReadOnly(new[]
            {
                new VolvoP2CemOptionEvidenceRequirement(
                    VolvoP2CemOptionEvidenceGap.IndependentApplicability, first),
                new VolvoP2CemOptionEvidenceRequirement(
                    VolvoP2CemOptionEvidenceGap.AuthenticatedCemRevision,
                    "Authenticated physical XC90/CEM hardware, application and diagnostic software revisions."),
                new VolvoP2CemOptionEvidenceRequirement(
                    VolvoP2CemOptionEvidenceGap.PhysicalOwnerAndEquipment,
                    "Physical option-owner module and installed equipment confirmed for this vehicle."),
                new VolvoP2CemOptionEvidenceRequirement(
                    VolvoP2CemOptionEvidenceGap.DocumentedReadOnlyMapping,
                    "Independently documented revision-scoped read-only transport, mapping, lengths and error behavior."),
                new VolvoP2CemOptionEvidenceRequirement(
                    VolvoP2CemOptionEvidenceGap.AllowedValuesAndDependencies,
                    "Verified value interpretation, permitted range and dependencies (empty catalogs mean unknown)."),
                new VolvoP2CemOptionEvidenceRequirement(
                    VolvoP2CemOptionEvidenceGap.BackupRecoveryAndBenchAcceptance,
                    "Verified backup/recovery procedure and authorized physical bench acceptance.")
            }),
            isAdvertisement
                ? "Corroborate the advertised option against independent XC90/CEM revision evidence."
                : "Corroborate the Volvo menu heading against an identified physical CEM revision.",
            option.AdditionalSafetyOrEquipmentReviewRequired);
    }

    internal static string BuildReadOnlyReport(string? canonicalId)
    {
        var review = Assess(canonicalId);
        if (review is null)
            return "Unknown CEM research label: no evidence assessment or vehicle capability.";

        var text = new StringBuilder()
            .AppendLine("OPTION-SPECIFIC EVIDENCE CHECKLIST (OFFLINE ONLY)")
            .Append("Source tier: ").AppendLine(review.SourceTier.ToString())
            .AppendLine("Six independent requirements remain OPEN:");
        foreach (var item in review.BlockingRequirements)
            text.Append("  [MISSING] ").AppendLine(item.MissingEvidence);
        text.Append("Next evidence to collect: ").AppendLine(review.FirstReviewAction);
        if (review.AdditionalSafetyOrEquipmentReviewRequired)
            text.AppendLine("Additional safety, legal or conditional-equipment review is required.");
        return text.AppendLine("The source label is NOT a read/write map, verified ECU identity or PIN route.")
            .AppendLine("No real configuration read, PIN access, transmission or write is permitted.")
            .ToString();
    }
}
