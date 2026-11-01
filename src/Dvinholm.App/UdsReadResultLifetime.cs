namespace Dvinholm.App;

/// <summary>
/// Thread-safe lifetime token shared by UI and read-only diagnostic workers.
/// Field edits, disconnects and explicit clears invalidate pending output.
/// No CAN/J2534 access, result persistence or payload retention.
/// </summary>
internal sealed class UdsReadResultLifetime
{
    private long _generation;

    internal long Generation => Interlocked.Read(ref _generation);

    internal long Invalidate() => Interlocked.Increment(ref _generation);

    internal bool CanDisplay(long generation) =>
        generation == Interlocked.Read(ref _generation) && generation > 0;
}
