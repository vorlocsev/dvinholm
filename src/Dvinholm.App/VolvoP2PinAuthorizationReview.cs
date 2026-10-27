namespace Dvinholm.App;

/// <summary>
/// Stages of an OFFLINE readiness checklist, NOT stages of an actual
/// CEM/PIN search, immobilizer service, or diagnostic programming task.
/// </summary>
internal enum VolvoP2PinPreviewState
{
    Reviewing,
    ReviewFinishedUnsupported,
    Cancelled
}

internal sealed record VolvoP2PinReviewStep(
    string Title,
    string Requirement,
    string CurrentStatus);

/// <summary>
/// Deliberately no adapter, UDS, credentials, VIN or ECU connection here.
/// A finished checklist is still UNSUPPORTED and cannot authorize a PIN
/// lookup, PIN guessing, service access or configuration modification.
/// </summary>
internal sealed class VolvoP2PinAuthorizationReview
{
    private static readonly IReadOnlyList<VolvoP2PinReviewStep> Checklist =
        Array.AsReadOnly(new[]
        {
            new VolvoP2PinReviewStep(
                "P2/CEM identity evidence",
                "Confirm XC90 model year, CEM hardware/software revision and " +
                "independently documented identity matching.",
                "Unknown: no validated production XC90 P2 CEM fingerprints."),
            new VolvoP2PinReviewStep(
                "Transport and adapter evidence",
                "Independently verify actual CAN/K-Line network, J2534 interface " +
                "capabilities and read-only communication with the specific CEM.",
                "Unknown: no validated physical CEM/adapter acceptance."),
            new VolvoP2PinReviewStep(
                "Legitimate service route",
                "Document the model-specific permitted PIN or authorized service " +
                "procedure and its supported prerequisites.",
                "Unavailable: no approved P2 PIN procedure or extraction algorithm."),
            new VolvoP2PinReviewStep(
                "Credential scope and explicit consent",
                "Separate configuration service authorization from immobilizer " +
                "secrets, verify entitlement and require consent before real use.",
                "Unavailable: no credential collection, PIN entry or service action.")
        });

    private int _index;
    internal VolvoP2PinPreviewState State { get; private set; } =
        VolvoP2PinPreviewState.Reviewing;

    internal int StepNumber => _index + 1;
    internal int TotalSteps => Checklist.Count;
    internal VolvoP2PinReviewStep CurrentStep => Checklist[_index];

    // The checklist is exclusively explanatory, including after completion.
    internal bool CanStartPinSearch => false;
    internal bool CanUseServiceCredentials => false;
    internal bool CanAccessImmobilizerSecrets => false;
    internal bool CanTransmitToEcu => false;
    internal bool CanEditOrWriteConfiguration => false;
    internal bool HasPinResult => false;
    internal bool HasEstimatedPinSearchTime => false;

    /// <summary>
    /// Advance to the next *explanatory* stage. Return false at the end,
    /// without implying a real authorization or PIN search succeeded.
    /// </summary>
    internal bool Advance()
    {
        if (State != VolvoP2PinPreviewState.Reviewing)
            return false;
        if (_index + 1 >= Checklist.Count)
        {
            State = VolvoP2PinPreviewState.ReviewFinishedUnsupported;
            return false;
        }

        _index++;
        return true;
    }

    internal void Cancel()
    {
        if (State == VolvoP2PinPreviewState.Reviewing)
            State = VolvoP2PinPreviewState.Cancelled;
    }

    internal string DescribeCurrent()
    {
        if (State == VolvoP2PinPreviewState.Cancelled)
            return "OFFLINE REVIEW CANCELLED. No PIN search or ECU operation was running.";

        if (State == VolvoP2PinPreviewState.ReviewFinishedUnsupported)
            return "OFFLINE CHECKLIST FINISHED: PIN and service authorization still " +
                "UNSUPPORTED. No credentials, results, vehicle requests or writes.";

        var step = CurrentStep;
        return $"Offline readiness review: step {StepNumber}/{TotalSteps}" +
            Environment.NewLine + Environment.NewLine +
            step.Title + Environment.NewLine +
            "Required evidence: " + step.Requirement + Environment.NewLine +
            "Current state: " + step.CurrentStatus + Environment.NewLine +
            Environment.NewLine +
            "PIN lookup: DISABLED. Estimated PIN-search time: unavailable." +
            Environment.NewLine +
            "This is a checklist, not search progress. No J2534/UDS request, " +
            "PIN attempt, credential entry, or ECU write occurs.";
    }
}
