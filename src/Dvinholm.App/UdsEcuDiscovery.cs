using System.Diagnostics;

namespace Dvinholm.App;

internal sealed record UdsEcuCandidate(
    uint ResponseId,
    uint? RequestId,
    bool IsExtended,
    int ResponseCount,
    int? MaxDataLength)
{
    // The scan observes response IDs only; a matching request ID is inferred
    // from common addressing conventions and has not been confirmed on the bus.
    internal string RequestIdText => RequestId?.ToString(IsExtended ? "X8" : "X3") ?? string.Empty;

    internal string RequestIdNote => RequestId is null
        ? "Request ID unknown — enter and verify it manually."
        : "Request ID inferred from standard addressing — verify before sending.";
}

internal sealed class UdsEcuDiscoveryCancelledException : OperationCanceledException
{
    internal IReadOnlyList<UdsEcuCandidate> Candidates { get; }

    internal UdsEcuDiscoveryCancelledException(IReadOnlyList<UdsEcuCandidate> candidates)
        : base("Passive ECU scan was cancelled. Observed candidates are preserved but the scan is incomplete.")
    {
        Candidates = candidates;
    }
}

internal sealed class UdsEcuDiscoveryIncompleteException : InvalidOperationException
{
    internal IReadOnlyList<UdsEcuCandidate> Candidates { get; }
    internal long DroppedFrameCount { get; }

    internal UdsEcuDiscoveryIncompleteException(
        IReadOnlyList<UdsEcuCandidate> candidates,
        long droppedFrameCount)
        : base($"Passive ECU scan is incomplete: {droppedFrameCount} CAN frame(s) were dropped from the receive queue. Observed candidates are shown but may be missing; repeat the scan with a larger queue or shorter duration.")
    {
        Candidates = candidates;
        DroppedFrameCount = droppedFrameCount;
    }
}

internal sealed class UdsEcuDiscovery
{
    private readonly CanBus _bus;

    internal UdsEcuDiscovery(CanBus bus)
        => _bus = bus ?? throw new ArgumentNullException(nameof(bus));

    internal Task<IReadOnlyList<UdsEcuCandidate>> ScanAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default,
        int receiveQueueCapacity = 4096)
    {
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration));
        if (receiveQueueCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(receiveQueueCapacity));

        // The receive loop waits synchronously on the CAN subscription. Run it
        // off the UI thread so an 8-second passive scan does not freeze WinUI.
        return Task.Run<IReadOnlyList<UdsEcuCandidate>>(
            () => Scan(duration, cancellationToken, receiveQueueCapacity), cancellationToken);
    }

    private IReadOnlyList<UdsEcuCandidate> Scan(
        TimeSpan duration,
        CancellationToken cancellationToken,
        int receiveQueueCapacity)
    {
        // Reject unrelated CAN traffic before it enters the bounded queue.
        // Otherwise high-rate non-UDS frames can evict useful ECU responses.
        using var subscription = _bus.Subscribe(
            receiveQueueCapacity,
            frame => IsLikelyUdsResponse(frame, out _));
        var initialDroppedFrameCount = subscription.DroppedFrameCount;
        var observations = new Dictionary<(uint Id, bool Extended), Observation>();
        // A monotonic clock keeps the scan duration stable if wall-clock time
        // is adjusted while the diagnostic tool is running.
        var elapsed = Stopwatch.StartNew();

        try
        {
            while (elapsed.Elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var remaining = duration - elapsed.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    break;

                if (!subscription.TryRead(
                        TimeSpan.FromMilliseconds(Math.Min(250, Math.Max(1, remaining.TotalMilliseconds))),
                        cancellationToken,
                        out var frame))
                    continue;

                // Passive only: no probe or diagnostic request is transmitted.
                if (!IsLikelyUdsResponse(frame, out _))
                    continue;

                var key = (frame.ArbitrationId, frame.IsExtended);
                if (!observations.TryGetValue(key, out var observation))
                {
                    observation = new Observation(frame.ArbitrationId, frame.IsExtended);
                    observations.Add(key, observation);
                }

                observation.Count++;
                observation.MaxLength = Math.Max(observation.MaxLength, frame.Data.Length);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Preserve useful observations so the UI can report a cancelled
            // scan without throwing away responders already seen.
            throw new UdsEcuDiscoveryCancelledException(BuildCandidates(observations));
        }

        var droppedFrameCount = subscription.DroppedFrameCount - initialDroppedFrameCount;
        var candidates = BuildCandidates(observations);
        if (droppedFrameCount > 0)
            throw new UdsEcuDiscoveryIncompleteException(candidates, droppedFrameCount);

        return candidates;
    }

    private static IReadOnlyList<UdsEcuCandidate> BuildCandidates(
        Dictionary<(uint Id, bool Extended), Observation> observations) =>
        observations.Values
            .OrderByDescending(x => x.Count)
            .Select(x => new UdsEcuCandidate(
                x.Id,
                GuessRequestId(x.Id, x.IsExtended),
                x.IsExtended,
                x.Count,
                x.MaxLength))
            .ToArray();

    internal static bool IsLikelyUdsResponse(CanFrame frame, out byte? responseService)
    {
        responseService = null;

        if (frame.IsRemote || frame.Data.Length == 0)
            return false;

        if (!IsoTp.TryDecode(frame, out var iso, out _))
            return false;

        // A UDS service ID appears only at the start of an ISO-TP message.
        // Consecutive Frames carry continuation bytes, which may coincidentally
        // fall in the positive-response SID range and must not identify an ECU.
        if (iso.Type is not (IsoTpFrameType.SingleFrame or IsoTpFrameType.FirstFrame))
            return false;

        if (iso.Data.Length == 0)
            return false;

        var sid = iso.Data[0];

        // UDS negative response.
        if (sid == 0x7F)
        {
            // A UDS negative response must contain the original service and
            // a negative-response code after 0x7F. A truncated frame is not
            // enough evidence to classify this CAN ID as a UDS responder.
            if (iso.Data.Length < 3)
                return false;

            responseService = sid;
            return true;
        }

        // Positive response SID = request SID + 0x40.
        // Current read-only client uses the standard 0x10..0x3E/0x22/0x19
        // service range, whose positive SIDs are in 0x50..0x7E.
        if (sid is >= 0x50 and <= 0x7E)
        {
            // A positive UDS response must contain service-specific data after
            // the response SID. A one-byte payload is too short to identify a
            // responder and is often just unrelated traffic.
            if (iso.Data.Length < 2)
                return false;

            responseService = sid;
            return true;
        }

        return false;
    }

    internal static uint? GuessRequestId(uint responseId, bool extended)
    {
        if (!extended && responseId is >= 0x7E8 and <= 0x7EF)
            return responseId - 8;

        // ISO 15765-4 fixed-normal 29-bit physical response:
        //   response 0x18DAF1xx -> request 0x18DAxxF1.
        // Do not invent a request ID for other 29-bit schemes.
        if (extended &&
            (responseId & 0x1FFFFF00) == 0x18DAF100)
        {
            var ecuAddress = responseId & 0xFF;
            return 0x18DA0000u | (ecuAddress << 8) | 0xF1;
        }

        return null;
    }

    private sealed class Observation
    {
        internal Observation(uint id, bool extended)
        {
            Id = id;
            IsExtended = extended;
        }

        internal uint Id { get; }
        internal bool IsExtended { get; }
        internal int Count { get; set; }
        internal int MaxLength { get; set; }
    }
}
