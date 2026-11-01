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
        // Capture once. Reconnecting must never revive an old lease.
        CanBus? bus;
        try { bus = session.IsConnected ? session.Bus : null; }
        catch (InvalidOperationException) { bus = null; }
        var guard = new CemPinSessionGuard(() => ownerConsent && bus is { IsDisposed: false } &&
            session.IsConnected && ReferenceEquals(session.Bus, bus) &&
            ReferenceEquals(currentEcuContext(), ecuContext));
        guard._session = session;
        session.ReadError += guard.OnReadError;
        return guard;
    }

    private void OnReadError(Exception _) => Interlocked.Exchange(ref _invalidated, 1);

    public void Dispose()
    {
        Interlocked.Exchange(ref _invalidated, 1);
        var session = Interlocked.Exchange(ref _session, null);
        if (session is not null) session.ReadError -= OnReadError;
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
