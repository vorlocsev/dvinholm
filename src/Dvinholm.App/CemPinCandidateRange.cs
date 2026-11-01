namespace Dvinholm.App;

/// <summary>
/// Bounded, single-consumer enumeration of six packed-decimal byte pairs.
/// This produces candidates, never a recovered or verified PIN. It has no
/// transport, unlock command, timing measurement or persistence capability.
/// A future guarded provider must verify its route and check cancellation
/// again before transmitting anything. No production provider registers this.
/// </summary>
internal sealed class CemPinCandidateRange
{
    internal const int CandidateLength = 6;
    internal const ulong SearchSpaceSize = 1_000_000_000_000;

    private ulong _next;
    private readonly ulong _endExclusive;

    // Both bounds are explicit: constructing a range cannot implicitly start
    // an unbounded full-space search. Ordinals are sensitive, never log them.
    internal CemPinCandidateRange(ulong firstOrdinal, ulong candidateCount)
    {
        if (firstOrdinal >= SearchSpaceSize)
            throw new ArgumentOutOfRangeException(nameof(firstOrdinal));
        if (candidateCount == 0 || candidateCount > SearchSpaceSize - firstOrdinal)
            throw new ArgumentOutOfRangeException(nameof(candidateCount));

        _next = firstOrdinal;
        _endExclusive = firstOrdinal + candidateCount;
    }

    internal ulong Remaining => _endExclusive - _next;

    /// <summary>
    /// Writes one candidate into caller-owned memory, with the least-significant
    /// decimal pair first. No CEM-specific byte permutation is assumed. The
    /// caller must clear this buffer after use and never log or serialize it.
    /// Cancellation, an invalid buffer and exhaustion do not advance the range.
    /// </summary>
    internal bool TryNext(Span<byte> destination, CancellationToken cancellationToken = default)
    {
        if (destination.Length != CandidateLength)
            throw new ArgumentException("A six-byte candidate buffer is required.", nameof(destination));
        cancellationToken.ThrowIfCancellationRequested();
        if (_next == _endExclusive)
            return false;

        var ordinal = _next;
        for (int pair = 0; pair < CandidateLength; pair++)
        {
            var value = ordinal % 100;
            destination[pair] = (byte)(((value / 10) << 4) | (value % 10));
            ordinal /= 100;
        }
        _next++;
        return true;
    }

    public override string ToString() => "CEM PIN candidate range (values withheld)";
}
