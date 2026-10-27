namespace Dvinholm.App;

/// <summary>
/// In Volvo P2, CEM is involved in multiple vehicle networks. The documented
/// model-year HS-CAN rate must never be generalized to low-speed CAN, K-line,
/// a specific ECU diagnostic transport, or an unverified model year.
/// </summary>
internal enum VolvoP2NetworkKind
{
    Unknown,
    HighSpeedCan,
    LowSpeedCan,
    OtherOrKLine
}

internal enum VolvoP2HsCanSpeedAssessmentKind
{
    VehicleYearUnconfirmed,
    OutsideDocumentedXc90Years,
    NetworkUnconfirmed,
    DifferentNetworkNotCovered,
    BaudRateNotSelected,
    MismatchedDocumentedHsCanSpeed,
    MatchesDocumentedHsCanSpeedOnly
}

internal sealed record VolvoP2HsCanSpeedAssessment(
    VolvoP2HsCanSpeedAssessmentKind Kind,
    uint? ReferenceHsCanBaudRate,
    string Explanation)
{
    // The rate advisory is NEVER sufficient to authorize a vehicle request,
    // certify a J2534 adapter, identify CEM firmware or program an ECU.
    internal bool AuthorizesAutomaticConnection => false;
    internal bool ConfirmsCemDiagnosticTransport => false;
    internal bool AuthorizesPinOrConfigurationWrite => false;
}

/// <summary>
/// Source-limited, offline-only advisory for XC90 P2 HS-CAN.
/// Referenced Volvo XC90 workshop fault-tracing documentation reports
/// 250 kbit/s through model year 2004 and 500 kbit/s from 2005.
/// No connection changes, passive discovery or hardware calls are performed.
/// </summary>
internal static class VolvoP2CemHsCanSpeedAdvisory
{
    internal const int FirstDocumentedXc90ModelYear = 2003;
    internal const int LastFirstGenerationXc90ModelYear = 2014;
    internal const uint EarlyHsCanBaudRate = 250_000;
    internal const uint LaterHsCanBaudRate = 500_000;

    internal static VolvoP2HsCanSpeedAssessment Evaluate(
        int? confirmedModelYear,
        bool modelYearIndependentlyConfirmed,
        VolvoP2NetworkKind network,
        uint? selectedBaudRate)
    {
        if (confirmedModelYear is null || !modelYearIndependentlyConfirmed)
            return new(VolvoP2HsCanSpeedAssessmentKind.VehicleYearUnconfirmed, null,
                "Verify the vehicle's actual model year first. Do not infer it from the build date or a profile name.");

        if (confirmedModelYear.Value is < FirstDocumentedXc90ModelYear or > LastFirstGenerationXc90ModelYear)
            return new(VolvoP2HsCanSpeedAssessmentKind.OutsideDocumentedXc90Years, null,
                "The first-generation XC90 HS-CAN guidance is not validated for this model year.");

        if (network == VolvoP2NetworkKind.Unknown)
            return new(VolvoP2HsCanSpeedAssessmentKind.NetworkUnconfirmed, null,
                "Identify the physical vehicle CAN network before selecting a speed.");

        if (network != VolvoP2NetworkKind.HighSpeedCan)
            return new(VolvoP2HsCanSpeedAssessmentKind.DifferentNetworkNotCovered, null,
                "This source documents HS-CAN only; do not apply its speed to LS-CAN, K-line or another bus.");

        uint documentedRate = confirmedModelYear.Value <= 2004
            ? EarlyHsCanBaudRate : LaterHsCanBaudRate;

        if (selectedBaudRate is null or 0)
            return new(VolvoP2HsCanSpeedAssessmentKind.BaudRateNotSelected, documentedRate,
                "The requested HS-CAN speed is unspecified. No connection can be inferred from this advisory.");

        if (selectedBaudRate.Value != documentedRate)
            return new(VolvoP2HsCanSpeedAssessmentKind.MismatchedDocumentedHsCanSpeed, documentedRate,
                "Selected HS-CAN rate differs from the Volvo XC90 model-year workshop guidance. " +
                "Stop and verify the physical vehicle profile before transmitting.");

        return new(VolvoP2HsCanSpeedAssessmentKind.MatchesDocumentedHsCanSpeedOnly, documentedRate,
            "The chosen rate matches published XC90 P2 HS-CAN model-year guidance only. " +
            "This does NOT verify an ECU's diagnostic addressing, CEM compatibility, adapter support or authorization.");
    }
}
