using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight Windows file-handle locks against a synthetic transaction only.
/// Never touches the installed Dvinholm application or invokes a production updater.
/// </summary>
public sealed class UnifiedX64LockedFixtureRecoveryTests
{
    private static byte[] Executable(byte version)
    {
        var bytes = new byte[1024];
        bytes[0] = 77; bytes[1] = 90;
        BitConverter.GetBytes(128).CopyTo(bytes, 60);
        bytes[128] = 80; bytes[129] = 69;
        BitConverter.GetBytes((ushort)0x8664).CopyTo(bytes, 132);
        bytes[250] = version;
        return bytes;
    }

    private static void InFixture(Action<string> action)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(),
            "dvinholm-recovery-fixture-" + Guid.NewGuid().ToString("N"));
        try
        {
            UnifiedX64RecoverySandbox.MarkFixtureForTests(root);
            var installed = Path.Combine(root, "installed");
            var candidate = Path.Combine(root, "candidate");
            var stagedDir = Path.Combine(root, "staging",
                "pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(installed);
            Directory.CreateDirectory(candidate);
            Directory.CreateDirectory(stagedDir);
            File.WriteAllBytes(Path.Combine(installed, "Dvinholm.exe"), Executable(1));
            File.WriteAllBytes(Path.Combine(candidate, "Dvinholm.exe"), Executable(2));
            File.WriteAllText(Path.Combine(installed, "settings.json"), "old");
            File.WriteAllText(Path.Combine(candidate, "settings.json"), "new");
            var zip = Path.Combine(stagedDir, UnifiedX64ReleaseFeed.PackageAsset);
            byte[] bytes = [1, 2, 3, 4];
            File.WriteAllBytes(zip, bytes);
            var staged = new UnifiedX64StagedPackage(stagedDir, zip,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                bytes.Length, 2, 1027);
            var plan = UnifiedX64RecoveryPreflight.Inspect(
                installed, staged, Path.Combine(root, "snapshots"));
            Assert.True(plan.CanPlanBackup);
            var snapshot = UnifiedX64BackupSnapshot.Create(plan, staged);
            UnifiedX64RecoverySandbox.Prepare(root, snapshot);
            action(root);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static FileStream Lock(string file) =>
        new(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    private static void AssertOriginal(string root)
    {
        Assert.Equal(Executable(1), File.ReadAllBytes(
            Path.Combine(root, "installed", "Dvinholm.exe")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(root, "installed", "settings.json")));
        Assert.Empty(Directory.EnumerateFiles(root, ".fixture-swap-*"));
    }

    [Fact]
    public void LockedExecutableAtPreflightDoesNotEnterApplying() =>
        InFixture(root =>
        {
            var exe = Path.Combine(root, "installed", "Dvinholm.exe");
            using (Lock(exe))
                Assert.ThrowsAny<IOException>(() =>
                    UnifiedX64RecoverySandbox.ApplyFixture(root));
            Assert.Equal(UnifiedX64RecoverySandbox.StateReady,
                UnifiedX64RecoverySandbox.ReadState(root));
            AssertOriginal(root);
        });

    [Fact]
    public void LockedCandidateExecutableCannotAdvanceReadyJournal() =>
        InFixture(root =>
        {
            using (Lock(Path.Combine(root, "candidate", "Dvinholm.exe")))
                Assert.ThrowsAny<IOException>(() =>
                    UnifiedX64RecoverySandbox.ApplyFixture(root));
            Assert.Equal(UnifiedX64RecoverySandbox.StateReady,
                UnifiedX64RecoverySandbox.ReadState(root));
            AssertOriginal(root);
        });

    [Fact]
    public void LockedBackupFileBlocksBeforeApplyingCheckpoint() =>
        InFixture(root =>
        {
            var backup = Directory.GetDirectories(Path.Combine(root, "snapshots"),
                "backup-*").Single();
            using (Lock(Path.Combine(backup, "payload", "Dvinholm.exe")))
                Assert.ThrowsAny<IOException>(() =>
                    UnifiedX64RecoverySandbox.ApplyFixture(root));
            Assert.Equal(UnifiedX64RecoverySandbox.StateReady,
                UnifiedX64RecoverySandbox.ReadState(root));
            AssertOriginal(root);
        });

    [Fact]
    public void ExecutableLockedAfterApplyingCheckpointRestoresAfterUnlock() =>
        InFixture(root =>
        {
            FileStream? handle = null;
            try
            {
                Assert.ThrowsAny<IOException>(() =>
                    UnifiedX64RecoverySandbox.ApplyFixture(root,
                        afterJournalForTests: () => handle = Lock(
                            Path.Combine(root, "installed", "Dvinholm.exe"))));
                Assert.Equal(UnifiedX64RecoverySandbox.StateApplying,
                    UnifiedX64RecoverySandbox.ReadState(root));
                Assert.Equal("old", File.ReadAllText(
                    Path.Combine(root, "installed", "settings.json")));
            }
            finally { handle?.Dispose(); }
            UnifiedX64RecoverySandbox.RecoverFixture(root);
            Assert.Equal(UnifiedX64RecoverySandbox.StateRolledBack,
                UnifiedX64RecoverySandbox.ReadState(root));
            AssertOriginal(root);
        });

    [Fact]
    public void LaterLockedSettingsFileLeavesApplyingUntilFullRecovery() =>
        InFixture(root =>
        {
            FileStream? handle = null;
            try
            {
                Assert.ThrowsAny<IOException>(() =>
                    UnifiedX64RecoverySandbox.ApplyFixture(root,
                        afterJournalForTests: () => handle = Lock(
                            Path.Combine(root, "installed", "settings.json"))));
                Assert.Equal(UnifiedX64RecoverySandbox.StateApplying,
                    UnifiedX64RecoverySandbox.ReadState(root));
                Assert.Equal(Executable(2), File.ReadAllBytes(
                    Path.Combine(root, "installed", "Dvinholm.exe")));
            }
            finally { handle?.Dispose(); }
            UnifiedX64RecoverySandbox.RecoverFixture(root);
            AssertOriginal(root);
        });

    [Fact]
    public void LockedExeAfterRecoveringCheckpointAllowsIdempotentRetry() =>
        InFixture(root =>
        {
            Assert.Throws<IOException>(() =>
                UnifiedX64RecoverySandbox.ApplyFixture(root, interruptAfterFiles: 1));
            FileStream? handle = null;
            try
            {
                Assert.ThrowsAny<IOException>(() =>
                    UnifiedX64RecoverySandbox.RecoverFixture(root,
                        afterJournalForTests: () => handle = Lock(
                            Path.Combine(root, "installed", "Dvinholm.exe"))));
                Assert.Equal(UnifiedX64RecoverySandbox.StateRecovering,
                    UnifiedX64RecoverySandbox.ReadState(root));
            }
            finally { handle?.Dispose(); }
            UnifiedX64RecoverySandbox.RecoverFixture(root);
            UnifiedX64RecoverySandbox.RecoverFixture(root);
            AssertOriginal(root);
        });

    [Fact]
    public void LockedLaterSettingsDuringRecoveryNeverCertifiesRollback() =>
        InFixture(root =>
        {
            Assert.Throws<IOException>(() =>
                UnifiedX64RecoverySandbox.ApplyFixture(root, interruptAfterFiles: 1));
            FileStream? handle = null;
            try
            {
                Assert.ThrowsAny<IOException>(() =>
                    UnifiedX64RecoverySandbox.RecoverFixture(root,
                        afterJournalForTests: () => handle = Lock(
                            Path.Combine(root, "installed", "settings.json"))));
                Assert.Equal(UnifiedX64RecoverySandbox.StateRecovering,
                    UnifiedX64RecoverySandbox.ReadState(root));
            }
            finally { handle?.Dispose(); }
            UnifiedX64RecoverySandbox.RecoverFixture(root);
            AssertOriginal(root);
        });

    [Fact]
    public void LockedJournalPreventsApplyWithoutModifyingOriginals() =>
        InFixture(root =>
        {
            using (Lock(Path.Combine(root, "fixture-transaction.json")))
                Assert.ThrowsAny<IOException>(() =>
                    UnifiedX64RecoverySandbox.ApplyFixture(root));
            Assert.Equal(UnifiedX64RecoverySandbox.StateReady,
                UnifiedX64RecoverySandbox.ReadState(root));
            AssertOriginal(root);
        });
}
