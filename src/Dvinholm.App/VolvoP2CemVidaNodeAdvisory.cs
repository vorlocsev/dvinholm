using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Source-scoped workshop research only. A VIDA "node address" in a
/// manufacturer technical journal is NOT a J2534 CAN arbitration ID,
/// an ISO-TP transport identifier, a UDS DID or a working CEM session.
/// </summary>
internal enum VolvoP2CemVidaNodeAssessmentKind
{
    ModelYearNotIndependentlyConfirmed,
    OutsideDocumentedXc90P2Scope,
    WorkshopLogicalNodeReferenceOnly
}

internal enum VolvoP2CemDiagnosticInterface
{
    Unspecified,
    DiCE,
    J2534_1,
    J2534_2,
    Other
}

internal sealed record VolvoP2CemVidaNodeAssessment(
    VolvoP2CemVidaNodeAssessmentKind Kind,
    string? ReportedWorkshopNode,
    bool SelectedInterfaceCannotDoVidaDiagnostics,
    string Explanation)
{
    // The source describes VIDA's logical module selection only.
    // Never pass ReportedWorkshopNode to a physical CAN/J2534 API.
    internal uint? VerifiedRequestCanId => null;
    internal uint? VerifiedResponseCanId => null;
    internal ushort? VerifiedDiagnosticDid => null;
    internal bool MayOpenCemSession => false;
    internal bool MayReadCemIdentity => false;
    internal bool MayReadOrWriteConfiguration => false;
    internal bool MayAcquirePin => false;
}

/// <summary>
/// Research advisory for early/late P2 CEM VIDA logical node labels and
/// manufacturer-published vehicle communication tool limitations.
/// The reported early/late node distinction is reproduced by a third-party
/// workshop-manual mirror of Volvo TJ US14902.6.0 (2010) and must be
/// independently checked for a specific CEM before any on-car work.
/// </summary>
internal static class VolvoP2CemVidaNodeAdvisory
{
    internal const int FirstDocumentedXc90ModelYear = 2003;
    internal const int LastFirstGenerationXc90ModelYear = 2014;
    internal const string ReportedEarlyP2WorkshopNode = "40";
    internal const string ReportedLaterP2WorkshopNode = "50";
    internal const string WorkshopBulletin = "Volvo TJ US14902.6.0 (2010), third-party workshop reproduction";
    internal const string OfficialToolGuidance = "Volvo Workshop System Requirements 320EN20, Vehicle Communication Tools";

    internal static VolvoP2CemVidaNodeAssessment Assess(
        int? independentlyConfirmedModelYear,
        bool modelYearIndependentlyConfirmed,
        VolvoP2CemDiagnosticInterface selectedInterface)
    {
        if (independentlyConfirmedModelYear is null ||
            !modelYearIndependentlyConfirmed)
            return new(VolvoP2CemVidaNodeAssessmentKind.ModelYearNotIndependentlyConfirmed,
                null, selectedInterface == VolvoP2CemDiagnosticInterface.J2534_1,
                "Confirm the actual XC90 model year independently. A typed year " +
                "or a VIDA research note is not vehicle authentication.");

        var year = independentlyConfirmedModelYear.Value;
        if (year is < FirstDocumentedXc90ModelYear or > LastFirstGenerationXc90ModelYear)
            return new(VolvoP2CemVidaNodeAssessmentKind.OutsideDocumentedXc90P2Scope,
                null, selectedInterface == VolvoP2CemDiagnosticInterface.J2534_1,
                "Outside the documented first-generation XC90 P2 CEM node research scope.");

        var label = year <= 2004
            ? ReportedEarlyP2WorkshopNode : ReportedLaterP2WorkshopNode;
        var interfaceText = selectedInterface switch
        {
            VolvoP2CemDiagnosticInterface.DiCE =>
                "Volvo recommends DiCE for D2/GGD vehicle communication; " +
                "adapter setup and CEM compatibility are not verified by this statement.",
            VolvoP2CemDiagnosticInterface.J2534_1 =>
                "Volvo explicitly says J2534-1 cannot be used for VIDA diagnostics. " +
                "This does not prove any third-party diagnostic application works.",
            VolvoP2CemDiagnosticInterface.J2534_2 =>
                "Volvo documents J2534-2 software downloads on HS/MS CAN; " +
                "diagnostics and CEM service availability must be verified separately.",
            _ => "An unspecified or other adapter is not verified for VIDA " +
                 "or Volvo P2 CEM diagnostics."
        };

        return new(VolvoP2CemVidaNodeAssessmentKind.WorkshopLogicalNodeReferenceOnly,
            label, selectedInterface == VolvoP2CemDiagnosticInterface.J2534_1,
            "Reported VIDA logical CEM node label: " + label +
            " (Volvo TJ reproduction, model-year scope only). " +
            "It is NOT a CAN ID, ISO-TP address, physical network endpoint " +
            "or a command to send. " + interfaceText);
    }

    internal static string BuildOfflineOverview()
    {
        var text = new StringBuilder();
        text.AppendLine("Volvo XC90 P2 CEM: VIDA diagnostic node research (offline)");
        text.AppendLine("Source tier: third-party reproduction of Volvo TJ US14902.6.0 (2010).");
        text.AppendLine("Reported logical VIDA CEM nodes: model years 2003-2004 = 40;");
        text.AppendLine("model years 2005-2014 = 50. Actual ECU identity remains unknown.");
        text.AppendLine("These are VIDA logical node LABELS, not raw CAN arbitration IDs,");
        text.AppendLine("ISO-TP request/response addresses, DID values, PINs or write commands.");
        text.AppendLine("Manufacturer guidance (Volvo 320EN20): DiCE recommended for D2/GGD;");
        text.AppendLine("J2534-1 is for certain software downloads and CANNOT do VIDA diagnostics;");
        text.AppendLine("J2534-2 support does NOT establish an XC90 CEM diagnostic transport.");
        text.AppendLine("A confirmed model year alone cannot select a physical CEM transport.");
        text.AppendLine("Network/address mapping and real hardware compatibility: UNVERIFIED.");
        text.AppendLine("No CEM connection, PIN retrieval, configuration read or write enabled.");
        return text.ToString();
    }
}
