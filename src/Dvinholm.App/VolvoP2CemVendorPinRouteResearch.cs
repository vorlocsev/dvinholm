using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Claims from a third-party vendor, not independently verified P2 protocols.
/// PIN decoding, programming keys and changing CEM configuration are distinct.
/// </summary>
internal enum VolvoP2CemClaimedEngine
{
    Unknown,
    OtherFiveCylinder,
    D5_147Kw,
    D5_120Kw,
    InlineSix32,
    V8_44,
    OtherUnclassified
}

internal enum VolvoP2CemVendorPinTool { Vdash, Vdd }

internal enum VolvoP2CemVendorPinClaimKind
{
    UnconfirmedYearOrEngine,
    AdvertisedForSomeP2Only,
    AdvertisedWithSeparateKeyProgrammingCaveat,
    ExcludedByVendorForThisEngine,
    UnspecifiedForThisVariant
}

internal sealed record VolvoP2CemVendorPinAssessment(
    VolvoP2CemVendorPinClaimKind Kind,
    bool SeparateKeyProgrammingCaveat,
    string Explanation)
{
    internal bool HasVerifiedCemRevision => false;
    internal bool HasVerifiedDiagnosticTransport => false;
    internal bool MayConnectToCem => false;
    internal bool MayDecodePin => false;
    internal bool MayProgramKeys => false;
    internal bool MayWriteConfiguration => false;
}

/// <summary>
/// Source-scoped offline compatibility claim classifier. Never evaluates real
/// ECU identity, opens J2534, accepts secrets or performs security requests.
/// A self-reported independent year check is still just a USER CLAIM.
/// </summary>
internal static class VolvoP2CemVendorPinRouteResearch
{
    internal static VolvoP2CemVendorPinAssessment Assess(
        int? claimedModelYear, bool userReportsYearChecked,
        VolvoP2CemClaimedEngine claimedEngine, VolvoP2CemVendorPinTool tool)
    {
        if (!userReportsYearChecked || claimedModelYear is null or < 2005 or > 2014 ||
            !Enum.IsDefined(claimedEngine) || !Enum.IsDefined(tool) ||
            claimedEngine is VolvoP2CemClaimedEngine.Unknown or
                VolvoP2CemClaimedEngine.OtherUnclassified)
            return new(VolvoP2CemVendorPinClaimKind.UnconfirmedYearOrEngine,
                false, "Model year/engine/tool NOT independently authenticated. " +
                "Vendor PIN decoding coverage remains UNKNOWN.");

        var special = claimedEngine is VolvoP2CemClaimedEngine.InlineSix32 or
            VolvoP2CemClaimedEngine.V8_44 or VolvoP2CemClaimedEngine.D5_147Kw;
        var keyCaveat = special ||
            (claimedEngine == VolvoP2CemClaimedEngine.D5_120Kw &&
             claimedModelYear >= 2013);

        if (tool == VolvoP2CemVendorPinTool.Vdd && special)
            return new(VolvoP2CemVendorPinClaimKind.ExcludedByVendorForThisEngine,
                keyCaveat, "D5T5 excludes 3.2, 4.4 V8 and 147 kW D5 from " +
                "VDD P2 CEM PIN decoding. Other products are not assessed.");

        if (tool == VolvoP2CemVendorPinTool.Vdash && special)
            return new(VolvoP2CemVendorPinClaimKind.AdvertisedWithSeparateKeyProgrammingCaveat,
                keyCaveat, "D5T5 advertises VDASH CEM PIN decoding for some P2 " +
                "including 3.2, 4.4 V8 and 147 kW D5. Its separate key " +
                "programming article lists restrictions. PIN decoding is " +
                "NOT verified key programming or configuration access.");

        if (tool == VolvoP2CemVendorPinTool.Vdd)
            return new(VolvoP2CemVendorPinClaimKind.AdvertisedForSomeP2Only,
                keyCaveat, "D5T5 advertises VDD decoding for SOME P2 vehicles " +
                "produced 2005-2014, outside the listed engine exclusions. " +
                "It explicitly does not guarantee success or a physical CEM match.");

        return new(VolvoP2CemVendorPinClaimKind.UnspecifiedForThisVariant,
            keyCaveat, "VDASH decoding coverage for this claimed engine is not " +
            "established by the named vendor examples. Key support is a " +
            "separate claim, not a diagnostic protocol specification.");
    }

    internal static string BuildOfflineOverview() =>
        new StringBuilder()
            .AppendLine()
            .AppendLine("XC90 P2 CEM VENDOR PIN CLAIMS BY ENGINE (OFFLINE ONLY)")
            .AppendLine("D5T5 PIN page: VDASH advertises some P2, including 3.2,")
            .AppendLine("4.4 V8 and 147 kW D5. Its VDD decoding offer covers")
            .AppendLine("SOME MY2005-2014 P2 but explicitly excludes those engines.")
            .AppendLine("Separate D5T5 KEY-programming article lists restrictions")
            .AppendLine("for those XC90s and 120 kW D5 MY2013+.")
            .AppendLine("PIN decoding, programming keys and vehicle configuration")
            .AppendLine("are DIFFERENT operations. Vendor claims are not Volvo")
            .AppendLine("protocol documentation or proof of ECU compatibility.")
            .AppendLine("No PIN, ECU request, security access or configuration write.")
            .AppendLine("Details: docs/P2-CEM-VENDOR-PIN-ROUTES.md")
            .ToString();
}
