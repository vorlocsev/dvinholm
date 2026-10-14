using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Dvinholm.App;

internal sealed record LatestBuildInfo(
    string CommitSha,
    int RunNumber,
    long RunId,
    long ArtifactId,
    long ArtifactSize,
    string? ArtifactDigest,
    string CreatedAt)
{
    public string ShortSha => CommitSha.Length >= 7 ? CommitSha[..7] : CommitSha;
    public string DisplayVersion => $"main {ShortSha} · CI #{RunNumber}";
}

internal static class LatestBuildUpdater
{
    private const string Repository = "vorlocsev/dvinholm";
    private const long WorkflowId = 378992585;
    private const string ArtifactName = "Dvinholm-Windows-x86";
    private const long MaximumDownloadBytes = 500L * 1024 * 1024;
    private const long MaximumExpandedBytes = 1_000L * 1024 * 1024;
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Dvinholm", "0.1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    public static string CurrentVersion
    {
        get
        {
            var value = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return string.IsNullOrWhiteSpace(value) ? "version unknown" : value;
        }
    }

    public static string CurrentDisplayVersion
    {
        get
        {
            var value = CurrentVersion;
            var plus = value.LastIndexOf('+');
            var sha = GetEmbeddedCommitSha(value, plus);
            return plus >= 0 && sha is not null
                ? $"{value[..plus]} · {sha[..7]}"
                : value;
        }
    }

    public static string CurrentWindowTitle =>
        CurrentCiRunNumber is int runNumber ? $"Dvinholm CI #{runNumber}" : "Dvinholm";

    public static int? CurrentCiRunNumber
    {
        get
        {
            var value = CurrentVersion;
            var plus = value.LastIndexOf('+');
            if (plus < 0)
                return null;

            var suffix = value[(plus + 1)..];
            var marker = suffix.LastIndexOf(".ci", StringComparison.OrdinalIgnoreCase);
            return marker >= 0 && int.TryParse(suffix[(marker + 3)..], out var runNumber) && runNumber > 0
                ? runNumber
                : null;
        }
    }

    public static string? CurrentCommitSha => GetEmbeddedCommitSha(CurrentVersion, CurrentVersion.LastIndexOf('+'));

    private static string? GetEmbeddedCommitSha(string value, int plus)
    {
        if (plus < 0)
            return null;

        var suffix = value[(plus + 1)..];
        var marker = suffix.IndexOf(".ci", StringComparison.OrdinalIgnoreCase);
        var sha = marker >= 0 ? suffix[..marker] : suffix;
        return sha.Length == 40 && sha.All(Uri.IsHexDigit) ? sha.ToLowerInvariant() : null;
    }

    public static async Task<LatestBuildInfo> GetLatestSuccessfulBuildAsync(CancellationToken cancellationToken = default)
    {
        var runsUrl = $"https://api.github.com/repos/{Repository}/actions/workflows/{WorkflowId}/runs?branch=main&status=success&per_page=20";
        using var runsResponse = await Http.GetAsync(runsUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        runsResponse.EnsureSuccessStatusCode();
        await using var runsStream = await runsResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var runsJson = await JsonDocument.ParseAsync(runsStream, cancellationToken: cancellationToken);

        if (!runsJson.RootElement.TryGetProperty("workflow_runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("GitHub did not return the CI workflow run list.");

        JsonElement latestRun = default;
        foreach (var run in runs.EnumerateArray())
        {
            if (ReadString(run, "head_branch") == "main" &&
                ReadString(run, "status") == "completed" &&
                ReadString(run, "conclusion") == "success")
            {
                latestRun = run;
                break;
            }
        }

        if (latestRun.ValueKind == JsonValueKind.Undefined)
            throw new InvalidOperationException("No successful Windows x86 CI run was found on main.");

        var sha = ReadString(latestRun, "head_sha");
        if (sha.Length != 40 || !sha.All(Uri.IsHexDigit))
            throw new InvalidDataException("The latest successful CI run has an invalid commit SHA.");

        var runId = ReadInt64(latestRun, "id");
        var runNumber = checked((int)ReadInt64(latestRun, "run_number"));
        var createdAt = ReadString(latestRun, "created_at");
        var artifactsUrl = $"https://api.github.com/repos/{Repository}/actions/runs/{runId}/artifacts";
        using var artifactsResponse = await Http.GetAsync(artifactsUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        artifactsResponse.EnsureSuccessStatusCode();
        await using var artifactsStream = await artifactsResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var artifactsJson = await JsonDocument.ParseAsync(artifactsStream, cancellationToken: cancellationToken);

        if (!artifactsJson.RootElement.TryGetProperty("artifacts", out var artifacts) || artifacts.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("GitHub did not return the latest CI artifact list.");

        foreach (var artifact in artifacts.EnumerateArray())
        {
            if (ReadString(artifact, "name") != ArtifactName)
                continue;
            if (ReadBoolean(artifact, "expired"))
                throw new InvalidOperationException("The latest CI artifact has expired. A new successful main build is required.");
            var size = ReadInt64(artifact, "size_in_bytes");
            if (size <= 0 || size > MaximumDownloadBytes)
                throw new InvalidDataException("The latest CI artifact has an invalid or excessive size.");

            var digest = ReadString(artifact, "digest");
            return new LatestBuildInfo(
                sha.ToLowerInvariant(),
                runNumber,
                runId,
                ReadInt64(artifact, "id"),
                size,
                digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest.ToLowerInvariant() : null,
                createdAt);
        }

        throw new InvalidOperationException("The latest successful main CI run has no Windows x86 artifact.");
    }

    public static async Task<string> DownloadAndPrepareAsync(
        LatestBuildInfo build,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dvinholm",
            "Builds");
        Directory.CreateDirectory(root);

        var destination = Path.Combine(root, build.CommitSha);
        var executable = Path.Combine(destination, "Dvinholm.App.exe");
        if (File.Exists(executable))
        {
            progress?.Report("The build is already downloaded; preparing to launch it…");
            return executable;
        }

        var staging = Path.Combine(root, $".staging-{build.CommitSha}-{Guid.NewGuid():N}");
        var archivePath = Path.Combine(Path.GetTempPath(), $"dvinholm-{build.CommitSha}-{Guid.NewGuid():N}.zip");

        try
        {
            progress?.Report($"Downloading CI artifact #{build.RunNumber}…");
            var downloadUrl = $"https://api.github.com/repos/{Repository}/actions/artifacts/{build.ArtifactId}/zip";
            using (var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long length && length > MaximumDownloadBytes)
                    throw new InvalidDataException("The downloaded artifact exceeds the configured size limit.");

                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                var buffer = new byte[81920];
                long total = 0;
                while (true)
                {
                    var read = await input.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;
                    total += read;
                    if (total > MaximumDownloadBytes)
                        throw new InvalidDataException("The downloaded artifact exceeds the configured size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            var downloadedSize = new FileInfo(archivePath).Length;
            if (downloadedSize != build.ArtifactSize)
                throw new InvalidDataException("The downloaded artifact size does not match GitHub's metadata.");

            if (build.ArtifactDigest is not null)
            {
                progress?.Report("Verifying the downloaded artifact checksum…");
                await using var checksumStream = File.OpenRead(archivePath);
                var checksum = Convert.ToHexString(await SHA256.HashDataAsync(checksumStream, cancellationToken)).ToLowerInvariant();
                if (!string.Equals("sha256:" + checksum, build.ArtifactDigest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded artifact checksum does not match GitHub's digest.");
            }

            progress?.Report("Validating and extracting the build…");
            Directory.CreateDirectory(staging);
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                long expandedBytes = 0;
                var fullRoot = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    expandedBytes = checked(expandedBytes + entry.Length);
                    if (expandedBytes > MaximumExpandedBytes)
                        throw new InvalidDataException("The extracted artifact exceeds the configured size limit.");

                    var target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                    if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The artifact contains an unsafe archive path.");
                }

                archive.ExtractToDirectory(staging);
            }

            if (!File.Exists(Path.Combine(staging, "Dvinholm.App.exe")))
                throw new InvalidDataException("The downloaded artifact does not contain Dvinholm.App.exe.");

            if (Directory.Exists(destination))
                Directory.Delete(destination, recursive: true);
            Directory.Move(staging, destination);
            return executable;
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch (IOException) { }
            try { if (File.Exists(archivePath)) File.Delete(archivePath); } catch (IOException) { }
        }
    }

    public static Process StartBuild(string executablePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = true
        };
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Windows could not start the updated application.");
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static long ReadInt64(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt64(out var result) ? result : 0;

    private static bool ReadBoolean(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
}
