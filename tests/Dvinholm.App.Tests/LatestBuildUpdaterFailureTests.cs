using System.IO.Compression;
using System.Net;

namespace Dvinholm.App.Tests;

public sealed class LatestBuildUpdaterFailureTests
{
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
        Assert.Contains("$replacementStarted = $true", script);
        Assert.Contains("$destination $backup /MIR", script);
        Assert.True(script.IndexOf("$destination $backup /MIR", StringComparison.Ordinal) < script.IndexOf("$source $destination /MIR", StringComparison.Ordinal));
        Assert.Contains("$source $destination /MIR", script);
        Assert.Contains("if ($replacementStarted -and $hadDestination -and $backupReady)", script);
        Assert.Contains("elseif ($replacementStarted -and -not $hadDestination)", script);
        Assert.Contains("Remove-Item -LiteralPath $destination -Recurse -Force -ErrorAction SilentlyContinue", script);
        Assert.Contains("The incomplete first-install destination was removed.", script);
        Assert.Contains("$backup $destination /MIR", script);
        Assert.Contains("The previous build was restored and will be restarted.", script);
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
function Wait-Process { param([int]$Id) }
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
    param([string]$FilePath, [string]$WorkingDirectory, [string[]]$ArgumentList)
    Add-Content -LiteralPath $env:DVINHOLM_TEST_LAUNCH_LOG -Value $FilePath
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
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
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
function Wait-Process { param([int]$Id) }
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
    param([string]$FilePath, [string]$WorkingDirectory, [string[]]$ArgumentList)
    Add-Content -LiteralPath $env:DVINHOLM_TEST_LAUNCH_LOG -Value $FilePath
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
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
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


}
