using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// The result refers to historical, separately user-supplied records only.
/// A sourced synthetic match never authenticates a physical CEM or enables
/// ECU operations. Nothing in this type can become a live read plan.
/// </summary>
internal enum VolvoP2CemHistoricalDidEvidenceKind
{
    MissingProductionEvidence,
    InvalidEvidenceCatalog,
    UnknownOrUnmatchedRevision,
    AmbiguousRevision,
    IdentityOnlyNoDocumentedDids,
    DocumentedPreviouslyObservedDidsResearchOnly
}

internal sealed record VolvoP2CemHistoricalDidEvidenceAssessment(
    VolvoP2CemHistoricalDidEvidenceKind Kind,
    IReadOnlyList<ushort> PreviouslyObservedDocumentedDids)
{
    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool HasProvenSameVehicleOrModule => false;
    internal bool HasVerifiedConfigurationSemantics => false;
    internal bool CanSendJ2534OrUds => false;
    internal bool CanReadLiveConfiguration => false;
    internal bool CanAcquirePin => false;
    internal bool CanWriteConfiguration => false;
}

/// <summary>
/// Compare one inspected historical DID export and a separate manual VIDA
/// research card against a bounded, source-scoped offline identity catalog.
/// Only DIDs ALREADY observed and separately documented for a uniquely
/// matching revision can be listed as research metadata. No future DID
/// request or transport/addressing suggestion is generated.
/// </summary>
internal static class VolvoP2CemHistoricalDidEvidence
{
    internal static VolvoP2CemHistoricalDidEvidenceAssessment Assess(
        VolvoP2CemIdentityResearchDocument manualCard,
        EcuConfigurationExportDocument historicalSnapshot,
        IEnumerable<VolvoP2CemSupportEntry> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        // Reuse the strict, privacy-aware paired-input guards. Both
        // documents remain editable claims with unknown common provenance.
        _ = VolvoP2CemPairedResearchIntakeDisplay.Build(
            manualCard, historicalSnapshot);

        var entries = evidence.Take(
            VolvoP2CemEvidenceCatalogValidator.MaximumEntries + 1).ToArray();

        if (entries.Length == 0)
            return Block(VolvoP2CemHistoricalDidEvidenceKind.MissingProductionEvidence);

        if (VolvoP2CemEvidenceCatalogValidator.Validate(entries).Count != 0)
            return Block(VolvoP2CemHistoricalDidEvidenceKind.InvalidEvidenceCatalog);

        // Historical snapshot CAN spelling, bounds, DID order and values
        // have already been validated by the paired-input guard.
        var observed = new EcuConfigurationReadSnapshot(
            historicalSnapshot.ProfileId,
            historicalSnapshot.ProfileName,
            uint.Parse(historicalSnapshot.RequestCanId, NumberStyles.HexNumber,
                CultureInfo.InvariantCulture),
            uint.Parse(historicalSnapshot.ResponseCanId, NumberStyles.HexNumber,
                CultureInfo.InvariantCulture),
            historicalSnapshot.IsExtended,
            historicalSnapshot.ProfileMarkedVerified,
            historicalSnapshot.CapturedAtUtc,
            historicalSnapshot.Values);

        var support = VolvoP2CemSupportMatrix.Assess(
            manualCard.ClaimedModelYear, observed, entries);

        if (support.Kind == VolvoP2CemSupportKind.Ambiguous)
            return Block(VolvoP2CemHistoricalDidEvidenceKind.AmbiguousRevision);

        if (support.Kind == VolvoP2CemSupportKind.Unknown)
            return Block(VolvoP2CemHistoricalDidEvidenceKind.UnknownOrUnmatchedRevision);

        if (support.Kind != VolvoP2CemSupportKind.DocumentedReadOnlyDidSet ||
            support.ObservedDocumentedDids.Count == 0)
            return Block(VolvoP2CemHistoricalDidEvidenceKind.IdentityOnlyNoDocumentedDids);

        // Copy the immutable list; do not expose any DID values or attributes
        // from the sensitive historical snapshot.
        var dids = support.ObservedDocumentedDids.Order().ToArray();
        return new(
            VolvoP2CemHistoricalDidEvidenceKind.DocumentedPreviouslyObservedDidsResearchOnly,
            Array.AsReadOnly(dids));
    }

    internal static string BuildReadOnlyReport(
        VolvoP2CemIdentityResearchDocument manualCard,
        EcuConfigurationExportDocument historicalSnapshot,
        IEnumerable<VolvoP2CemSupportEntry> evidence)
    {
        var result = Assess(manualCard, historicalSnapshot, evidence);
        return new StringBuilder()
            .AppendLine("P2/CEM REVISION-SCOPED HISTORICAL DID RESEARCH (OFFLINE)")
            .Append("Evidence classification: ").AppendLine(result.Kind.ToString())
            .Append("Documented and ALREADY OBSERVED historical DID count: ")
            .AppendLine(result.PreviouslyObservedDocumentedDids.Count
                .ToString(CultureInfo.InvariantCulture))
            .AppendLine("A user-entered VIDA card, independently editable evidence entries")
            .AppendLine("and a historical JSON checksum cannot authenticate a physical ECU")
            .AppendLine("or prove they belong to the same vehicle or CEM revision.")
            .AppendLine("DID identifiers, part numbers, CAN IDs, profile names and raw values")
            .AppendLine("are intentionally not included in this extra evidence report.")
            .AppendLine("No verified ECU configuration meaning, transport/session or PIN.")
            .AppendLine("No automatic request list, J2534/UDS transmission or export.")
            .AppendLine("Live CEM configuration reads, security and writes: DISABLED.")
            .ToString();
    }

    private static VolvoP2CemHistoricalDidEvidenceAssessment Block(
        VolvoP2CemHistoricalDidEvidenceKind kind) =>
        new(kind, Array.Empty<ushort>());
}
