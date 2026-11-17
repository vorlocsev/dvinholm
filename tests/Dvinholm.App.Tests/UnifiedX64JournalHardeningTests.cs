using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight negative tests for damaged local recovery evidence. Only disposable
/// marked GUID fixtures are used. No real application path can be modified.
/// </summary>
public sealed class UnifiedX64JournalHardeningTests
{
    private static void InFixture(Action<string, string, string> test)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "dvinholm-recovery-fixture-" + Guid.NewGuid().ToString("N"));
        try
        {
            UnifiedX64RecoverySandbox.MarkFixtureForTests(root);
            var installed = Path.Combine(root, "installed");
            var candidate = Path.Combine(root, "candidate");
            var stage = Path.Combine(root, "staging",
                "pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(installed);
            Directory.CreateDirectory(candidate);
            Directory.CreateDirectory(stage);
            var exe = new byte[1024];
            exe[0] = 77; exe[1] = 90;
            BitConverter.GetBytes(128).CopyTo(exe, 60);
            exe[128] = 80; exe[129] = 69;
            BitConverter.GetBytes((ushort)0x8664).CopyTo(exe, 132);
            File.WriteAllBytes(Path.Combine(installed, "Dvinholm.exe"), exe);
            File.WriteAllText(Path.Combine(installed, "settings.json"), "{}");
            File.WriteAllBytes(Path.Combine(candidate, "Dvinholm.exe"), exe);
            File.WriteAllText(Path.Combine(candidate, "settings.json"), "[]");
            var zip = Path.Combine(stage, UnifiedX64ReleaseFeed.PackageAsset);
            var bytes = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(zip, bytes);
            var staged = new UnifiedX64StagedPackage(stage, zip,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                bytes.Length, 4, 515);
            var plan = UnifiedX64RecoveryPreflight.Inspect(
                installed, staged, Path.Combine(root, "snapshots"));
            Assert.True(plan.CanPlanBackup);
            var receipt = UnifiedX64BackupSnapshot.Create(plan, staged);
            UnifiedX64RecoverySandbox.Prepare(root, receipt);
            test(root,
                Path.Combine(receipt.Directory, "snapshot-journal.json"),
                Path.Combine(root, "fixture-transaction.json"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void ChangeJson(string path, Action<JsonObject> mutation)
    {
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        mutation(json);
        File.WriteAllText(path, json.ToJsonString());
    }

    private static void AssertSnapshotFails(string root, string journal)
    {
        var directory = Path.GetDirectoryName(journal)!;
        Assert.Throws<InvalidDataException>(() =>
            UnifiedX64BackupSnapshot.Verify(directory));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(root, "installed", "settings.json")));
    }

    private static void AssertTransactionFails(string root)
    {
        Assert.Throws<InvalidDataException>(() =>
            UnifiedX64RecoverySandbox.ReadState(root));
        Assert.Throws<InvalidDataException>(() =>
            UnifiedX64RecoverySandbox.ApplyFixture(root));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(root, "installed", "settings.json")));
    }

    [Fact]
    public void NullBackupJournalRecordFailsWithoutDereference() =>
        InFixture((root, snapshot, transaction) =>
        {
            ChangeJson(snapshot, node => node["Files"]!.AsArray()[0] = null);
            AssertSnapshotFails(root, snapshot);
        });

    [Fact]
    public void NullBackupDigestFailsBeforeAnyFileHash() =>
        InFixture((root, snapshot, transaction) =>
        {
            ChangeJson(snapshot, node =>
                node["Files"]!.AsArray()[0]!["Sha256"] = null);
            AssertSnapshotFails(root, snapshot);
        });

    [Fact]
    public void TraversalInBackupJournalIsNeverAccepted() =>
        InFixture((root, snapshot, transaction) =>
        {
            ChangeJson(snapshot, node =>
                node["Files"]!.AsArray()[0]!["RelativePath"] = "../outside.dll");
            AssertSnapshotFails(root, snapshot);
        });

    [Fact]
    public void DuplicateBackupNameIgnoringCaseFails() =>
        InFixture((root, snapshot, transaction) =>
        {
            ChangeJson(snapshot, node =>
            {
                var files = node["Files"]!.AsArray();
                files[1]!["RelativePath"] = files[0]!["RelativePath"]!.ToString()
                    .ToUpperInvariant();
            });
            AssertSnapshotFails(root, snapshot);
        });

    [Fact]
    public void NullTransactionJournalRecordIsRefused() =>
        InFixture((root, snapshot, transaction) =>
        {
            ChangeJson(transaction, node => node["Files"]!.AsArray()[0] = null);
            AssertTransactionFails(root);
        });

    [Fact]
    public void NullTransactionIdentityFieldsAreRefused() =>
        InFixture((root, snapshot, transaction) =>
        {
            ChangeJson(transaction, node => node["SnapshotName"] = null);
            AssertTransactionFails(root);
        });

    [Fact]
    public void MissingTransactionDigestCannotBeTreatedAsTrusted() =>
        InFixture((root, snapshot, transaction) =>
        {
            ChangeJson(transaction, node =>
                node["Files"]!.AsArray()[0]!["CandidateSha256"] = null);
            AssertTransactionFails(root);
        });

    [Fact]
    public void TruncatedJsonJournalIsAnInvalidDataFailure() =>
        InFixture((root, snapshot, transaction) =>
        {
            File.WriteAllText(transaction, "{\"Schema\":1,\"Files\":[");
            AssertTransactionFails(root);
        });
}
