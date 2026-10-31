namespace Dvinholm.App;

/// <summary>
/// Status of a finished generic read-only DID capture relative to the
/// separately verified XC90 P2/CEM signature catalog. A user profile or
/// synthetic J2534 response is never authenticated CEM identity.
/// </summary>
internal enum VolvoP2CemCaptureIdentityKind
{
    InvalidCapture,
    NoProductionSignatures,
    ExactModelYearRequired,
    NoMatchingReadOnlySignature,
    ConflictingReadOnlySignatures,
    ResearchSignatureMatched
}

internal sealed record VolvoP2CemCaptureIdentityAssessment(
    VolvoP2CemCaptureIdentityKind Kind,
    int ObservedDidCount,
    string ActivityMessage)
{
    // No outcome from a generic UDS profile read authorizes a CEM-specific
    // read, any PIN operation, a configuration editor or a vehicle write.
    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool MayReadCemConfiguration => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteConfiguration => false;
}

/// <summary>
/// Fail-closed, payload-free status for the already completed UDS 0x22
/// snapshot flow. Does not issue diagnostic requests, inspect secret strings
/// in the UI, save anything or infer CEM transport from a profile name.
/// The optional evidence list is for deterministic offline research tests;
/// the live UI always uses the intentionally empty production catalog.
/// </summary>
internal static class VolvoP2CemCaptureIdentityReview
{
    internal static VolvoP2CemCaptureIdentityAssessment Assess(
        EcuConfigurationReadSnapshot snapshot,
        int? independentlyConfirmedModelYear = null,
        IEnumerable<VolvoP2CemSupportEntry>? evidence = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!ValidReadback(snapshot))
            return Result(VolvoP2CemCaptureIdentityKind.InvalidCapture, 0,
                "CEM identity not evaluated: the read-only DID capture is invalid.");

        int count = snapshot.Values.Count;
        var catalog = (evidence ?? VolvoP2CemSupportMatrix.ProductionEntries)
            .Take(VolvoP2CemEvidenceCatalogValidator.MaximumEntries + 1)
            .ToArray();

        if (catalog.Length == 0)
            return Result(VolvoP2CemCaptureIdentityKind.NoProductionSignatures, count,
                "CEM identity UNKNOWN: no verified production CEM signatures.");

        if (independentlyConfirmedModelYear is null or < 2003 or > 2014)
            return Result(VolvoP2CemCaptureIdentityKind.ExactModelYearRequired, count,
                "CEM identity UNKNOWN: independently confirmed XC90 P2 model year required.");

        var review = VolvoP2CemSupportMatrix.Assess(
            independentlyConfirmedModelYear.Value, snapshot, catalog);

        return review.Kind switch
        {
            VolvoP2CemSupportKind.Ambiguous =>
                Result(VolvoP2CemCaptureIdentityKind.ConflictingReadOnlySignatures, count,
                    "CEM identity UNKNOWN: conflicting read-only signature evidence."),
            VolvoP2CemSupportKind.VerifiedIdentityReadOnly or
                VolvoP2CemSupportKind.DocumentedReadOnlyDidSet =>
                Result(VolvoP2CemCaptureIdentityKind.ResearchSignatureMatched, count,
                    "CEM read-only signature matches research evidence; physical CEM identity and service authorization remain unconfirmed."),
            _ => Result(VolvoP2CemCaptureIdentityKind.NoMatchingReadOnlySignature, count,
                "CEM identity UNKNOWN: no applicable verified read-only signature matched.")
        };
    }

    private static bool ValidReadback(EcuConfigurationReadSnapshot snapshot)
    {
        var maxCanId = snapshot.IsExtended ? 0x1FFFFFFFu : 0x7FFu;
        if (snapshot.CapturedAtUtc == default ||
            snapshot.CapturedAtUtc.Offset != TimeSpan.Zero ||
            snapshot.RequestId > maxCanId || snapshot.ResponseId > maxCanId ||
            snapshot.RequestId == snapshot.ResponseId ||
            snapshot.Values is null ||
            snapshot.Values.Count is < 1 or > EcuConfigurationReadback.MaximumDataIdentifiers)
            return false;

        var seen = new HashSet<ushort>();
        foreach (var entry in snapshot.Values)
        {
            if (entry is null || !seen.Add(entry.DataIdentifier) ||
                entry.ValueHex is not { Length: >= 2 } ||
                entry.ValueHex.Length > EcuConfigurationReadback.MaximumDidValueBytes * 2 ||
                (entry.ValueHex.Length & 1) != 0 ||
                !entry.ValueHex.All(c =>
                    c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f'))
                return false;
        }

        return true;
    }

    private static VolvoP2CemCaptureIdentityAssessment Result(
        VolvoP2CemCaptureIdentityKind kind, int didCount, string message) =>
        new(kind, didCount, message);
}
