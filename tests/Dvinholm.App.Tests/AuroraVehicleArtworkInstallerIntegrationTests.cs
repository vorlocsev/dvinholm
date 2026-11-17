using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Exercises the actual Windows PowerShell 5.1 installer against an isolated
/// disposable copy of the vehicle assets. No real source checkout is edited.
/// </summary>
public sealed class AuroraVehicleArtworkInstallerIntegrationTests
{
    [Fact]
    public void DryRunPrintsCandidateButNeverMutatesAssets()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new IsolatedArtworkCheckout();
        var before = fixture.Snapshot();
        var result = fixture.Run("s80-p2", fixture.Candidate);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("incomingSha256", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("applied", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, fixture.Snapshot());
        fixture.AssertNoStagingFiles();
    }

    [Fact]
    public void ApplyUpdatesOnlyChosenJpgAndCorrespondingManifestEntry()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new IsolatedArtworkCheckout();
        var before = fixture.ModelHashes();
        var candidateHash = Hash(fixture.Candidate);
        Assert.NotEqual(before["s80-p2"], candidateHash);

        var result = fixture.Run("s80-p2", fixture.Candidate, apply: true);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(candidateHash, Hash(fixture.VehicleFile("s80-p2")));
        using var manifest = JsonDocument.Parse(File.ReadAllText(fixture.Manifest));
        var records = manifest.RootElement.EnumerateArray().ToArray();
        Assert.Equal(9, records.Length);
        foreach (var record in records)
        {
            var id = record.GetProperty("modelId").GetString()!;
            var digest = record.GetProperty("sha256").GetString()!;
            Assert.Equal(id == "s80-p2" ? candidateHash : before[id], digest);
            Assert.Equal(Hash(fixture.VehicleFile(id)), digest);
        }
        fixture.AssertNoStagingFiles();
    }

    [Fact]
    public void TruncatedJpgFailsWithoutTouchingAssets()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new IsolatedArtworkCheckout();
        var before = fixture.Snapshot();
        File.WriteAllBytes(fixture.Candidate, [0xff, 0xd8, 0xff, 0xd9]);

        var result = fixture.Run("s80-p2", fixture.Candidate, apply: true);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(before, fixture.Snapshot());
        fixture.AssertNoStagingFiles();
    }

    [Fact]
    public void InPlaceSourceFailsWithoutTouchingAssets()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new IsolatedArtworkCheckout();
        var before = fixture.Snapshot();

        var result = fixture.Run("s80-p2", fixture.VehicleFile("s80-p2"), apply: true);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(before, fixture.Snapshot());
        fixture.AssertNoStagingFiles();
    }

    [Fact]
    public void DuplicateCatalogIdFailsBeforeAnyWrites()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new IsolatedArtworkCheckout();
        var text = File.ReadAllText(fixture.Manifest);
        // Retain nine entries, but duplicate the chosen model's canonical ID.
        File.WriteAllText(fixture.Manifest,
            text.Replace("\"modelId\": \"s60-p2\"", "\"modelId\": \"s80-p2\"",
                StringComparison.Ordinal));
        var before = fixture.Snapshot();

        var result = fixture.Run("s80-p2", fixture.Candidate, apply: true);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(before, fixture.Snapshot());
        fixture.AssertNoStagingFiles();
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private sealed class IsolatedArtworkCheckout : IDisposable
    {
        private readonly string _root;
        private readonly string _vehicles;
        private readonly string _script;

        internal IsolatedArtworkCheckout()
        {
            _root = Path.Combine(Path.GetTempPath(), "DvinholmVehicleArtworkFixture",
                Guid.NewGuid().ToString("N"));
            _vehicles = Path.Combine(_root, "assets", "vehicles");
            Directory.CreateDirectory(_vehicles);
            var scriptFolder = Path.Combine(_root, "eng");
            Directory.CreateDirectory(scriptFolder);
            _script = Path.Combine(scriptFolder, "Install-VolvoVehicleArtwork.ps1");
            File.Copy(Source("eng/Install-VolvoVehicleArtwork.ps1"), _script);
            var appFolder = Path.Combine(_root, "src", "Dvinholm.App");
            Directory.CreateDirectory(appFolder);
            File.WriteAllText(Path.Combine(appFolder, "Dvinholm.App.csproj"), "<Project />");
            foreach (var model in AuroraVehicleArtworkCatalog.Models)
                File.Copy(Source("assets/vehicles/" + model.Id + ".jpg"),
                    VehicleFile(model.Id));
            File.Copy(Source("assets/vehicles/manifest.json"), Manifest);
            // A different valid 1280x510 JPG is a deliberate fixture-only
            // candidate; these tests do NOT certify visual model identity.
            File.Copy(Source("assets/vehicles/xc90-p2.jpg"), Candidate);
        }

        internal string Manifest => Path.Combine(_vehicles, "manifest.json");
        internal string Candidate => Path.Combine(_root, "candidate.jpg");
        internal string VehicleFile(string id) => Path.Combine(_vehicles, id + ".jpg");

        internal Dictionary<string, string> ModelHashes() =>
            AuroraVehicleArtworkCatalog.Models.ToDictionary(
                m => m.Id, m => Hash(VehicleFile(m.Id)), StringComparer.Ordinal);

        internal string Snapshot()
        {
            using var sha = SHA256.Create();
            var entries = AuroraVehicleArtworkCatalog.Models
                .Select(m => m.Id + ":" + Hash(VehicleFile(m.Id)))
                .Append("manifest:" + Hash(Manifest));
            return string.Join("|", entries);
        }

        internal void AssertNoStagingFiles()
        {
            Assert.Empty(Directory.EnumerateFiles(_vehicles, "*.tmp"));
            Assert.Empty(Directory.EnumerateFiles(_vehicles, "*.bak"));
        }

        internal (int ExitCode, string Output) Run(string model, string sourceJpg,
            bool apply = false)
        {
            var psi = new ProcessStartInfo("powershell.exe")
            {
                WorkingDirectory = _root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[]
            {
                "-NoLogo", "-NoProfile", "-NonInteractive",
                "-ExecutionPolicy", "Bypass", "-File", _script,
                "-ModelId", model, "-SourceJpg", sourceJpg
            })
                psi.ArgumentList.Add(argument);
            if (apply) psi.ArgumentList.Add("-Apply");
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("PowerShell did not start.");
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Isolated PowerShell installer timed out.");
            }
            return (process.ExitCode,
                process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
        }

        private static string Source(string relative)
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
            try { Directory.Delete(_root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
