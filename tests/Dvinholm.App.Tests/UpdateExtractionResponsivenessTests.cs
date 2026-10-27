using System.IO.Compression;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for asynchronous, guarded ZIP extraction. Creates
/// disposable temporary ZIPs only; no network, WinUI, installed app or J2534.
/// </summary>
public sealed class UpdateExtractionResponsivenessTests
{
    [Fact]
    public async Task ValidNestedArchiveExtractsExactlyIntoPrivateStaging()
    {
        using var files = new TemporaryArchive();
        files.WriteZip(("Dvinholm.App.exe", "fixture exe"), ("assets/icon.txt", "private data"));

        await LatestBuildUpdater.ValidateAndExtractArchiveAsync(files.ArchivePath, files.StagingPath);

        Assert.Equal("fixture exe", File.ReadAllText(Path.Combine(files.StagingPath, "Dvinholm.App.exe")));
        Assert.Equal("private data", File.ReadAllText(Path.Combine(files.StagingPath, "assets", "icon.txt")));
    }

    [Fact]
    public async Task AlreadyCancelledArchiveNeverCreatesPrivateStage()
    {
        using var files = new TemporaryArchive();
        files.WriteZip(("Dvinholm.App.exe", "fixture"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(
                files.ArchivePath, files.StagingPath, cancellation.Token));
        Assert.False(Directory.Exists(files.StagingPath));
    }

    [Fact]
    public async Task TraversalIsRejectedBeforeEvenHarmlessEntryIsWritten()
    {
        using var files = new TemporaryArchive();
        files.WriteZip(("safe.txt", "would be first"), ("../outside.txt", "unsafe"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(files.ArchivePath, files.StagingPath));

        Assert.False(File.Exists(Path.Combine(files.StagingPath, "safe.txt")));
        Assert.False(File.Exists(Path.Combine(files.RootPath, "outside.txt")));
    }

    [Fact]
    public async Task InvalidArchiveFailsWithoutCreatingReadyApplication()
    {
        using var files = new TemporaryArchive();
        File.WriteAllText(files.ArchivePath, "This is not a ZIP archive");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateAndExtractArchiveAsync(files.ArchivePath, files.StagingPath));

        Assert.False(File.Exists(Path.Combine(files.StagingPath, "Dvinholm.App.exe")));
    }

    [Fact]
    public void ExtractorRunsAsTaskOnWorkerInsteadOfSynchronouslyOnCaller()
    {
        var src = ReadSource();
        var start = src.IndexOf("internal static Task ValidateAndExtractArchiveAsync(", StringComparison.Ordinal);
        var end = src.IndexOf("internal static void TryCleanup(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = src[start..end];

        Assert.Contains("Task.Run(async () =>", method);
        Assert.Contains("await CopyArchiveEntryAsync(input, output, entry.Length, cancellationToken);", method);
        Assert.Contains("}, cancellationToken);", method);
    }

    [Fact]
    public void ValidationsRunBeforeExtractorWritesAnyEntry()
    {
        var method = ExtractorMethod();
        var expanded = method.IndexOf("ValidateExpandedSize(archive.Entries.Select(entry => entry.Length))", StringComparison.Ordinal);
        var pathValidation = method.IndexOf("if (!target.StartsWith(fullRoot,", StringComparison.Ordinal);
        var unzip = method.IndexOf("await CopyArchiveEntryAsync(input, output, entry.Length, cancellationToken);", StringComparison.Ordinal);
        Assert.True(expanded >= 0 && pathValidation > expanded && unzip > pathValidation);
        Assert.Contains("Path.GetFullPath(Path.Combine(staging, entry.FullName))", method);
        Assert.Contains("throw new InvalidDataException(\"The artifact contains an unsafe archive path.\")", method);
    }

    [Fact]
    public void CancellationIsCheckedBeforeEntryValidationAndAfterExtraction()
    {
        var method = ExtractorMethod();
        // The following helper has its own per-chunk cancellation checks.
        // Test the worker's pre- and post-copy checkpoints independently.
        var helperStart = method.IndexOf("internal static async Task CopyArchiveEntryAsync(", StringComparison.Ordinal);
        Assert.True(helperStart > 0);
        var worker = method[..helperStart];
        var checks = AllIndexes(worker, "cancellationToken.ThrowIfCancellationRequested();");
        var unzip = worker.IndexOf("await CopyArchiveEntryAsync(input, output, entry.Length, cancellationToken);", StringComparison.Ordinal);
        Assert.True(checks.Count >= 4 && unzip >= 0);
        Assert.True(checks[0] < worker.IndexOf("Directory.CreateDirectory(staging);", StringComparison.Ordinal));
        Assert.True(checks[^2] < unzip && checks[^1] > unzip);
    }

    [Fact]
    public void DownloaderAwaitsExtractionAndRetainsChecksumAndFailureCleanup()
    {
        var src = ReadSource();
        var start = src.IndexOf("internal static async Task<string> DownloadAndPrepareAsync(", StringComparison.Ordinal);
        var end = src.IndexOf("internal static Task ValidateAndExtractArchiveAsync(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var body = src[start..end];
        var digest = body.IndexOf("SHA256.HashDataAsync(checksumStream, cancellationToken)", StringComparison.Ordinal);
        var awaitExtraction = body.IndexOf("await ValidateAndExtractArchiveAsync(archivePath, staging, cancellationToken)", StringComparison.Ordinal);
        var validateExecutable = body.IndexOf("ValidateStagedExecutable(staging);", StringComparison.Ordinal);
        var prepared = body.IndexOf("prepared = true;", StringComparison.Ordinal);
        Assert.True(digest >= 0 && awaitExtraction > digest &&
            validateExecutable > awaitExtraction && prepared > validateExecutable);
        var verifierStart = body.IndexOf("internal static void ValidateStagedExecutable(", StringComparison.Ordinal);
        Assert.True(verifierStart > prepared);
        var verifier = body[verifierStart..];
        Assert.Contains("File.Exists(executable)", verifier);
        Assert.Contains("file.Length == 0", verifier);
        Assert.Contains("FileAttributes.ReparsePoint", verifier);
        Assert.Contains("if (!prepared)", body);
        Assert.Contains("Directory.Delete(staging, recursive: true)", body);
    }

    private static string ExtractorMethod()
    {
        var src = ReadSource();
        var start = src.IndexOf("internal static Task ValidateAndExtractArchiveAsync(", StringComparison.Ordinal);
        var end = src.IndexOf("internal static void TryCleanup(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return src[start..end];
    }

    private static List<int> AllIndexes(string text, string needle)
    {
        var found = new List<int>();
        for (var start = 0; ;)
        {
            var index = text.IndexOf(needle, start, StringComparison.Ordinal);
            if (index < 0) return found;
            found.Add(index);
            start = index + needle.Length;
        }
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

    private sealed class TemporaryArchive : IDisposable
    {
        internal string RootPath { get; } = Path.Combine(Path.GetTempPath(),
            "dvinholm-extract-" + Guid.NewGuid().ToString("N"));
        internal string ArchivePath => Path.Combine(RootPath, "fixture.zip");
        internal string StagingPath => Path.Combine(RootPath, "private-staging");

        internal TemporaryArchive() => Directory.CreateDirectory(RootPath);

        internal void WriteZip(params (string path, string text)[] entries)
        {
            using var archive = ZipFile.Open(ArchivePath, ZipArchiveMode.Create);
            foreach (var (path, text) in entries)
            {
                using var stream = archive.CreateEntry(path).Open();
                using var writer = new StreamWriter(stream);
                writer.Write(text);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
