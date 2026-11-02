using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

/// <summary>
/// Deterministic offline capacity and lock tests. No disk is filled and no
/// actual Dvinholm installation, published ZIP or user data is modified.
/// </summary>
public sealed class UnifiedX64SnapshotIoFaultTests
{
    private static void InFixture(Action<UnifiedX64RecoveryPlan, UnifiedX64StagedPackage> check)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "dvinholm-snapshot-" + Guid.NewGuid().ToString("N"));
        try
        {
            var install = Path.Combine(root, "app");
            var stage = Path.Combine(root, "stage",
                "pending-" + Guid.NewGuid().ToString("N"));
            var backup = Path.Combine(root, "backups");
            Directory.CreateDirectory(install);
            Directory.CreateDirectory(stage);
            var exe = new byte[1024];
            exe[0] = (byte)'M'; exe[1] = (byte)'Z';
            BitConverter.GetBytes(128).CopyTo(exe, 60);
            exe[128] = (byte)'P'; exe[129] = (byte)'E';
            BitConverter.GetBytes((ushort)0x8664).CopyTo(exe, 132);
            File.WriteAllBytes(Path.Combine(install, "Dvinholm.exe"), exe);
            File.WriteAllText(Path.Combine(install, "settings.json"), "{}");
            var zip = Path.Combine(stage, UnifiedX64ReleaseFeed.PackageAsset);
            var zipBytes = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(zip, zipBytes);
            var staged = new UnifiedX64StagedPackage(stage, zip,
                Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant(),
                zipBytes.Length, 4, 515);
            var plan = UnifiedX64RecoveryPreflight.Inspect(install, staged, backup);
            Assert.True(plan.CanPlanBackup);
            check(plan, staged);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertUncommitted(UnifiedX64RecoveryPlan plan)
    {
        Assert.False(Directory.Exists(plan.BackupDirectory));
        var backupRoot = Path.GetDirectoryName(plan.BackupDirectory)!;
        if (Directory.Exists(backupRoot))
            Assert.Empty(Directory.EnumerateDirectories(backupRoot, ".snapshot-*"));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(plan.InstallDirectory, "settings.json")));
        Assert.Equal(1024, new FileInfo(Path.Combine(plan.InstallDirectory, "Dvinholm.exe")).Length);
    }

    [Fact]
    public void NoAvailableBytesFailsBeforeCopyAndPreservesSource() =>
        InFixture((plan, stage) =>
        {
            Assert.Throws<IOException>(() =>
                UnifiedX64BackupSnapshot.Create(plan, stage,
                    failAfterCopiedBytesForTests: 0));
            AssertUncommitted(plan);
        });

    [Fact]
    public void OneByteCapacityFailsWithoutPublishingBackup() =>
        InFixture((plan, stage) =>
        {
            Assert.Throws<IOException>(() =>
                UnifiedX64BackupSnapshot.Create(plan, stage,
                    failAfterCopiedBytesForTests: 1));
            AssertUncommitted(plan);
        });

    [Fact]
    public void HalfSourceCapacityFailsAndCleansPendingCopy() =>
        InFixture((plan, stage) =>
        {
            Assert.Throws<IOException>(() =>
                UnifiedX64BackupSnapshot.Create(plan, stage,
                    failAfterCopiedBytesForTests: 513));
            AssertUncommitted(plan);
        });

    [Fact]
    public void ExhaustionAtFileBoundaryCannotCommitSnapshot() =>
        InFixture((plan, stage) =>
        {
            Assert.Throws<IOException>(() =>
                UnifiedX64BackupSnapshot.Create(plan, stage,
                    failAfterCopiedBytesForTests: 1024));
            AssertUncommitted(plan);
        });

    [Fact]
    public void AlmostEntireSourceCopiedStillCannotCommit() =>
        InFixture((plan, stage) =>
        {
            Assert.Throws<IOException>(() =>
                UnifiedX64BackupSnapshot.Create(plan, stage,
                    failAfterCopiedBytesForTests: 1025));
            AssertUncommitted(plan);
        });

    [Fact]
    public void FullSourceCopiedButFailureBeforeJournalStillRefusesCommit() =>
        InFixture((plan, stage) =>
        {
            Assert.Equal(1026, plan.BytesToBackUp);
            Assert.Throws<IOException>(() =>
                UnifiedX64BackupSnapshot.Create(plan, stage,
                    failAfterCopiedBytesForTests: plan.BytesToBackUp));
            AssertUncommitted(plan);
        });

    [Fact]
    public void ExclusiveSourceFileLockPreventsIncompleteSnapshot() =>
        InFixture((plan, stage) =>
        {
            if (!OperatingSystem.IsWindows()) return;
            var settings = Path.Combine(plan.InstallDirectory, "settings.json");
            using (var locked = new FileStream(settings, FileMode.Open,
                FileAccess.ReadWrite, FileShare.None))
            {
                Assert.ThrowsAny<IOException>(() =>
                    UnifiedX64BackupSnapshot.Create(plan, stage));
                AssertUncommitted(plan);
            }
            // The failed attempt must not destroy the ability to retry safely.
            var refreshed = UnifiedX64RecoveryPreflight.Inspect(
                plan.InstallDirectory, stage, Path.GetDirectoryName(plan.BackupDirectory)!);
            Assert.True(refreshed.CanPlanBackup);
            var receipt = UnifiedX64BackupSnapshot.Create(refreshed, stage);
            Assert.Equal(receipt, UnifiedX64BackupSnapshot.Verify(receipt.Directory));
        });

    [Fact]
    public void InvalidOrUntrustedInjectionLimitsAreRejectedBeforeDiskWrites() =>
        InFixture((plan, stage) =>
        {
            foreach (var invalid in new[] { -1L, plan.BytesToBackUp + 1 })
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    UnifiedX64BackupSnapshot.Create(plan, stage,
                        failAfterCopiedBytesForTests: invalid));

            var foreign = plan with {
                InstallDirectory = Path.Combine(Path.GetTempPath(), "outside-test-install")
            };
            Assert.Throws<InvalidOperationException>(() =>
                UnifiedX64BackupSnapshot.Create(foreign, stage,
                    failAfterCopiedBytesForTests: 1));
            AssertUncommitted(plan);
        });
}
