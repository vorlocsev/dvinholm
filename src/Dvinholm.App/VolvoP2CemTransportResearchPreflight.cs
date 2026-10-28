using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Combines existing *offline* manufacturer communication-tool guidance
/// and year-scoped Volvo HS-CAN fault-tracing advisory. This is not
/// a physical adapter probe, an ECU addressing map, or a command planner.
/// </summary>
internal enum VolvoP2CemTransportResearchKind
{
    ModelYearUnconfirmed,
    OutsideFirstGenerationXc90,
    NetworkUnconfirmed,
    NetworkNotHighSpeedCan,
    BaudRateUnspecified,
    ConflictsWithPublishedHsCanGuidance,
    HistoricalHsCanRateMatchesOnly
}

internal sealed record VolvoP2CemTransportResearchAssessment(
    VolvoP2CemTransportResearchKind Kind,
    uint? HistoricalHsCanRate,
    string? ReportedVidaLogicalNode,
    bool J2534OneUnsupportedForVidaDiagnostics,
    string Explanation)
{
    internal bool HasAuthenticatedCemIdentity => false;
    internal bool HasVerifiedPhysicalTransport => false;
    internal bool MayAutoSelectAdapterSpeed => false;
    internal bool MayOpenCemDiagnosticSession => false;
    internal bool MaySendJ2534OrUds => false;
    internal bool MayReadConfiguration => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteConfiguration => false;
}

/// <summary>
/// Reuses two existing source-limited P2 research checks rather than
/// promoting HS-CAN bit rate or VIDA logical node labels to real IDs.
/// Especially IMPORTANT: Volvo states J2534-1 is NOT a VIDA diagnostics
/// interface. A J2534-2 or DiCE selection is not independent proof of
/// CEM support or a usable security/configuration service.
/// </summary>
internal static class VolvoP2CemTransportResearchPreflight
{
    internal static VolvoP2CemTransportResearchAssessment Assess(
        int? modelYear, bool userReportsYearIndependentlyChecked,
        VolvoP2NetworkKind network, uint? proposedBaudRate,
        VolvoP2CemDiagnosticInterface selectedInterface)
    {
        var speed = VolvoP2CemHsCanSpeedAdvisory.Evaluate(
            modelYear, userReportsYearIndependentlyChecked,
            network, proposedBaudRate);
        var node = VolvoP2CemVidaNodeAdvisory.Assess(
            modelYear, userReportsYearIndependentlyChecked,
            selectedInterface);
        var kind = speed.Kind switch
        {
            VolvoP2HsCanSpeedAssessmentKind.VehicleYearUnconfirmed =>
                VolvoP2CemTransportResearchKind.ModelYearUnconfirmed,
            VolvoP2HsCanSpeedAssessmentKind.OutsideDocumentedXc90Years =>
                VolvoP2CemTransportResearchKind.OutsideFirstGenerationXc90,
            VolvoP2HsCanSpeedAssessmentKind.NetworkUnconfirmed =>
                VolvoP2CemTransportResearchKind.NetworkUnconfirmed,
            VolvoP2HsCanSpeedAssessmentKind.DifferentNetworkNotCovered =>
                VolvoP2CemTransportResearchKind.NetworkNotHighSpeedCan,
            VolvoP2HsCanSpeedAssessmentKind.BaudRateNotSelected =>
                VolvoP2CemTransportResearchKind.BaudRateUnspecified,
            VolvoP2HsCanSpeedAssessmentKind.MismatchedDocumentedHsCanSpeed =>
                VolvoP2CemTransportResearchKind.ConflictsWithPublishedHsCanGuidance,
            _ => VolvoP2CemTransportResearchKind.HistoricalHsCanRateMatchesOnly
        };
        return new(kind, speed.ReferenceHsCanBaudRate,
            node.ReportedWorkshopNode,
            node.SelectedInterfaceCannotDoVidaDiagnostics,
            speed.Explanation + " " + node.Explanation);
    }

    internal static string BuildReadOnlyReport(
        int? modelYear, bool userReportsYearIndependentlyChecked,
        VolvoP2NetworkKind network, uint? proposedBaudRate,
        VolvoP2CemDiagnosticInterface selectedInterface)
    {
        var assessment = Assess(modelYear,
            userReportsYearIndependentlyChecked,
            network, proposedBaudRate, selectedInterface);
        var result = new StringBuilder()
            .AppendLine("XC90 P2 CEM LEGACY TRANSPORT PRE-FLIGHT (OFFLINE)")
            .AppendLine("The model year and adapter are USER CLAIMS, not a verified vehicle.")
            .AppendLine("No hardware is connected or queried; no CAN configuration changes.")
            .Append("Result: ").AppendLine(assessment.Kind.ToString())
            .AppendLine(assessment.Explanation);

        if (assessment.HistoricalHsCanRate is uint rate)
            result.Append("Published model-year HS-CAN reference ONLY: ")
                .Append(rate / 1000).AppendLine(" kbit/s (NOT an auto-selected setting).");
        else
            result.AppendLine("Reference CAN speed: UNKNOWN or outside HS-CAN evidence.");

        if (assessment.ReportedVidaLogicalNode is string node)
            result.Append("Reported VIDA logical CEM node ONLY: ")
                .AppendLine(node + " (NOT a CAN ID or diagnostic address).");

        result.AppendLine(assessment.J2534OneUnsupportedForVidaDiagnostics
                ? "STOP: Volvo states J2534-1 cannot be used for VIDA diagnostics."
                : "Adapter selection does not establish working XC90 CEM diagnostics.")
            .AppendLine("HS-CAN guidance does NOT establish CEM diagnostic CAN IDs or a protocol.")
            .AppendLine("MS/LS-CAN and K-line speed/diagnostic paths are NOT established here.")
            .AppendLine("The published warning notes that wrong model-year HS-CAN speed can")
            .AppendLine("block communication and cause control-module diagnostic trouble codes.")
            .AppendLine("Do NOT auto-connect, scan or transmit based on this advisory.")
            .AppendLine("CEM configuration read/write, security/PIN and backup/recovery: DISABLED.")
            .AppendLine("This preview changes no ECU, adapter setting, saved profile or file.");

        return result.ToString();
    }
}
