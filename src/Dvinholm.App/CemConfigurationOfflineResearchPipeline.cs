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
        var (catalog, comparison) = await InspectInputsAsync(
            firstBodyPath, secondBodyPath, researchJsonPath, cancellationToken);
        return CemConfigurationRevisionResearchTriage.Assess(
            comparison, catalog, claimedHardwarePartNumber, claimedSoftwareRevision);
    }

    /// <summary>
    /// Use one inspected catalog and one compared body pair for BOTH views;
    /// separately sourced/inconsistent research assessments must not be joined.
    /// A valid asserted part/revision is required for whole-format coverage.
    /// </summary>
    internal static async Task<CemConfigurationResearchChangeCoverage> ReviewCoverageAndChangesAsync(
        string firstBodyPath,
        string secondBodyPath,
        string researchJsonPath,
        string? claimedHardwarePartNumber,
        string? claimedSoftwareRevision,
        CancellationToken cancellationToken = default)
    {
        // Fail before reading either body when the asserted scope is invalid.
        // Without it, full-format coverage would be misleading.
        if (!CemConfigurationMappingResearch.ValidPartNumber(claimedHardwarePartNumber) ||
            !CemConfigurationMappingResearch.ValidSoftwareRevision(claimedSoftwareRevision))
            throw new InvalidDataException("A valid asserted CEM part/revision is required.");

        var (catalog, comparison) = await InspectInputsAsync(
            firstBodyPath, secondBodyPath, researchJsonPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var coverage = CemConfigurationResearchCoverage.Assess(
            catalog, comparison.Format, claimedHardwarePartNumber, claimedSoftwareRevision);
        var changed = CemConfigurationRevisionResearchTriage.Assess(
            comparison, catalog, claimedHardwarePartNumber, claimedSoftwareRevision);
        return CemConfigurationResearchChangeCoverage.Combine(changed, coverage);
    }

    private static async Task<(CemConfigurationResearchCatalogDocument Catalog,
        CemConfigurationOfflineComparisonResult Comparison)> InspectInputsAsync(
        string firstBodyPath,
        string secondBodyPath,
        string researchJsonPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstBodyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondBodyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(researchJsonPath);
        cancellationToken.ThrowIfCancellationRequested();

        // Validate the 32 KiB research JSON before touching binary inputs.
        var catalog = await CemConfigurationResearchCatalogImport.InspectFileAsync(
            researchJsonPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // Each raw body is separately verified: 142-byte P2 or 254-byte
        // P3 with valid count/checksum and no mixed-generation alignment.
        var comparison = await CemConfigurationOfflineComparison.CompareFilesAsync(
            firstBodyPath, secondBodyPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return (catalog, comparison);
    }
}
