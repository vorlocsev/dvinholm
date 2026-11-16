namespace Dvinholm.App;

/// <summary>
/// One user's unverified metadata row and an optional raw byte from a
/// separately selected local body. Neither value identifies an OEM option.
/// </summary>
internal sealed record CemConfigurationResearchViewRow(
    CemConfigurationIndexedFormat Format,
    int Index1Based,
    string HardwarePartNumber,
    string SoftwareRevision,
    string ClaimedMeaning,
    string EvidenceReference,
    string? LocalRawHex)
{
    public string Display =>
        (Format == CemConfigurationIndexedFormat.P2B9Fc ? "P2" : "P3")
        + "  #" + Index1Based.ToString("D3")
        + " · " + ClaimedMeaning
        + (LocalRawHex is null ? string.Empty : "  ·  0x" + LocalRawHex + " (local, unverified)");

    internal bool HasVerifiedMeaning => false;
    internal bool MatchesAuthenticatedCem => false;
    internal bool MayReadOrWriteVehicle => false;
}

/// <summary>
/// UI-neutral, read-only projection of user-authored research claims. The
/// separately selected raw body can be compared by ordinal ONLY. No joining
/// by claimed hardware ID makes a source authentic, and no unknown byte
/// is mapped to a manufacturer option.
/// </summary>
internal static class CemConfigurationResearchCatalogProjection
{
    internal static IReadOnlyList<CemConfigurationResearchViewRow> Filter(
        CemConfigurationResearchCatalogDocument document,
        CemConfigurationIndexedFormat? selectedFormat,
        CemConfigurationIndexedSnapshot? localBody = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Format != CemConfigurationResearchCatalogImport.Format ||
            document.SchemaVersion != 1 || !document.IsUnverifiedResearch ||
            document.IsRestorableBackup || document.MayAccessEcu ||
            document.Claims is null || document.Claims.Count is < 1 or > CemConfigurationMappingResearch.MaximumRecords ||
            CemConfigurationMappingResearch.Validate(document.Claims).Count != 0)
            throw new InvalidDataException("Untrusted CEM research catalog cannot be presented.");

        if (selectedFormat is not null &&
            selectedFormat != CemConfigurationIndexedFormat.P2B9Fc &&
            selectedFormat != CemConfigurationIndexedFormat.P3F106)
            throw new InvalidDataException("Unknown P2/P3 display filter.");

        if (localBody is not null)
        {
            var count = localBody.Format switch
            {
                CemConfigurationIndexedFormat.P2B9Fc => CemConfigurationBlockDecoder.P2ValueCount,
                CemConfigurationIndexedFormat.P3F106 => CemConfigurationBlockDecoder.P3ValueCount,
                _ => 0
            };
            if (count == 0 || localBody.Values is null || localBody.Values.Count != count ||
                localBody.Values.Where((item, index) => item.Index1Based != index + 1).Any() ||
                localBody.BodySha256 is not { Length: 64 } ||
                localBody.BodySha256.Any(c => c is not (>= '0' and <= '9' or >= 'A' and <= 'F')))
                throw new InvalidDataException("Invalid local indexed body.");
        }

        return Array.AsReadOnly(document.Claims
            .Where(claim => selectedFormat is null || claim.Format == selectedFormat)
            .OrderBy(claim => claim.Format)
            .ThenBy(claim => claim.Index1Based)
            .ThenBy(claim => claim.HardwarePartNumber, StringComparer.Ordinal)
            .ThenBy(claim => claim.SoftwareRevision, StringComparer.Ordinal)
            .Select(claim => new CemConfigurationResearchViewRow(
                claim.Format, claim.Index1Based, claim.HardwarePartNumber,
                claim.SoftwareRevision, claim.ClaimedMeaning, claim.EvidenceReference,
                localBody?.Format == claim.Format
                    ? localBody.Values[claim.Index1Based - 1].ValueHex
                    : null))
            .ToArray());
    }
}
