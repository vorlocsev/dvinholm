using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Dvinholm.App.Tests;

/// <summary>
/// Private Windows PowerShell checks against synthetic ZIP central directories.
/// No driver is loaded and no application or vehicle is accessed.
/// </summary>
public sealed class CemPinBenchArchiveLayoutTests
{
    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("..\\windows.txt")]
    [InlineData("C:/drive.txt")]
    [InlineData("Assets//empty-segment.dll")]
    [InlineData("Assets/./dot.dll")]
    [InlineData("Assets/../parent.dll")]
    public void PreparatoryScriptRejectsUnsafeArchivePaths(string invalidName)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        fixture.AddEntry(invalidName);
        var run = fixture.Prepare();
        Assert.NotEqual(0, run.Code);
        Assert.False(File.Exists(fixture.Report));
        Assert.False(Directory.Exists(fixture.Output));
        Assert.Contains("unsafe, ambiguous or duplicate", run.Text);
    }

    [Fact]
    public void PreparationRejectsCaseInsensitiveDuplicateArchiveNames()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        fixture.AddEntry("Assets/Data.dll");
        fixture.AddEntry("assets/data.DLL");
        Assert.NotEqual(0, fixture.Prepare().Code);
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public void PreparationRejectsCanonicalExePlusCaseVariant()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        fixture.AddEntry("DVINHOLM.EXE");
        Assert.NotEqual(0, fixture.Prepare().Code);
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public void SafeNestedEntriesSurvivePreparationAndVerification()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        fixture.AddEntry("Assets/Runtime/Native.dll");
        var creation = fixture.Prepare();
        Assert.True(creation.Code == 0, $"Preparation: {creation.Text}");
        var validation = fixture.Verify();
        Assert.True(validation.Code == 0, $"Verification: {validation.Text}");
        Assert.Contains("NOT_TESTED", validation.Text);
        Assert.False(validation.Text.Contains("PASSED", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VerifierRejectsPathTraversalEvenWhenReportDigestIsForged()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        var creation = fixture.Prepare();
        Assert.True(creation.Code == 0, $"Preparation: {creation.Text}");
        fixture.AddEntry("../forged.txt");
        var report = JsonNode.Parse(File.ReadAllText(fixture.Report))!.AsObject();
        report["buildZipSha256"] = fixture.Hash();
        report["buildZipBytes"] = new FileInfo(fixture.Zip).Length;
        File.WriteAllText(fixture.Report, report.ToJsonString());
        var before = File.ReadAllText(fixture.Report);
        var check = fixture.Verify();
        Assert.NotEqual(0, check.Code);
        Assert.Contains("unsafe, ambiguous or duplicate", check.Text);
        Assert.Equal(before, File.ReadAllText(fixture.Report));
    }

    [Fact]
    public void BothScriptsRefuseTraversalWithoutExtractingArchive()
    {
        var prep = File.ReadAllText(Fixture.Source("eng/Prepare-CemPinBenchAcceptance.ps1"));
        var verify = File.ReadAllText(Fixture.Source("eng/Verify-CemPinBenchAcceptance.ps1"));
        foreach (var script in new[] { prep, verify })
        {
            Assert.Contains("Assert-CemBenchArchiveLayout", script);
            Assert.Contains("HashSet[string]", script);
            Assert.Contains("$name.Contains('//')", script);
            Assert.Contains("$name -match", script);
            Assert.Contains("$entry.ExternalAttributes", script);
            Assert.Contains("$exeCount -ne 1", script);
            Assert.DoesNotContain("ExtractToDirectory", script);
            Assert.DoesNotContain("PassThruConnect", script);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private const string Commit = "0123456789abcdef0123456789abcdef01234567";
        private readonly string _root = Path.Combine(Path.GetTempPath(),
            "DvinholmCemZipLayoutFixture", Guid.NewGuid().ToString("N"));
        private readonly string _prepare;
        private readonly string _verify;
        internal string Zip => Path.Combine(_root, "Dvinholm-Windows-x64.zip");
        internal string Output => Path.Combine(_root, "private-evidence");
        internal string Report => Path.Combine(Output, "cem-pin-bench-acceptance.json");

        internal Fixture()
        {
            var eng = Path.Combine(_root, "checkout", "eng");
            Directory.CreateDirectory(eng);
            _prepare = Path.Combine(eng, "Prepare-CemPinBenchAcceptance.ps1");
            _verify = Path.Combine(eng, "Verify-CemPinBenchAcceptance.ps1");
            File.Copy(Source("eng/Prepare-CemPinBenchAcceptance.ps1"), _prepare);
            File.Copy(Source("eng/Verify-CemPinBenchAcceptance.ps1"), _verify);
            AddEntry("Dvinholm.exe");
        }

        internal void AddEntry(string name)
        {
            using var zip = ZipFile.Open(Zip, File.Exists(Zip)
                ? ZipArchiveMode.Update : ZipArchiveMode.Create);
            var entry = zip.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write("synthetic fixture bytes, not an executable");
        }

        internal string Hash() =>
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Zip)))
                .ToLowerInvariant();

        internal (int Code, string Text) Prepare() => Run(_prepare,
            new[] { "-BuildZip", Zip, "-CommitSha", Commit,
                    "-OutputDirectory", Output, "-IConfirmOfflineNoSecrets" });

        internal (int Code, string Text) Verify() => Run(_verify,
            new[] { "-BuildZip", Zip, "-ReportJson", Report,
                    "-IConfirmOfflineNoSecrets" });

        private (int Code, string Text) Run(string script, string[] args)
        {
            var psi = new ProcessStartInfo("powershell.exe")
            {
                WorkingDirectory = _root, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive",
                "-ExecutionPolicy", "Bypass", "-File", script }.Concat(args))
                psi.ArgumentList.Add(arg);
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("PowerShell did not start");
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Private synthetic ZIP check timed out.");
            }
            return (process.ExitCode,
                process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
        }

        internal static string Source(string relative)
        {
            for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
                dir is not null; dir = dir.Parent)
            {
                var path = Path.Combine(dir.FullName,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) return path;
            }
            throw new FileNotFoundException(relative);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
