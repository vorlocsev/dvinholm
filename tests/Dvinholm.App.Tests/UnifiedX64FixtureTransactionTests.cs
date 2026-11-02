using System.IO.Compression;
using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline full-pipeline scenarios. Every write is confined to two
/// freshly marked GUID fixtures directly under the temporary directory.
/// </summary>
public sealed class UnifiedX64FixtureTransactionTests
{
    private sealed record Fixture(string ExtractionRoot, string RecoveryRoot,
        UnifiedX64StagedPackage Stage, UnifiedX64BackupSnapshot.Receipt Backup);

    private static byte[] Executable(byte marker)
    {
        var bytes = new byte[1024];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        BitConverter.GetBytes(128).CopyTo(bytes, 60);
        bytes[128] = (byte)'P'; bytes[129] = (byte)'E';
        BitConverter.GetBytes((ushort)0x8664).CopyTo(bytes, 132);
        bytes[250] = marker;
        return bytes;
    }

    private static void Add(ZipArchive zip, string name, byte[] bytes)
    {
        using var stream = zip.CreateEntry(name).Open();
        stream.Write(bytes);
    }

    private static void InFixture(Action<Fixture> action)
    {
        var extractionRoot = Path.Combine(Path.GetTempPath(),
            "dvinholm-extract-fixture-" + Guid.NewGuid().ToString("N"));
        var recoveryRoot = Path.Combine(Path.GetTempPath(),
            "dvinholm-recovery-fixture-" + Guid.NewGuid().ToString("N"));
        try
        {
            UnifiedX64ExtractionSandbox.MarkForTests(extractionRoot);
            UnifiedX64RecoverySandbox.MarkFixtureForTests(recoveryRoot);

            var installed = Path.Combine(recoveryRoot, "installed");
            var stageDir = Path.Combine(extractionRoot,
                "pending-" + Guid.NewGuid().ToString("N"));
            var recoveryStageDir = Path.Combine(recoveryRoot, "staging",
                "pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(installed);
            Directory.CreateDirectory(stageDir);
            Directory.CreateDirectory(recoveryStageDir);

            var original = new (string Name, byte[] Bytes)[] {
                ("Dvinholm.exe", Executable(1)),
                ("coreclr.dll", [1, 1]),
                ("hostfxr.dll", [2]),
                ("Microsoft.UI.Xaml.dll", [3, 3])
            };
            var updated = new (string Name, byte[] Bytes)[] {
                ("Dvinholm.exe", Executable(2)),
                ("coreclr.dll", [4, 4]),
                ("hostfxr.dll", [5]),
                ("Microsoft.UI.Xaml.dll", [6, 6])
            };
            foreach (var item in original)
                File.WriteAllBytes(Path.Combine(installed, item.Name), item.Bytes);
            var zipPath = Path.Combine(stageDir, UnifiedX64ReleaseFeed.PackageAsset);
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                foreach (var item in updated) Add(zip, item.Name, item.Bytes);

            var zipBytes = new FileInfo(zipPath).Length;
            var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zipPath)))
                .ToLowerInvariant();
            long expandedBytes = updated.Sum(x => (long)x.Bytes.Length);
            var staged = new UnifiedX64StagedPackage(stageDir, zipPath, sha,
                zipBytes, updated.Length, expandedBytes);

            var copied = Path.Combine(recoveryStageDir, UnifiedX64ReleaseFeed.PackageAsset);
            File.Copy(zipPath, copied);
            var recoveryStage = staged with {
                Directory = recoveryStageDir,
                ZipPath = copied
            };
            var plan = UnifiedX64RecoveryPreflight.Inspect(
                installed, recoveryStage, Path.Combine(recoveryRoot, "snapshots"));
            Assert.True(plan.CanPlanBackup);
            var backup = UnifiedX64BackupSnapshot.Create(plan, recoveryStage);
            action(new Fixture(extractionRoot, recoveryRoot, staged, backup));
        }
        finally
        {
            if (Directory.Exists(recoveryRoot))
                Directory.Delete(recoveryRoot, recursive: true);
            if (Directory.Exists(extractionRoot))
                Directory.Delete(extractionRoot, recursive: true);
        }
    }

    private static UnifiedX64ExtractionSandbox.Receipt Extract(Fixture f) =>
        UnifiedX64ExtractionSandbox.ExtractFixture(f.Stage, f.ExtractionRoot);

    private static void Prepare(Fixture f,
        UnifiedX64ExtractionSandbox.Receipt extracted) =>
        UnifiedX64FixtureTransaction.Prepare(f.ExtractionRoot, f.Stage, extracted,
            f.RecoveryRoot, f.Backup);

    private static void AssertOriginal(Fixture f)
    {
        Assert.Equal(Executable(1), File.ReadAllBytes(
            Path.Combine(f.RecoveryRoot, "installed", "Dvinholm.exe")));
        Assert.Equal(new byte[] { 1, 1 }, File.ReadAllBytes(
            Path.Combine(f.RecoveryRoot, "installed", "coreclr.dll")));
        Assert.True(UnifiedX64BackupSnapshot.Verify(f.Backup.Directory) == f.Backup);
    }

    [Fact]
    public void VerifiedZipToCandidateToJournalToSyntheticApplySucceeds() =>
        InFixture(f =>
        {
            var extracted = Extract(f);
            Prepare(f, extracted);
            Assert.Equal(UnifiedX64RecoverySandbox.StateReady,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            AssertOriginal(f);
            UnifiedX64RecoverySandbox.ApplyFixture(f.RecoveryRoot);
            Assert.Equal(UnifiedX64RecoverySandbox.StateApplied,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            Assert.Equal(Executable(2), File.ReadAllBytes(Path.Combine(
                f.RecoveryRoot, "installed", "Dvinholm.exe")));
        });

    [Fact]
    public void InterruptedApplyRestoresTheVerifiedOriginals() =>
        InFixture(f =>
        {
            Prepare(f, Extract(f));
            Assert.Throws<IOException>(() =>
                UnifiedX64RecoverySandbox.ApplyFixture(f.RecoveryRoot,
                    interruptAfterFirstFile: true));
            Assert.Equal(UnifiedX64RecoverySandbox.StateApplying,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot);
            Assert.Equal(UnifiedX64RecoverySandbox.StateRolledBack,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            AssertOriginal(f);
        });

    [Fact]
    public void InterruptedRollbackCanBeReplayedTwice() =>
        InFixture(f =>
        {
            Prepare(f, Extract(f));
            Assert.Throws<IOException>(() =>
                UnifiedX64RecoverySandbox.ApplyFixture(f.RecoveryRoot, true));
            Assert.Throws<IOException>(() =>
                UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot, true));
            Assert.Equal(UnifiedX64RecoverySandbox.StateRecovering,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot);
            UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot);
            AssertOriginal(f);
        });

    [Fact]
    public void ZipTamperingBeforeExtractionDoesNotCreateCandidateOrJournal() =>
        InFixture(f =>
        {
            File.AppendAllText(f.Stage.ZipPath, "corrupt");
            Assert.Throws<InvalidDataException>(() => Extract(f));
            AssertOriginal(f);
            Assert.False(Directory.Exists(Path.Combine(f.RecoveryRoot, "candidate")));
            Assert.False(File.Exists(Path.Combine(f.RecoveryRoot, "fixture-transaction.json")));
        });

    [Fact]
    public void MutatedExtractedBytesAreRefusedBeforeCandidateCreation() =>
        InFixture(f =>
        {
            var extracted = Extract(f);
            File.WriteAllBytes(Path.Combine(extracted.Directory, "coreclr.dll"), [9, 9]);
            Assert.Throws<InvalidDataException>(() => Prepare(f, extracted));
            AssertOriginal(f);
            Assert.False(Directory.Exists(Path.Combine(f.RecoveryRoot, "candidate")));
        });

    [Fact]
    public void DamagedBackupRefusesHandoffAndLeavesOriginalInstalled() =>
        InFixture(f =>
        {
            var extracted = Extract(f);
            File.WriteAllBytes(Path.Combine(f.Backup.Directory, "payload", "hostfxr.dll"), [9]);
            Assert.Throws<InvalidDataException>(() => Prepare(f, extracted));
            Assert.Equal(Executable(1), File.ReadAllBytes(
                Path.Combine(f.RecoveryRoot, "installed", "Dvinholm.exe")));
            Assert.False(File.Exists(Path.Combine(f.RecoveryRoot, "fixture-transaction.json")));
        });

    [Fact]
    public void CandidateAndJournalCannotBeOverwrittenByReplayingPreparation() =>
        InFixture(f =>
        {
            var expanded = Extract(f);
            Prepare(f, expanded);
            Assert.Throws<InvalidDataException>(() => Prepare(f, expanded));
            Assert.Equal(UnifiedX64RecoverySandbox.StateReady,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            AssertOriginal(f);
        });

    [Fact]
    public void CrashImmediatelyAfterApplyingJournalCanRestoreUntouchedInstall() =>
        InFixture(f =>
        {
            Prepare(f, Extract(f));
            Assert.Throws<IOException>(() => UnifiedX64RecoverySandbox.ApplyFixture(
                f.RecoveryRoot, interruptAfterFiles: 0));
            Assert.Equal(UnifiedX64RecoverySandbox.StateApplying,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            AssertOriginal(f);
            UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot);
            AssertOriginal(f);
            Assert.Equal(UnifiedX64RecoverySandbox.StateRolledBack,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
        });

    [Fact]
    public void CrashAfterEachOfFourAppliedFilesRestoresCompleteBackup()
    {
        for (var boundary = 1; boundary <= 4; boundary++)
        {
            var stop = boundary;
            InFixture(f =>
            {
                Prepare(f, Extract(f));
                Assert.Throws<IOException>(() => UnifiedX64RecoverySandbox.ApplyFixture(
                    f.RecoveryRoot, interruptAfterFiles: stop));
                UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot);
                AssertOriginal(f);
                Assert.Equal(UnifiedX64RecoverySandbox.StateRolledBack,
                    UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            });
        }
    }

    [Fact]
    public void CrashAfterFinalApplyFileStillRefusesFalseCommit() =>
        InFixture(f =>
        {
            Prepare(f, Extract(f));
            Assert.Throws<IOException>(() => UnifiedX64RecoverySandbox.ApplyFixture(
                f.RecoveryRoot, interruptAfterFiles: 4));
            Assert.Equal(Executable(2), File.ReadAllBytes(Path.Combine(
                f.RecoveryRoot, "installed", "Dvinholm.exe")));
            Assert.Equal(UnifiedX64RecoverySandbox.StateApplying,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot);
            AssertOriginal(f);
        });

    [Fact]
    public void CrashAfterRecoveryJournalBeforeFirstRestoreReplaysSafely() =>
        InFixture(f =>
        {
            Prepare(f, Extract(f));
            Assert.Throws<IOException>(() => UnifiedX64RecoverySandbox.ApplyFixture(
                f.RecoveryRoot, interruptAfterFiles: 2));
            Assert.Throws<IOException>(() => UnifiedX64RecoverySandbox.RecoverFixture(
                f.RecoveryRoot, interruptAfterFiles: 0));
            Assert.Equal(UnifiedX64RecoverySandbox.StateRecovering,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot);
            AssertOriginal(f);
        });

    [Fact]
    public void CrashAfterEachOfFourRecoveryFilesIsRepeatable()
    {
        for (var boundary = 1; boundary <= 4; boundary++)
        {
            var stop = boundary;
            InFixture(f =>
            {
                Prepare(f, Extract(f));
                Assert.Throws<IOException>(() => UnifiedX64RecoverySandbox.ApplyFixture(
                    f.RecoveryRoot, interruptAfterFiles: 1));
                Assert.Throws<IOException>(() => UnifiedX64RecoverySandbox.RecoverFixture(
                    f.RecoveryRoot, interruptAfterFiles: stop));
                Assert.Equal(UnifiedX64RecoverySandbox.StateRecovering,
                    UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
                UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot);
                UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot);
                AssertOriginal(f);
            });
        }
    }

    [Fact]
    public void InvalidFaultInjectionCountFailsBeforeJournalTransition() =>
        InFixture(f =>
        {
            Prepare(f, Extract(f));
            foreach (var point in new[] { -2, 5 })
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    UnifiedX64RecoverySandbox.ApplyFixture(
                        f.RecoveryRoot, interruptAfterFiles: point));
            Assert.Equal(UnifiedX64RecoverySandbox.StateReady,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            AssertOriginal(f);
        });

    [Fact]
    public void RecoveryOfCommittedFixtureIsNotMisreportedAsInterrupted() =>
        InFixture(f =>
        {
            Prepare(f, Extract(f));
            UnifiedX64RecoverySandbox.ApplyFixture(f.RecoveryRoot);
            Assert.Equal(UnifiedX64RecoverySandbox.StateApplied,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot));
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64RecoverySandbox.ApplyFixture(f.RecoveryRoot));
        });

    [Fact]
    public void ExtraFileDuringRollbackPreventsJournalCertificationUntilRemoved() =>
        InFixture(f =>
        {
            Prepare(f, Extract(f));
            Assert.Throws<IOException>(() => UnifiedX64RecoverySandbox.ApplyFixture(
                f.RecoveryRoot, interruptAfterFiles: 1));
            var foreign = Path.Combine(f.RecoveryRoot, "installed", "untracked.dll");
            File.WriteAllText(foreign, "untracked");
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot));
            Assert.Equal(UnifiedX64RecoverySandbox.StateRecovering,
                UnifiedX64RecoverySandbox.ReadState(f.RecoveryRoot));
            File.Delete(foreign);
            UnifiedX64RecoverySandbox.RecoverFixture(f.RecoveryRoot);
            AssertOriginal(f);
        });

    [Fact]
    public void MissingRecoveryFixtureMarkerBlocksAllHandoffWrites() =>
        InFixture(f =>
        {
            var expanded = Extract(f);
            File.Delete(Path.Combine(f.RecoveryRoot, ".dvinholm-offline-fixture"));
            Assert.Throws<InvalidOperationException>(() => Prepare(f, expanded));
            Assert.False(Directory.Exists(Path.Combine(f.RecoveryRoot, "candidate")));
            Assert.False(File.Exists(Path.Combine(f.RecoveryRoot, "fixture-transaction.json")));
            Assert.Equal(Executable(1), File.ReadAllBytes(
                Path.Combine(f.RecoveryRoot, "installed", "Dvinholm.exe")));
        });
}
