namespace Dvinholm.App;

/// <summary>
/// A deliberately payload-free projection of existing experimental CEM
/// part-number observations. It does not issue J2534 requests, interpret
/// proprietary diagnostic payloads, authenticate hardware, or grant access.
/// </summary>
internal enum CemIdentityEvidenceKind
{
    NoCurrentEvidence,
    UnattestedPartNumberObservation
}

internal sealed record CemIdentityEvidenceAssessment(CemIdentityEvidenceKind Kind)
{
    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool HasVerifiedFirmwareRevision => false;
    internal bool MayAcquirePin => false;
    internal bool MayReadConfiguration => false;
    internal bool MayWriteConfiguration => false;
}

internal static class CemIdentityEvidenceReport
{
    // A source-compatible experimental response is only evidence that a
    // part-number-shaped value was observed in the same live session. No
    // inference about a physical CEM, software revision or PIN is permitted.
    internal static CemIdentityEvidenceAssessment Assess(
        uint? observedPartNumber, bool guardCurrent,
        bool sessionConnected, bool developmentContext)
    {
        var observed = observedPartNumber is >= 1_000_000 and <= 99_999_999;
        return new(observed && guardCurrent && sessionConnected && developmentContext
            ? CemIdentityEvidenceKind.UnattestedPartNumberObservation
            : CemIdentityEvidenceKind.NoCurrentEvidence);
    }

    internal static string Format(CemIdentityEvidenceAssessment assessment, bool english)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        var state = assessment.Kind switch
        {
            CemIdentityEvidenceKind.NoCurrentEvidence => english
                ? "CEM identity: unknown. No current experimental observation."
                : "Идентификация CEM: неизвестна. Нет актуального экспериментального наблюдения.",
            CemIdentityEvidenceKind.UnattestedPartNumberObservation => english
                ? "CEM: part-number response observed in the current session (unattested)."
                : "CEM: в текущей сессии наблюдался ответ с номером детали (не подтверждён).",
            _ => throw new InvalidDataException("Unknown CEM identity evidence state.")
        };
        var limit = english
            ? "User-selected ECU profiles and generic DID snapshots do not authenticate a physical CEM. Hardware/software revision, compatibility, PIN and configuration access are unverified."
            : "Профили ECU и обычные снимки DID не подтверждают физический CEM. Ревизия HW/SW, совместимость, PIN и доступ к конфигурации не проверены.";
        return state + Environment.NewLine + limit;
    }
}
