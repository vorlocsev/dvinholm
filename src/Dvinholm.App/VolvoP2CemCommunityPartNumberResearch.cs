namespace Dvinholm.App;

/// <summary>
/// Scope of a part-number mention in one external, community-maintained
/// research list. A mention is not a Volvo-approved compatibility table,
/// an independently authenticated hardware revision or an ECU capability.
/// </summary>
internal enum VolvoP2CemCommunityPartNumberKind
{
    InvalidOrMissingPartNumber,
    NotInSelectedResearchSample,
    ReportedBrickCem,
    ReportedLCem,
    ReportedHCem,
    ConflictingCommunityCategories
}

internal sealed record VolvoP2CemCommunityPartNumberAssessment(
    VolvoP2CemCommunityPartNumberKind Kind,
    string ReadOnlyExplanation)
{
    internal bool IsAuthenticatedPhysicalCem => false;
    internal bool HasVerifiedXc90Fitment => false;
    internal bool HasVerifiedProtocolOrCanAddress => false;
    internal bool MaySelectBusSpeed => false;
    internal bool MayIssueDiagnosticRequest => false;
    internal bool MayRequestPin => false;
    internal bool MayReadOrWriteConfiguration => false;
}

/// <summary>
/// A deliberately *small, non-exhaustive* factual cross-reference of P2 CEM
/// part-number mentions from one historical community project, NOT a copy
/// of its source code, PIN algorithm, service traffic or configuration.
/// Source: https://github.com/vtl/volvo-cem-cracker (GPL-3.0), commit
/// 0130cd055da075823dd15a98e4a11d5496128e60.
/// Never promote a matching P/N to a verified vehicle, authenticated CEM,
/// diagnostic transport, supported PIN route or service eligibility.
/// </summary>
internal static class VolvoP2CemCommunityPartNumberResearch
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SelectedMentions =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["8645716"] = Array.AsReadOnly(new[] { "B" }),
            ["8688434"] = Array.AsReadOnly(new[] { "B" }),
            ["30682981"] = Array.AsReadOnly(new[] { "L" }),
            ["30786889"] = Array.AsReadOnly(new[] { "L" }),
            ["30728539"] = Array.AsReadOnly(new[] { "H" }),
            ["30786890"] = Array.AsReadOnly(new[] { "H" }),
            // The original community source explicitly lists this same
            // number under BOTH P2 CEM-L and P2 CEM-H. Do not pick one.
            ["30682982"] = Array.AsReadOnly(new[] { "L", "H" })
        };

    internal static VolvoP2CemCommunityPartNumberAssessment Assess(
        string? claimedHardwarePartNumber)
    {
        // Accept ONLY an exact numerical hardware part number. Arbitrary
        // notes, VIN-shaped text, user-entered URLs or software P/N values
        // never become a source catalog key or appear in the report.
        if (claimedHardwarePartNumber is not { Length: >= 7 and <= 8 } ||
            !claimedHardwarePartNumber.All(c => c is >= '0' and <= '9'))
            return Result(VolvoP2CemCommunityPartNumberKind.InvalidOrMissingPartNumber,
                "Community part-number cross-reference not evaluated: a canonical 7-8 digit hardware P/N is required.");

        if (!SelectedMentions.TryGetValue(claimedHardwarePartNumber, out var categories))
            return Result(VolvoP2CemCommunityPartNumberKind.NotInSelectedResearchSample,
                "Community part-number cross-reference: not in the selected, NON-EXHAUSTIVE research sample. This is not evidence of incompatibility.");

        if (categories.Count != 1)
            return Result(VolvoP2CemCommunityPartNumberKind.ConflictingCommunityCategories,
                "Community part-number cross-reference: the same number appears in multiple P2 CEM groups. CATEGORY AMBIGUOUS; do not select a variant.");

        return categories[0] switch
        {
            "B" => Result(VolvoP2CemCommunityPartNumberKind.ReportedBrickCem,
                "Community part-number cross-reference: mentioned under P2 CEM-B in a third-party research list only."),
            "L" => Result(VolvoP2CemCommunityPartNumberKind.ReportedLCem,
                "Community part-number cross-reference: mentioned under P2 CEM-L in a third-party research list only."),
            "H" => Result(VolvoP2CemCommunityPartNumberKind.ReportedHCem,
                "Community part-number cross-reference: mentioned under P2 CEM-H in a third-party research list only."),
            _ => Result(VolvoP2CemCommunityPartNumberKind.ConflictingCommunityCategories,
                "Community part-number category unrecognized. No CEM variant selected.")
        };
    }

    internal static string BuildResearchNote(string? claimedHardwarePartNumber)
    {
        var result = Assess(claimedHardwarePartNumber);
        return result.ReadOnlyExplanation + Environment.NewLine +
               "Community list is NOT independently verified Volvo/VIDA data, nor an XC90 model-year fitment table." +
               Environment.NewLine +
               "No CEM identity, diagnostic address, vehicle configuration, PIN or service authorization is established." +
               Environment.NewLine +
               "No speed, transceiver, CAN request or unlock command may be inferred from this cross-reference.";
    }

    private static VolvoP2CemCommunityPartNumberAssessment Result(
        VolvoP2CemCommunityPartNumberKind kind, string explanation) =>
        new(kind, explanation);
}
