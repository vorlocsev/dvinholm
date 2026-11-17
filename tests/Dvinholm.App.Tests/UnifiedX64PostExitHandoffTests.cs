using System.Diagnostics;
using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight Windows-only offline checks for a real-process exit signal and a
/// synthetic fixture. No production application is launched or replaced.
/// </summary>
public sealed class UnifiedX64PostExitHandoffTests
{
    private static void InFixture(Action<string> test)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "dvinholm-recovery-fixture-" + Guid.NewGuid().ToString("N"));
        try
        {
            UnifiedX64RecoverySandbox.MarkFixtureForTests(root);
            var installed = Path.Combine(root, "installed");
            var candidate = Path.Combine(root, "candidate");
            var stage = Path.Combine(root, "stage", "pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(installed);
            Directory.CreateDirectory(candidate);
            Directory.CreateDirectory(stage);
            var exe = new byte[1024];
            exe[0] = 77; exe[1] = 90;
            exe[128] = 80; exe[129] = 69;
            BitConverter.GetBytes(128).CopyTo(exe, 60);
            BitConverter.GetBytes((ushort)0x8664).CopyTo(exe, 132);
            File.WriteAllBytes(Path.Combine(installed, "Dvinholm.exe"), exe);
            File.WriteAllText(Path.Combine(installed, "settings.json"), "original");
            File.WriteAllBytes(Path.Combine(candidate, "Dvinholm.exe"), exe);
            File.WriteAllText(Path.Combine(candidate, "settings.json"), "updated");
            var zip = Path.Combine(stage, UnifiedX64ReleaseFeed.PackageAsset);
            var zipBytes = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(zip, zipBytes);
            var staged = new UnifiedX64StagedPackage(stage, zip,
                Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant(),
                zipBytes.Length, 4, 515);
            var plan = UnifiedX64RecoveryPreflight.Inspect(
                installed, staged, Path.Combine(root, "snapshots"));
            Assert.True(plan.CanPlanBackup);
            var receipt = UnifiedX64BackupSnapshot.Create(plan, staged);
            UnifiedX64RecoverySandbox.Prepare(root, receipt);
            test(root);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static Process StartSleeper(int milliseconds)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -Command \"Start-Sleep -Milliseconds " + milliseconds + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        Assert.True(process.Start());
        return process;
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException) { }
    }

    [Fact]
    public void FinishedChildAllowsFixtureHandoff()
    {
        if (!OperatingSystem.IsWindows()) return;
        InFixture(root =>
        {
            using var child = StartSleeper(1);
            child.WaitForExit(10000);
            UnifiedX64PostExitHandoff.WaitAndApplyFixtureAsync(
                root, child, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Assert.Equal(UnifiedX64RecoverySandbox.StateApplied,
                UnifiedX64RecoverySandbox.ReadState(root));
            Assert.Equal("updated", File.ReadAllText(
                Path.Combine(root, "installed", "settings.json")));
        });
    }

    [Fact]
    public void LiveProcessPreventsAnyPrematureFileReplacement()
    {
        if (!OperatingSystem.IsWindows()) return;
        InFixture(root =>
        {
            using var child = StartSleeper(1600);
            try
            {
                var task = UnifiedX64PostExitHandoff.WaitAndApplyFixtureAsync(
                    root, child, TimeSpan.FromSeconds(8));
                Thread.Sleep(150);
                Assert.False(task.IsCompleted);
                Assert.Equal("original", File.ReadAllText(
                    Path.Combine(root, "installed", "settings.json")));
                task.GetAwaiter().GetResult();
                Assert.Equal("updated", File.ReadAllText(
                    Path.Combine(root, "installed", "settings.json")));
            }
            finally { KillIfRunning(child); }
        });
    }

    [Fact]
    public void CancellationWhileProcessLivesLeavesOriginalUnchanged()
    {
        if (!OperatingSystem.IsWindows()) return;
        InFixture(root =>
        {
            using var child = StartSleeper(2500);
            try
            {
                using var cancel = new CancellationTokenSource();
                cancel.CancelAfter(100);
                Assert.ThrowsAny<OperationCanceledException>(() =>
                    UnifiedX64PostExitHandoff.WaitAndApplyFixtureAsync(
                        root, child, TimeSpan.FromSeconds(8), cancel.Token)
                    .GetAwaiter().GetResult());
                Assert.Equal(UnifiedX64RecoverySandbox.StateReady,
                    UnifiedX64RecoverySandbox.ReadState(root));
            }
            finally { KillIfRunning(child); }
        });
    }

    [Fact]
    public void TimeoutWhileProcessLivesLeavesOriginalUnchanged()
    {
        if (!OperatingSystem.IsWindows()) return;
        InFixture(root =>
        {
            using var child = StartSleeper(2500);
            try
            {
                Assert.ThrowsAny<OperationCanceledException>(() =>
                    UnifiedX64PostExitHandoff.WaitAndApplyFixtureAsync(
                        root, child, TimeSpan.FromMilliseconds(80))
                    .GetAwaiter().GetResult());
                Assert.Equal("original", File.ReadAllText(
                    Path.Combine(root, "installed", "settings.json")));
            }
            finally { KillIfRunning(child); }
        });
    }

    [Fact]
    public void SelfProcessWaitIsRejectedWithoutDeadlock() =>
        InFixture(root =>
        {
            using var current = Process.GetCurrentProcess();
            Assert.Throws<InvalidOperationException>(() =>
                UnifiedX64PostExitHandoff.WaitAndApplyFixtureAsync(
                    root, current, TimeSpan.FromSeconds(1)).GetAwaiter().GetResult());
        });

    [Fact]
    public void CorruptedJournalIsRejectedBeforeWait()
    {
        if (!OperatingSystem.IsWindows()) return;
        InFixture(root =>
        {
            File.WriteAllText(Path.Combine(root, "fixture-transaction.json"), "{}");
            using var child = StartSleeper(1);
            try
            {
                Assert.Throws<InvalidDataException>(() =>
                    UnifiedX64PostExitHandoff.WaitAndApplyFixtureAsync(
                        root, child, TimeSpan.FromSeconds(2)).GetAwaiter().GetResult());
            }
            finally { KillIfRunning(child); }
        });
    }

    [Fact]
    public void MissingMarkerCannotHandoffToAnyInstalledProgram()
    {
        if (!OperatingSystem.IsWindows()) return;
        InFixture(root =>
        {
            File.Delete(Path.Combine(root, ".dvinholm-offline-fixture"));
            using var child = StartSleeper(1);
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    UnifiedX64PostExitHandoff.WaitAndApplyFixtureAsync(
                        root, child, TimeSpan.FromSeconds(2)).GetAwaiter().GetResult());
            }
            finally { KillIfRunning(child); }
        });
    }

    [Fact]
    public void ChangedCandidateAfterPrepareNeverGetsApplied()
    {
        if (!OperatingSystem.IsWindows()) return;
        InFixture(root =>
        {
            File.WriteAllText(Path.Combine(root, "candidate", "settings.json"), "tampered");
            using var child = StartSleeper(1);
            try
            {
                Assert.Throws<InvalidDataException>(() =>
                    UnifiedX64PostExitHandoff.WaitAndApplyFixtureAsync(
                        root, child, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());
                Assert.Equal("original", File.ReadAllText(
                    Path.Combine(root, "installed", "settings.json")));
            }
            finally { KillIfRunning(child); }
        });
    }
}
