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

    /// <summary>
    /// One complete, validated LOCAL research pass plus an explicit list
    /// of independent physical/OEM evidence still missing. Session status
    /// is intentionally not inferred from any local file.
    /// </summary>
    internal static async Task<CemConfigurationOfflineEvidenceChecklistAssessment> ReviewEvidenceChecklistAsync(
        string firstBodyPath,
        string secondBodyPath,
        string researchJsonPath,
        string? claimedHardwarePartNumber,
        string? claimedSoftwareRevision,
        CancellationToken cancellationToken = default)
    {
        var research = await ReviewCoverageAndChangesAsync(
            firstBodyPath, secondBodyPath, researchJsonPath,
            claimedHardwarePartNumber, claimedSoftwareRevision,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return CemConfigurationOfflineEvidenceChecklist.Assess(research);
    }

    /// <summary>
    /// Separates three USER-DECLARED capture claims from real selected
    /// file bytes. Exact local digest agreement does not authenticate
    /// the physical CEM, acquisition time or source.
    /// </summary>
    internal static async Task<CemOfflineProvenanceAssessment> ReviewProvenanceAsync(
        string firstBodyPath,
        string secondBodyPath,
        string researchJsonPath,
        string? claimedHardwarePartNumber,
        string? claimedSoftwareRevision,
        IReadOnlyList<CemOfflineProvenanceDeclaration> declarations,
        CancellationToken cancellationToken = default)
    {
        // All claimed metadata is checked before touching any local file.
        CemOfflineProvenanceAudit.ValidateDeclarations(declarations);
        if (!CemConfigurationMappingResearch.ValidPartNumber(claimedHardwarePartNumber) ||
            !CemConfigurationMappingResearch.ValidSoftwareRevision(claimedSoftwareRevision))
            throw new InvalidDataException("Missing asserted CEM research part/revision.");

        // Verify catalog's byte identity on both sides of the strict parser;
        // body identities come from the existing exact-body decoder hashes.
        var catalogBefore = await CemOfflineProvenanceAudit.ReadLocalSha256Async(
            researchJsonPath, CemOfflineProvenanceRole.ResearchCatalog, cancellationToken);
        var (catalog, comparison) = await InspectInputsAsync(
            firstBodyPath, secondBodyPath, researchJsonPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var catalogAfter = await CemOfflineProvenanceAudit.ReadLocalSha256Async(
            researchJsonPath, CemOfflineProvenanceRole.ResearchCatalog, cancellationToken);
        var previousAfter = await CemOfflineProvenanceAudit.ReadLocalSha256Async(
            firstBodyPath, CemOfflineProvenanceRole.PreviousBody, cancellationToken);
        var currentAfter = await CemOfflineProvenanceAudit.ReadLocalSha256Async(
            secondBodyPath, CemOfflineProvenanceRole.CurrentBody, cancellationToken);
        if (!string.Equals(catalogBefore, catalogAfter, StringComparison.Ordinal) ||
            !string.Equals(comparison.FirstBodySha256, previousAfter, StringComparison.Ordinal) ||
            !string.Equals(comparison.SecondBodySha256, currentAfter, StringComparison.Ordinal))
            throw new InvalidDataException(
                "Selected local file content changed between independent inspections.");

        var coverage = CemConfigurationResearchCoverage.Assess(
            catalog, comparison.Format, claimedHardwarePartNumber, claimedSoftwareRevision);
        var changed = CemConfigurationRevisionResearchTriage.Assess(
            comparison, catalog, claimedHardwarePartNumber, claimedSoftwareRevision);
        var combined = CemConfigurationResearchChangeCoverage.Combine(changed, coverage);
        var checkpoint = CemConfigurationOfflineEvidenceChecklist.Assess(combined);

        return CemOfflineProvenanceAudit.Assess(
            declarations, previousAfter, currentAfter, catalogAfter,
            claimedHardwarePartNumber!, claimedSoftwareRevision!, checkpoint);
    }

    /// <summary>
    /// Bound the same three user-supplied evidence declarations to a strict
    /// offline provenance review, then inspect only user-asserted links and
    /// an optional in-memory history of local rechecks. Neither can grant
    /// any OEM, security or vehicle-access privilege.
    /// </summary>
    internal static async Task<CemOfflineEvidenceLedgerAssessment> ReviewEvidenceLedgerAsync(
        string firstBodyPath,
        string secondBodyPath,
        string researchJsonPath,
        string? claimedHardwarePartNumber,
        string? claimedSoftwareRevision,
        IReadOnlyList<CemOfflineProvenanceDeclaration> declarations,
        IReadOnlyList<CemOfflineEvidenceLink> links,
        IReadOnlyList<CemOfflineEvidenceRecheck> rechecks,
        CancellationToken cancellationToken = default)
    {
        CemOfflineProvenanceAudit.ValidateDeclarations(declarations);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(rechecks);
        if (links.Count > CemOfflineEvidenceLedger.MaximumLinks ||
            rechecks.Count > CemOfflineEvidenceLedger.MaximumRechecks)
            throw new InvalidDataException("CEM evidence ledger exceeds its bounds.");

        var provenance = await ReviewProvenanceAsync(
            firstBodyPath, secondBodyPath, researchJsonPath,
            claimedHardwarePartNumber, claimedSoftwareRevision,
            declarations, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return CemOfflineEvidenceLedger.Assess(declarations, provenance, links, rechecks);
    }

    /// <summary>
    /// Deterministic local-only age and consistency review of the three
    /// selected source declarations and bounded in-memory recheck journal.
    /// Claimed UTC is not independently authenticated.
    /// </summary>
    internal static async Task<CemOfflineEvidenceFreshnessAssessment> ReviewFreshnessAsync(
        string firstBodyPath,
        string secondBodyPath,
        string researchJsonPath,
        string? claimedHardwarePartNumber,
        string? claimedSoftwareRevision,
        IReadOnlyList<CemOfflineProvenanceDeclaration> declarations,
        IReadOnlyList<CemOfflineEvidenceLink> links,
        IReadOnlyList<CemOfflineEvidenceRecheck> rechecks,
        DateTimeOffset claimedReviewUtc,
        TimeSpan maximumClaimedAge,
        CancellationToken cancellationToken = default)
    {
        // Refuse malformed time policy and oversized inputs before opening
        // even the first local file.
        CemOfflineEvidenceFreshness.ValidateReviewClockAndWindow(
            claimedReviewUtc, maximumClaimedAge);
        CemOfflineProvenanceAudit.ValidateDeclarations(declarations);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(rechecks);
        if (links.Count > CemOfflineEvidenceLedger.MaximumLinks ||
            rechecks.Count > CemOfflineEvidenceLedger.MaximumRechecks)
            throw new InvalidDataException("CEM evidence history exceeds its limits.");

        var provenance = await ReviewProvenanceAsync(
            firstBodyPath, secondBodyPath, researchJsonPath,
            claimedHardwarePartNumber, claimedSoftwareRevision,
            declarations, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return CemOfflineEvidenceFreshness.Assess(
            declarations, provenance, links, rechecks,
            claimedReviewUtc, maximumClaimedAge);
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
