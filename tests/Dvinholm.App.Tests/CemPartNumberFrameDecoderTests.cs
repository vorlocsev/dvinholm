namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline contracts for the source-defined CEM P/N
/// fragment decoder. No native DLL, vehicle, PIN or diagnostic transmission.
/// </summary>
public sealed class CemPartNumberFrameDecoderTests
{
    private const uint SourceId = 0x800003;

    private static CanFrame First(uint id = SourceId, byte a = 0x30,
        byte b = 0x68, byte c = 0x29) =>
        new(id, true, false, [0x80, 0, 0, 0, 0, a, b, c], 0, 0);

    private static CanFrame Last(uint id = SourceId, byte bcd = 0x81,
        byte flag = 0) =>
        new(id, true, false, [flag, bcd, 0, 0, 0, 0, 0, 0], 0, 0);

    [Fact]
    public void TwoValidSourceFragmentsYieldOneUnattestedPartNumber()
    {
        var decoder = new CemPartNumberFrameDecoder();
        Assert.Null(decoder.Observe(First()));
        Assert.Equal(30_682_981u, decoder.Observe(Last()));
        Assert.Null(decoder.Observe(Last()));
    }

    [Fact]
    public void LoneTailCannotBecomeAnObservationAcrossAttempts()
    {
        var first = new CemPartNumberFrameDecoder();
        Assert.Null(first.Observe(First()));
        var nextAttempt = new CemPartNumberFrameDecoder();
        Assert.Null(nextAttempt.Observe(Last()));
        first.Reset();
        Assert.Null(first.Observe(Last()));
    }

    [Fact]
    public void NonExtendedRemoteWrongMaskAndShortFramesAreRejected()
    {
        var decoder = new CemPartNumberFrameDecoder();
        foreach (var invalid in new[]
        {
            First() with { IsExtended = false },
            First() with { IsRemote = true },
            First(0x800004),
            First() with { Data = [0x80] },
            First() with { Data = new byte[9] }
        })
            Assert.Null(decoder.Observe(invalid));
        Assert.Null(decoder.Observe(Last()));
        Assert.Null(decoder.Observe(First()));
        Assert.Equal(30_682_981u, decoder.Observe(Last()));
    }

    [Fact]
    public void BadBcdInEitherFragmentFailsWithoutLeakingOldPrefix()
    {
        var decoder = new CemPartNumberFrameDecoder();
        foreach (var invalid in new byte[] { 0xFA, 0xAF, 0xFF })
        {
            Assert.Null(decoder.Observe(First(a: invalid)));
            Assert.Null(decoder.Observe(Last()));
            Assert.Null(decoder.Observe(First(b: invalid)));
            Assert.Null(decoder.Observe(Last()));
            Assert.Null(decoder.Observe(First(c: invalid)));
            Assert.Null(decoder.Observe(Last()));
            Assert.Null(decoder.Observe(First()));
            Assert.Null(decoder.Observe(Last(bcd: invalid)));
            Assert.Null(decoder.Observe(Last()));
        }
    }

    [Fact]
    public void SecondFragmentFromDifferentSourceClearsPendingPair()
    {
        var decoder = new CemPartNumberFrameDecoder();
        Assert.Null(decoder.Observe(First()));
        Assert.Null(decoder.Observe(Last(0x900003)));
        Assert.Null(decoder.Observe(Last()));
        Assert.Null(decoder.Observe(First()));
        Assert.Equal(30_682_981u, decoder.Observe(Last()));
    }

    [Fact]
    public void NewStartReplacesAnyIncompleteEarlierObservation()
    {
        var decoder = new CemPartNumberFrameDecoder();
        Assert.Null(decoder.Observe(First()));
        Assert.Null(decoder.Observe(First(a: 0x31, b: 0x23, c: 0x45)));
        Assert.Equal(31_234_581u, decoder.Observe(Last()));
        Assert.Null(decoder.Observe(Last()));
    }

    [Fact]
    public void UnexpectedSecondFragmentFlagsConsumePrefix()
    {
        var decoder = new CemPartNumberFrameDecoder();
        Assert.Null(decoder.Observe(First()));
        Assert.Null(decoder.Observe(Last(flag: 0x40)));
        Assert.Null(decoder.Observe(Last()));
        Assert.Null(decoder.Observe(First()));
        Assert.Equal(30_682_981u, decoder.Observe(Last()));
    }

    [Fact]
    public void CanonicalSevenAndEightDigitBoundsAreEnforcedWithoutTransport()
    {
        var decoder = new CemPartNumberFrameDecoder();
        Assert.Null(decoder.Observe(First(a: 0x01, b: 0x23, c: 0x45)));
        Assert.Equal(1_234_567u, decoder.Observe(Last(bcd: 0x67)));
        Assert.Null(decoder.Observe(First(a: 0x99, b: 0x99, c: 0x99)));
        Assert.Equal(99_999_999u, decoder.Observe(Last(bcd: 0x99)));
        Assert.Null(decoder.Observe(First(a: 0, b: 0, c: 0)));
        Assert.Null(decoder.Observe(Last(bcd: 0x01)));
        Assert.Null(decoder.Observe(Last()));
    }
}
