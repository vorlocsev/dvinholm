using System.IO.Compression;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for cancellable per-entry ZIP streaming and the
/// no-overwrite, pre-validation and metadata behavior of the updater.
/// All test ZIPs live in temporary folders; no installed app is touched.
/// </summary>
public sealed class UpdateExtractionCancellationTests
{
    [Fact]
    public async Task PerEntryCopyRetainsAllBytes()
    {
        var bytes = Enumerable.Range(0, 250_000).Select(i => (byte)(i % 251)).ToArray();
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();

        await LatestBuildUpdater.CopyArchiveEntryAsync(input, output, bytes.Length, CancellationToken.None);

        Assert.Equal(bytes, output.ToArray());
    }

    [Fact]
    public async Task PerEntryCopyHonorsPreCancelledToken()
    {
        using var input = new MemoryStream(new byte[200_000]);
        using var output = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LatestBuildUpdater.CopyArchiveEntryAsync(input, output, 200_000, cts.Token));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task PerEntryCopyStopsMidwayWhenDownloadIsCancelled()
    {
        using var input = new BlockingAfterFirstReadStream();
        using var output = new MemoryStream();
        using var cts = new CancellationTokenSource();

        var copy = LatestBuildUpdater.CopyArchiveEntryAsync(input, output, 200_000, cts.Token);
        await input.Blocked.WaitAsync(TimeSpan.FromSeconds(10));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await copy);
        Assert.InRange(output.Length, 1, 81920);
    }

    [Fact]
    public async Task ArchiveSupportsExplicitEmptyDirectories()
    {
        using var folder = new Scratch();
        using (var archive = ZipFile.Open(folder.Zip, ZipArchiveMode.Create))
        {
            archive.CreateEntry("empty/");
            Write(archive, "nested/data.txt", "payload");
        }

        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(folder.Zip, folder.Stage);

        Assert.True(Directory.Exists(Path.Combine(folder.Stage, "empty")));
        Assert.Equal("payload", File.ReadAllText(Path.Combine(folder.Stage, "nested", "data.txt")));
    }

    [Fact]
    public async Task EntryTimestampIsPreservedOnExtractedFile()
    {
        using var folder = new Scratch();
        var timestamp = new DateTimeOffset(2026, 10, 20, 12, 30, 0, TimeSpan.Zero);
        using (var archive = ZipFile.Open(folder.Zip, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("Dvinholm.App.exe");
            entry.LastWriteTime = timestamp;
            using var writer = new StreamWriter(entry.Open());
            writer.Write("fixture");
        }

        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(folder.Zip, folder.Stage);
        var extracted = File.GetLastWriteTime(Path.Combine(folder.Stage, "Dvinholm.App.exe"));
        Assert.InRange(Math.Abs((extracted - timestamp.LocalDateTime).TotalSeconds), 0, 4);
    }

    [Fact]
    public async Task RepeatedFileNameCannotOverwriteFirstExtractedFile()
    {
        using var folder = new Scratch();
        using (var archive = ZipFile.Open(folder.Zip, ZipArchiveMode.Create))
        {
            Write(archive, "same.txt", "first");
            Write(archive, "same.txt", "second");
        }

        // Duplicate paths are now rejected during metadata preflight, before
        // the stage exists; the first entry is never extracted.
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(folder.Zip, folder.Stage));

        Assert.False(Directory.Exists(folder.Stage));
    }

    [Fact]
    public async Task UnsafeEntryIsRejectedBeforeEvenSafeFileIsWritten()
    {
        using var folder = new Scratch();
        using (var archive = ZipFile.Open(folder.Zip, ZipArchiveMode.Create))
        {
            Write(archive, "safe.txt", "should-not-be-written");
            Write(archive, "../outside.txt", "unsafe");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(folder.Zip, folder.Stage));

        Assert.False(File.Exists(Path.Combine(folder.Stage, "safe.txt")));
        Assert.False(File.Exists(Path.Combine(folder.Root, "outside.txt")));
    }

    [Fact]
    public void WorkerValidatesAllEntriesBeforeAsyncCopiesAndKeepsFinalCancelCheck()
    {
        var src = ReadSource();
        var start = src.IndexOf("internal static Task ValidateAndExtractArchiveAsync(", StringComparison.Ordinal);
        var end = src.IndexOf("internal static void TryCleanup(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = src[start..end];

        var worker = method.IndexOf("Task.Run(async () =>", StringComparison.Ordinal);
        var expanded = method.IndexOf("ValidateExpandedSize(archive.Entries.Select(entry => entry.Length))", StringComparison.Ordinal);
        var pathGuard = method.IndexOf("if (!target.StartsWith(fullRoot,", StringComparison.Ordinal);
        var copy = method.IndexOf("await CopyArchiveEntryAsync(input, output, entry.Length, cancellationToken);", StringComparison.Ordinal);
        var finalCancel = method.LastIndexOf("cancellationToken.ThrowIfCancellationRequested();", StringComparison.Ordinal);
        Assert.True(worker >= 0 && expanded > worker && pathGuard > expanded && copy > pathGuard);
        Assert.True(finalCancel > copy);
        Assert.Contains("FileMode.CreateNew", method);
        Assert.Contains("if (read > declaredLength - written)", method);
        Assert.DoesNotContain("archive.ExtractToDirectory(staging)", method);
    }

    private static void Write(ZipArchive archive, string name, string value)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(value);
    }

    private static string ReadSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "Dvinholm.App", "LatestBuildUpdater.cs");
            if (File.Exists(path))
                return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        throw new FileNotFoundException("LatestBuildUpdater.cs not found.");
    }

    private sealed class Scratch : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "dvinholm-entry-" + Guid.NewGuid().ToString("N"));
        internal string Zip => Path.Combine(Root, "data.zip");
        internal string Stage => Path.Combine(Root, "staging");
        internal Scratch() => Directory.CreateDirectory(Root);
        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class BlockingAfterFirstReadStream : Stream
    {
        private bool _sent;
        private readonly TaskCompletionSource _blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Blocked => _blocked.Task;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_sent)
            {
                _sent = true;
                var count = Math.Min(8192, buffer.Length);
                buffer.Span[..count].Fill(0x5A);
                return ValueTask.FromResult(count);
            }

            _blocked.TrySetResult();
            return new ValueTask<int>(WaitForCancellation(cancellationToken));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        private static async Task<int> WaitForCancellation(CancellationToken token)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
