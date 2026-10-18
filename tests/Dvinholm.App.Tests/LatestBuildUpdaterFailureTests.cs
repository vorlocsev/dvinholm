using System.IO.Compression;
using System.Net;

namespace Dvinholm.App.Tests;

public sealed class LatestBuildUpdaterFailureTests
{
    [Fact]
    public void ValidateExpandedSize_AcceptsEntriesAtConfiguredLimit()
    {
        LatestBuildUpdater.ValidateExpandedSize([500_000_000L, 548_576_000L]);
    }

    [Fact]
    public void ValidateExpandedSize_RejectsNegativeEntryLength()
    {
        Assert.Throws<InvalidDataException>(
            () => LatestBuildUpdater.ValidateExpandedSize([1L, -1L]));
    }

    [Fact]
    public void ValidateExpandedSize_RejectsAggregateOverLimitWithoutOverflow()
    {
        Assert.Throws<InvalidDataException>(
            () => LatestBuildUpdater.ValidateExpandedSize([1_048_576_000L, 1L]));

        Assert.Throws<InvalidDataException>(
            () => LatestBuildUpdater.ValidateExpandedSize([long.MaxValue, 1L]));
    }
    [Fact]
    public async Task DownloadFailure_ThrowsAndCleansStagingDirectory()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var build = BuildInfo(new byte[] { 1, 2, 3 });
        var stagingRoot = GetStagingRoot();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        Assert.NotNull(exception);
        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
    }

    [Fact]
    public async Task ReleaseAssetApiDownload_RequestsBinaryContent()
    {
        var bytes = CreateZip(("Dvinholm.App.exe", "fake executable"));
        HttpRequestMessage? capturedRequest = null;
        using var client = CreateClient(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
        });
        var build = BuildInfo(
            bytes,
            url: "https://api.github.com/repos/vorlocsev/dvinholm/releases/assets/12345");
        string? staging = null;

        try
        {
            staging = await LatestBuildUpdater.DownloadAndPrepareAsync(build, client);

            Assert.NotNull(capturedRequest);
            Assert.Contains(capturedRequest!.Headers.Accept,
                header => header.MediaType == "application/octet-stream");
            Assert.True(File.Exists(Path.Combine(staging, "Dvinholm.App.exe")));
        }
        finally
        {
            capturedRequest?.Dispose();
            if (staging is not null && Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    [Fact]
    public async Task SizeMismatch_ThrowsAndCleansStagingDirectory()
    {
        var bytes = CreateZip(("Dvinholm.App.exe", "fake executable"));
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var build = BuildInfo(bytes, sizeOverride: bytes.Length + 1);
        var stagingRoot = GetStagingRoot();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
    }

    [Fact]
    public async Task OversizedDeclaredResponse_ThrowsAndCleansStagingDirectory()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
            {
                Headers = { ContentLength = 500L * 1024 * 1024 + 1 }
            }
        });
        var build = BuildInfo(Array.Empty<byte>());
        var stagingRoot = GetStagingRoot();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
    }

    [Fact]
    public async Task PartialDownloadFailure_CleansTemporaryArchiveAndStagingDirectory()
    {
        var build = BuildInfo(new byte[] { 1, 2, 3, 4, 5, 6 });
        var stagingRoot = GetStagingRoot();
        var tempPattern = $"dvinholm-{build.CommitSha}-*.zip";
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new ThrowAfterFirstChunkStream(new byte[] { 1, 2, 3, 4, 5, 6 }))
        });

        await Assert.ThrowsAsync<IOException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
        Assert.Empty(Directory.EnumerateFiles(Path.GetTempPath(), tempPattern));
    }

    [Fact]
    public async Task CanceledDownload_ThrowsAndCleansStagingDirectory()
    {
        using var client = new HttpClient(new CancelingHttpMessageHandler());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var build = BuildInfo(new byte[] { 1, 2, 3 });
        var stagingRoot = GetStagingRoot();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client, cancellationToken: cancellation.Token));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
    }

    [Fact]
    public async Task CancellationAfterPartialDownload_CleansTemporaryArchiveAndStagingDirectory()
    {
        using var cancellation = new CancellationTokenSource();
        var build = BuildInfo(new byte[] { 1, 2, 3, 4, 5, 6 });
        var stagingRoot = GetStagingRoot();
        var tempPattern = $"dvinholm-{build.CommitSha}-*.zip";
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new CancelAfterFirstChunkStream(new byte[] { 1, 2, 3, 4, 5, 6 }, cancellation))
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client, cancellationToken: cancellation.Token));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
        Assert.Empty(Directory.EnumerateFiles(Path.GetTempPath(), tempPattern));
    }

    [Fact]
    public async Task DigestMismatch_ThrowsAndCleansTemporaryArchiveAndStagingDirectory()
    {
        var bytes = CreateZip(("Dvinholm.App.exe", "fake executable"));
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var build = BuildInfo(bytes, digest: "sha256:" + new string('0', 64));
        var stagingRoot = GetStagingRoot();
        var tempPattern = $"dvinholm-{build.CommitSha}-*.zip";

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
        Assert.Empty(Directory.EnumerateFiles(Path.GetTempPath(), tempPattern));
    }


    [Fact]
    public async Task MatchingSha256_PreparesBuildAndKeepsExtractedExecutable()
    {
        var bytes = CreateZip(("Dvinholm.App.exe", "fake executable"));
        var digest = "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var build = BuildInfo(bytes, digest: digest);
        string? staging = null;

        try
        {
            staging = await LatestBuildUpdater.DownloadAndPrepareAsync(build, client);

            var executable = Path.Combine(staging, "Dvinholm.App.exe");
            Assert.True(File.Exists(executable));
            Assert.Equal("fake executable", await File.ReadAllTextAsync(executable));
        }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    [Fact]
    public async Task CorruptZip_ThrowsAndCleansTemporaryArchiveAndStagingDirectory()
    {
        var bytes = new byte[] { 0x44, 0x56, 0x49, 0x4E, 0x48, 0x4F, 0x4C, 0x4D };
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var build = BuildInfo(bytes);
        var stagingRoot = GetStagingRoot();
        var tempPattern = $"dvinholm-{build.CommitSha}-*.zip";

        await Assert.ThrowsAnyAsync<Exception>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
        Assert.Empty(Directory.EnumerateFiles(Path.GetTempPath(), tempPattern));
    }

    [Fact]
    public async Task UnsafeArchivePath_ThrowsAndCleansTemporaryArchiveAndStagingDirectoryWithoutEscapingRoot()
    {
        var escapedFile = Path.Combine(GetStagingRoot(), $"escape-{Guid.NewGuid():N}.txt");
        var escapedEntry = $"../{Path.GetFileName(escapedFile)}";
        var bytes = CreateZip((escapedEntry, "unsafe"), ("Dvinholm.App.exe", "fake executable"));
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var build = BuildInfo(bytes);
        var stagingRoot = GetStagingRoot();
        var tempPattern = $"dvinholm-{build.CommitSha}-*.zip";

        Assert.False(File.Exists(escapedFile));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        Assert.False(File.Exists(escapedFile));
        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
        Assert.Empty(Directory.EnumerateFiles(Path.GetTempPath(), tempPattern));
    }

    [Fact]
    public async Task MissingExecutable_ThrowsAndCleansTemporaryArchiveAndStagingDirectory()
    {
        var bytes = CreateZip(("readme.txt", "not an app"));
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var build = BuildInfo(bytes);
        var stagingRoot = GetStagingRoot();
        var tempPattern = $"dvinholm-{build.CommitSha}-*.zip";

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
        Assert.Empty(Directory.EnumerateFiles(Path.GetTempPath(), tempPattern));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryCleanup_DoesNotPropagateExpectedFileSystemCleanupFailures(bool unauthorized)
    {
        var exception = unauthorized
            ? (Exception)new UnauthorizedAccessException("simulated access denial")
            : new IOException("simulated file-system failure");

        LatestBuildUpdater.TryCleanup(() => throw exception);
    }

    [Fact]
    public void InvalidDestinationPath_CleansStagingWhenReplacementSetupFails()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dvinholm-invalid-destination-{Guid.NewGuid():N}");
        var staging = Path.Combine(root, "staging");
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "Dvinholm.App.exe"), "fake executable");

        try
        {
            Assert.Throws<ArgumentException>(() =>
                LatestBuildUpdater.StartReplacement(staging, "\0", Environment.ProcessId));

            Assert.False(Directory.Exists(staging));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FailedHelperStartCleanup_RemovesScriptAndStagingDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dvinholm-helper-start-failure-{Guid.NewGuid():N}");
        var staging = Path.Combine(root, "staging");
        var script = Path.Combine(root, "replace.ps1");
        Directory.CreateDirectory(staging);
        await File.WriteAllTextAsync(Path.Combine(staging, "Dvinholm.App.exe"), "fake executable");
        await File.WriteAllTextAsync(script, "test script");

        try
        {
            LatestBuildUpdater.CleanupFailedStart(script, staging);

            Assert.False(File.Exists(script));
            Assert.False(Directory.Exists(staging));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReplacementScript_EscapesApostrophesInAllFilesystemPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dvinholm O'Neil {Guid.NewGuid():N}");
        var staging = Path.Combine(root, "staging O'Neil");
        var destination = Path.Combine(root, "installed O'Neil");
        var scriptPath = Path.Combine(root, "replace O'Neil.ps1");
        var backup = Path.Combine(root, "backup O'Neil");

        var script = LatestBuildUpdater.BuildReplacementScript(
            staging,
            destination,
            1234,
            scriptPath,
            backup);

        static string Escape(string path) => Path.GetFullPath(path).Replace("'", "''", StringComparison.Ordinal);

        Assert.Contains($"$source = '{Escape(staging)}'", script);
        Assert.Contains($"$destination = '{Escape(destination)}'", script);
        Assert.Contains($"$scriptPath = '{Escape(scriptPath)}'", script);
        Assert.Contains($"$backup = '{Escape(backup)}'", script);
        Assert.DoesNotContain(Path.GetFullPath(staging), script.Replace(Escape(staging), string.Empty, StringComparison.Ordinal));
        Assert.DoesNotContain(Path.GetFullPath(destination), script.Replace(Escape(destination), string.Empty, StringComparison.Ordinal));
        Assert.DoesNotContain(Path.GetFullPath(scriptPath), script.Replace(Escape(scriptPath), string.Empty, StringComparison.Ordinal));
        Assert.DoesNotContain(Path.GetFullPath(backup), script.Replace(Escape(backup), string.Empty, StringComparison.Ordinal));
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new StubHttpMessageHandler(responseFactory));

    private static LatestBuildInfo BuildInfo(byte[] bytes, long? sizeOverride = null, string? digest = null, string? url = null)
    {
        var sha = Guid.NewGuid().ToString("N").PadRight(40, 'a')[..40];
        return new LatestBuildInfo(
            sha,
            1,
            1,
            url ?? "https://example.invalid/update.zip",
            sizeOverride ?? bytes.LongLength,
            digest,
            DateTimeOffset.UtcNow.ToString("O"));
    }

    private static byte[] CreateZip(params (string Name, string Content)[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(file.Content);
            }
        }

        return stream.ToArray();
    }

    private static string GetStagingRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dvinholm",
        "UpdateStaging");

    private static void AssertNoStagingDirectory(string root, string commitSha)
    {
        if (!Directory.Exists(root))
            return;

        Assert.Empty(Directory.EnumerateDirectories(root, commitSha + "-*"));
    }

    private sealed class ThrowAfterFirstChunkStream(byte[] bytes) : Stream
    {
        private int _position;
        private bool _hasThrown;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position > 0 && !_hasThrown)
            {
                _hasThrown = true;
                throw new IOException("Simulated connection failure during download.");
            }

            if (_position >= bytes.Length)
                return 0;

            var read = Math.Min(count, Math.Min(3, bytes.Length - _position));
            Array.Copy(bytes, _position, buffer, offset, read);
            _position += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_position > 0 && !_hasThrown)
            {
                _hasThrown = true;
                throw new IOException("Simulated connection failure during download.");
            }

            if (_position >= bytes.Length)
                return ValueTask.FromResult(0);

            var read = Math.Min(buffer.Length, Math.Min(3, bytes.Length - _position));
            bytes.AsMemory(_position, read).CopyTo(buffer);
            _position += read;
            return ValueTask.FromResult(read);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CancelAfterFirstChunkStream(byte[] bytes, CancellationTokenSource cancellation) : Stream
    {
        private int _position;
        private bool _canceled;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("The test uses asynchronous reads only.");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_position > 0 && !_canceled)
            {
                _canceled = true;
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (_position >= bytes.Length)
                return ValueTask.FromResult(0);

            var read = Math.Min(buffer.Length, Math.Min(3, bytes.Length - _position));
            bytes.AsMemory(_position, read).CopyTo(buffer);
            _position += read;
            return ValueTask.FromResult(read);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CancelingHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The canceled request should not return a response.");
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    [Fact]
    public void ReplacementScript_CreatesBackupBeforeReplacingAndRestoresOnFailure()
    {
        var script = LatestBuildUpdater.BuildReplacementScript(
            @"C:\\staging",
            @"C:\\app\\Builds\\current",
            1234,
            @"C:\\temp\\replace.ps1",
            @"C:\\backup\\previous");

        Assert.Contains("$backupReady = $false", script);
        Assert.Contains("Wait-Process -Id $processId -Timeout 60 -ErrorAction Stop", script);
        Assert.Contains("if (Get-Process -Id $processId -ErrorAction SilentlyContinue)", script);
        Assert.Contains("if it is now gone; if it still exists, preserve the timeout/error.", script);
        Assert.Contains("$replacementStarted = $true", script);
        Assert.Contains("$destination $backup /MIR", script);
        Assert.True(script.IndexOf("$destination $backup /MIR", StringComparison.Ordinal) < script.IndexOf("$source $destination /MIR", StringComparison.Ordinal));
        Assert.Contains("$source $destination /MIR", script);
        Assert.Contains("if ($replacementStarted -and $hadDestination -and $backupReady)", script);
        Assert.Contains("elseif ($replacementStarted -and -not $hadDestination)", script);
        Assert.Contains("Remove-Item -LiteralPath $destination -Recurse -Force -ErrorAction Stop", script);
        Assert.Contains("Could not remove the incomplete first-install destination:", script);
        Assert.Contains("The incomplete first-install destination was removed.", script);
        Assert.Contains("$backup $destination /MIR", script);
        Assert.Contains("The previous build was restored and will be restarted.", script);
        Assert.Contains("Start-Process -FilePath $targetExe -WorkingDirectory $destination -PassThru", script);
        Assert.Contains("The updated application exited during startup", script);
        Assert.Contains("The restored application exited during startup", script);
        Assert.Contains("Rollback failed:", script);
    }


    [Fact]
    public async Task ReplacementScript_RestoresPreviousBuildWhenReplacementFailsAtRuntime()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), $"dvinholm-rollback-test-{Guid.NewGuid():N}");
        var staging = Path.Combine(root, "staging");
        var destination = Path.Combine(root, "installed");
        var backup = Path.Combine(root, "backup");
        var scriptPath = Path.Combine(root, "replace.ps1");
        var localAppData = Path.Combine(root, "localappdata");
        var launchLog = Path.Combine(root, "launch.log");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(localAppData);
        Directory.CreateDirectory(Path.Combine(localAppData, "Dvinholm"));
        await File.WriteAllTextAsync(Path.Combine(staging, "Dvinholm.App.exe"), "new-build");
        await File.WriteAllTextAsync(Path.Combine(staging, "new.marker"), "new");
        await File.WriteAllTextAsync(Path.Combine(destination, "Dvinholm.App.exe"), "old-build");
        await File.WriteAllTextAsync(Path.Combine(destination, "old.marker"), "old");

        try
        {
            var generated = LatestBuildUpdater.BuildReplacementScript(staging, destination, -1, scriptPath, backup);
            generated = generated.Replace(
                "& (Join-Path $env:SystemRoot 'System32\\robocopy.exe')",
                "Invoke-TestRobocopy",
                StringComparison.Ordinal);
            Assert.DoesNotContain("System32\\robocopy.exe", generated, StringComparison.OrdinalIgnoreCase);

            var mocks = @"
function Wait-Process { param([int]$Id, [int]$Timeout) }
function Invoke-TestRobocopy {
    param([string]$From, [string]$To, [Parameter(ValueFromRemainingArguments=$true)][string[]]$Options)
    if ($From -eq $env:DVINHOLM_TEST_FAIL_SOURCE) {
        if (Test-Path -LiteralPath $To) { Remove-Item -LiteralPath $To -Recurse -Force }
        New-Item -ItemType Directory -Path $To -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $To 'partial.marker') -Value 'partial'
        $global:LASTEXITCODE = 8
        return
    }
    if (Test-Path -LiteralPath $To) { Remove-Item -LiteralPath $To -Recurse -Force }
    New-Item -ItemType Directory -Path $To -Force | Out-Null
    Copy-Item -Path (Join-Path $From '*') -Destination $To -Recurse -Force
    $global:LASTEXITCODE = 1
}
function Start-Process {
    param([string]$FilePath, [string]$WorkingDirectory, [string[]]$ArgumentList, [switch]$PassThru)
    Add-Content -LiteralPath $env:DVINHOLM_TEST_LAUNCH_LOG -Value $FilePath
    return [pscustomobject]@{ HasExited = $false; ExitCode = 0 }
}
";
            await File.WriteAllTextAsync(scriptPath, mocks + Environment.NewLine + generated);

            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            Assert.True(File.Exists(powershell), $"Windows PowerShell was not found at {powershell}");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = powershell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.Environment["LOCALAPPDATA"] = localAppData;
            startInfo.Environment["DVINHOLM_TEST_FAIL_SOURCE"] = Path.GetFullPath(staging);
            startInfo.Environment["DVINHOLM_TEST_LAUNCH_LOG"] = launchLog;

            using (var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start isolated PowerShell rollback test."))
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                // Drain both redirected streams while the process is running to avoid pipe-buffer deadlocks.
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(timeout.Token);
                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                Assert.True(process.ExitCode == 0, $"PowerShell exited with {process.ExitCode}. stdout: {stdout} stderr: {stderr}");
            }

            // Dispose the process handle before deleting its working directory in the finally block.
            Assert.Equal("old-build", await File.ReadAllTextAsync(Path.Combine(destination, "Dvinholm.App.exe")));
            Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(destination, "old.marker")));
            Assert.False(File.Exists(Path.Combine(destination, "partial.marker")));
            var updateLog = await File.ReadAllTextAsync(Path.Combine(localAppData, "Dvinholm", "update.log"));
            Assert.Contains("The previous build was restored and will be restarted.", updateLog);
            Assert.Contains(Path.Combine(destination, "Dvinholm.App.exe"), await File.ReadAllTextAsync(launchLog));
        }
        finally
        {
            // Windows can briefly retain handles to files in a temporary test
            // tree after PowerShell exits. Retry transient I/O cleanup failures
            // for a bounded interval; a persistent lock must still fail the test.
            for (var attempt = 0; Directory.Exists(root); attempt++)
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch (IOException) when (attempt < 49)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                }
            }
        }
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementScript_RollsBackWhenUpdatedApplicationExitsDuringStartup(bool restoredApplicationExits)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), $"dvinholm-startup-exit-test-{Guid.NewGuid():N}");
        var staging = Path.Combine(root, "staging");
        var destination = Path.Combine(root, "installed");
        var backup = Path.Combine(root, "backup");
        var scriptPath = Path.Combine(root, "replace.ps1");
        var localAppData = Path.Combine(root, "localappdata");
        var launchLog = Path.Combine(root, "launch.log");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(localAppData);
        Directory.CreateDirectory(Path.Combine(localAppData, "Dvinholm"));
        await File.WriteAllTextAsync(Path.Combine(staging, "Dvinholm.App.exe"), "new-build");
        await File.WriteAllTextAsync(Path.Combine(destination, "Dvinholm.App.exe"), "old-build");
        await File.WriteAllTextAsync(Path.Combine(destination, "old.marker"), "old");

        try
        {
            var generated = LatestBuildUpdater.BuildReplacementScript(staging, destination, -1, scriptPath, backup);
            generated = generated.Replace(
                "& (Join-Path $env:SystemRoot 'System32\\robocopy.exe')",
                "Invoke-TestRobocopy",
                StringComparison.Ordinal);

            var mocks = @"
function Wait-Process { param([int]$Id, [int]$Timeout) }
function Start-Sleep { param([int]$Seconds, [int]$Milliseconds) }
function Invoke-TestRobocopy {
    param([string]$From, [string]$To, [Parameter(ValueFromRemainingArguments=$true)][string[]]$Options)
    if (Test-Path -LiteralPath $To) { Remove-Item -LiteralPath $To -Recurse -Force }
    New-Item -ItemType Directory -Path $To -Force | Out-Null
    Copy-Item -Path (Join-Path $From '*') -Destination $To -Recurse -Force
    $global:LASTEXITCODE = 1
}
function Start-Process {
    param([string]$FilePath, [string]$WorkingDirectory, [string[]]$ArgumentList, [switch]$PassThru)
    Add-Content -LiteralPath $env:DVINHOLM_TEST_LAUNCH_LOG -Value $FilePath
    if ($FilePath -ne 'notepad.exe' -and (
        (Get-Content -LiteralPath $FilePath -Raw) -eq 'new-build' -or
        $env:DVINHOLM_TEST_RESTORED_APP_EXITS -eq '1')) {
        return [pscustomobject]@{ HasExited = $true; ExitCode = 17 }
    }
    return [pscustomobject]@{ HasExited = $false; ExitCode = 0 }
}
";
            await File.WriteAllTextAsync(scriptPath, mocks + Environment.NewLine + generated);

            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            Assert.True(File.Exists(powershell), $"Windows PowerShell was not found at {powershell}");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = powershell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.Environment["LOCALAPPDATA"] = localAppData;
            startInfo.Environment["DVINHOLM_TEST_LAUNCH_LOG"] = launchLog;
            startInfo.Environment["DVINHOLM_TEST_RESTORED_APP_EXITS"] = restoredApplicationExits ? "1" : "0";

            using (var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start isolated PowerShell startup-exit test."))
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(timeout.Token);
                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                Assert.True(process.ExitCode == 0, $"PowerShell exited with {process.ExitCode}. stdout: {stdout} stderr: {stderr}");
            }

            Assert.Equal("old-build", await File.ReadAllTextAsync(Path.Combine(destination, "Dvinholm.App.exe")));
            Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(destination, "old.marker")));
            var updateLog = await File.ReadAllTextAsync(Path.Combine(localAppData, "Dvinholm", "update.log"));
            Assert.Contains("The updated application exited during startup (exit code 17).", updateLog);
            Assert.Contains("The previous build was restored and will be restarted.", updateLog);
            var launches = await File.ReadAllTextAsync(launchLog);
            Assert.Contains(Path.Combine(destination, "Dvinholm.App.exe"), launches);

            if (restoredApplicationExits)
            {
                Assert.Contains("The restored application exited during startup (exit code 17).", updateLog);
                Assert.Contains("Rollback failed:", updateLog);
                Assert.True(
                    File.Exists(Path.Combine(backup, "Dvinholm.App.exe")),
                    "The rollback backup must be retained when the restored app also exits during startup.");
                Assert.True(
                    Directory.Exists(staging),
                    "The extracted update must be retained when rollback fails so recovery data is not discarded.");
                Assert.False(File.Exists(scriptPath), "The temporary helper script should be removed even when rollback fails.");
            }
            else
            {
                Assert.DoesNotContain("Rollback failed:", updateLog);
                Assert.False(Directory.Exists(backup), "The backup should be removed only after the restored app survives startup.");
            }
        }
        finally
        {
            for (var attempt = 0; Directory.Exists(root); attempt++)
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch (IOException) when (attempt < 49)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                }
            }
        }
    }


    [Fact]
    public async Task ReplacementScript_ReportsWhenFirstInstallCleanupFailsAtRuntime()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "dvinholm-first-install-cleanup-failure-" + Guid.NewGuid().ToString("N"));
        var staging = Path.Combine(root, "staging");
        var destination = Path.Combine(root, "installed");
        var backup = Path.Combine(root, "backup");
        var scriptPath = Path.Combine(root, "replace.ps1");
        var localAppData = Path.Combine(root, "localappdata");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(localAppData);
        Directory.CreateDirectory(Path.Combine(localAppData, "Dvinholm"));
        await File.WriteAllTextAsync(Path.Combine(staging, "Dvinholm.App.exe"), "new-build");

        try
        {
            var generated = LatestBuildUpdater.BuildReplacementScript(staging, destination, -1, scriptPath, backup);
            generated = generated.Replace(
                "& (Join-Path $env:SystemRoot 'System32\\robocopy.exe')",
                "Invoke-TestRobocopy",
                StringComparison.Ordinal);
            var mocks = @"
function Wait-Process { param([int]$Id, [int]$Timeout) }
function Invoke-TestRobocopy {
    param([string]$From, [string]$To, [Parameter(ValueFromRemainingArguments=$true)][string[]]$Options)
    if ($From -eq $env:DVINHOLM_TEST_FAIL_SOURCE) {
        New-Item -ItemType Directory -Path $To -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $To 'partial.marker') -Value 'partial'
        $global:LASTEXITCODE = 8
        return
    }
    $global:LASTEXITCODE = 1
}
function Remove-Item {
    param([string]$LiteralPath, [switch]$Recurse, [switch]$Force, [string]$ErrorAction)
    if ($LiteralPath -eq $env:DVINHOLM_TEST_DESTINATION) {
        throw 'Simulated access denied while removing partial destination.'
    }
    Microsoft.PowerShell.Management\Remove-Item -LiteralPath $LiteralPath -Recurse:$Recurse -Force:$Force -ErrorAction $ErrorAction
}
function Start-Process {
    param([string]$FilePath, [string]$WorkingDirectory, [string[]]$ArgumentList)
    if ($FilePath -eq 'notepad.exe') { return }
    throw 'The failed first install must not launch an executable.'
}
";
            await File.WriteAllTextAsync(scriptPath, mocks + Environment.NewLine + generated);

            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            Assert.True(File.Exists(powershell), "Windows PowerShell was not found at " + powershell);

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = powershell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.Environment["LOCALAPPDATA"] = localAppData;
            startInfo.Environment["DVINHOLM_TEST_FAIL_SOURCE"] = Path.GetFullPath(staging);
            startInfo.Environment["DVINHOLM_TEST_DESTINATION"] = Path.GetFullPath(destination);

            using (var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start isolated PowerShell cleanup-failure test."))
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(timeout.Token);
                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                Assert.True(process.ExitCode == 0, "PowerShell exited with " + process.ExitCode + ". stdout: " + stdout + " stderr: " + stderr);
            }

            Assert.True(Directory.Exists(destination), "The mock should leave the incomplete destination in place.");
            var updateLog = await File.ReadAllTextAsync(Path.Combine(localAppData, "Dvinholm", "update.log"));
            Assert.Contains("Could not remove the incomplete first-install destination", updateLog);
            Assert.Contains("Simulated access denied", updateLog);
            Assert.DoesNotContain("The incomplete first-install destination was removed.", updateLog);
        }
        finally
        {
            for (var attempt = 0; Directory.Exists(root); attempt++)
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch (IOException) when (attempt < 49)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                }
            }
        }
    }


    [Fact]
    public async Task ReplacementScript_RemovesPartialDestinationWhenFirstInstallFailsAtRuntime()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "dvinholm-first-install-test-" + Guid.NewGuid().ToString("N"));
        var staging = Path.Combine(root, "staging");
        var destination = Path.Combine(root, "installed");
        var backup = Path.Combine(root, "backup");
        var scriptPath = Path.Combine(root, "replace.ps1");
        var localAppData = Path.Combine(root, "localappdata");
        var launchLog = Path.Combine(root, "launch.log");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(localAppData);
        Directory.CreateDirectory(Path.Combine(localAppData, "Dvinholm"));
        await File.WriteAllTextAsync(Path.Combine(staging, "Dvinholm.App.exe"), "new-build");
        await File.WriteAllTextAsync(Path.Combine(staging, "new.marker"), "new");

        try
        {
            var generated = LatestBuildUpdater.BuildReplacementScript(staging, destination, -1, scriptPath, backup);
            generated = generated.Replace(
                "& (Join-Path $env:SystemRoot 'System32\\robocopy.exe')",
                "Invoke-TestRobocopy",
                StringComparison.Ordinal);
            Assert.DoesNotContain("System32\\robocopy.exe", generated, StringComparison.OrdinalIgnoreCase);

            var mocks = @"
function Wait-Process { param([int]$Id, [int]$Timeout) }
function Invoke-TestRobocopy {
    param([string]$From, [string]$To, [Parameter(ValueFromRemainingArguments=$true)][string[]]$Options)
    if ($From -eq $env:DVINHOLM_TEST_FAIL_SOURCE) {
        if (Test-Path -LiteralPath $To) { Remove-Item -LiteralPath $To -Recurse -Force }
        New-Item -ItemType Directory -Path $To -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $To 'partial.marker') -Value 'partial'
        $global:LASTEXITCODE = 8
        return
    }
    if (Test-Path -LiteralPath $To) { Remove-Item -LiteralPath $To -Recurse -Force }
    New-Item -ItemType Directory -Path $To -Force | Out-Null
    Copy-Item -Path (Join-Path $From '*') -Destination $To -Recurse -Force
    $global:LASTEXITCODE = 1
}
function Start-Process {
    param([string]$FilePath, [string]$WorkingDirectory, [string[]]$ArgumentList, [switch]$PassThru)
    Add-Content -LiteralPath $env:DVINHOLM_TEST_LAUNCH_LOG -Value $FilePath
    return [pscustomobject]@{ HasExited = $false; ExitCode = 0 }
}
";
            await File.WriteAllTextAsync(scriptPath, mocks + Environment.NewLine + generated);

            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            Assert.True(File.Exists(powershell), "Windows PowerShell was not found at " + powershell);

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = powershell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.Environment["LOCALAPPDATA"] = localAppData;
            startInfo.Environment["DVINHOLM_TEST_FAIL_SOURCE"] = Path.GetFullPath(staging);
            startInfo.Environment["DVINHOLM_TEST_LAUNCH_LOG"] = launchLog;

            using (var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start isolated PowerShell first-install test."))
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(timeout.Token);
                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                Assert.True(process.ExitCode == 0, "PowerShell exited with " + process.ExitCode + ". stdout: " + stdout + " stderr: " + stderr);
            }

            Assert.False(Directory.Exists(destination), "The incomplete first-install destination should be removed.");
            Assert.False(File.Exists(Path.Combine(destination, "partial.marker")));
            var updateLog = await File.ReadAllTextAsync(Path.Combine(localAppData, "Dvinholm", "update.log"));
            Assert.Contains("The incomplete first-install destination was removed.", updateLog);
            if (File.Exists(launchLog))
                Assert.DoesNotContain(Path.Combine(destination, "Dvinholm.App.exe"), await File.ReadAllTextAsync(launchLog));
        }
        finally
        {
            for (var attempt = 0; Directory.Exists(root); attempt++)
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch (IOException) when (attempt < 49)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                }
            }
        }
    }



    [Fact]
    public async Task ReplacementScript_RemovesPartialBackupWhenBackupCopyFails()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), $"dvinholm-backup-copy-failure-{Guid.NewGuid():N}");
        var staging = Path.Combine(root, "staging");
        var destination = Path.Combine(root, "installed");
        var backup = Path.Combine(root, "backup");
        var scriptPath = Path.Combine(root, "replace.ps1");
        var localAppData = Path.Combine(root, "localappdata");
        var launchLog = Path.Combine(root, "launch.log");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(localAppData);
        Directory.CreateDirectory(Path.Combine(localAppData, "Dvinholm"));
        await File.WriteAllTextAsync(Path.Combine(staging, "Dvinholm.App.exe"), "new-build");
        await File.WriteAllTextAsync(Path.Combine(destination, "Dvinholm.App.exe"), "old-build");
        await File.WriteAllTextAsync(Path.Combine(destination, "old.marker"), "old");

        try
        {
            var generated = LatestBuildUpdater.BuildReplacementScript(staging, destination, -1, scriptPath, backup);
            generated = generated.Replace(
                "& (Join-Path $env:SystemRoot 'System32\\robocopy.exe')",
                "Invoke-TestRobocopy",
                StringComparison.Ordinal);
            var mocks = @"
function Wait-Process { param([int]$Id, [int]$Timeout) }
function Invoke-TestRobocopy {
    param([string]$From, [string]$To, [Parameter(ValueFromRemainingArguments=$true)][string[]]$Options)
    if ($From -eq $env:DVINHOLM_TEST_FAIL_SOURCE) {
        New-Item -ItemType Directory -Path $To -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $To 'partial.marker') -Value 'partial'
        $global:LASTEXITCODE = 8
        return
    }
    $global:LASTEXITCODE = 1
}
function Start-Process {
    param([string]$FilePath, [string]$WorkingDirectory, [string[]]$ArgumentList, [switch]$PassThru)
    if ($FilePath -eq 'notepad.exe') { return }
    Add-Content -LiteralPath $env:DVINHOLM_TEST_LAUNCH_LOG -Value $FilePath
    throw 'The application must not launch when backup creation fails.'
}
";
            await File.WriteAllTextAsync(scriptPath, mocks + Environment.NewLine + generated);

            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            Assert.True(File.Exists(powershell), $"Windows PowerShell was not found at {powershell}");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = powershell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.Environment["LOCALAPPDATA"] = localAppData;
            startInfo.Environment["DVINHOLM_TEST_FAIL_SOURCE"] = Path.GetFullPath(destination);
            startInfo.Environment["DVINHOLM_TEST_LAUNCH_LOG"] = launchLog;

            using (var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start isolated PowerShell backup-copy test."))
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(timeout.Token);
                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                Assert.True(process.ExitCode == 0, $"PowerShell exited with {process.ExitCode}. stdout: {stdout} stderr: {stderr}");
            }

            Assert.Equal("old-build", await File.ReadAllTextAsync(Path.Combine(destination, "Dvinholm.App.exe")));
            Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(destination, "old.marker")));
            Assert.False(Directory.Exists(backup), "A partial backup should be removed while the installed build is still untouched.");
            Assert.False(Directory.Exists(staging), "Staging files should be removed when backup creation fails.");
            Assert.False(File.Exists(scriptPath), "The helper script should be removed after backup creation fails.");
            Assert.False(File.Exists(launchLog), "The application must not launch if the existing build could not be backed up.");
            var updateLog = await File.ReadAllTextAsync(Path.Combine(localAppData, "Dvinholm", "update.log"));
            Assert.Contains("Could not back up the current build", updateLog);
            Assert.Contains("The incomplete update backup was removed because replacement had not started.", updateLog);
        }
        finally
        {
            for (var attempt = 0; Directory.Exists(root); attempt++)
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch (IOException) when (attempt < 49)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                }
            }
        }
    }

    [Fact]
    public async Task ReplacementScript_AbortsWhenPreviousProcessDoesNotExitWithinTimeout()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), $"dvinholm-process-exit-timeout-{Guid.NewGuid():N}");
        var staging = Path.Combine(root, "staging");
        var destination = Path.Combine(root, "installed");
        var backup = Path.Combine(root, "backup");
        var scriptPath = Path.Combine(root, "replace.ps1");
        var localAppData = Path.Combine(root, "localappdata");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(localAppData);
        Directory.CreateDirectory(Path.Combine(localAppData, "Dvinholm"));
        await File.WriteAllTextAsync(Path.Combine(staging, "Dvinholm.App.exe"), "new-build");
        await File.WriteAllTextAsync(Path.Combine(destination, "Dvinholm.App.exe"), "old-build");

        try
        {
            var generated = LatestBuildUpdater.BuildReplacementScript(staging, destination, 1234, scriptPath, backup);
            var mocks = @"
function Get-Process { param([int]$Id) return [pscustomobject]@{ Id = $Id } }
function Wait-Process { [CmdletBinding()] param([int]$Id, [int]$Timeout) throw 'Simulated process-exit timeout.' }
function Start-Process {
    param([string]$FilePath, [string]$WorkingDirectory, [string[]]$ArgumentList, [switch]$PassThru)
    if ($FilePath -eq 'notepad.exe') { return }
    throw 'The application must not launch when the old process does not exit.'
}
";
            await File.WriteAllTextAsync(scriptPath, mocks + Environment.NewLine + generated);

            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            Assert.True(File.Exists(powershell), $"Windows PowerShell was not found at {powershell}");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = powershell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.Environment["LOCALAPPDATA"] = localAppData;

            using (var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start isolated PowerShell process-timeout test."))
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(timeout.Token);
                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                Assert.True(process.ExitCode == 0, $"PowerShell exited with {process.ExitCode}. stdout: {stdout} stderr: {stderr}");
            }

            Assert.Equal("old-build", await File.ReadAllTextAsync(Path.Combine(destination, "Dvinholm.App.exe")));
            Assert.False(Directory.Exists(backup), "The helper must abort before creating a backup or replacing the installed build.");
            Assert.False(Directory.Exists(staging), "Staging files should be removed after an early updater failure.");
            Assert.False(File.Exists(scriptPath), "The helper script should be removed after an early updater failure.");
            var updateLog = await File.ReadAllTextAsync(Path.Combine(localAppData, "Dvinholm", "update.log"));
            Assert.Contains("Simulated process-exit timeout.", updateLog);
        }
        finally
        {
            for (var attempt = 0; Directory.Exists(root); attempt++)
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch (IOException) when (attempt < 49)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                }
            }
        }
    }


    [Fact]
    public async Task ReplacementScript_ContinuesWhenPreviousProcessExitsBeforeWaitStarts()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), $"dvinholm-process-exit-race-{Guid.NewGuid():N}");
        var staging = Path.Combine(root, "staging");
        var destination = Path.Combine(root, "installed");
        var backup = Path.Combine(root, "backup");
        var scriptPath = Path.Combine(root, "replace.ps1");
        var localAppData = Path.Combine(root, "localappdata");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(localAppData);
        Directory.CreateDirectory(Path.Combine(localAppData, "Dvinholm"));
        await File.WriteAllTextAsync(Path.Combine(staging, "Dvinholm.App.exe"), "new-build");

        try
        {
            var generated = LatestBuildUpdater.BuildReplacementScript(staging, destination, 1234, scriptPath, backup);
            generated = generated.Replace(
                "& (Join-Path $env:SystemRoot 'System32\\robocopy.exe')",
                "Invoke-TestRobocopy",
                StringComparison.Ordinal);
            var mocks = @"
$script:processChecks = 0
function Get-Process {
    param([int]$Id)
    $script:processChecks++
    if ($script:processChecks -eq 1) { return [pscustomobject]@{ Id = $Id } }
    return $null
}
function Wait-Process {
    param([int]$Id, [int]$Timeout)
    throw 'Simulated process exit between Get-Process and Wait-Process.'
}
function Invoke-TestRobocopy {
    param([string]$From, [string]$To, [Parameter(ValueFromRemainingArguments=$true)][string[]]$Options)
    if (Test-Path -LiteralPath $To) {
        Get-ChildItem -LiteralPath $To -Force | Remove-Item -Recurse -Force
    } else {
        New-Item -ItemType Directory -Path $To -Force | Out-Null
    }
    if (Test-Path -LiteralPath $From) {
        Get-ChildItem -LiteralPath $From -Force | Copy-Item -Destination $To -Recurse -Force
    }
    $global:LASTEXITCODE = 1
}
function Start-Process {
    param([string]$FilePath, [string]$WorkingDirectory, [string[]]$ArgumentList, [switch]$PassThru)
    if ($FilePath -eq 'notepad.exe') { return }
    return [pscustomobject]@{ HasExited = $false; ExitCode = 0 }
}
";
            await File.WriteAllTextAsync(scriptPath, mocks + Environment.NewLine + generated);

            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            Assert.True(File.Exists(powershell), $"Windows PowerShell was not found at {powershell}");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = powershell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.Environment["LOCALAPPDATA"] = localAppData;

            using (var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start isolated PowerShell process-exit race test."))
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(timeout.Token);
                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                Assert.True(process.ExitCode == 0, $"PowerShell exited with {process.ExitCode}. stdout: {stdout} stderr: {stderr}");
            }

            Assert.Equal("new-build", await File.ReadAllTextAsync(Path.Combine(destination, "Dvinholm.App.exe")));
            Assert.False(File.Exists(Path.Combine(localAppData, "Dvinholm", "update.log")),
                "A process that has already exited should not produce an update failure.");
        }
        finally
        {
            for (var attempt = 0; Directory.Exists(root); attempt++)
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch (IOException) when (attempt < 49)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                }
            }
        }
    }

}
