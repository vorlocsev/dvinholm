using System.Diagnostics;
using System.Security.Cryptography;

namespace Dvinholm.App;

internal enum CemPinSourceStage { CheckingContext, CheckingCandidates, Confirming, Finished }
internal enum CemPinSourceFailure { None, Prerequisites, ServiceContextUnavailable, UnknownHardware, UnexpectedReply, Transport }

/// <summary>
/// Development source experiment, not physical acceptance. Uses an already
/// available service context; never enters programming mode or resets ECUs.
/// Caller owns the existing diagnostic gate and stops capture before use.
/// </summary>
internal sealed class ExperimentalCemPinCanProvider : ICemPinAuthorizedProvider, ICemPinCandidateSource
{
    internal const string SourceReference =
        "https://github.com/vtl/volvo-cem-cracker/blob/2d5de865d21094889a6c1c689797d5a636e909fc/volvo-cem-cracker.ino";
    internal const ulong MaximumBatch = 10_000;
    private readonly J2534Session _session;
    private readonly CemPinSessionGuard _guard;
    private readonly uint _expectedPartNumber;
    private readonly CemPinCandidateRange _range;
    private readonly bool _approved;
    private readonly CemPinResultOrigin _origin;
    private readonly TimeSpan _replyTimeout;
    private CemPinSecret? _candidate;
    private int _stage;
    internal CemPinSourceStage Stage => (CemPinSourceStage)Volatile.Read(ref _stage);
    internal CemPinSourceFailure Failure { get; private set; }

    // Factual P2 L/H source entries sharing order 1, excluding the ambiguous
    // 30682982 and all P1/early P2 entries. No MY2009/V8 fitment is implied.
    internal static bool HasSourceProfile(uint partNumber) => partNumber is
        30682981 or 30728356 or 30728542 or 30765149 or 30765646 or 30786475 or
        30786889 or 31282457 or 31314468 or 31394158 or 30786476 or 30728539 or
        30728357 or 30765148 or 30765643 or 30786890 or 30795115 or 31282455 or
        31394157 or 30786579;

    internal ExperimentalCemPinCanProvider(J2534Session session, CemPinSessionGuard guard,
        uint expectedPartNumber, ulong firstOrdinal, ulong count, bool approved,
        CemPinResultOrigin origin, TimeSpan replyTimeout = default)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _expectedPartNumber = expectedPartNumber;
        if (count is 0 or > MaximumBatch) throw new ArgumentOutOfRangeException(nameof(count));
        _range = new(firstOrdinal, count);
        _approved = approved;
        if (!Enum.IsDefined(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
        _origin = origin;
        if (replyTimeout == default) replyTimeout = TimeSpan.FromSeconds(2);
        if (replyTimeout <= TimeSpan.Zero || replyTimeout > TimeSpan.FromSeconds(2))
            throw new ArgumentOutOfRangeException(nameof(replyTimeout));
        _replyTimeout = replyTimeout;
    }

    public CemPinSecret? TakeCandidate() => Interlocked.Exchange(ref _candidate, null);
    public Task<CemPinProviderReceipt> TryRunAsync(CancellationToken token) =>
        Task.Factory.StartNew(() => Run(token), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private CemPinProviderReceipt Run(CancellationToken token)
    {
        TakeCandidate()?.Dispose();
        Failure = CemPinSourceFailure.Prerequisites;
        token.ThrowIfCancellationRequested();
        if (!_approved || !_guard.IsCurrent || !_session.IsConnected ||
            _session.ConnectedBaudRate != 500_000 || _session.ConnectedExtended != true ||
            _session.AdapterCapabilities?.HardwareCanFilters != true)
            return CemPinProviderReceipt.Unsupported;
        if (!HasSourceProfile(_expectedPartNumber))
        {
            Failure = CemPinSourceFailure.UnknownHardware;
            return CemPinProviderReceipt.Unsupported;
        }
        var bus = _session.Bus;
        var request = new byte[8];
        var canonical = new byte[6];
        var wire = new byte[6];
        CemPinSecret? found = null;
        try
        {
            using var rx = bus.Subscribe(64, frame => frame.IsExtended && !frame.IsRemote &&
                frame.Data is { Length: 8 } && (frame.ArbitrationId & 0xFFFF) == 3);
            Check();
            if (!bus.StartCanMaskFilter(0xFFFF, 3, true, out var filterId, out _))
                throw new InvalidOperationException("Source receive filter unavailable.");
            try
            {
                Volatile.Write(ref _stage, (int)CemPinSourceStage.CheckingContext);
                Failure = CemPinSourceFailure.ServiceContextUnavailable;
                request[0] = 0x50;
                request[1] = 0x88; // Source service-context identification, not mode entry.
                Send();
                var identity = Receive();
                ulong number = 0;
                // The source establishes only BCD bytes 2..7 for this reply;
                // do not invent an identification response opcode.
                for (int i = 2; i < 8; i++)
                {
                    var b = identity.Data[i];
                    if ((b & 15) > 9 || (b >> 4) > 9) return NoContext();
                    number = number * 100 + (uint)((b >> 4) * 10 + (b & 15));
                }
                if (number != _expectedPartNumber) return NoContext();
                var responseId = identity.ArbitrationId;
                Failure = CemPinSourceFailure.None;
                Volatile.Write(ref _stage, (int)CemPinSourceStage.CheckingCandidates);
                request[1] = 0xBE;
                byte? rejection = null;
                while (_range.TryNext(canonical, token))
                {
                    Check();
                    // Source order 1, independently encoded; no timing attack.
                    wire[3] = canonical[0]; wire[1] = canonical[1]; wire[5] = canonical[2];
                    wire[0] = canonical[3]; wire[2] = canonical[4]; wire[4] = canonical[5];
                    wire.CopyTo(request, 2);
                    Send();
                    var status = ReadAck(responseId);
                    if (status != 0)
                    {
                        if (rejection is byte previous && previous != status)
                            throw BadReply(); // Changed rejection may indicate lockout/context loss.
                        rejection = status;
                        continue;
                    }
                    Volatile.Write(ref _stage, (int)CemPinSourceStage.Confirming);
                    Send(); // Same candidate, separate response; never trust a single acceptance.
                    if (ReadAck(responseId) != 0) throw BadReply();
                    Check();
                    found = new CemPinSecret(wire.ToArray(), _origin);
                    break;
                }
            }
            finally
            {
                if (!bus.IsDisposed)
                {
                    try
                    {
                        if (!bus.StopCanFilter(filterId, out _)) throw new CemPartNumberCleanupException();
                    }
                    catch (Exception)
                    {
                        _session.InvalidateTransport();
                        throw new CemPartNumberCleanupException();
                    }
                }
            }
            Check();
            Volatile.Write(ref _stage, (int)CemPinSourceStage.Finished);
            if (found is null) return CemPinProviderReceipt.FinishedWithoutPin;
            _candidate = found;
            found = null;
            return CemPinProviderReceipt.CandidateAccepted;

            void Check()
            {
                token.ThrowIfCancellationRequested();
                if (!_guard.IsCurrent || bus.IsDisposed)
                    throw new InvalidOperationException("Source session invalidated.");
            }
            void Send()
            {
                Check();
                rx.DrainPendingFrames();
                if (rx.DroppedFrameCount != 0) throw BadReply();
                if (!bus.Send(0xFFFFE, request, true, _replyTimeout, token, out _))
                    throw new InvalidOperationException("Source transmission failed.");
            }
            CanFrame Receive()
            {
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < _replyTimeout)
                {
                    Check();
                    if (rx.DroppedFrameCount != 0) throw BadReply();
                    var remaining = _replyTimeout - watch.Elapsed;
                    if (remaining <= TimeSpan.Zero) break;
                    if (rx.TryRead(TimeSpan.FromMilliseconds(Math.Min(50, remaining.TotalMilliseconds)), token, out var frame))
                        return frame;
                }
                throw new TimeoutException("Source reply deadline expired; the search is stopped.");
            }
            byte ReadAck(uint expectedId)
            {
                var reply = Receive();
                // Fail closed on tails not established by this strict route.
                // This also excludes late P/N replies from PIN acceptance.
                if (reply.ArbitrationId != expectedId || reply.Data[0] != 0x50 || reply.Data[1] != 0xB9 ||
                    reply.Data.AsSpan(3).ContainsAnyExcept((byte)0)) throw BadReply();
                Check();
                return reply.Data[2];
            }
            CemPinProviderReceipt NoContext()
            {
                Failure = CemPinSourceFailure.ServiceContextUnavailable;
                return CemPinProviderReceipt.Unsupported;
            }
            Exception BadReply()
            {
                Failure = CemPinSourceFailure.UnexpectedReply;
                return new InvalidOperationException("Source response shape or status changed; the search is stopped.");
            }
        }
        finally
        {
            found?.Dispose();
            CryptographicOperations.ZeroMemory(request);
            CryptographicOperations.ZeroMemory(canonical);
            CryptographicOperations.ZeroMemory(wire);
        }
    }

    public override string ToString() => "Experimental source PIN provider (sensitive state withheld)";
}
