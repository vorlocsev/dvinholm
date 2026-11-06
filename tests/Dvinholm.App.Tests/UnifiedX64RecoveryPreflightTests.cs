using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline acceptance gates for a future signed x64 rollback workflow.
/// No live installation files are copied, deleted, replaced or executed.
/// </summary>
public sealed class UnifiedX64RecoveryPreflightTests
{
    private static byte[] Exe(ushort machine)
    {
        var bytes = new byte[1024];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        BitConverter.GetBytes(128).CopyTo(bytes, 0x3C);
        bytes[128] = (byte)'P'; bytes[129] = (byte)'E';
        BitConverter.GetBytes(machine).CopyTo(bytes, 132);
        return bytes;
    }

    private static async Task WithFixture(
        Func<string, string, string, UnifiedX64StagedPackage, Task> test,
        ushort installedArchitecture = 0x8664)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "dvinholm-preflight-" + Guid.NewGuid().ToString("N"));
        var installed = Path.Combine(root, "installed");
        var stage = Path.Combine(root, "staging",
            "pending-" + Guid.NewGuid().ToString("N"));
        var backup = Path.Combine(root, "backups");
        Directory.CreateDirectory(installed);
        Directory.CreateDirectory(stage);
        File.WriteAllBytes(Path.Combine(installed, "Dvinholm.exe"),
            Exe(installedArchitecture));
        File.WriteAllText(Path.Combine(installed, "appsettings.json"), "{}");
        var zipFile = Path.Combine(stage, UnifiedX64ReleaseFeed.PackageAsset);
        var zipBytes = new byte[] {1, 2, 3, 4, 5, 6, 7, 8};
        File.WriteAllBytes(zipFile, zipBytes);
        var checksum = Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant();
        var staged = new UnifiedX64StagedPackage(
            stage, zipFile, checksum, zipBytes.Length, 4, 515);
        try { await test(installed, stage, backup, staged); }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task CompleteReadOnlyInventoryCanPlanBackupButNeverInstall() =>
        await WithFixture((install, stage, backup, staged) =>
        {
            var result = UnifiedX64RecoveryPreflight.Inspect(
                install, staged, backup);
            Assert.True(result.CanPlanBackup);
            Assert.False(result.CanInstall);
            Assert.Equal(2, result.FilesToBackUp);
            Assert.Equal(1026, result.BytesToBackUp);
            Assert.False(Directory.Exists(result.BackupDirectory));
            Assert.True(File.Exists(Path.Combine(install, "Dvinholm.exe")));
            return Task.CompletedTask;
        });

    [Fact]
    public async Task PreviouslyInstalledX86ExecutableBlocksMigration() =>
        await WithFixture((install, stage, backup, staged) =>
        {
            var result = UnifiedX64RecoveryPreflight.Inspect(install, staged, backup);
            Assert.Contains("INSTALLED_EXE_NOT_AMD64", result.Blockers);
            Assert.False(result.CanPlanBackup);
            return Task.CompletedTask;
        }, installedArchitecture: 0x14c);

    [Fact]
    public async Task BackupPathCannotContainOrBeInsideInstallation() =>
        await WithFixture((install, stage, backup, staged) =>
        {
            foreach (var unsafeBackup in new[] {
                install, Path.Combine(install, "backups"),
                Path.GetDirectoryName(install)! })
            {
                var result = UnifiedX64RecoveryPreflight.Inspect(
                    install, staged, unsafeBackup);
                Assert.Contains("BACKUP_PATH_UNSAFE", result.Blockers);
            }
            return Task.CompletedTask;
        });

    [Fact]
    public async Task BackupMustNotOverlapStagedDownload() =>
        await WithFixture((install, stage, backup, staged) =>
        {
            var result = UnifiedX64RecoveryPreflight.Inspect(
                install, staged, Path.Combine(stage, "backups"));
            Assert.Contains("BACKUP_PATH_UNSAFE", result.Blockers);
            return Task.CompletedTask;
        });

    [Fact]
    public async Task StagingMustBeAnIsolatedPendingGuidFolder() =>
        await WithFixture((install, stage, backup, staged) =>
        {
            var malformed = staged with {
                Directory = Path.GetDirectoryName(stage)!
            };
            var result = UnifiedX64RecoveryPreflight.Inspect(
                install, malformed, backup);
            Assert.Contains("STAGING_PATH_UNSAFE", result.Blockers);
            return Task.CompletedTask;
        });

    [Fact]
    public async Task ModifiedStagedZipCannotPassAnInventoryAudit() =>
        await WithFixture((install, stage, backup, staged) =>
        {
            File.WriteAllBytes(staged.ZipPath, new byte[] { 9, 2, 3, 4, 5, 6, 7, 8 });
            var result = UnifiedX64RecoveryPreflight.Inspect(install, staged, backup);
            Assert.Contains("STAGING_EVIDENCE_MISMATCH", result.Blockers);
            return Task.CompletedTask;
        });

    [Fact]
    public void UnsignedPublicReleaseCannotBecomeInstallableByAccident()
    {
        var digest = new string('a', 64);
        Assert.False(UnifiedX64PublisherTrust.HasTrustedPublisherKey);
        Assert.False(UnifiedX64PublisherTrust.Verify("v0.3.0", digest, new byte[64]));
        Assert.False(UnifiedX64PublisherTrust.Verify("ci-5001", digest, null));
    }

    [Fact]
    public void DetachedSignatureMustMatchPinnedKeyTagAndExactDigest()
    {
        using var privateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var anotherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var digest = new string('a', 64);
        const string tag = "v0.3.0";
        var signature = privateKey.SignData(
            UnifiedX64PublisherTrust.GetCanonicalPayload(tag, digest),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var key = privateKey.ExportSubjectPublicKeyInfoPem();

        Assert.True(UnifiedX64PublisherTrust.VerifyUsingTrustedPublicKey(
            key, tag, digest, signature));
        Assert.False(UnifiedX64PublisherTrust.VerifyUsingTrustedPublicKey(
            anotherKey.ExportSubjectPublicKeyInfoPem(), tag, digest, signature));
        Assert.False(UnifiedX64PublisherTrust.VerifyUsingTrustedPublicKey(
            key, "v0.3.1", digest, signature));
        Assert.False(UnifiedX64PublisherTrust.VerifyUsingTrustedPublicKey(
            key, tag, new string('b', 64), signature));
        Assert.False(UnifiedX64PublisherTrust.VerifyUsingTrustedPublicKey(
            key, "v0.3.0\nInjected: true", digest, signature));
    }
}
