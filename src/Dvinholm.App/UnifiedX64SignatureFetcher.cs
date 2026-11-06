using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Dvinholm.App;

/// <summary>
/// Fetches only the detached signature explicitly listed on an official
/// release, with tightly restricted GitHub asset redirects. Signature
/// transport and syntactic validity NEVER imply publisher trust.
/// </summary>
internal sealed record UnifiedX64SignatureEvidence(
    bool Published, bool WellFormed, bool PublisherVerified);

internal static class UnifiedX64SignatureFetcher
{
    private const int MaximumRedirects = 3;
    private const int MaximumEncodedBytes = UnifiedX64SignatureArtifact.EncodedChars + 1;
    private const int MaximumUrlChars = 8192;

    internal static HttpClient CreateClient()
    {
        // GitHub asset downloads redirect to a signed CDN URL; automatic
        // redirects would bypass the explicit redirect-host allowlist below.
        var handler = new HttpClientHandler {
            AllowAutoRedirect = false,
            UseCookies = false
        };
        var client = new HttpClient(handler) {
            Timeout = TimeSpan.FromSeconds(25)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Dvinholm", "0.2.4"));
        return client;
    }

    internal static async Task<UnifiedX64SignatureEvidence> FetchAsync(
        UnifiedX64UpdateCandidate candidate,
        UnifiedX64StagedPackage staged,
        HttpClient http,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(staged);
        ArgumentNullException.ThrowIfNull(http);

        if (!UnifiedX64StageDownloader.ValidateCandidate(candidate) ||
            !string.Equals(staged.Sha256, staged.Sha256.ToLowerInvariant(),
                StringComparison.Ordinal) || staged.Sha256.Length != 64 ||
            staged.ZipBytes != candidate.PackageBytes)
            throw new InvalidDataException("Detached signature is not bound to a valid x64 candidate.");

        if (candidate.SignatureUrl is null)
            return new UnifiedX64SignatureEvidence(false, false, false);
        if (!UnifiedX64ReleaseFeed.TrustedAssetUrl(
            candidate.SignatureUrl, candidate.Tag,
            UnifiedX64ReleaseFeed.SignatureAsset))
            throw new InvalidDataException("Untrusted detached signature URL.");

        // Prevent a stale/tampered ZIP from receiving a seemingly credible
        // detached-signature status in the UI.
        using (var zip = new FileStream(staged.ZipPath, FileMode.Open,
            FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan))
        {
            if (zip.Length != staged.ZipBytes ||
                !string.Equals(Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant(),
                    staged.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException("Staged x64 ZIP changed since verification.");
        }

        var endpoint = new Uri(candidate.SignatureUrl);
        for (var redirect = 0; redirect <= MaximumRedirects; redirect++)
        {
            token.ThrowIfCancellationRequested();
            using var response = await http.GetAsync(endpoint,
                HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.MovedPermanently or
                HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or
                HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                if (redirect == MaximumRedirects || response.Headers.Location is null)
                    throw new InvalidDataException("Detached signature redirect limit exceeded.");
                var destination = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(endpoint, response.Headers.Location);
                if (!TrustedRedirect(destination))
                    throw new InvalidDataException("Detached signature redirected outside GitHub release CDN.");
                endpoint = destination;
                continue;
            }

            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long advertised &&
                advertised is not (88 or 89))
                throw new InvalidDataException("Invalid detached signature Content-Length.");

            await using var stream = await response.Content.ReadAsStreamAsync(token)
                .ConfigureAwait(false);
            var buffer = new byte[MaximumEncodedBytes + 1];
            var received = 0;
            while (received < buffer.Length)
            {
                var count = await stream.ReadAsync(
                    buffer.AsMemory(received), token).ConfigureAwait(false);
                if (count == 0) break;
                received += count;
            }
            if (received is not (88 or 89))
                throw new InvalidDataException("Detached signature exceeded fixed encoding size.");
            var encoded = buffer.AsSpan(0, received).ToArray();
            // Throws on malformed Base64, BOM, non-canonical padding, etc.
            UnifiedX64SignatureArtifact.Parse(encoded);
            return new UnifiedX64SignatureEvidence(
                true, true,
                UnifiedX64SignatureArtifact.VerifyPinned(
                    candidate.Tag, staged.Sha256, encoded));
        }
        throw new InvalidDataException("Detached signature retrieval did not complete.");
    }

    internal static bool TrustedRedirect(Uri uri) =>
        uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443 &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) &&
        uri.OriginalString.Length <= MaximumUrlChars &&
        (uri.Host.Equals("release-assets.githubusercontent.com",
            StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("objects.githubusercontent.com",
            StringComparison.OrdinalIgnoreCase)) &&
        uri.AbsolutePath.Length > 1;
}
