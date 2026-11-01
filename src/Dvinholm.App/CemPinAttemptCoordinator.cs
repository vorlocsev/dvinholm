using System.Threading;

namespace Dvinholm.App;

/// <summary>
/// Non-secret lifecycle outcomes for a future explicitly authorized CEM PIN
/// provider. None of these outcomes contains or proves possession of a PIN.
/// </summary>
internal enum CemPinAttemptStatus
{
    Unsupported,
    AlreadyRunning,
    Cancelled,
    Failed,
    FinishedWithoutPin
}

internal sealed record CemPinAttemptResult(CemPinAttemptStatus Status, string PublicMessage)
{
    internal bool HasPin => false;
    internal bool MayWriteEcu => false;

    internal static CemPinAttemptResult From(CemPinAttemptStatus status) => status switch
    {
        CemPinAttemptStatus.Unsupported => new(status,
            "No validated CEM PIN provider and physical service authorization are available."),
        CemPinAttemptStatus.AlreadyRunning => new(status,
            "A CEM PIN operation is already active."),
        CemPinAttemptStatus.Cancelled => new(status,
            "CEM PIN operation cancelled; no PIN result."),
        CemPinAttemptStatus.Failed => new(status,
            "CEM PIN operation failed; provider details and private data are withheld."),
        CemPinAttemptStatus.FinishedWithoutPin => new(status,
            "Provider finished without a verified PIN result."),
        _ => new(CemPinAttemptStatus.Failed,
            "CEM PIN operation failed; provider details and private data are withheld.")
    };
}

/// <summary>
/// A future explicitly registered, approved provider boundary. The interface
/// deliberately cannot return a PIN, seed, key, ECU dump or sensitive bytes:
/// secure PIN delivery requires a separate reviewed design and hardware proof.
/// </summary>
internal enum CemPinProviderReceipt
{
    Unsupported,
    FinishedWithoutPin
}

internal interface ICemPinAuthorizedProvider
{
    Task<CemPinProviderReceipt> TryRunAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Cancellable, single-flight coordinator. The production constructor has
/// NO provider and uses the existing fail-closed CEM capability matrix.
/// An injected gate/provider is solely a unit-test seam, not connected to UI,
/// ECU, J2534 or any production provider registry.
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

    // Only deterministic fake providers may be supplied at this internal seam.
    // It cannot authenticate a real ECU and is never used by production UI.
    internal CemPinAttemptCoordinator(
        ICemPinAuthorizedProvider? provider,
        Func<VolvoP2CemServiceResearchContext, bool> authorized)
    {
        _provider = provider;
        _authorized = authorized ?? throw new ArgumentNullException(nameof(authorized));
    }

    internal bool IsRunning => Volatile.Read(ref _running) != 0;

    internal async Task<CemPinAttemptResult> TryRunAsync(
        VolvoP2CemServiceResearchContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (cancellationToken.IsCancellationRequested)
            return CemPinAttemptResult.From(CemPinAttemptStatus.Cancelled);

        // Fail closed before even invoking a provider: editable evidence text,
        // a synthetic DID match and an apparently correct VIN are not proof.
        // Do not pass this context or any secret to the provider.
        if (_provider is null || !_authorized(context))
            return CemPinAttemptResult.From(CemPinAttemptStatus.Unsupported);

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            return CemPinAttemptResult.From(CemPinAttemptStatus.AlreadyRunning);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var receipt = await _provider.TryRunAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return receipt == CemPinProviderReceipt.FinishedWithoutPin
                ? CemPinAttemptResult.From(CemPinAttemptStatus.FinishedWithoutPin)
                : CemPinAttemptResult.From(CemPinAttemptStatus.Unsupported);
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
            Volatile.Write(ref _running, 0);
        }
    }
}
