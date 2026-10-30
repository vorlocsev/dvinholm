namespace Dvinholm.App;

/// <summary>
/// Fixed, payload-free activity for passive ECU discovery. The global status
/// is copied to Aurora's home screen, so it must never include exception
/// messages, driver paths, CAN payloads, inferred request IDs or VINs.
/// Detailed observations stay in the dedicated discovery controls.
/// </summary>
internal enum UdsPassiveDiscoveryOutcome
{
    Completed,
    CancelledWithObservations,
    IncompleteWithDrops,
    CancelledBeforeStart,
    Failed
}

internal static class UdsPassiveDiscoveryActivity
{
    internal static string Message(UdsPassiveDiscoveryOutcome outcome) => outcome switch
    {
        UdsPassiveDiscoveryOutcome.Completed =>
            "Passive ECU scan complete. Observations are not verified module identities.",
        UdsPassiveDiscoveryOutcome.CancelledWithObservations =>
            "Passive ECU scan cancelled. Partial observations are not selectable; no ECU identity verified.",
        UdsPassiveDiscoveryOutcome.IncompleteWithDrops =>
            "Passive ECU scan incomplete due to receive-queue drops. Partial observations are not selectable.",
        UdsPassiveDiscoveryOutcome.CancelledBeforeStart =>
            "Passive ECU scan cancelled before observations were available.",
        UdsPassiveDiscoveryOutcome.Failed =>
            "Passive ECU scan failed. Check the adapter connection; no ECU identity verified.",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };
}
