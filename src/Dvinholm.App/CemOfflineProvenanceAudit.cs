using System.Globalization;
using System.Security.Cryptography;

namespace Dvinholm.App;

/// <summary>
/// A user-authored claim about one selected local source. A declared SHA-256
/// can be compared with local bytes but is NOT an OEM signature, a capture
/// attestation, or a proof that files originate from one physical CEM.
/// </summary>
internal enum CemOfflineProvenanceRole
{
    PreviousBody,
    CurrentBody,
    ResearchCatalog
}

internal sealed record CemOfflineProvenanceDeclaration(
    CemOfflineProvenanceRole Role,
    string SourceReference,
    string ClaimedModuleAssociation,
    string ClaimedHardwarePartNumber,
    string ClaimedSoftwareRevision,
    DateTimeOffset ClaimedObservedUtc,
    string ExpectedSha256);

internal readonly record struct CemOfflineProvenanceFingerprintResult(
    CemOfflineProvenanceRole Role, bool MatchesLocalSha256);

internal sealed class CemOfflineProvenanceAssessment
{
    internal IReadOnlyList<CemOfflineProvenanceFingerprintResult> Fingerprints { get; }
    internal int LocallyMatchingFingerprintCount =>
        Fingerprints.Count(item => item.MatchesLocalSha256);
    internal bool AllThreeDeclaredFingerprintsMatch =>
        LocallyMatchingFingerprintCount == 3;
    internal bool ClaimedModuleAssociationTextsAgree { get; }
    internal bool ClaimedHardwareSoftwareTextsAgree { get; }
    internal bool ClaimedObservationOrderPlausible { get; }
    internal int ChangedRawIndices { get; }
    internal IReadOnlyList<CemConfigurationEvidenceGap> OpenPhysicalEvidenceGates { get; }

    internal bool HasIndependentlyAuthenticatedOrigin => false;
    internal bool HasVerifiedSamePhysicalCem => false;
    internal bool HasVerifiedFirmwareRevision => false;
    internal bool HasVerifiedOptionMeanings => false;
    internal bool MayReadLiveConfiguration => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteConfiguration => false;
    internal bool IsRestorableBackup => false;

    internal CemOfflineProvenanceAssessment(
        IReadOnlyList<CemOfflineProvenanceFingerprintResult> fingerprints,
        bool moduleLabelsAgree, bool claimedScopeAgrees, bool observationOrder,
        CemConfigurationOfflineEvidenceChecklistAssessment checkpoint)
    {
        ArgumentNullException.ThrowIfNull(fingerprints);
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (fingerprints.Count != 3 ||
            fingerprints.Select(item => item.Role).Distinct().Count() != 3 ||
            fingerprints.Any(item => !Enum.IsDefined(item.Role)) ||
            checkpoint.OpenGates.Count != 7)
            throw new InvalidDataException("Incomplete CEM provenance research status.");
        Fingerprints = Array.AsReadOnly(fingerprints.ToArray());
        ClaimedModuleAssociationTextsAgree = moduleLabelsAgree;
        ClaimedHardwareSoftwareTextsAgree = claimedScopeAgrees;
        ClaimedObservationOrderPlausible = observationOrder;
        ChangedRawIndices = checkpoint.ChangedRawOrdinals;
        OpenPhysicalEvidenceGates = Array.AsReadOnly(checkpoint.OpenGates.ToArray());
    }

    internal string CreateRedactedReport(bool english)
    {
        var lines = new List<string>
        {
            english ? "CEM LOCAL FILE PROVENANCE CLAIM AUDIT (OFFLINE)"
                : "CEM: ПРОВЕРКА ЗАЯВЛЕНИЙ О ПРОИСХОЖДЕНИИ ФАЙЛОВ (ОФЛАЙН)",
            (english ? "Changed raw indices: " : "Изменённых сырых индексов: ") +
                ChangedRawIndices.ToString(CultureInfo.InvariantCulture),
            (english ? "Local SHA-256 matches: " : "Совпадений локальных SHA-256: ") +
                LocallyMatchingFingerprintCount.ToString(CultureInfo.InvariantCulture) + "/3"
        };
        foreach (var item in Fingerprints.OrderBy(x => x.Role))
        {
            var name = item.Role switch
            {
                CemOfflineProvenanceRole.PreviousBody => english ? "Previous .bin" : "Предыдущий .bin",
                CemOfflineProvenanceRole.CurrentBody => english ? "Current .bin" : "Текущий .bin",
                CemOfflineProvenanceRole.ResearchCatalog => english ? "Research JSON" : "Исследовательский JSON",
                _ => throw new InvalidDataException("Unknown local source role.")
            };
            lines.Add(name + ": " + (item.MatchesLocalSha256
                ? (english ? "local digest matches declared digest" : "локальный хеш совпадает с заявленным")
                : (english ? "local digest DOES NOT MATCH" : "локальный хеш НЕ СОВПАДАЕТ")));
        }
        lines.Add((english ? "Claimed module-label agreement: " : "Совпадение заявленных меток блока: ") +
            YesNo(ClaimedModuleAssociationTextsAgree, english));
        lines.Add((english ? "Claimed hardware/software text agreement: " :
            "Совпадение заявленных HW/SW: ") + YesNo(ClaimedHardwareSoftwareTextsAgree, english));
        lines.Add((english ? "Claimed before/after UTC observation order: " :
            "Порядок заявленного времени UTC до/после: ") +
            YesNo(ClaimedObservationOrderPlausible, english));
        lines.Add(english ? "Independent OEM/physical evidence gates: OPEN 7/7" :
            "Независимые OEM/физические требования: ОТКРЫТО 7/7");
        lines.Add(english
            ? "SHA-256 proves only a local byte comparison; claimed timestamps, sources and module labels are NOT authenticated."
            : "SHA-256 подтверждает только совпадение локальных байтов; заявленные даты, источники и метки блока НЕ проверены.");
        lines.Add(english
            ? "No independent same-module proof. Raw hashes, file paths, source references, part numbers, software strings, VIN and payload bytes omitted."
            : "Нет независимого доказательства одного блока. Хеши, пути, источники, номера, версии, VIN и сырые байты не выводятся.");
        lines.Add(english
            ? "NOT a restorable backup; CEM reading, PIN and writing remain disabled."
            : "НЕ резервная копия; чтение CEM, PIN и запись запрещены.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string YesNo(bool value, bool english) =>
        english ? value ? "same (USER-CLAIMED)" : "different / unresolved"
            : value ? "совпадают (СО СЛОВ ПОЛЬЗОВАТЕЛЯ)" : "расходятся / не установлено";
}

internal static class CemOfflineProvenanceAudit
{
    internal static void ValidateDeclarations(
        IReadOnlyList<CemOfflineProvenanceDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        if (declarations.Count != 3 ||
            declarations.Select(x => x?.Role).Distinct().Count() != 3 ||
            declarations.Any(x => x is null || !Enum.IsDefined(x.Role) ||
                !ValidReference(x.SourceReference) ||
                !ValidModuleLabel(x.ClaimedModuleAssociation) ||
                !CemConfigurationMappingResearch.ValidPartNumber(x.ClaimedHardwarePartNumber) ||
                !CemConfigurationMappingResearch.ValidSoftwareRevision(x.ClaimedSoftwareRevision) ||
                !ValidUtc(x.ClaimedObservedUtc) ||
                !ValidDigest(x.ExpectedSha256)))
            throw new InvalidDataException("Invalid or incomplete CEM file provenance claims.");
    }

    internal static CemOfflineProvenanceAssessment Assess(
        IReadOnlyList<CemOfflineProvenanceDeclaration> declarations,
        string previousLocalSha256, string currentLocalSha256,
        string catalogLocalSha256,
        string assertedHardwarePartNumber, string assertedSoftwareRevision,
        CemConfigurationOfflineEvidenceChecklistAssessment checkpoint)
    {
        ValidateDeclarations(declarations);
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (!ValidDigest(previousLocalSha256) ||
            !ValidDigest(currentLocalSha256) ||
            !ValidDigest(catalogLocalSha256) ||
            !CemConfigurationMappingResearch.ValidPartNumber(assertedHardwarePartNumber) ||
            !CemConfigurationMappingResearch.ValidSoftwareRevision(assertedSoftwareRevision))
            throw new InvalidDataException("Invalid inspected local file/scope fingerprint.");

        var byRole = declarations.ToDictionary(x => x.Role);
        var previous = byRole[CemOfflineProvenanceRole.PreviousBody];
        var current = byRole[CemOfflineProvenanceRole.CurrentBody];
        var catalog = byRole[CemOfflineProvenanceRole.ResearchCatalog];
        var rows = new[]
        {
            new CemOfflineProvenanceFingerprintResult(CemOfflineProvenanceRole.PreviousBody,
                string.Equals(previous.ExpectedSha256, previousLocalSha256, StringComparison.Ordinal)),
            new CemOfflineProvenanceFingerprintResult(CemOfflineProvenanceRole.CurrentBody,
                string.Equals(current.ExpectedSha256, currentLocalSha256, StringComparison.Ordinal)),
            new CemOfflineProvenanceFingerprintResult(CemOfflineProvenanceRole.ResearchCatalog,
                string.Equals(catalog.ExpectedSha256, catalogLocalSha256, StringComparison.Ordinal))
        };
        var modulesAgree = declarations.All(x => string.Equals(
            x.ClaimedModuleAssociation, previous.ClaimedModuleAssociation,
            StringComparison.Ordinal));
        var scopesAgree = declarations.All(x =>
            string.Equals(x.ClaimedHardwarePartNumber, assertedHardwarePartNumber, StringComparison.Ordinal) &&
            string.Equals(x.ClaimedSoftwareRevision, assertedSoftwareRevision, StringComparison.Ordinal));
        var orderPlausible = previous.ClaimedObservedUtc <= current.ClaimedObservedUtc;

        return new(rows, modulesAgree, scopesAgree, orderPlausible, checkpoint);
    }

    /// <summary>
    /// Bounded local-only fingerprinting for the selected strict file types.
    /// It is called before/after the main validated pipeline to detect
    /// changed bytes; no hash is written to disk or added to reports.
    /// </summary>
    internal static async Task<string> ReadLocalSha256Async(
        string selectedPath, CemOfflineProvenanceRole role,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        cancellationToken.ThrowIfCancellationRequested();
        var isCatalog = role == CemOfflineProvenanceRole.ResearchCatalog;
        if (!Enum.IsDefined(role) ||
            !string.Equals(Path.GetExtension(selectedPath),
                isCatalog ? ".json" : ".bin", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unexpected selected CEM provenance file type.");

        await using var input = new FileStream(selectedPath, FileMode.Open,
            FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var length = input.Length;
        var acceptable = isCatalog
            ? length is >= 1 and <= CemConfigurationResearchCatalogImport.MaximumFileBytes
            : length == CemConfigurationBlockDecoder.P2BodyLength ||
                length == CemConfigurationBlockDecoder.P3BodyLength;
        if (!acceptable)
            throw new InvalidDataException("Invalid local CEM provenance input size.");
        var bytes = new byte[checked((int)length)];
        await input.ReadExactlyAsync(bytes.AsMemory(), cancellationToken);
        if (input.Length != length ||
            await input.ReadAsync(new byte[1], cancellationToken) != 0)
            throw new InvalidDataException("CEM provenance input changed while reading.");
        cancellationToken.ThrowIfCancellationRequested();
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static bool ValidDigest(string? digest) =>
        digest is { Length: 64 } &&
        digest.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static bool ValidReference(string? reference) =>
        reference is { Length: >= 4 and <= 160 } &&
        !reference.Any(c => char.IsControl(c) || c is '<' or '>');

    private static bool ValidModuleLabel(string? label) =>
        label is { Length: >= 3 and <= 48 } &&
        label.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'Z' or
            >= 'a' and <= 'z' or '_' or '-');

    private static bool ValidUtc(DateTimeOffset timestamp) =>
        timestamp.Offset == TimeSpan.Zero && timestamp.Year is >= 2000 and <= 2100;
}
