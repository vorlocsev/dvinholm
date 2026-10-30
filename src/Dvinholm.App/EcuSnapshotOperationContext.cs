namespace Dvinholm.App;

/// <summary>
/// Keeps an in-flight read-only ECU DID snapshot bound to the same selected
/// profile and diagnostic UI generation it had at consent time.
/// Pure UI gating: no J2534 or file I/O.
/// </summary>
internal static class EcuSnapshotOperationContext
{
    internal static bool IsCurrent(
        EcuProfile? startedProfile,
        EcuProfile? selectedProfile,
        long startedGeneration,
        long currentGeneration,
        bool addressAndSessionStillValid) =>
        startedProfile is not null &&
        ReferenceEquals(startedProfile, selectedProfile) &&
        startedGeneration > 0 &&
        currentGeneration == startedGeneration &&
        addressAndSessionStillValid;
}
