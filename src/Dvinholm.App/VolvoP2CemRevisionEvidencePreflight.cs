using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Source-limited checklist explaining which independent evidence is still
/// missing to assess one XC90 P2/CEM variant. It accepts two user-edited
/// artifacts as uncorrelated research only. It CANNOT authenticate or
/// enroll an ECU, plan a live request or grant service access.
/// </summary>
internal enum VolvoP2CemRevisionEvidenceGap
{
    IndependentVehicleYearAndModuleAssociation,
    AuthenticatedHardwareAndSoftwareRevisions,
    VerifiedPhysicalTransportAndAddresses,
    DocumentedReadOnlyIdentityAndConfigurationSemantics,
    LegitimatePinAndServiceAuthorization,
    AuthorizedBenchAndRecoveryAcceptance,
    ProvenSameModuleProvenance
}

internal sealed record VolvoP2CemRevisionEvidenceReview(
    int ClaimedModelYear,
    IReadOnlyList<VolvoP2CemRevisionEvidenceGap> MissingEvidence,
    IReadOnlyList<string> AdvertisedMenusForClaimedYear)
{
    internal bool HasAuthenticatedCemVariant => false;
    internal bool HasConfirmedTransport => false;
    internal bool CanReadLiveConfiguration => false;
    internal bool CanAcquirePin => false;
    internal bool CanWriteConfiguration => false;
}

/// <summary>
/// A public scanner feature list gives a research direction only, even when
/// the model year in a manually entered VIDA card happens to match.
/// </summary>
internal static class VolvoP2CemRevisionEvidencePreflight
{
    internal static VolvoP2CemRevisionEvidenceReview Assess(
        VolvoP2CemIdentityResearchDocument manualCard,
        EcuConfigurationExportDocument historicalSnapshot)
    {
        // Validate *both* inputs with the already tested strict, fail-closed
        // redacted paired-intake inspector. This does not link the files.
        _ = VolvoP2CemPairedResearchIntakeDisplay.Build(
            manualCard, historicalSnapshot);

        var gapList = Enum.GetValues<VolvoP2CemRevisionEvidenceGap>();
        var knownMenus = VolvoP2CemThirdPartyScannerCoverage.Entries
            .Where(x => x.ModelYear == manualCard.ClaimedModelYear)
            .Select(x =>
                (x.Module == VolvoP2CemPublishedModule.Cem ? "CEM: " : "CEM-HS: ") +
                x.PublishedLabel + " (PDF page " + x.SourcePdfPage + ")")
            .ToArray();

        return new VolvoP2CemRevisionEvidenceReview(
            manualCard.ClaimedModelYear,
            Array.AsReadOnly(gapList),
            Array.AsReadOnly(knownMenus));
    }

    internal static string BuildReadOnlyReport(
        VolvoP2CemIdentityResearchDocument manualCard,
        EcuConfigurationExportDocument historicalSnapshot)
    {
        var review = Assess(manualCard, historicalSnapshot);
        var result = new StringBuilder()
            .AppendLine("XC90 P2/CEM REVISION EVIDENCE GAPS (OFFLINE)")
            .AppendLine("ALL 7/7 independent evidence gates remain OPEN.")
            .AppendLine("A self-reported VIDA card and a historic DID snapshot do not prove")
            .AppendLine("a shared vehicle, authenticated ECU, supported transport or PIN.")
            .AppendLine("The following scanner features are advertised for the CLAIMED year,")
            .AppendLine("not independently confirmed for this car or firmware:");

        if (review.AdvertisedMenusForClaimedYear.Count == 0)
            result.AppendLine("  No indexed feature for this exact year (coverage UNKNOWN).");
        else
            foreach (var line in review.AdvertisedMenusForClaimedYear)
                result.Append("  - ").AppendLine(line);

        result.AppendLine()
            .AppendLine("EVIDENCE STILL REQUIRED (none established by these files):");
        foreach (var gap in review.MissingEvidence)
            result.Append("  [MISSING] ").AppendLine(gap switch
            {
                VolvoP2CemRevisionEvidenceGap.IndependentVehicleYearAndModuleAssociation =>
                    "Independently verify XC90 model year and which physical ECU is CEM/CEM-HS.",
                VolvoP2CemRevisionEvidenceGap.AuthenticatedHardwareAndSoftwareRevisions =>
                    "Authenticate exact hardware, application and diagnostic software revisions.",
                VolvoP2CemRevisionEvidenceGap.VerifiedPhysicalTransportAndAddresses =>
                    "Source exact diagnostic transport and request/response addressing for this revision.",
                VolvoP2CemRevisionEvidenceGap.DocumentedReadOnlyIdentityAndConfigurationSemantics =>
                    "Document read-only identity/option mappings, lengths and negative responses.",
                VolvoP2CemRevisionEvidenceGap.LegitimatePinAndServiceAuthorization =>
                    "Independently verify authorized PIN/service entitlement and prerequisites.",
                VolvoP2CemRevisionEvidenceGap.AuthorizedBenchAndRecoveryAcceptance =>
                    "Complete authorized DiCE/J2534 bench, power, interruption and recovery acceptance.",
                VolvoP2CemRevisionEvidenceGap.ProvenSameModuleProvenance =>
                    "Prove both records actually refer to the same CEM and capture context.",
                _ => "Unrecognized evidence gap; operation remains blocked."
            });

        result.AppendLine()
            .AppendLine("Scanner menus are third-party advertising, NOT usable ECU commands.")
            .AppendLine("CEM-HS menu names are NOT confirmed CAN bus IDs or diagnostic addresses.")
            .AppendLine("No raw DID values, VIN, profile metadata, file paths or credentials are shown.")
            .AppendLine("No CEM data was authenticated; no live CAN, PIN or configuration action enabled.")
            .AppendLine("This checklist is not a vehicle compatibility verdict or backup.");
        return result.ToString();
    }
}
