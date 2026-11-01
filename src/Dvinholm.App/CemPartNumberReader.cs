using System.Diagnostics;

namespace Dvinholm.App;

internal sealed record CemPartNumberObservation(uint PartNumber)
{
    internal string Source => CemPartNumberReader.SourceReference;
    internal bool PhysicalRevisionVerified => false;
    internal bool PinRetrievalSupported => false;
    public override string ToString() => "Experimental CEM part-number observation (value withheld)";
}

internal sealed class CemPartNumberCleanupException : Exception
{
    internal CemPartNumberCleanupException()
        : base("CEM receive-filter cleanup failed; reconnect the adapter before further diagnostics.") { }
}

/// <summary>
/// One source-defined normal-mode P/N request on an explicitly configured
/// 500 kbit/s, 29-bit channel. No probing, retries, K-line, programming mode,
/// resets, PIN attempts or software-identity claim. Caller owns the shared
/// diagnostic gate and confirms the experimental route before calling.
/// </summary>
internal static class CemPartNumberReader
{
    internal const string SourceReference =
        "https://github.com/vtl/volvo-cem-cracker/blob/2d5de865d21094889a6c1c689797d5a636e909fc/volvo-cem-cracker.ino#L455-L507";
    private const uint RequestId = 0xFFFFE;
    private static readonly byte[] Request = { 0xCB, 0x50, 0xB9, 0xF0, 0, 0, 0, 0 };

    internal static CemPartNumberObservation Read(
        J2534Session session, CemPinSessionGuard guard, bool routeApproved,
        bool hardwareFilterActive = false, TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(guard);
        cancellationToken.ThrowIfCancellationRequested();
        if (timeout == default) timeout = TimeSpan.FromSeconds(2);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (!routeApproved || hardwareFilterActive || !session.IsConnected ||
            session.ConnectedBaudRate != 500_000 || session.ConnectedExtended != true ||
            session.AdapterCapabilities?.HardwareCanFilters != true || !guard.IsCurrent)
            throw new InvalidOperationException("Experimental CEM read prerequisites are not satisfied.");

        var bus = session.Bus;
        try
        {
            var result = ReadCore(bus, guard, timeout, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!guard.IsCurrent) throw new InvalidOperationException("CEM read session was invalidated.");
            return result;
        }
        catch (CemPartNumberCleanupException)
        {
            session.InvalidateTransport();
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("CEM part-number read cancelled.", cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException("No complete source-compatible CEM part-number reply arrived before the deadline.");
        }
        catch (Exception)
        {
            // Never retain or expose private vendor errors or raw response bytes.
            throw new InvalidOperationException("Experimental CEM part-number read failed or its session was invalidated.");
        }
    }

    private static CemPartNumberObservation ReadCore(
        CanBus bus, CemPinSessionGuard guard, TimeSpan timeout, CancellationToken token)
    {
        var elapsed = Stopwatch.StartNew();
        void CheckSession()
        {
            token.ThrowIfCancellationRequested();
            if (!guard.IsCurrent || bus.IsDisposed)
                throw new InvalidOperationException("CEM read session is no longer current.");
        }
        CheckSession();
        using var rx = bus.Subscribe(capacity: 32, filter: frame =>
            frame.IsExtended && !frame.IsRemote && frame.Data is { Length: 8 } &&
            (frame.ArbitrationId & 0xFFFF) == 3);
        if (!bus.StartCanMaskFilter(0xFFFF, 3, true, out var filterId, out _))
            throw new InvalidOperationException("The adapter rejected the required masked receive filter.");
        try
        {
            CheckSession();
            rx.DrainPendingFrames();
            var remaining = timeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero) throw new TimeoutException();
            if (!bus.Send(RequestId, Request, true, remaining, token, out _))
                throw new InvalidOperationException("CEM part-number request transmission failed.");

            uint? prefix = null;
            uint prefixId = 0;
            while (elapsed.Elapsed < timeout)
            {
                CheckSession();
                if (rx.DroppedFrameCount != 0)
                    throw new InvalidOperationException("CEM reply queue overflowed.");
                remaining = timeout - elapsed.Elapsed;
                if (remaining <= TimeSpan.Zero) break;
                if (!rx.TryRead(TimeSpan.FromMilliseconds(Math.Min(50, remaining.TotalMilliseconds)), token, out var frame))
                    continue;
                var bytes = frame.Data;
                if ((bytes[0] & 0x80) != 0)
                {
                    // A new first frame replaces an incomplete observation.
                    prefix = null;
                    if (TryBcd(bytes[5], out var a) && TryBcd(bytes[6], out var b) && TryBcd(bytes[7], out var c))
                    {
                        prefix = a * 10_000 + b * 100 + c;
                        prefixId = frame.ArbitrationId;
                    }
                }
                else if (prefix is uint first && prefixId == frame.ArbitrationId && (bytes[0] & 0x40) == 0)
                {
                    prefix = null;
                    if (!TryBcd(bytes[1], out var last)) continue;
                    var partNumber = first * 100 + last;
                    if (partNumber is < 1_000_000 or > 99_999_999) continue;
                    CheckSession();
                    if (rx.DroppedFrameCount != 0)
                        throw new InvalidOperationException("CEM reply queue overflowed.");
                    return new CemPartNumberObservation(partNumber);
                }
            }
            CheckSession();
            throw new TimeoutException();
        }
        finally
        {
            if (!bus.IsDisposed)
            {
                try
                {
                    if (!bus.StopCanFilter(filterId, out _)) throw new CemPartNumberCleanupException();
                }
                catch (Exception) { throw new CemPartNumberCleanupException(); }
            }
        }
    }

    private static bool TryBcd(byte value, out uint decoded)
    {
        decoded = 0;
        if ((value & 15) > 9 || (value >> 4) > 9) return false;
        decoded = (uint)((value >> 4) * 10 + (value & 15));
        return true;
    }
}
