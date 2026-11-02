using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dvinholm.App;

/// <summary>
/// Read-only release metadata. This is NOT a package integrity verification:
/// a release and a checksum asset merely establish a possible x64 candidate.
/// Installation stays disabled until the ZIP is downloaded, hashed and its
/// payload architecture and trusted provenance are independently verified.
/// </summary>
internal sealed record UnifiedX64UpdateCandidate(
    string Tag, string PackageName, long PackageBytes, string PackageUrl,
    string ChecksumUrl);

internal static class UnifiedX64ReleaseFeed
{
    internal const string PackageAsset = "Dvinholm-Windows-x64.zip";
    internal const string ChecksumsAsset = "SHA256SUMS.txt";
    private const string ApiUrl =
        "https://api.github.com/repos/vorlocsev/dvinholm/releases?per_page=100";
    private const int MaximumMetadataBytes = 1024 * 1024;
    private const long MaximumPackageBytes = 500L * 1024 * 1024;

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Dvinholm", "0.2.4"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    internal static async Task<UnifiedX64UpdateCandidate?> CheckAsync(
        UpdateSourceChannel channel, CancellationToken token = default)
    {
        using var response = await Http.GetAsync(
            ApiUrl, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long size &&
            size > MaximumMetadataBytes)
            throw new InvalidDataException("GitHub release metadata is too large.");

        await using var source = await response.Content.ReadAsStreamAsync(token);
        using var copy = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await source.ReadAsync(chunk, token)) != 0)
        {
            if (copy.Length + read > MaximumMetadataBytes)
                throw new InvalidDataException("GitHub release metadata exceeded the size limit.");
            copy.Write(chunk, 0, read);
        }
        return SelectCandidate(copy.ToArray(), channel);
    }

    internal static UnifiedX64UpdateCandidate? SelectCandidate(
        ReadOnlyMemory<byte> json, UpdateSourceChannel channel)
    {
        if (!Enum.IsDefined(channel))
            throw new ArgumentOutOfRangeException(nameof(channel));
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Unexpected GitHub release response.");

        // GitHub returns releases newest first. Requiring matching tag and asset
        // names prevents legacy x86 assets from being advertised as x64.
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object ||
                Bool(release, "draft") != false)
                continue;
            var tag = Str(release, "tag_name");
            var prerelease = Bool(release, "prerelease");
            if (!MatchesChannel(tag, prerelease, channel) ||
                !release.TryGetProperty("assets", out var assets) ||
                assets.ValueKind != JsonValueKind.Array)
                continue;

            JsonElement? package = null, checksums = null;
            var packageCount = 0;
            var checksumCount = 0;
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.ValueKind != JsonValueKind.Object)
                    continue;
                var name = Str(asset, "name");
                if (name == PackageAsset) { package = asset; packageCount++; }
                if (name == ChecksumsAsset) { checksums = asset; checksumCount++; }
            }
            if (packageCount != 1 || checksumCount != 1 ||
                package is null || checksums is null)
                continue;

            var bytes = Int64(package.Value, "size");
            var packageUrl = Str(package.Value, "browser_download_url");
            var sumsUrl = Str(checksums.Value, "browser_download_url");
            if (bytes is null or <= 0 or > MaximumPackageBytes ||
                !TrustedAssetUrl(packageUrl, tag, PackageAsset) ||
                !TrustedAssetUrl(sumsUrl, tag, ChecksumsAsset))
                continue;

            return new UnifiedX64UpdateCandidate(
                tag, PackageAsset, bytes.Value, packageUrl, sumsUrl);
        }
        return null;
    }

    internal static bool MatchesChannel(string tag, bool? prerelease, UpdateSourceChannel channel) =>
        channel switch
        {
            UpdateSourceChannel.Stable => prerelease == false &&
                Regex.IsMatch(tag, @"^v[0-9]+\.[0-9]+\.[0-9]+$", RegexOptions.CultureInvariant),
            UpdateSourceChannel.Development => prerelease == true &&
                Regex.IsMatch(tag, @"^ci-[0-9]+$", RegexOptions.CultureInvariant),
            _ => false
        };

    internal static bool TrustedAssetUrl(string url, string tag, string name)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.Host != "github.com" ||
            uri.Port != 443 || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            return false;
        return uri.AbsolutePath == "/vorlocsev/dvinholm/releases/download/" +
            Uri.EscapeDataString(tag) + "/" + name;
    }

    private static string Str(JsonElement element, string key) =>
        element.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.String
            ? item.GetString() ?? string.Empty
            : string.Empty;

    private static bool? Bool(JsonElement element, string key) =>
        element.TryGetProperty(key, out var item) ?
            item.ValueKind switch {
                JsonValueKind.True => true, JsonValueKind.False => false, _ => null
            } : null;

    private static long? Int64(JsonElement element, string key) =>
        element.TryGetProperty(key, out var item) &&
        item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var number)
            ? number : null;
}
