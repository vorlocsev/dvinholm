using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic, Windows-only DPAPI vault checks. No live CEM, PIN search
/// transport, vehicle identifier or real user vault is ever accessed.
/// </summary>
public sealed class CemPinVaultHardeningTests
{
    private static CemPinAttemptResult SyntheticAdapterCandidate() =>
        CemPinAttemptResult.Accepted(new CemPinSecret(
            [0x12, 0x34, 0x56, 0x78, 0x90, 0x12],
            CemPinResultOrigin.AdapterUnattested));

    private static void InVault(Action<string, CemPinStore> test)
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Path.Combine(Path.GetTempPath(),
            "dvinholm-cem-vault-check-" + Guid.NewGuid().ToString("N"));
        try { test(path, new CemPinStore(path)); }
        finally { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
    }

    [Fact]
    public void CurrentUserDpapiRoundtripOnlyPublishesOpaqueFile() =>
        InVault((folder, store) =>
        {
            using var candidate = SyntheticAdapterCandidate();
            var record = store.Save(candidate, 30682981);
            var encoded = File.ReadAllBytes(Path.Combine(folder, record.Id.ToString("N") + ".dpapi"));
            Assert.True(encoded.Length > 23);
            Assert.Equal(-1, encoded.AsSpan().IndexOf(
                new byte[] { 0x12, 0x34, 0x56, 0x78, 0x90, 0x12 }));
            Assert.Single(store.List().Entries);
            using var restored = store.Load(record.Id);
            Assert.True(restored.TryReveal(bytes =>
                Assert.True(bytes.SequenceEqual(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x90, 0x12 }))));
        });

    [Fact]
    public void InventoryNeverProcessesMoreThanBoundedFileCount() =>
        InVault((folder, store) =>
        {
            Directory.CreateDirectory(folder);
            for (var i = 0; i <= CemPinStore.MaxVisibleRecords; i++)
                File.WriteAllText(Path.Combine(folder,
                    Guid.NewGuid().ToString("N") + ".dpapi"), "not DPAPI");
            var result = store.List();
            Assert.Empty(result.Entries);
            Assert.Equal(CemPinStore.MaxVisibleRecords + 1, result.UnavailableCount);
        });

    [Fact]
    public void OversizedRecordIsUnreadableAndNeverDisclosesItsContents() =>
        InVault((folder, store) =>
        {
            Directory.CreateDirectory(folder);
            var id = Guid.NewGuid();
            File.WriteAllBytes(Path.Combine(folder, id.ToString("N") + ".dpapi"),
                new byte[8193]);
            Assert.Equal(1, store.List().UnavailableCount);
            var ex = Assert.Throws<InvalidOperationException>(() => store.Load(id));
            Assert.DoesNotContain(folder, ex.ToString());
            Assert.DoesNotContain("123456789012", ex.ToString());
        });

    [Fact]
    public void RenamedEncryptedRecordCannotBeReadUnderAnotherGuid() =>
        InVault((folder, store) =>
        {
            using var candidate = SyntheticAdapterCandidate();
            var saved = store.Save(candidate, 30682981);
            var second = Guid.NewGuid();
            File.Move(Path.Combine(folder, saved.Id.ToString("N") + ".dpapi"),
                Path.Combine(folder, second.ToString("N") + ".dpapi"));
            Assert.Throws<InvalidOperationException>(() => store.Load(second));
            Assert.Equal(1, store.List().UnavailableCount);
        });

    [Fact]
    public void ExclusiveRecordLockTemporarilyPreventsDecryptWithoutDataLoss() =>
        InVault((folder, store) =>
        {
            using var candidate = SyntheticAdapterCandidate();
            var saved = store.Save(candidate, 30682981);
            var file = Path.Combine(folder, saved.Id.ToString("N") + ".dpapi");
            using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Throws<InvalidOperationException>(() => store.Load(saved.Id));
                Assert.Equal(1, store.List().UnavailableCount);
            }
            using var restored = store.Load(saved.Id);
            Assert.True(restored.IsAvailable);
        });

    [Fact]
    public void FailedTruncatedStagingDoesNotOverwritePreviousEncryptedPin() =>
        InVault((folder, store) =>
        {
            using var candidate = SyntheticAdapterCandidate();
            var original = store.Save(candidate, 30682981);
            var broken = new CemPinStore(folder, (path, bytes) =>
            {
                File.WriteAllBytes(path, bytes[..Math.Min(8, bytes.Length)]);
                throw new IOException("PRIVATE PIN NEVER LOG");
            });
            var ex = Assert.Throws<InvalidOperationException>(() =>
                broken.Save(candidate, 30682981));
            Assert.DoesNotContain("PRIVATE PIN NEVER LOG", ex.ToString());
            Assert.Single(Directory.GetFiles(folder, "*.dpapi"));
            Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
            using var restored = store.Load(original.Id);
            Assert.True(restored.IsAvailable);
        });

    [Fact]
    public void DeleteIsIdempotentAndCannotAffectAnotherSavedEntry() =>
        InVault((folder, store) =>
        {
            using var first = SyntheticAdapterCandidate();
            using var second = SyntheticAdapterCandidate();
            var a = store.Save(first, 30682981);
            var b = store.Save(second, 30682981);
            store.Delete(a.Id);
            store.Delete(a.Id);
            Assert.Throws<InvalidOperationException>(() => store.Load(a.Id));
            Assert.Equal(b.Id, Assert.Single(store.List().Entries).Id);
        });

    [Fact]
    public void ReplacingSavedResultDisposesPreviousSecretBeforeNewReveal() =>
        InVault((folder, store) =>
        {
            using var first = SyntheticAdapterCandidate();
            var oldRecord = store.Save(first, 30682981);
            using var second = SyntheticAdapterCandidate();
            var newRecord = store.Save(second, 30682981);
            var previous = store.Load(oldRecord.Id);
            using var oldResult = CemPinAttemptResult.Restored(previous);
            Assert.True(previous.IsAvailable);
            var next = store.Load(newRecord.Id);
            oldResult.Dispose();
            Assert.False(previous.IsAvailable);
            using var newResult = CemPinAttemptResult.Restored(next);
            Assert.True(newResult.HasPin);
            Assert.False(oldResult.TryReveal(_ =>
                Assert.Fail("Old PIN should not be revealable after replacement.")));
        });
}
