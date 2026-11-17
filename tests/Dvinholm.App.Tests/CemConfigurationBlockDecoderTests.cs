namespace Dvinholm.App.Tests;

/// <summary>
/// Synthetic offline format checks; no vehicle, PIN, read or write operation.
/// </summary>
public sealed class CemConfigurationBlockDecoderTests
{
    private static byte[] Body(int count)
    {
        var result = new byte[count + 2];
        result[0] = checked((byte)count);
        int checksum = 0;
        for (int index = 0; index < count; index++)
        {
            var value = (byte)(index * 17 + 3);
            result[index + 2] = value;
            checksum = (checksum + value) & 0xFF;
        }
        result[1] = (byte)checksum;
        return result;
    }

    [Fact]
    public void ValidP2Has140ValuesIn142ByteBody()
    {
        var body = Body(140);
        var values = CemConfigurationBlockDecoder.DecodeP2B9Fc(body);
        Assert.Equal(142, body.Length);
        Assert.Equal(140, values.Length);
        Assert.True(values.AsSpan().SequenceEqual(body.AsSpan(2)));
    }

    [Fact]
    public void ValidP3Has252ValuesIn254ByteBody()
    {
        var body = Body(252);
        var values = CemConfigurationBlockDecoder.DecodeP3F106(body);
        Assert.Equal(254, body.Length);
        Assert.Equal(252, values.Length);
        Assert.True(values.AsSpan().SequenceEqual(body.AsSpan(2)));
    }

    [Fact]
    public void UnknownOptionValuesAreKeptAsRawData()
    {
        var body = Body(140);
        Array.Fill(body, (byte)0xFF, 2, 140);
        body[1] = unchecked((byte)(140 * 255));
        var values = CemConfigurationBlockDecoder.DecodeP2B9Fc(body);
        Assert.All(values, value => Assert.Equal((byte)0xFF, value));
    }

    [Fact]
    public void InvalidChecksumFailsForBothFormats()
    {
        var p2 = Body(140);
        p2[1] ^= 1;
        Assert.Throws<FormatException>(() => CemConfigurationBlockDecoder.DecodeP2B9Fc(p2));
        var p3 = Body(252);
        p3[1] ^= 128;
        Assert.Throws<FormatException>(() => CemConfigurationBlockDecoder.DecodeP3F106(p3));
    }

    [Fact]
    public void CountByteCannotDisagreeWithBodyLength()
    {
        var p2 = Body(140);
        p2[0] = 139;
        Assert.Throws<FormatException>(() => CemConfigurationBlockDecoder.DecodeP2B9Fc(p2));
        var p3 = Body(252);
        p3[0] = 253;
        Assert.Throws<FormatException>(() => CemConfigurationBlockDecoder.DecodeP3F106(p3));
    }

    [Fact]
    public void InterchangedP2P3BodiesAreRejected()
    {
        Assert.Throws<FormatException>(() => CemConfigurationBlockDecoder.DecodeP2B9Fc(Body(252)));
        Assert.Throws<FormatException>(() => CemConfigurationBlockDecoder.DecodeP3F106(Body(140)));
    }

    [Fact]
    public void EmptyTruncatedOrTrailingBodiesAreRejected()
    {
        Assert.Throws<FormatException>(() => CemConfigurationBlockDecoder.DecodeP2B9Fc(Array.Empty<byte>()));
        Assert.Throws<FormatException>(() => CemConfigurationBlockDecoder.DecodeP3F106(Array.Empty<byte>()));
        Assert.Throws<FormatException>(() => CemConfigurationBlockDecoder.DecodeP2B9Fc(Body(140)[..^1]));
        Assert.Throws<FormatException>(() => CemConfigurationBlockDecoder.DecodeP3F106(Body(252)[..^1]));
        Assert.Throws<FormatException>(() =>
            CemConfigurationBlockDecoder.DecodeP2B9Fc(Body(140).Concat(new byte[] { 0 }).ToArray()));
        Assert.Throws<FormatException>(() =>
            CemConfigurationBlockDecoder.DecodeP3F106(Body(252).Concat(new byte[] { 0 }).ToArray()));
    }

    [Fact]
    public void PositiveUdsHeaderMustBeRemovedBeforeP3Decode()
    {
        var body = Body(252);
        var pdu = new byte[257];
        pdu[0] = 0x62;
        pdu[1] = 0xF1;
        pdu[2] = 0x06;
        body.CopyTo(pdu, 3);
        Assert.Throws<FormatException>(() => CemConfigurationBlockDecoder.DecodeP3F106(pdu));
        Assert.True(CemConfigurationBlockDecoder.DecodeP3F106(
            pdu.AsSpan(3)).AsSpan().SequenceEqual(body.AsSpan(2)));
    }

    [Fact]
    public void ReturnedByteArrayNeverAliasesCallerMemory()
    {
        var body = Body(140);
        var expected = body[2];
        var one = CemConfigurationBlockDecoder.DecodeP2B9Fc(body);
        body[2] ^= 0xFF;
        Assert.Equal(expected, one[0]);

        var two = CemConfigurationBlockDecoder.DecodeP2B9Fc(Body(140));
        one[0] ^= 0xFF;
        Assert.Equal(expected, two[0]);
    }

    [Fact]
    public void ExistingStrictIsoTpReassembles257ByteP3Response()
    {
        var body = Body(252);
        var pdu = new byte[257];
        pdu[0] = 0x62; pdu[1] = 0xF1; pdu[2] = 0x06;
        body.CopyTo(pdu, 3);
        var frames = IsoTp.Segment(pdu);
        Assert.Equal(37, frames.Count);
        var reassembler = new IsoTpReassembler();
        byte[]? completed = null;
        foreach (var data in frames)
        {
            var frame = new CanFrame(0x7E8, false, false, data, 0, 0);
            Assert.True(reassembler.Push(frame, out var payload, out var error), error);
            if (payload is not null) completed = payload;
        }
        Assert.NotNull(completed);
        Assert.True(completed.AsSpan().SequenceEqual(pdu));
        Assert.Equal(252, CemConfigurationBlockDecoder.DecodeP3F106(completed.AsSpan(3)).Length);
    }

    [Fact]
    public void OfflineDecoderCannotBypassExistingReadAndWritePreflight()
    {
        Assert.Equal(140, CemConfigurationBlockDecoder.DecodeP2B9Fc(Body(140)).Length);
        var source = Source("src/Dvinholm.App/CemConfigurationBlockDecoder.cs");
        foreach (var forbidden in new[] { "PassThru", "J2534", "Connect(", "SendAsync(",
            "WriteConfiguration(", "ReadCemPin(", "Unlock(", "SecurityAccess(" })
            Assert.DoesNotContain(forbidden, source);
        var preflight = Source("src/Dvinholm.App/VolvoP2CemConfigurationReadPreflight.cs");
        Assert.Contains("internal bool MayReadLiveCemConfiguration => false;", preflight);
        Assert.Contains("internal bool MayWriteConfiguration => false;", preflight);
        Assert.Contains("Assets\\ReadOnlyFormats-MIT.txt",
            Source("src/Dvinholm.App/Dvinholm.App.csproj"));
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }
}
