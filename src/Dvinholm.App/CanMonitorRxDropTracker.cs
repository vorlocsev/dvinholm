namespace Dvinholm.App;

/// <summary>
/// Tracks RX subscription queue drops across monitor restarts within a single
/// user-visible Clear epoch. All methods are called on the WinUI thread.
/// This never reads CAN payloads, opens hardware or transmits a frame.
/// </summary>
internal sealed class CanMonitorRxDropTracker
{
    private long _completedDrops;
    private long _baseline;
    private long _lastObserved;
    private bool _active;
    private bool _hasEvidence;

    internal void Start(long subscriptionDroppedFrames)
    {
        if (_active || subscriptionDroppedFrames < 0)
            throw new InvalidDataException("Invalid CAN RX subscription start.");

        _active = true;
        _hasEvidence = true;
        _baseline = subscriptionDroppedFrames;
        _lastObserved = subscriptionDroppedFrames;
    }

    /// <summary>
    /// Returns null if no subscription has been observed since Clear, or if
    /// the current subscription's counter cannot be sampled.
    /// </summary>
    internal long? Snapshot(long? currentSubscriptionDrops)
    {
        if (!_active)
            return _hasEvidence ? _completedDrops : null;

        if (currentSubscriptionDrops is not long current ||
            current < _lastObserved)
            return null;

        _lastObserved = current;
        var delta = current - _baseline;
        if (delta < 0 || _completedDrops > long.MaxValue - delta)
            return null;
        return _completedDrops + delta;
    }

    internal void Complete(long finalSubscriptionDrops)
    {
        if (!_active || Snapshot(finalSubscriptionDrops) is not long count)
            throw new InvalidDataException("Invalid final CAN RX drop count.");

        _completedDrops = count;
        _active = false;
    }

    /// <summary>
    /// Rebaseline a live subscription instead of incorrectly including drops
    /// from before the user's Clear action.
    /// </summary>
    internal void Clear(long? currentSubscriptionDrops)
    {
        if (_active)
        {
            if (Snapshot(currentSubscriptionDrops) is null)
                throw new InvalidDataException("Cannot rebaseline CAN RX drops.");

            _baseline = currentSubscriptionDrops!.Value;
            _lastObserved = _baseline;
            _completedDrops = 0;
        }
        else
        {
            _completedDrops = 0;
            _hasEvidence = false;
        }
    }
}
