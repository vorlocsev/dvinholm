namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline guards against ZIP entries whose actual decompressed byte
/// count disagrees with untrusted central-directory length metadata.
/// </summary>
public sealed class UpdateExtractedLengthGuardTests
{
    [Fact]
    public async Task ExactDeclaredLengthIsCopiedByteForByte()
    {
        var bytes = Enumerable.Range(0, 4_096).Select(i => (byte)(i % 251)).ToArray();
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();

        await LatestBuildUpdater.CopyArchiveEntryAsync(
            input, output, bytes.Length, CancellationToken.None);

        Assert.Equal(bytes, output.ToArray());
    }

    [Fact]
    public async Task FileLargerThanCopyBufferIsFullyVerified()
    {
        var bytes = Enumerable.Range(0, 210_123).Select(i => (byte)(i % 233)).ToArray();
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();

        await LatestBuildUpdater.CopyArchiveEntryAsync(
            input, output, bytes.LongLength, CancellationToken.None);

        Assert.Equal(bytes, output.ToArray());
    }

    [Fact]
    public async Task EmptyEntryWithZeroDeclaredLengthIsValid()
    {
        using var input = new MemoryStream();
        using var output = new MemoryStream();

        await LatestBuildUpdater.CopyArchiveEntryAsync(
            input, output, 0, CancellationToken.None);

        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task NegativeDeclaredLengthIsRejectedWithoutWriting()
    {
        using var input = new MemoryStream(new byte[] { 1 });
        using var output = new MemoryStream();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.CopyArchiveEntryAsync(input, output, -1, CancellationToken.None));

        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task ExpandedEntryAboveOneGigabyteLimitIsRejectedBeforeRead()
    {
        using var input = new MemoryStream(new byte[] { 1 });
        using var output = new MemoryStream();
        const long maxExpanded = 1_000L * 1024 * 1024;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.CopyArchiveEntryAsync(
                input, output, maxExpanded + 1, CancellationToken.None));

        Assert.Equal(0, input.Position);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task DecompressionBeyondDeclaredLengthFailsBeforeWritingExcessChunk()
    {
        var bytes = Enumerable.Repeat((byte)0x41, 100_000).ToArray();
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        const long declared = 90_000;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.CopyArchiveEntryAsync(
                input, output, declared, CancellationToken.None));

        Assert.True(output.Length <= declared);
        Assert.True(output.Length < bytes.LongLength);
    }

    [Fact]
    public async Task TruncatedEntryFailsEvenIfAllAvailableBytesWereCopied()
    {
        var bytes = Enumerable.Repeat((byte)0x42, 14).ToArray();
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.CopyArchiveEntryAsync(input, output, 15, CancellationToken.None));

        Assert.Equal(bytes, output.ToArray());
    }

    [Fact]
    public async Task CancelledEntryDoesNotWriteDataOrClaimSuccess()
    {
        using var input = new MemoryStream(new byte[200_000]);
        using var output = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LatestBuildUpdater.CopyArchiveEntryAsync(
                input, output, 200_000, cancellation.Token));

        Assert.Equal(0, output.Length);
    }
}
