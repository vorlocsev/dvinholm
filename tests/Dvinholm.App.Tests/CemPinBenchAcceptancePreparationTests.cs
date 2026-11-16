using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>Runs only against synthetic ZIPs and temporary Windows folders.</summary>
public sealed class CemPinBenchAcceptancePreparationTests
{
    [Fact]
    public void NoConfirmationCreatesNothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        Assert.NotEqual(0, f.Run(false).Code);
        Assert.False(Directory.Exists(f.Output));
    }

    [Fact]
    public void ExactZipHashAndEightNotTestedGatesAreWritten()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        var result = f.Run(true);
        Assert.Equal(0, result.Code);
        using var doc = JsonDocument.Parse(File.ReadAllText(f.Report));
        var root = doc.RootElement;
        Assert.Equal("dvinholm.cem-pin.bench-preparation.v1",
            root.GetProperty("schema").GetString());
        Assert.Equal("NOT_TESTED", root.GetProperty("status").GetString());
        Assert.Equal(Fixture.CommitSha, root.GetProperty("sourceCommitOperatorDeclared").GetString());
        Assert.False(root.GetProperty("sourceCommitIndependentlyVerified").GetBoolean());
        Assert.False(root.GetProperty("pinAcquisitionVerified").GetBoolean());
        Assert.False(root.GetProperty("exactPhysicalCemIdentityVerified").GetBoolean());
        Assert.False(root.GetProperty("mayIssueCemRequest").GetBoolean());
        Assert.False(root.GetProperty("mayWriteConfiguration").GetBoolean());
        Assert.Equal(f.ZipSha, root.GetProperty("buildZipSha256").GetString());
        Assert.Equal(new FileInfo(f.ZipPath).Length, root.GetProperty("buildZipBytes").GetInt64());
        var gates = root.GetProperty("gates").EnumerateArray().ToArray();
        Assert.Equal(8, gates.Length);
        Assert.Equal(8, gates.Select(x => x.GetProperty("gateId").GetString())
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(gates, g =>
        {
            Assert.Equal("NotTested", g.GetProperty("status").GetString());
            Assert.False(g.GetProperty("evidenceVerified").GetBoolean());
        });
        Assert.Single(Directory.EnumerateFiles(f.Output));
        Assert.Contains("NOT_TESTED", result.Text);
    }

    [Fact]
    public void ExistingOutputDirectoryIsNeverOverwritten()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        Directory.CreateDirectory(f.Output);
        var existing = Path.Combine(f.Output, "keep.txt");
        File.WriteAllText(existing, "retain");
        Assert.NotEqual(0, f.Run(true).Code);
        Assert.Equal("retain", File.ReadAllText(existing));
        Assert.False(File.Exists(f.Report));
    }

    [Fact]
    public void OutputInsideCheckoutIsForbidden()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        var output = Path.Combine(f.Checkout, "bench");
        Assert.NotEqual(0, f.Run(true, outputOverride: output).Code);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void MissingExeInArchiveFailsBeforeAnyOutput()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture(executable: false);
        Assert.NotEqual(0, f.Run(true).Code);
        Assert.False(Directory.Exists(f.Output));
    }

    [Fact]
    public void InvalidCommitShaCannotCreateEvidence()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        Assert.NotEqual(0, f.Run(true, shaOverride: "invalid").Code);
        Assert.False(Directory.Exists(f.Output));
    }

    [Fact]
    public void PrivatePathsAndVehicleIdentifiersAreNotSerialized()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        Assert.Equal(0, f.Run(true).Code);
        var json = File.ReadAllText(f.Report);
        Assert.DoesNotContain(f.Checkout, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(f.Output, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(f.ZipPath, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VIN", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("serialNumber", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("candidateBytes", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("txId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rxId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScriptContainsNoVehicleExecutionOrAutomaticPassing()
    {
        var script = File.ReadAllText(Fixture.SourcePath);
        Assert.Contains("#requires -Version 5.1", script);
        Assert.Contains("sourceCommitIndependentlyVerified = $false", script);
        Assert.Contains("pinAcquisitionVerified = $false", script);
        Assert.Contains("mayIssueCemRequest = $false", script);
        Assert.Contains("status = 'NotTested'", script);
        foreach (var forbidden in new[]
        {
            "PassThruOpen", "PassThruConnect", "Start-Process",
            "Invoke-WebRequest", "Invoke-RestMethod", "SendAsync",
            "CanBus.Send(", "CemPartNumberReader.Read(", "Get-Process",
            "git push", "gh release", "ReadCemPin_Click("
        })
            Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Fixture : IDisposable
    {
        internal const string CommitSha = "0123456789abcdef0123456789abcdef01234567";
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "DvinholmCemBenchFixture", Guid.NewGuid().ToString("N"));
        internal string Checkout => Path.Combine(_root, "checkout");
        internal string Output => Path.Combine(_root, "private-review");
        internal string Report => Path.Combine(Output, "cem-pin-bench-acceptance.json");
        internal string ZipPath => Path.Combine(_root, "Dvinholm-Windows-x64.zip");
        internal string ZipSha => Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(ZipPath))).ToLowerInvariant();
        internal static string SourcePath => Source();

        internal Fixture(bool executable = true)
        {
            var eng = Path.Combine(Checkout, "eng");
            Directory.CreateDirectory(eng);
            File.Copy(SourcePath, Path.Combine(eng, "Prepare-CemPinBenchAcceptance.ps1"));
            using var zip = ZipFile.Open(ZipPath, ZipArchiveMode.Create);
            var entry = zip.CreateEntry(executable ? "Dvinholm.exe" : "README.txt");
            using var stream = new StreamWriter(entry.Open());
            stream.Write("fixture only, never executed");
        }

        internal (int Code, string Text) Run(bool confirm,
            string? outputOverride = null, string? shaOverride = null)
        {
            var psi = new ProcessStartInfo("powershell.exe")
            {
                WorkingDirectory = _root, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[]
            {
                "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", Path.Combine(Checkout, "eng", "Prepare-CemPinBenchAcceptance.ps1"),
                "-BuildZip", ZipPath, "-CommitSha", shaOverride ?? CommitSha,
                "-OutputDirectory", outputOverride ?? Output
            })
                psi.ArgumentList.Add(arg);
            if (confirm) psi.ArgumentList.Add("-IConfirmOfflineNoSecrets");
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("PowerShell did not start.");
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("CEM bench preparation timed out.");
            }
            return (process.ExitCode,
                process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
        }

        private static string Source()
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
                 dir is not null; dir = dir.Parent)
            {
                var path = Path.Combine(dir.FullName, "eng", "Prepare-CemPinBenchAcceptance.ps1");
                if (File.Exists(path)) return path;
            }
            throw new FileNotFoundException("Prepare-CemPinBenchAcceptance.ps1");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
