using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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
                writer.Write("""
                    <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
                      <Identity Name="Dvinholm.Lite" Publisher="CN=Dvinholm Development"
                                ProcessorArchitecture="x86" Version="0.2.2.0" />
                    </Package>
                    """);

            using var rsa = RSA.Create(2048);
            var subject = scenario == "publisher-mismatch"
                ? "CN=Unexpected Publisher" : "CN=Dvinholm Development";
            var req = new CertificateRequest(new X500DistinguishedName(subject),
                rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var usages = scenario == "wrong-key-usage"
                ? X509KeyUsageFlags.KeyEncipherment
                : X509KeyUsageFlags.DigitalSignature;
            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(usages, critical: true));
            var enhancedUsages = new OidCollection
            {
                new Oid(scenario == "wrong-eku"
                    ? "1.3.6.1.5.5.7.3.2" : "1.3.6.1.5.5.7.3.3")
            };
            req.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(enhancedUsages, critical: true));
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
            var certPath = Path.Combine(root, "public.cer");
            if (scenario != "missing-certificate")
                File.WriteAllBytes(certPath, cert.Export(X509ContentType.Cert));

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
