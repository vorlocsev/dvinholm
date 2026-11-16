using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight local verifier checks. Synthetic ZIP and private temp folder only;
/// no J2534 session, ECU, PIN bytes or network access.
/// </summary>
public sealed class CemPinBenchAcceptanceVerifierTests
{
    [Fact]
    public void ValidGeneratorReportRemainsStrictlyNotTested()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        var result = f.Check();
        Assert.True(result.Code == 0, $"Verifier failed: {result.Text}");
        Assert.Contains("NOT_TESTED", result.Text);
        Assert.Contains("False", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(f.PriorJson, File.ReadAllText(f.Report));
        Assert.Equal(f.PriorHash, Hash(f.ZipFile));
    }

    [Fact]
    public void RequiresExplicitConfirmationBeforeInspection()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        var result = f.Check(confirm: false);
        Assert.NotEqual(0, result.Code);
        Assert.Equal(f.PriorJson, File.ReadAllText(f.Report));
    }

    [Fact]
    public void MutatingLocalZipInvalidatesReportHash()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        // Valid ZIP after modification, but SHA-256 differs.
        using (var zip = ZipFile.Open(f.ZipFile, ZipArchiveMode.Update))
        {
            var entry = zip.CreateEntry("modified-after-preparation.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("changed bytes");
        }
        var result = f.Check();
        Assert.NotEqual(0, result.Code);
        Assert.Contains("SHA-256", result.Text);
    }

    [Fact]
    public void ForgedGatePassOrEvidenceIsRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        f.Mutate(root =>
        {
            root["gates"]![0]!["status"] = "Passed";
            root["gates"]![0]!["evidenceVerified"] = true;
        });
        Assert.NotEqual(0, f.Check().Code);
    }

    [Fact]
    public void AddedCredentialFieldIsRejectedByStrictSchema()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        f.Mutate(root => root["pinBytes"] = "synthetic-dont-publish");
        Assert.NotEqual(0, f.Check().Code);
        f.Restore();
        f.Mutate(root => root["gates"]![1]!["vin"] = "synthetic-no-real-vin");
        Assert.NotEqual(0, f.Check().Code);
    }

    [Fact]
    public void DuplicateMissingOrReorderedGatesFail()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        f.Mutate(root =>
        {
            var gates = root["gates"]!.AsArray();
            gates[2]!["gateId"] = gates[1]!["gateId"]!.GetValue<string>();
        });
        Assert.NotEqual(0, f.Check().Code);
        f.Restore();
        f.Mutate(root => root["gates"]!.AsArray().RemoveAt(7));
        Assert.NotEqual(0, f.Check().Code);
    }

    [Fact]
    public void InvalidTopLevelClaimsAndMalformedJsonFailClosed()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        f.Mutate(root => root["sourceCommitIndependentlyVerified"] = true);
        Assert.NotEqual(0, f.Check().Code);
        f.Restore();
        f.Mutate(root => root["sourceCommitOperatorDeclared"] = "bad");
        Assert.NotEqual(0, f.Check().Code);
        f.Restore();
        File.WriteAllText(f.Report, "{");
        Assert.NotEqual(0, f.Check().Code);
    }

    [Fact]
    public void ValidatorHasNoTransmitMutationOrAutomatedAcceptance()
    {
        var script = File.ReadAllText(Fixture.Find("eng/Verify-CemPinBenchAcceptance.ps1"));
        Assert.Contains("#requires -Version 5.1", script);
        Assert.Contains("Assert-ExactProperties", script);
        Assert.Contains("Compute-Sha256", script);
        Assert.Contains("sourceCommitIndependentlyVerified = $false", script);
        Assert.Contains("result = 'NOT_TESTED'", script);
        foreach (var forbidden in new[] {
            "PassThruOpen", "PassThruConnect", "SendAsync", "Start-Process",
            "Invoke-WebRequest", "Invoke-RestMethod", "ReadCemPin_Click",
            "Set-Content", "WriteAllText", "Copy-Item", "Move-Item",
            "ExtractToDirectory", "gh release", "git push"
        })
            Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "DvinholmCemBenchVerifierFixture", Guid.NewGuid().ToString("N"));
        private readonly string _generator;
        private readonly string _verifier;
        internal string ZipFile => Path.Combine(_root, "Dvinholm-Windows-x64.zip");
        internal string Report => Path.Combine(_root, "evidence", "cem-pin-bench-acceptance.json");
        internal string PriorJson { get; }
        internal string PriorHash { get; }

        internal Fixture()
        {
            Directory.CreateDirectory(_root);
            var eng = Path.Combine(_root, "checkout", "eng");
            Directory.CreateDirectory(eng);
            _generator = Path.Combine(eng, "Prepare-CemPinBenchAcceptance.ps1");
            _verifier = Path.Combine(eng, "Verify-CemPinBenchAcceptance.ps1");
            File.Copy(Find("eng/Prepare-CemPinBenchAcceptance.ps1"), _generator);
            File.Copy(Find("eng/Verify-CemPinBenchAcceptance.ps1"), _verifier);
            using (var zip = System.IO.Compression.ZipFile.Open(ZipFile, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("Dvinholm.exe");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("synthetic-only fixture; never executed");
            }
            var generated = Run(_generator, new[] {
                "-BuildZip", ZipFile, "-CommitSha",
                "0123456789abcdef0123456789abcdef01234567",
                "-OutputDirectory", Path.GetDirectoryName(Report)!,
                "-IConfirmOfflineNoSecrets"
            });
            Assert.True(generated.Code == 0, $"Failed synthetic preparation: {generated.Text}");
            PriorJson = File.ReadAllText(Report);
            PriorHash = Hash(ZipFile);
        }

        internal (int Code, string Text) Check(bool confirm = true)
        {
            var args = new List<string> { "-ReportJson", Report, "-BuildZip", ZipFile };
            if (confirm) args.Add("-IConfirmOfflineNoSecrets");
            return Run(_verifier, args);
        }

        internal void Mutate(Action<JsonObject> action)
        {
            var root = JsonNode.Parse(File.ReadAllText(Report))!.AsObject();
            action(root);
            File.WriteAllText(Report, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        internal void Restore() => File.WriteAllText(Report, PriorJson);

        private (int Code, string Text) Run(string script, IEnumerable<string> args)
        {
            var psi = new ProcessStartInfo("powershell.exe")
            {
                WorkingDirectory = _root,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoLogo", "-NoProfile", "-NonInteractive",
                "-ExecutionPolicy", "Bypass", "-File", script
            }.Concat(args))
                psi.ArgumentList.Add(arg);
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Windows PowerShell did not start.");
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Offline CEM acceptance verifier timed out.");
            }
            return (process.ExitCode,
                process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
        }

        internal static string Find(string relative)
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
                 dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
            }
            throw new FileNotFoundException(relative);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
