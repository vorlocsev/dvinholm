namespace Dvinholm.App;

/// <summary>
/// UI-only lifetime token for an already supported read-only UDS request.
/// Field edits, disconnects and explicit clears invalidate pending output.
/// No CAN/J2534 access, result persistence or payload retention.
/// </summary>
internal sealed class UdsReadResultLifetime
{
    private long _generation;

    internal long Generation => _generation;

    internal long Invalidate() => ++_generation;

    internal bool CanDisplay(long generation) =>
        generation == _generation && generation > 0;
}
