#if DVINHOLM_DEVELOPMENT_FEATURES
using System.Security.Cryptography;
using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>Real Windows DPAPI, synthetic values only; no vehicle or user vault access.</summary>
public sealed class CemPinStoreTests
{
    private static byte[] Value() => new byte[] { 0x12, 0x34, 0x56, 0x78, 0x90, 0x12 };
    private static CemPinAttemptResult Result(CemPinResultOrigin origin = CemPinResultOrigin.AdapterUnattested) =>
        CemPinAttemptResult.Accepted(new CemPinSecret(Value(), origin));
    private sealed class Folder : IDisposable
    {
        internal readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Dvinholm-PinStore-Tests-" + Guid.NewGuid().ToString("N"));
        internal CemPinStore Store => new(Path);
        internal string File(Guid id) => System.IO.Path.Combine(Path, id.ToString("N") + ".dpapi");
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }

    [Fact]
    public void RealDpapiRoundTripSurvivesNewStoreInstanceWithoutPlaintextFiles()
    {
        using var folder = new Folder();
        using var found = Result();
        var entry = folder.Store.Save(found, 30682981);
        var bytes = File.ReadAllBytes(folder.File(entry.Id));
        Assert.True(bytes.Length > Value().Length);
        Assert.Equal(-1, bytes.AsSpan().IndexOf(Value()));
        Assert.DoesNotContain("123456789012", System.Text.Encoding.UTF8.GetString(bytes));
        var inventory = new CemPinStore(folder.Path).List();
        var saved = Assert.Single(inventory.Entries);
        Assert.Equal(entry.Id, saved.Id);
        Assert.Equal(30682981u, saved.PartNumber);
        Assert.Equal(0, inventory.UnavailableCount);
        using var restored = CemPinAttemptResult.Restored(folder.Store.Load(saved.Id));
        Assert.True(restored.TryReveal(pin => Assert.True(pin.SequenceEqual(Value()))));
        Assert.False(restored.PhysicalAcceptanceVerified);
        Assert.Contains("not been checked", restored.PublicMessage);
        Assert.DoesNotContain("123456789012", restored.ToString());
        Assert.Single(Directory.GetFiles(folder.Path));
    }

    [Fact]
    public void EqualPartNumbersRemainSeparateFindsWithNoOverwriteOrVehicleInference()
    {
        using var folder = new Folder();
        using var first = Result();
        using var second = Result();
        var a = folder.Store.Save(first, 30682981);
        var b = folder.Store.Save(second, 30682981);
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(2, folder.Store.List().Entries.Count);
        Assert.NotEqual(File.ReadAllBytes(folder.File(a.Id)), File.ReadAllBytes(folder.File(b.Id)));
        Assert.InRange(a.SavedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
    }

    [Fact]
    public void SyntheticExpiredAndMetadataOnlyResultsCannotBeSaved()
    {
        using var folder = new Folder();
        using var synthetic = Result(CemPinResultOrigin.Synthetic);
        using var disposed = Result();
        disposed.Dispose();
        using var metadata = CemPinAttemptResult.From(CemPinAttemptStatus.FinishedWithoutPin);
        foreach (var result in new[] { synthetic, disposed, metadata })
            Assert.Throws<InvalidOperationException>(() => folder.Store.Save(result, 30682981));
        using var valid = Result();
        Assert.Throws<InvalidOperationException>(() => folder.Store.Save(valid, 0));
        Assert.False(Directory.Exists(folder.Path));
    }

    [Fact]
    public void RevokedCandidateDuringEncryptedStagingCannotPublishOrRetainFiles()
    {
        using var folder = new Folder();
        var owned = Value();
        var leaseCurrent = true;
        using var result = CemPinAttemptResult.Accepted(
            new CemPinSecret(owned, CemPinResultOrigin.AdapterUnattested),
            () => leaseCurrent);
        var writerInvocations = 0;
        var store = new CemPinStore(folder.Path, (path, encrypted) =>
        {
            writerInvocations++;
            Assert.Equal(-1, encrypted.AsSpan().IndexOf(Value()));
            File.WriteAllBytes(path, encrypted);
            leaseCurrent = false; // Synthetic disconnect while staging finishes.
        });

        var error = Assert.Throws<InvalidOperationException>(() =>
            store.Save(result, 30682981));
        Assert.Equal("The encrypted PIN record could not be saved.", error.Message);
        Assert.Equal(1, writerInvocations);
        Assert.Empty(Directory.GetFiles(folder.Path, "*.dpapi"));
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
        Assert.False(result.HasPin);
        Assert.All(owned, b => Assert.Equal((byte)0, b));
    }

    [Fact]
    public void RevokedStagingPreservesPreviouslyCommittedEncryptedRecord()
    {
        using var folder = new Folder();
        using var oldCandidate = Result();
        var previous = folder.Store.Save(oldCandidate, 30682981);
        var previousEncrypted = File.ReadAllBytes(folder.File(previous.Id));
        var stillAuthorized = true;
        using var next = CemPinAttemptResult.Accepted(
            new CemPinSecret(Value(), CemPinResultOrigin.AdapterUnattested),
            () => stillAuthorized);
        var store = new CemPinStore(folder.Path, (path, encrypted) =>
        {
            File.WriteAllBytes(path, encrypted);
            stillAuthorized = false;
        });

        Assert.Throws<InvalidOperationException>(() => store.Save(next, 30682981));
        Assert.Equal(previousEncrypted, File.ReadAllBytes(folder.File(previous.Id)));
        Assert.Single(folder.Store.List().Entries);
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
        using var restored = folder.Store.Load(previous.Id);
        Assert.True(restored.IsAvailable);
    }

    [Fact]
    public void ValidLiveCandidateRemainsSavableWhileLeaseIsCurrent()
    {
        using var folder = new Folder();
        var stillAuthorized = true;
        using var candidate = CemPinAttemptResult.Accepted(
            new CemPinSecret(Value(), CemPinResultOrigin.AdapterUnattested),
            () => stillAuthorized);
        var record = folder.Store.Save(candidate, 30682981);
        Assert.Equal(record.Id, Assert.Single(folder.Store.List().Entries).Id);
        using var restored = folder.Store.Load(record.Id);
        Assert.True(restored.TryReveal(pin => Assert.True(pin.SequenceEqual(Value()))));
        stillAuthorized = false;
        Assert.False(candidate.HasPin);
        Assert.True(restored.IsAvailable); // Saved records are separate from live leases.
    }

    [Fact]
    public void NextSaveReapsOnlyOldOwnedTemporaryFiles()
    {
        using var folder = new Folder();
        Directory.CreateDirectory(folder.Path);
        static string StageName(string root) =>
            System.IO.Path.Combine(root, $"{Guid.NewGuid():N}.{Guid.NewGuid():N}.tmp");

        var oldDvinholmStage = StageName(folder.Path);
        var freshDvinholmStage = StageName(folder.Path);
        var unrelated = System.IO.Path.Combine(folder.Path, "other-program.tmp");
        File.WriteAllBytes(oldDvinholmStage, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(freshDvinholmStage, new byte[] { 4, 5, 6 });
        File.WriteAllBytes(unrelated, new byte[] { 7, 8, 9 });
        File.SetLastWriteTimeUtc(oldDvinholmStage, DateTime.UtcNow.AddDays(-3));
        File.SetLastWriteTimeUtc(unrelated, DateTime.UtcNow.AddDays(-3));

        using var candidate = Result();
        var record = folder.Store.Save(candidate, 30682981);

        Assert.False(File.Exists(oldDvinholmStage));
        Assert.True(File.Exists(freshDvinholmStage));
        Assert.True(File.Exists(unrelated));
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(freshDvinholmStage));
        Assert.Equal(record.Id, Assert.Single(folder.Store.List().Entries).Id);
    }

    [Fact]
    public void FailedStageStillCleansCurrentTemporaryFileAfterStaleRecovery()
    {
        using var folder = new Folder();
        Directory.CreateDirectory(folder.Path);
        var stale = System.IO.Path.Combine(folder.Path,
            $"{Guid.NewGuid():N}.{Guid.NewGuid():N}.tmp");
        File.WriteAllBytes(stale, new byte[] { 1 });
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-3));
        using var candidate = Result();
        var store = new CemPinStore(folder.Path, (path, encrypted) =>
        {
            File.WriteAllBytes(path, encrypted[..8]);
            throw new IOException("PRIVATE-SYNTHETIC-STAGE-FAILURE");
        });

        var failure = Assert.Throws<InvalidOperationException>(() =>
            store.Save(candidate, 30682981));
        Assert.Equal("The encrypted PIN record could not be saved.", failure.Message);
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
        Assert.Empty(Directory.GetFiles(folder.Path, "*.dpapi"));
    }

    [Fact]
    public void TamperingTruncationAndRecordRenameCannotRevealMaterial()
    {
        using var folder = new Folder();
        using var found = Result();
        var entry = folder.Store.Save(found, 30682981);
        var original = File.ReadAllBytes(folder.File(entry.Id));
        foreach (var damaged in new[] { original[..10], new byte[9000], Array.Empty<byte>(),
            original.Select((b, i) => i == original.Length - 1 ? (byte)(b ^ 1) : b).ToArray() })
        {
            File.WriteAllBytes(folder.File(entry.Id), damaged);
            var error = Assert.Throws<InvalidOperationException>(() => folder.Store.Load(entry.Id));
            Assert.DoesNotContain(folder.Path, error.ToString());
            Assert.DoesNotContain("123456789012", error.ToString());
        }
        var renamed = Guid.NewGuid();
        File.WriteAllBytes(folder.File(renamed), original);
        Assert.Throws<InvalidOperationException>(() => folder.Store.Load(renamed));
    }

    [Fact]
    public void MissingAndUnavailableEntriesDoNotHideReadableRecordsOrGetDeleted()
    {
        using var folder = new Folder();
        Assert.Empty(folder.Store.List().Entries);
        using var found = Result();
        var entry = folder.Store.Save(found, 30682981);
        var bad = folder.File(Guid.NewGuid());
        File.WriteAllBytes(bad, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(System.IO.Path.Combine(folder.Path, "invalid-name.dpapi"), new byte[] { 1 });
        var inventory = folder.Store.List();
        Assert.Equal(entry.Id, Assert.Single(inventory.Entries).Id);
        Assert.Equal(2, inventory.UnavailableCount);
        Assert.True(File.Exists(bad));
    }

    [Fact]
    public void InterruptedOrTruncatedStagingPreservesExistingRecordsAndRedactsErrors()
    {
        using var folder = new Folder();
        using var found = Result();
        var existing = folder.Store.Save(found, 30682981);
        foreach (var fail in new[] { true, false })
        {
            var store = new CemPinStore(folder.Path, (path, bytes) =>
            {
                Assert.Equal(-1, bytes.AsSpan().IndexOf(Value()));
                File.WriteAllBytes(path, bytes[..10]);
                if (fail) throw new IOException("SYNTHETIC-PRIVATE-PIN-PATH");
            });
            var error = Assert.Throws<InvalidOperationException>(() => store.Save(found, 30682981));
            Assert.DoesNotContain("SYNTHETIC-PRIVATE", error.ToString());
            Assert.Single(Directory.GetFiles(folder.Path));
            using var pin = folder.Store.Load(existing.Id);
            Assert.True(pin.IsAvailable);
        }
    }

    [Fact]
    public void DeletionRemovesOnlySelectedRecordAndIsIdempotent()
    {
        using var folder = new Folder();
        using var found = Result();
        var a = folder.Store.Save(found, 30682981);
        var b = folder.Store.Save(found, 30682981);
        folder.Store.Delete(a.Id);
        folder.Store.Delete(a.Id);
        Assert.Throws<InvalidOperationException>(() => folder.Store.Load(a.Id));
        Assert.Equal(b.Id, Assert.Single(folder.Store.List().Entries).Id);
        using var pin = folder.Store.Load(b.Id);
        Assert.True(pin.TryReveal(bytes => Assert.True(bytes.SequenceEqual(Value()))));
    }

    [Fact]
    public void SavedUiIsHiddenByDefaultAndSavedMaterialRetainsExpiryAndRedaction()
    {
        using var stream = typeof(CemPinStoreTests).Assembly.GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml");
        var doc = XDocument.Load(stream!);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var button = Assert.Single(doc.Descendants(), e => (string?)e.Attribute(x + "Name") == "CemPinSavedButton");
        Assert.Equal("Collapsed", (string?)button.Attribute("Visibility"));
        Assert.Equal("SavedCemPins_Click", (string?)button.Attribute("Click"));
        using var folder = new Folder();
        using var found = Result();
        var saved = folder.Store.Save(found, 30682981);
        var secret = folder.Store.Load(saved.Id);
        using var restored = CemPinAttemptResult.Restored(secret);
        restored.Dispose();
        Assert.False(secret.IsAvailable);
        Assert.False(restored.TryReveal(_ => Assert.Fail("Disposed stored material was revealed.")));
        Assert.DoesNotContain("123456789012", saved.ToString());
    }
}
#endif
