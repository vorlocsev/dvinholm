using System.Threading;

namespace Dvinholm.App;

/// <summary>
/// Public lifecycle metadata. Candidate material is separately owned and
/// expires; no result claims authenticated physical CEM support.
/// </summary>
internal enum CemPinAttemptStatus
{
    Unsupported,
    AlreadyRunning,
    Cancelled,
    SessionInvalidated,
    TimedOut,
    Failed,
    FinishedWithoutPin,
    CandidateAccepted
}

internal sealed record CemPinAttemptResult(CemPinAttemptStatus Status, string PublicMessage) : IDisposable
{
    private CemPinSecret? _pin;
    private Func<bool>? _currentLease;

    // A candidate can be accepted and then outlive the caller's current CAN,
    // consent or cancellation context. Revoke the sensitive payload itself
    // instead of relying solely on a UI timer or an external result guard.
    // Stored/vault results are intentionally separate and have no live lease.
    private bool LeaseCurrent()
    {
        if (_currentLease is null) return true;
        try
        {
            if (_currentLease()) return true;
        }
        catch (Exception) { /* Never disclose private lease callback details. */ }

        Dispose();
        return false;
    }

    internal bool HasPin => LeaseCurrent() && Volatile.Read(ref _pin)?.IsAvailable == true;
    internal CemPinResultOrigin? Origin =>
        LeaseCurrent() ? Volatile.Read(ref _pin)?.Origin : null;
    internal bool PhysicalAcceptanceVerified => false;
    internal bool MayWriteEcu => false;
    internal bool TryReveal(CemPinReveal reveal)
    {
        if (!LeaseCurrent()) return false;
        var pin = Volatile.Read(ref _pin);
        if (pin?.TryReveal(reveal) != true) return false;
        // A lease may have been revoked from another thread during the
        // reveal callback. Report success only if it remained valid.
        return LeaseCurrent();
    }

    internal static CemPinAttemptResult Accepted(
        CemPinSecret pin, Func<bool>? currentLease = null) =>
        new(CemPinAttemptStatus.CandidateAccepted,
            "A candidate was accepted twice by the current adapter session; physical provenance remains unverified.")
        { _pin = pin, _currentLease = currentLease };
    internal static CemPinAttemptResult Restored(CemPinSecret pin) =>
        new(CemPinAttemptStatus.CandidateAccepted,
            "Saved PIN record; the current vehicle and service context have not been checked.") { _pin = pin };
    public void Dispose() => Interlocked.Exchange(ref _pin, null)?.Dispose();
    public override string ToString() => $"{Status}: {PublicMessage}";

    internal static CemPinAttemptResult From(CemPinAttemptStatus status) => status switch
    {
        CemPinAttemptStatus.Unsupported => new(status,
            "No validated CEM PIN provider and physical service authorization are available."),
        CemPinAttemptStatus.AlreadyRunning => new(status,
            "A CEM PIN operation is already active."),
        CemPinAttemptStatus.Cancelled => new(status,
            "CEM PIN operation cancelled; no PIN result."),
        CemPinAttemptStatus.SessionInvalidated => new(status,
            "CEM PIN operation stopped: consent, connection or ECU context is no longer valid."),
        CemPinAttemptStatus.TimedOut => new(status,
            "CEM PIN provider deadline expired; no PIN result."),
        CemPinAttemptStatus.Failed => new(status,
            "CEM PIN operation failed; provider details and private data are withheld."),
        CemPinAttemptStatus.FinishedWithoutPin => new(status,
            "Provider finished without a verified PIN result."),
        _ => new(CemPinAttemptStatus.Failed,
            "CEM PIN operation failed; provider details and private data are withheld.")
    };
}

/// <summary>
/// Public metadata channel. Confidential material, if produced, transfers
/// separately through ICemPinCandidateSource and never enters a receipt.
/// </summary>
internal enum CemPinProviderReceipt
{
    Unsupported,
    FinishedWithoutPin,
    CandidateAccepted
}

internal interface ICemPinAuthorizedProvider
{
    Task<CemPinProviderReceipt> TryRunAsync(CancellationToken cancellationToken);
}

internal interface ICemPinCandidateSource
{
    // Single ownership transfer, including on cancellation/failure cleanup.
    CemPinSecret? TakeCandidate();
}

/// <summary>
/// Cancellable, single-flight coordinator. The production constructor has
/// NO provider and uses the existing fail-closed CEM capability matrix.
/// Development has an explicit source-experiment factory; the default
/// production registry still denies acquisition.
/// </summary>
internal sealed class CemPinAttemptCoordinator
{
    private readonly ICemPinAuthorizedProvider? _provider;
    private readonly Func<VolvoP2CemServiceResearchContext, bool> _authorized;
    private int _running;

    internal CemPinAttemptCoordinator()
        : this(null, context => VolvoP2CemServiceCapabilityMatrix.Assess(
            VolvoP2CemServiceOperation.ReviewPinAcquisition, context).MayAcquirePin)
    {
    }

    // Internal injection seam shared by tests and the explicit Development
    // factory. A delegate cannot authenticate physical ECU provenance.
    internal CemPinAttemptCoordinator(
        ICemPinAuthorizedProvider? provider,
        Func<VolvoP2CemServiceResearchContext, bool> authorized)
    {
        _provider = provider;
        _authorized = authorized ?? throw new ArgumentNullException(nameof(authorized));
    }

    internal bool IsRunning => Volatile.Read(ref _running) != 0;

#if DVINHOLM_DEVELOPMENT_FEATURES
    // Explicit experimental source route; research strings cannot select it.
    // The provider independently rechecks transport, consent, P/N and replies.
    internal static CemPinAttemptCoordinator ForSourceExperiment(ExperimentalCemPinCanProvider provider) =>
        new(provider, _ => true);
#endif

    internal async Task<CemPinAttemptResult> TryRunAsync(
        VolvoP2CemServiceResearchContext context,
        CancellationToken cancellationToken = default,
        CemPinSessionGuard? sessionGuard = null,
        TimeSpan timeout = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (cancellationToken.IsCancellationRequested)
            return CemPinAttemptResult.From(CemPinAttemptStatus.Cancelled);

        // Bound the deadline to a supported timer range before any provider call.
        if (timeout == default) timeout = TimeSpan.FromMinutes(2);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(timeout));

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            return CemPinAttemptResult.From(CemPinAttemptStatus.AlreadyRunning);

        try
        {
            // Gate evaluation is inside the single-flight/error boundary too.
            // Editable evidence cannot grant production service entitlement.
            if (_provider is null || !_authorized(context))
                return CemPinAttemptResult.From(CemPinAttemptStatus.Unsupported);
            if (sessionGuard?.IsCurrent != true)
                return CemPinAttemptResult.From(CemPinAttemptStatus.SessionInvalidated);

            cancellationToken.ThrowIfCancellationRequested();
            using var deadline = new CancellationTokenSource(timeout);
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, deadline.Token);
            using var stopMonitor = new CancellationTokenSource();
            var invalidated = 0;
            async Task MonitorAsync()
            {
                try
                {
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(50), stopMonitor.Token)
                            .ConfigureAwait(false);
                        if (!sessionGuard.IsCurrent)
                        {
                            Interlocked.Exchange(ref invalidated, 1);
                            operation.Cancel();
                            return;
                        }
                    }
                }
                catch (OperationCanceledException) when (stopMonitor.IsCancellationRequested) { }
            }
            var monitor = MonitorAsync();
            CemPinAttemptStatus? StopReason() => cancellationToken.IsCancellationRequested
                ? CemPinAttemptStatus.Cancelled
                : Volatile.Read(ref invalidated) != 0 || !sessionGuard.IsCurrent
                    ? CemPinAttemptStatus.SessionInvalidated
                    : deadline.IsCancellationRequested ? CemPinAttemptStatus.TimedOut : null;
            CemPinAttemptStatus completion;
            try
            {
                // The first guard check happened before constructing the deadline,
                // linked token and monitor. The ECU/consent context can change in
                // that gap, including a connection replacement. Recheck directly
                // before calling a provider; it must never be invoked if that
                // context has already become stale. This is not an atomic native
                // I/O lock: the provider must keep checking its own lease.
                if (!sessionGuard.IsCurrent)
                    return CemPinAttemptResult.From(CemPinAttemptStatus.SessionInvalidated);
                cancellationToken.ThrowIfCancellationRequested();
                // Await provider termination even after cancellation. Never release
                // the gate while an uncooperative provider may still be using ECU.
                var receipt = await _provider.TryRunAsync(operation.Token).ConfigureAwait(false);
                completion = receipt switch
                {
                    CemPinProviderReceipt.FinishedWithoutPin => CemPinAttemptStatus.FinishedWithoutPin,
                    CemPinProviderReceipt.CandidateAccepted when _provider is ICemPinCandidateSource => CemPinAttemptStatus.CandidateAccepted,
                    _ => CemPinAttemptStatus.Unsupported
                };
            }
            catch (OperationCanceledException) when (operation.IsCancellationRequested)
            {
                completion = CemPinAttemptStatus.Failed;
            }
            catch (TimeoutException)
            {
                completion = CemPinAttemptStatus.TimedOut;
            }
            catch (Exception)
            {
                completion = CemPinAttemptStatus.Failed;
            }
            finally
            {
                stopMonitor.Cancel();
                await monitor.ConfigureAwait(false);
            }
            // Revalidate after monitor teardown, immediately before publishing.
            var stopped = StopReason();
            if (stopped is null && completion == CemPinAttemptStatus.CandidateAccepted &&
                _provider is ICemPinCandidateSource source && source.TakeCandidate() is { } pin)
            {
                stopped = StopReason();
                if (stopped is null && pin.IsAvailable)
                {
                    // Keep the returned secret revocable after this method
                    // releases the single-flight gate. A late disconnect,
                    // context change or caller cancellation must erase it
                    // on the next result access, not allow an old reveal.
                    var operationToken = operation.Token;
                    return CemPinAttemptResult.Accepted(pin, () =>
                        !cancellationToken.IsCancellationRequested &&
                        !operationToken.IsCancellationRequested &&
                        sessionGuard.IsCurrent);
                }
                pin.Dispose();
            }
            return CemPinAttemptResult.From(stopped ??
                (completion == CemPinAttemptStatus.CandidateAccepted ? CemPinAttemptStatus.Failed : completion));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CemPinAttemptResult.From(CemPinAttemptStatus.Cancelled);
        }
        catch (Exception)
        {
            // Never log, echo, or persist provider exception messages.
            return CemPinAttemptResult.From(CemPinAttemptStatus.Failed);
        }
        finally
        {
            try
            {
                if (_provider is ICemPinCandidateSource source) source.TakeCandidate()?.Dispose();
            }
            catch (Exception) { /* Neither private provider text nor cleanup faults escape. */ }
            finally { Volatile.Write(ref _running, 0); }
        }
    }
}
