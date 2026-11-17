using System.Security.Cryptography;
using System.Text.Json;

namespace Dvinholm.App.Tests;

public sealed class UnifiedX64RecoverySandboxTests
{
    private static byte[] Executable(byte marker)
    {
        var data = new byte[1024];
        data[0] = 77; data[1] = 90;
        data[128] = 80; data[129] = 69;
        BitConverter.GetBytes(128).CopyTo(data, 60);
        BitConverter.GetBytes((ushort)0x8664).CopyTo(data, 132);
        data[250] = marker;
        return data;
    }

    private static void InFixture(
        Action<string, UnifiedX64BackupSnapshot.Receipt> test)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "dvinholm-recovery-fixture-" + Guid.NewGuid().ToString("N"));
        try
        {
            UnifiedX64RecoverySandbox.MarkFixtureForTests(root);
            var installed = Path.Combine(root, "installed");
            var candidate = Path.Combine(root, "candidate");
            var stage = Path.Combine(root, "staging", "pending-" + Guid.NewGuid().ToString("N"));
            var backupRoot = Path.Combine(root, "snapshots");
            Directory.CreateDirectory(installed);
            Directory.CreateDirectory(candidate);
            Directory.CreateDirectory(stage);
            File.WriteAllBytes(Path.Combine(installed, "Dvinholm.exe"), Executable(1));
            File.WriteAllText(Path.Combine(installed, "settings.json"), "original");
            File.WriteAllBytes(Path.Combine(candidate, "Dvinholm.exe"), Executable(2));
            File.WriteAllText(Path.Combine(candidate, "settings.json"), "updated");
            var zip = Path.Combine(stage, UnifiedX64ReleaseFeed.PackageAsset);
            var zipData = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(zip, zipData);
            var staged = new UnifiedX64StagedPackage(
                stage, zip,
                Convert.ToHexString(SHA256.HashData(zipData)).ToLowerInvariant(),
                zipData.Length, 4, 515);
            var plan = UnifiedX64RecoveryPreflight.Inspect(installed, staged, backupRoot);
            Assert.True(plan.CanPlanBackup);
            var receipt = UnifiedX64BackupSnapshot.Create(plan, staged);
            test(root, receipt);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertOriginal(string root)
    {
        Assert.Equal(Executable(1), File.ReadAllBytes(Path.Combine(root, "installed", "Dvinholm.exe")));
        Assert.Equal("original", File.ReadAllText(Path.Combine(root, "installed", "settings.json")));
    }

    [Fact]
    public void PreparedJournalDoesNotMutateInstalledFiles() =>
        InFixture((root, backup) =>
        {
            UnifiedX64RecoverySandbox.Prepare(root, backup);
            Assert.Equal(UnifiedX64RecoverySandbox.StateReady,
                UnifiedX64RecoverySandbox.ReadState(root));
            AssertOriginal(root);
        });

    [Fact]
    public void CompleteFixtureApplyIsJournalledButNeverAnInstallApi() =>
        InFixture((root, backup) =>
        {
            UnifiedX64RecoverySandbox.Prepare(root, backup);
            UnifiedX64RecoverySandbox.ApplyFixture(root);
            Assert.Equal(UnifiedX64RecoverySandbox.StateApplied,
                UnifiedX64RecoverySandbox.ReadState(root));
            Assert.Equal(Executable(2),
                File.ReadAllBytes(Path.Combine(root, "installed", "Dvinholm.exe")));
            Assert.Equal("updated", File.ReadAllText(Path.Combine(root, "installed", "settings.json")));
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64RecoverySandbox.RecoverFixture(root));
        });

    [Fact]
    public void InterruptedApplicationRestoresVerifiedOriginals() =>
        InFixture((root, backup) =>
        {
            UnifiedX64RecoverySandbox.Prepare(root, backup);
            Assert.Throws<IOException>(() =>
                UnifiedX64RecoverySandbox.ApplyFixture(root, interruptAfterFirstFile: true));
            Assert.Equal(UnifiedX64RecoverySandbox.StateApplying,
                UnifiedX64RecoverySandbox.ReadState(root));
            UnifiedX64RecoverySandbox.RecoverFixture(root);
            Assert.Equal(UnifiedX64RecoverySandbox.StateRolledBack,
                UnifiedX64RecoverySandbox.ReadState(root));
            AssertOriginal(root);
        });

    [Fact]
    public void InterruptedRecoveryCanBeReplayedIdempotently() =>
        InFixture((root, backup) =>
        {
            UnifiedX64RecoverySandbox.Prepare(root, backup);
            Assert.Throws<IOException>(() =>
                UnifiedX64RecoverySandbox.ApplyFixture(root, interruptAfterFirstFile: true));
            Assert.Throws<IOException>(() =>
                UnifiedX64RecoverySandbox.RecoverFixture(root, interruptAfterFirstFile: true));
            Assert.Equal(UnifiedX64RecoverySandbox.StateRecovering,
                UnifiedX64RecoverySandbox.ReadState(root));
            UnifiedX64RecoverySandbox.RecoverFixture(root);
            UnifiedX64RecoverySandbox.RecoverFixture(root);
            AssertOriginal(root);
        });

    [Fact]
    public void TamperedOriginalBackupRefusesRecovery() =>
        InFixture((root, backup) =>
        {
            UnifiedX64RecoverySandbox.Prepare(root, backup);
            Assert.Throws<IOException>(() =>
                UnifiedX64RecoverySandbox.ApplyFixture(root, interruptAfterFirstFile: true));
            File.WriteAllText(Path.Combine(backup.Directory, "payload", "settings.json"), "malicious");
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64RecoverySandbox.RecoverFixture(root));
            Assert.Equal(UnifiedX64RecoverySandbox.StateApplying,
                UnifiedX64RecoverySandbox.ReadState(root));
        });

    [Fact]
    public void TamperedJournalIsRejectedBeforeAnyRecovery() =>
        InFixture((root, backup) =>
        {
            UnifiedX64RecoverySandbox.Prepare(root, backup);
            Assert.Throws<IOException>(() =>
                UnifiedX64RecoverySandbox.ApplyFixture(root, interruptAfterFirstFile: true));
            var journal = Path.Combine(root, "fixture-transaction.json");
            File.WriteAllText(journal,
                "{\"Schema\":1,\"State\":\"Installed\",\"SnapshotName\":\"injected\",\"Files\":[]}");
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64RecoverySandbox.RecoverFixture(root));
        });

    [Fact]
    public void ModifiedCandidateBeforeApplyBlocksWithoutMutatingInstall() =>
        InFixture((root, backup) =>
        {
            UnifiedX64RecoverySandbox.Prepare(root, backup);
            File.WriteAllText(Path.Combine(root, "candidate", "settings.json"), "tampered");
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64RecoverySandbox.ApplyFixture(root));
            Assert.Equal(UnifiedX64RecoverySandbox.StateReady,
                UnifiedX64RecoverySandbox.ReadState(root));
            AssertOriginal(root);
        });

    [Fact]
    public void NonFixtureDirectoryAndMissingMarkerCannotBeUsed() =>
        InFixture((root, backup) =>
        {
            UnifiedX64RecoverySandbox.Prepare(root, backup);
            var arbitrary = Path.Combine(Path.GetTempPath(), "dvinholm-installed");
            Assert.Throws<InvalidOperationException>(() =>
                UnifiedX64RecoverySandbox.ReadState(arbitrary));
            var marker = Path.Combine(root, ".dvinholm-offline-fixture");
            File.Delete(marker);
            Assert.Throws<InvalidOperationException>(() =>
                UnifiedX64RecoverySandbox.RecoverFixture(root));
        });
}
