using System.IO.Compression;
using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

public sealed class UnifiedX64InstallReadinessGateTests
{
    private sealed record Fixture(
        string Root,
        string Installed,
        string Backups,
        UnifiedX64UpdateCandidate Candidate,
        UnifiedX64StagedPackage Stage,
        UnifiedX64BackupSnapshot.Receipt Backup);

    private static byte[] Pe()
    {
        var exe = new byte[1024];
        exe[0] = (byte)'M'; exe[1] = (byte)'Z';
        BitConverter.GetBytes(128).CopyTo(exe, 60);
        exe[128] = (byte)'P'; exe[129] = (byte)'E';
        BitConverter.GetBytes((ushort)0x8664).CopyTo(exe, 132);
        return exe;
    }

    private static void CreateZip(string file)
    {
        using var archive = ZipFile.Open(file, ZipArchiveMode.Create);
        foreach (var (name, bytes) in new[] {
            (Name: "Dvinholm.exe", Content: Pe()),
            (Name: "coreclr.dll", Content: new byte[] { 1 }),
            (Name: "hostfxr.dll", Content: new byte[] { 2 }),
            (Name: "Microsoft.UI.Xaml.dll", Content: new byte[] { 3 })
        })
        {
            using var target = archive.CreateEntry(name).Open();
            target.Write(bytes);
        }
    }

    private static void InFixture(Action<Fixture> test)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "dvinholm-readiness-" + Guid.NewGuid().ToString("N"));
        try
        {
            var installed = Path.Combine(root, "installed");
            var stageDirectory = Path.Combine(root, "staging",
                "pending-" + Guid.NewGuid().ToString("N"));
            var backups = Path.Combine(root, "backups");
            Directory.CreateDirectory(installed);
            Directory.CreateDirectory(stageDirectory);
            File.WriteAllBytes(Path.Combine(installed, "Dvinholm.exe"), Pe());
            File.WriteAllText(Path.Combine(installed, "preferences.json"), "{}");
            var zip = Path.Combine(stageDirectory, UnifiedX64ReleaseFeed.PackageAsset);
            CreateZip(zip);
            var bytes = new FileInfo(zip).Length;
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip)))
                .ToLowerInvariant();
            var verified = UnifiedX64PackageVerifier.Verify(zip,
                hash + "  Dvinholm-Windows-x64.zip\n", bytes);
            var stage = new UnifiedX64StagedPackage(stageDirectory, zip, hash,
                bytes, verified.EntryCount, verified.ExpandedBytes);
            const string url = "https://github.com/vorlocsev/dvinholm/releases/download/v0.3.0/";
            var candidate = new UnifiedX64UpdateCandidate("v0.3.0",
                UnifiedX64ReleaseFeed.PackageAsset, bytes,
                url + UnifiedX64ReleaseFeed.PackageAsset,
                url + UnifiedX64ReleaseFeed.ChecksumsAsset);
            var plan = UnifiedX64RecoveryPreflight.Inspect(installed, stage, backups);
            Assert.True(plan.CanPlanBackup);
            var receipt = UnifiedX64BackupSnapshot.Create(plan, stage);
            test(new Fixture(root, installed, backups, candidate, stage, receipt));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static UnifiedX64InstallReadiness Inspect(
        Fixture f, UnifiedX64UpdateCandidate? candidate = null,
        UnifiedX64StagedPackage? stage = null,
        UnifiedX64BackupSnapshot.Receipt? backup = null) =>
        UnifiedX64InstallReadinessGate.Inspect(candidate ?? f.Candidate,
            stage ?? f.Stage, backup ?? f.Backup, detachedSignature: null,
            f.Installed, f.Backups);

    [Fact]
    public void ValidLocalEvidenceStillCannotInstallWithoutPublisherAndHelper() =>
        InFixture(f =>
        {
            var result = Inspect(f);
            Assert.False(result.CanInstall);
            Assert.Equal("v0.3.0", result.ReleaseTag);
            Assert.Contains("PUBLISHER_KEY_NOT_PROVISIONED", result.Blockers);
            Assert.Contains("SIGNED_RELEASE_NOT_VERIFIED", result.Blockers);
            Assert.Contains("PRODUCTION_INSTALL_HANDOFF_UNAVAILABLE", result.Blockers);
            Assert.DoesNotContain("STAGED_ARCHIVE_CHANGED", result.Blockers);
            Assert.DoesNotContain("BACKUP_VERIFICATION_FAILED", result.Blockers);
        });

    [Fact]
    public void MissingReleaseMetadataIsRejected() =>
        InFixture(f =>
        {
            var result = UnifiedX64InstallReadinessGate.Inspect(
                null, f.Stage, f.Backup, null, f.Installed, f.Backups);
            Assert.Contains("RELEASE_METADATA_NOT_TRUSTED", result.Blockers);
            Assert.False(result.CanInstall);
        });

    [Fact]
    public void X86CandidateCannotBeConsideredAnX64Release() =>
        InFixture(f =>
        {
            var x86 = f.Candidate with { PackageName = "Dvinholm-Windows-x86.zip" };
            Assert.Contains("RELEASE_METADATA_NOT_TRUSTED",
                Inspect(f, candidate: x86).Blockers);
        });

    [Fact]
    public void NoStagedPackageMeansNoInstallReadiness() =>
        InFixture(f =>
        {
            var result = UnifiedX64InstallReadinessGate.Inspect(
                f.Candidate, null, f.Backup, null, f.Installed, f.Backups);
            Assert.Contains("X64_PACKAGE_NOT_STAGED", result.Blockers);
        });

    [Fact]
    public void TamperedZipIsDetectedOnNewInspection() =>
        InFixture(f =>
        {
            using (var file = new FileStream(f.Stage.ZipPath, FileMode.Append))
                file.WriteByte(42);
            var result = Inspect(f);
            Assert.Contains("STAGED_ARCHIVE_CHANGED", result.Blockers);
        });

    [Fact]
    public void MissingOrDamagedBackupBlocksUpdate() =>
        InFixture(f =>
        {
            var missing = UnifiedX64InstallReadinessGate.Inspect(
                f.Candidate, f.Stage, null, null, f.Installed, f.Backups);
            Assert.Contains("VERIFIED_BACKUP_MISSING", missing.Blockers);
            File.WriteAllText(Path.Combine(f.Backup.Directory, "payload", "preferences.json"), "BAD");
            Assert.Contains("BACKUP_VERIFICATION_FAILED", Inspect(f).Blockers);
        });

    [Fact]
    public void BackupFromAnotherRootIsRejected() =>
        InFixture(f =>
        {
            var alien = f.Backup with { Directory = Path.Combine(
                f.Root, "outside", Path.GetFileName(f.Backup.Directory)) };
            Assert.Contains("BACKUP_PATH_UNTRUSTED",
                Inspect(f, backup: alien).Blockers);
        });

    [Fact]
    public void ModifiedInstalledInventoryInvalidatesSnapshot() =>
        InFixture(f =>
        {
            // "{}" becomes "[]" with the SAME byte length: an inventory
            // that checks only file count and size would incorrectly pass.
            File.WriteAllText(Path.Combine(f.Installed, "preferences.json"), "[]");
            var result = Inspect(f);
            Assert.Contains("BACKUP_NO_LONGER_MATCHES_INSTALL", result.Blockers);
            Assert.False(result.CanInstall);
        });
}
