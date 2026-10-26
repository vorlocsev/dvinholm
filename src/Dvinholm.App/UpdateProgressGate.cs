namespace Dvinholm.App;

/// <summary>
/// UI-thread-owned epoch for updater progress. Background progress callbacks
/// are queued to the WinUI dispatcher; the epoch must only be inspected from
/// that dispatcher. Late callbacks from a completed or superseded attempt are
/// never allowed to reactivate the progress bar or overwrite an error.
/// </summary>
public sealed class UpdateProgressGate
{
    private long _epoch;
    private bool _active;

    public long Begin()
    {
        // A new attempt revokes callbacks from every previous attempt.
        _epoch++;
        _active = true;
        return _epoch;
    }

    public bool Accepts(long epoch) => _active && epoch == _epoch;

    public void Complete(long epoch)
    {
        // A stale completion cannot disable an active newer attempt.
        if (Accepts(epoch))
            _active = false;
    }
}
