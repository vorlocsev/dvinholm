using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Public DER-only signing certificate preflight. Uses disposable in-memory
/// self-signed certificates; never writes a PFX or changes Windows trust stores.
/// </summary>
public sealed class WindowsLiteMsixSigningCertificatePreflightTests
{
    [Theory]
    [InlineData("valid", true, "PRE-SIGN CHECK ONLY")]
    [InlineData("publisher-mismatch", false, "Subject does not exactly match")]
    [InlineData("missing-certificate", false, "Cannot find path")]
    [InlineData("wrong-eku", false, "code-signing EKU")]
    [InlineData("wrong-key-usage", false, "digitalSignature")]
    [InlineData("expired", false, "expired")]
    [InlineData("not-yet-valid", false, "not yet valid")]
    [InlineData("ca-certificate", false, "not a CA")]
    [InlineData("non-der-pem", false, "DER .cer")]
    [InlineData("der-trailing-bytes", false, "DER .cer")]
    [InlineData("wrong-extension", false, "Use only a public DER .cer")]
    [InlineData("missing-eku", false, "must declare code-signing EKU")]
    [InlineData("multiple-eku", false, "code-signing EKU only")]
    [InlineData("missing-keyusage", false, "must declare digitalSignature")]
    [InlineData("missing-basicconstraints", false, "end-entity signing certificate")]
    [InlineData("wrong-msix-identity", false, "MSIX identity is not exactly")]
    [InlineData("rsa-1024", false, "RSA key size at least 2048")]
    [InlineData("rsa-3072", true, "PRE-SIGN CHECK ONLY")]
    [InlineData("rsa-4096", true, "PRE-SIGN CHECK ONLY")]
    [InlineData("sha1-rsa", false, "requires SHA-2 RSA signature")]
    [InlineData("sha384-rsa", true, "PRE-SIGN CHECK ONLY")]
    [InlineData("sha512-rsa", true, "PRE-SIGN CHECK ONLY")]
    [InlineData("ecdsa-p256", false, "must use RSA public key")]
    [InlineData("ecdsa-p384", false, "must use RSA public key")]
    public async Task PublicCertificatePreflight_RejectsUnusableCertificates(
        string scenario, bool success, string expectedMessage)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-msix-certificate",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var packagePath = Path.Combine(root, "fixture.msix");
            using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("AppxManifest.xml").Open()))
            {
                var manifest = """
                    <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
                      <Identity Name="Dvinholm.Lite" Publisher="CN=Dvinholm Development"
                                ProcessorArchitecture="x86" Version="0.2.2.0" />
                    </Package>
                    """;
                if (scenario == "wrong-msix-identity")
                    manifest = manifest.Replace("Name=\"Dvinholm.Lite\"",
                        "Name=\"Another.Package\"");
                writer.Write(manifest);
            }

            var useEcdsa = scenario.StartsWith("ecdsa-", StringComparison.Ordinal);
            var rsaBits = scenario switch
            {
                "rsa-1024" => 1024,
                "rsa-3072" => 3072,
                "rsa-4096" => 4096,
                _ => 2048
            };
            using RSA? rsa = useEcdsa ? null : RSA.Create(rsaBits);
            using ECDsa? ecdsa = useEcdsa
                ? ECDsa.Create(scenario == "ecdsa-p384"
                    ? ECCurve.NamedCurves.nistP384 : ECCurve.NamedCurves.nistP256)
                : null;
            var subject = scenario == "publisher-mismatch"
                ? "CN=Unexpected Publisher" : "CN=Dvinholm Development";
            var digest = scenario switch
            {
                "sha384-rsa" or "ecdsa-p384" => HashAlgorithmName.SHA384,
                "sha512-rsa" => HashAlgorithmName.SHA512,
                _ => HashAlgorithmName.SHA256
            };
            var req = useEcdsa
                ? new CertificateRequest(new X500DistinguishedName(subject),
                    ecdsa!, digest)
                : new CertificateRequest(new X500DistinguishedName(subject),
                    rsa!, digest, RSASignaturePadding.Pkcs1);
            var usages = scenario == "wrong-key-usage"
                ? X509KeyUsageFlags.KeyEncipherment
                : X509KeyUsageFlags.DigitalSignature;
            if (scenario != "missing-keyusage")
                req.CertificateExtensions.Add(
                    new X509KeyUsageExtension(usages, critical: true));
            var enhancedUsages = new OidCollection
            {
                new Oid(scenario == "wrong-eku"
                    ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.3")
            };
            if (scenario == "multiple-eku")
                enhancedUsages.Add(new Oid("1.3.6.1.5.5.7.3.2"));
            if (scenario != "missing-eku")
                req.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(enhancedUsages, critical: true));
            if (scenario != "missing-basicconstraints")
                req.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(
                        certificateAuthority: scenario == "ca-certificate",
                        hasPathLengthConstraint: false, pathLengthConstraint: 0,
                        critical: true));
            var now = DateTimeOffset.UtcNow;
            var from = scenario == "expired" ? now.AddDays(-5)
                : scenario == "not-yet-valid" ? now.AddDays(2) : now.AddDays(-1);
            var until = scenario == "expired" ? now.AddDays(-1)
                : scenario == "not-yet-valid" ? now.AddDays(5) : now.AddDays(5);
            using var cert = req.CreateSelfSigned(from, until);
            var certPath = Path.Combine(root, scenario == "wrong-extension"
                ? "public.pem" : "public.cer");
            if (scenario != "missing-certificate")
            {
                byte[] publicDer = cert.Export(X509ContentType.Cert);
                if (scenario == "sha1-rsa")
                    publicDer = WithSyntheticSha1SignatureAlgorithm(publicDer);
                if (scenario == "non-der-pem")
                    publicDer = Encoding.ASCII.GetBytes(
                        "-----BEGIN CERTIFICATE-----\n" +
                        Convert.ToBase64String(publicDer, Base64FormattingOptions.InsertLineBreaks) +
                        "\n-----END CERTIFICATE-----\n");
                if (scenario == "der-trailing-bytes")
                    publicDer = publicDer.Concat(new byte[] { 0x01, 0x02 }).ToArray();
                File.WriteAllBytes(certPath, publicDer);
            }

            var shell = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell",
                "v1.0", "powershell.exe");
            var start = new ProcessStartInfo
            {
                FileName = shell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindScript(), "-PackagePath", packagePath,
                "-CertificatePath", certPath
            }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("PowerShell preflight did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (success)
            {
                Assert.True(process.ExitCode == 0, output);
                Assert.Contains("metadata matches MSIX Publisher", output);
                Assert.Contains("no private key", output);
            }
            else Assert.NotEqual(0, process.ExitCode);
            Assert.Contains(expectedMessage, output);
        }
        finally { await WindowsTestFixtureCleanup.RemoveBestEffortAsync(root); }
    }

    // CertificateRequest in .NET 10 refuses SHA-1 when creating a test
    // certificate. Change only both equal-length signature-algorithm OIDs
    // in a DER-encoded SHA-256 test certificate, so the read-only metadata
    // preflight can reject SHA-1. This deliberately invalidates its actual
    // signature and is NOT a fixture for verifying cryptographic signatures.
    private static byte[] WithSyntheticSha1SignatureAlgorithm(byte[] original)
    {
        ReadOnlySpan<byte> sha256RsaOid =
            [0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0B];
        var result = (byte[])original.Clone();
        var matches = 0;
        for (var i = 0; i <= result.Length - sha256RsaOid.Length; i++)
        {
            if (!result.AsSpan(i, sha256RsaOid.Length).SequenceEqual(sha256RsaOid))
                continue;
            result[i + sha256RsaOid.Length - 1] = 0x05; // sha1WithRSAEncryption
            matches++;
            i += sha256RsaOid.Length - 1;
        }
        Assert.Equal(2, matches); // tbsCertificate and outer signatureAlgorithm
        return result;
    }

    private static string FindScript()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "eng",
                "Test-LiteMsixSigningCertificate.ps1");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Public MSIX certificate preflight script not found.");
    }
}
