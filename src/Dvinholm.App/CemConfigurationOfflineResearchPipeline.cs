namespace Dvinholm.App;

/// <summary>
/// The entire explicitly selected, local-only P2/P3 research flow.
/// It first rejects malformed research metadata, then independently
/// validates both raw bodies before comparing their one-based ordinals.
/// The returned assessment contains no paths, raw bytes, body hashes
/// or source claims and grants no diagnostic privileges.
/// </summary>
internal static class CemConfigurationOfflineResearchPipeline
{
    internal static async Task<CemConfigurationRevisionResearchAssessment> ReviewFilesAsync(
        string firstBodyPath,
        string secondBodyPath,
        string researchJsonPath,
        string? claimedHardwarePartNumber,
        string? claimedSoftwareRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstBodyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondBodyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(researchJsonPath);
        cancellationToken.ThrowIfCancellationRequested();

        // Reject an unsafe or unrecognized JSON catalog *before* looking
        // at either binary input. No inferred catalog, VIN lookup, automatic
        // hardware discovery or interpretation of a byte is permitted.
        var catalog = await CemConfigurationResearchCatalogImport.InspectFileAsync(
            researchJsonPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // Each file is bounded to precisely 142 (P2) or 254 (P3)
        // bytes and decoded with count/checksum enforcement. The
        // comparison itself prohibits cross-generation matching.
        var comparison = await CemConfigurationOfflineComparison.CompareFilesAsync(
            firstBodyPath, secondBodyPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // Explicitly claimed part/revision strings are untrusted metadata:
        // even an exact match means only "unverified research claim".
        return CemConfigurationRevisionResearchTriage.Assess(
            comparison, catalog, claimedHardwarePartNumber, claimedSoftwareRevision);
    }
}
