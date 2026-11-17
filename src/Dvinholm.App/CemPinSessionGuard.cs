namespace Dvinholm.App;

/// <summary>
/// An operation lease, not proof of CEM identity or provider entitlement.
/// The context must be a fresh opaque object whenever the selected ECU,
/// addressing, identity evidence or owner consent changes. Never use a VIN.
/// </summary>
internal sealed class CemPinSessionGuard : IDisposable
{
    private readonly Func<bool> _isCurrent;
    private J2534Session? _session;
    private CanBus? _capturedBus;
    private long _capturedEpoch;
    private int _invalidated;

    // Synthetic test seam; production captures the existing J2534 session.
    internal CemPinSessionGuard(Func<bool> isCurrent) =>
        _isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));

    internal static CemPinSessionGuard Capture(
        J2534Session session, object ecuContext, Func<object?> currentEcuContext,
        bool ownerConsent)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(ecuContext);
        ArgumentNullException.ThrowIfNull(currentEcuContext);
        // Capture one coherent bus/epoch under the J2534 lifecycle gate.
        // A concurrent reconnect cannot produce a mismatched lease.
        var snapshot = session.CaptureConnectedBusSnapshot();
        var bus = snapshot?.Bus;
        var epoch = snapshot?.Epoch ?? -1L;
        var guard = new CemPinSessionGuard(() => ownerConsent && bus is { IsDisposed: false } &&
            session.IsConnected && session.ConnectionEpoch == epoch &&
            ReferenceEquals(session.Bus, bus) &&
            ReferenceEquals(currentEcuContext(), ecuContext));
        guard._session = session;
        guard._capturedBus = bus;
        guard._capturedEpoch = epoch;
        session.ReadErrorEpoch += guard.OnReadError;
        return guard;
    }

    private void OnReadError(long originatingEpoch)
    {
        // A late callback from a previous or later CAN bus must not poison
        // this lease or a different J2534 connection.
        if (originatingEpoch != _capturedEpoch) return;
        var session = _session;
        var capturedBus = _capturedBus;
        Interlocked.Exchange(ref _invalidated, 1);
        if (session is null || capturedBus is null || !session.IsConnected)
            return;
        try
        {
            if (ReferenceEquals(session.Bus, capturedBus))
                session.InvalidateTransport();
        }
        catch (InvalidOperationException)
        {
            // An intervening disconnect already invalidated this lease.
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _invalidated, 1);
        var session = Interlocked.Exchange(ref _session, null);
        Interlocked.Exchange(ref _capturedBus, null);
        if (session is not null) session.ReadErrorEpoch -= OnReadError;
    }

    internal bool IsCurrent
    {
        get
        {
            try
            {
                if (Volatile.Read(ref _invalidated) != 0) return false;
                if (!_isCurrent()) Interlocked.Exchange(ref _invalidated, 1);
                return Volatile.Read(ref _invalidated) == 0;
            }
            catch (Exception)
            {
                Interlocked.Exchange(ref _invalidated, 1);
                return false; // No private callback/driver text.
            }
        }
    }
}
