using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Offline XC90 P2 compatibility evidence states. Published workshop facts
/// are reference guidance, never authorization or physical ECU validation.
/// </summary>
internal enum VolvoP2CemReadinessStatus
{
    Unconfirmed,
    PublishedReferenceOnly,
    ConflictsWithPublishedReference,
    NotSupportedForVidaDiagnostics,
    OutsideFirstGenerationScope,
    UnavailableInDvinholm
}

internal enum VolvoP2CemReadinessDimension
{
    Xc90P2ModelYear,
    HighSpeedCanReference,
    VidaCommunicationInterface,
    CemHardwareSoftwareIdentity,
    CemDiagnosticTransportAndAddresses,
    DocumentedConfigurationReadMapping,
    RestorableConfigurationBackup,
    AuthorizedPinAndConfigurationWrites
}

internal sealed record VolvoP2CemReadinessRow(
    VolvoP2CemReadinessDimension Dimension,
    VolvoP2CemReadinessStatus Status,
    string EvidenceNeeded,
    string Source);

internal sealed record VolvoP2CemReadinessAssessment(
    IReadOnlyList<VolvoP2CemReadinessRow> Rows)
{
    // The source register is not an identity, protocol, DID or write allowlist.
    // Even synthetic "verified" checkboxes cannot grant any live operation.
    internal bool CanConnectToCem => false;
    internal bool CanReadConfiguration => false;
    internal bool CanGenerateRecoverableBackup => false;
    internal bool CanAcquireOrAuthorizePin => false;
    internal bool CanWriteConfiguration => false;
}

/// <summary>
/// One compact, reproducible evidence matrix for XC90 P2. Delegates all
/// speed/tool observations to existing source-scoped offline advisory classes;
/// production CEM fingerprint and config DID catalogs remain empty.
/// No user strings, VIN, PIN, CAN payload, adapter paths or writable bytes
/// are accepted or returned. No physical device or ECU is ever accessed.
/// </summary>
internal static class VolvoP2CemReadinessMatrix
{
    internal const string VolvoPlatformReference = "https://volvotechinfo.com/vida";
    internal const string VolvoCommunicationReference =
        "https://www.volvospecialtools.com/index.cfm?event=info.dice";

    internal static VolvoP2CemReadinessAssessment Assess(
        int? modelYear, bool userReportsYearIndependentlyChecked,
        VolvoP2NetworkKind network, uint? proposedBaudRate,
        VolvoP2CemDiagnosticInterface selectedInterface)
    {
        var speed = VolvoP2CemHsCanSpeedAdvisory.Evaluate(
            modelYear, userReportsYearIndependentlyChecked, network, proposedBaudRate);
        var inScope = modelYear is >= 2003 and <= 2014;
        var yearAccepted = inScope && userReportsYearIndependentlyChecked;

        var yearStatus = !userReportsYearIndependentlyChecked || modelYear is null
            ? VolvoP2CemReadinessStatus.Unconfirmed
            : inScope ? VolvoP2CemReadinessStatus.PublishedReferenceOnly
                : VolvoP2CemReadinessStatus.OutsideFirstGenerationScope;

        var speedStatus = !yearAccepted
            ? VolvoP2CemReadinessStatus.Unconfirmed
            : speed.Kind switch
            {
                VolvoP2HsCanSpeedAssessmentKind.MatchesDocumentedHsCanSpeedOnly =>
                    VolvoP2CemReadinessStatus.PublishedReferenceOnly,
                VolvoP2HsCanSpeedAssessmentKind.MismatchedDocumentedHsCanSpeed =>
                    VolvoP2CemReadinessStatus.ConflictsWithPublishedReference,
                _ => VolvoP2CemReadinessStatus.Unconfirmed
            };

        // Volvo's J2534-1 limitation is for VIDA diagnostics. DiCE and
        // J2534-2 are NOT independently validated for any given XC90 CEM.
        var toolStatus = selectedInterface switch
        {
            VolvoP2CemDiagnosticInterface.J2534_1 =>
                VolvoP2CemReadinessStatus.NotSupportedForVidaDiagnostics,
            VolvoP2CemDiagnosticInterface.DiCE or
                VolvoP2CemDiagnosticInterface.J2534_2 =>
                VolvoP2CemReadinessStatus.PublishedReferenceOnly,
            _ => VolvoP2CemReadinessStatus.Unconfirmed
        };

        return new VolvoP2CemReadinessAssessment(Array.AsReadOnly(new[]
        {
            new VolvoP2CemReadinessRow(VolvoP2CemReadinessDimension.Xc90P2ModelYear,
                yearStatus,
                "Confirm exact vehicle model year from an independent vehicle record; the checkbox is self-reported.",
                VolvoPlatformReference),
            new VolvoP2CemReadinessRow(VolvoP2CemReadinessDimension.HighSpeedCanReference,
                speedStatus,
                "Workshop HS-CAN year/rate guidance is not a CEM diagnostic speed or proof of physical bus selection.",
                "Volvo XC90 workshop HS-CAN fault-tracing reference (reproduced; see P2 compatibility register)"),
            new VolvoP2CemReadinessRow(VolvoP2CemReadinessDimension.VidaCommunicationInterface,
                toolStatus,
                "J2534-1 cannot perform VIDA diagnostics. DiCE/J2534-2 guidance does not confirm a working CEM interface.",
                VolvoCommunicationReference),
            new VolvoP2CemReadinessRow(VolvoP2CemReadinessDimension.CemHardwareSoftwareIdentity,
                VolvoP2CemReadinessStatus.Unconfirmed,
                "Require independently corroborated hardware, software and diagnostic software revisions for this CEM.",
                "No authenticated production CEM variant in Dvinholm"),
            new VolvoP2CemReadinessRow(VolvoP2CemReadinessDimension.CemDiagnosticTransportAndAddresses,
                VolvoP2CemReadinessStatus.Unconfirmed,
                "Require authorized physical CEM test of exact bus, J2534 driver and request/response addressing.",
                "No production CEM request/response ID or K-line protocol mapping"),
            new VolvoP2CemReadinessRow(VolvoP2CemReadinessDimension.DocumentedConfigurationReadMapping,
                VolvoP2CemReadinessStatus.UnavailableInDvinholm,
                "Require revision-scoped documented read service, identifiers, value schema and negative-response limits.",
                "Existing generic DID snapshots contain opaque values, NOT confirmed CEM configuration"),
            new VolvoP2CemReadinessRow(VolvoP2CemReadinessDimension.RestorableConfigurationBackup,
                VolvoP2CemReadinessStatus.UnavailableInDvinholm,
                "Require a proven restorable backup and bench-tested recovery procedure for the exact revision.",
                "A saved DID snapshot is NOT a restorable configuration backup"),
            new VolvoP2CemReadinessRow(VolvoP2CemReadinessDimension.AuthorizedPinAndConfigurationWrites,
                VolvoP2CemReadinessStatus.UnavailableInDvinholm,
                "Require separate authorized service entitlement, documented procedure and supervised hardware acceptance.",
                "PIN acquisition, CEM authorization and write services are NOT implemented")
        }));
    }

    internal static string BuildOfflineReport(
        int? modelYear, bool userReportsYearIndependentlyChecked,
        VolvoP2NetworkKind network, uint? proposedBaudRate,
        VolvoP2CemDiagnosticInterface selectedInterface)
    {
        var assessment = Assess(modelYear, userReportsYearIndependentlyChecked,
            network, proposedBaudRate, selectedInterface);
        var result = new StringBuilder()
            .AppendLine("XC90 P2/CEM COMPATIBILITY EVIDENCE MATRIX (OFFLINE)")
            .AppendLine("Published references are not verified vehicle observations.")
            .AppendLine("Required evidence per field (no CAN requests or application settings changed):");

        foreach (var row in assessment.Rows)
            result.Append("• ").Append(row.Dimension).Append(": ")
                .AppendLine(row.Status.ToString())
                .Append("  Needed: ").AppendLine(row.EvidenceNeeded)
                .Append("  Reference: ").AppendLine(row.Source);

        var next = VolvoP2CemNextEvidencePlanner.Plan(modelYear,
            userReportsYearIndependentlyChecked, network, proposedBaudRate, selectedInterface);
        return result
            .Append("Next evidence checkpoint (OFFLINE ONLY): ")
            .AppendLine(next.Kind.ToString())
            .AppendLine(next.Action)
            .AppendLine("This worksheet action does not authenticate a physical CEM or enable a diagnostic service.")
            .AppendLine("BLOCKED: CEM configuration reads, PIN/service access, restorable backups and writes.")
            .AppendLine("The generic profile-based read-only DID path is separate and is not CEM compatibility proof.")
            .ToString();
    }
}
