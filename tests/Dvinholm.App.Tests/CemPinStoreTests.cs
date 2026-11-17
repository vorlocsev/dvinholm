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
    public void LockedAbandonedStageDoesNotPreventSavingAnotherRecord()
    {
        using var folder = new Folder();
        Directory.CreateDirectory(folder.Path);
        var lockedStage = System.IO.Path.Combine(folder.Path,
            $"{Guid.NewGuid():N}.{Guid.NewGuid():N}.tmp");
        File.WriteAllBytes(lockedStage, new byte[] { 1, 2, 3, 4 });
        File.SetLastWriteTimeUtc(lockedStage, DateTime.UtcNow.AddDays(-3));

        using (new FileStream(lockedStage, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            using var candidate = Result();
            var saved = folder.Store.Save(candidate, 30682981);
            Assert.True(File.Exists(lockedStage));
            Assert.Equal(saved.Id, Assert.Single(folder.Store.List().Entries).Id);
        }
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(lockedStage));
    }

    [Fact]
    public void OldMalformedTemporaryNameIsNeverDeleted()
    {
        using var folder = new Folder();
        Directory.CreateDirectory(folder.Path);
        var otherFile = System.IO.Path.Combine(folder.Path,
            $"{Guid.NewGuid():N}.not-a-guid.tmp");
        File.WriteAllBytes(otherFile, new byte[] { 9, 8, 7 });
        File.SetLastWriteTimeUtc(otherFile, DateTime.UtcNow.AddDays(-3));

        using var candidate = Result();
        var saved = folder.Store.Save(candidate, 30682981);
        Assert.True(File.Exists(otherFile));
        Assert.Equal(new byte[] { 9, 8, 7 }, File.ReadAllBytes(otherFile));
        Assert.Equal(saved.Id, Assert.Single(folder.Store.List().Entries).Id);
    }

    [Fact]
    public void DeniedStageWriteDoesNotExposeFailureOrDamagePreviousPin()
    {
        using var folder = new Folder();
        using var original = Result();
        var saved = folder.Store.Save(original, 30682981);
        var before = File.ReadAllBytes(folder.File(saved.Id));
        var denied = new CemPinStore(folder.Path, (path, encrypted) =>
        {
            File.WriteAllBytes(path, encrypted[..Math.Min(8, encrypted.Length)]);
            throw new UnauthorizedAccessException("PRIVATE-SYNTHETIC-PIN-PATH");
        });

        using var candidate = Result();
        var failure = Assert.Throws<InvalidOperationException>(() =>
            denied.Save(candidate, 30682981));
        Assert.Equal("The encrypted PIN record could not be saved.", failure.Message);
        Assert.DoesNotContain("PRIVATE-SYNTHETIC-PIN-PATH", failure.ToString());
        Assert.Equal(before, File.ReadAllBytes(folder.File(saved.Id)));
        Assert.Single(folder.Store.List().Entries);
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
    }

    [Fact]
    public void FullVaultRejectsAnotherRecordWithoutChangingExistingFiles()
    {
        using var folder = new Folder();
        Directory.CreateDirectory(folder.Path);
        var first = Guid.NewGuid();
        for (var i = 0; i < CemPinStore.MaxVisibleRecords; i++)
        {
            var id = i == 0 ? first : Guid.NewGuid();
            File.WriteAllBytes(folder.File(id), new byte[] { 1, 2, 3 });
        }
        var original = File.ReadAllBytes(folder.File(first));
        using var candidate = Result();
        var error = Assert.Throws<InvalidOperationException>(() =>
            folder.Store.Save(candidate, 30682981));
        Assert.Equal("The encrypted PIN record could not be saved.", error.Message);
        Assert.Equal(CemPinStore.MaxVisibleRecords,
            Directory.GetFiles(folder.Path, "*.dpapi").Length);
        Assert.Equal(original, File.ReadAllBytes(folder.File(first)));
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
        Assert.True(candidate.HasPin);

        // Removing one corrupt entry restores capacity without changing the
        // remaining records. A valid save must be present in the bounded UI list.
        File.Delete(folder.File(first));
        var saved = folder.Store.Save(candidate, 30682981);
        var inventory = folder.Store.List();
        Assert.Equal(saved.Id, Assert.Single(inventory.Entries).Id);
        Assert.Equal(CemPinStore.MaxVisibleRecords - 1, inventory.UnavailableCount);
    }

    [Fact]
    public void VaultFilledWhileStagingRejectsUnpublishedRecord()
    {
        using var folder = new Folder();
        Directory.CreateDirectory(folder.Path);
        for (var i = 0; i < CemPinStore.MaxVisibleRecords - 1; i++)
            File.WriteAllBytes(folder.File(Guid.NewGuid()), new byte[] { 1 });
        var injected = Guid.NewGuid();
        var stage = new CemPinStore(folder.Path, (path, encrypted) =>
        {
            File.WriteAllBytes(path, encrypted);
            // Mimic a second writer publishing a new record while this
            // Save still owns only a temporary encrypted staging file.
            File.WriteAllBytes(folder.File(injected), new byte[] { 1, 2 });
        });

        using var candidate = Result();
        var error = Assert.Throws<InvalidOperationException>(() =>
            stage.Save(candidate, 30682981));
        Assert.Equal("The encrypted PIN record could not be saved.", error.Message);
        Assert.Equal(CemPinStore.MaxVisibleRecords,
            Directory.GetFiles(folder.Path, "*.dpapi").Length);
        Assert.True(File.Exists(folder.File(injected)));
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
        Assert.True(candidate.HasPin);
    }

    [Fact]
    public async Task ConcurrentStoreInstancesCannotCommitBeyondVisibleCapacity()
    {
        using var folder = new Folder();
        Directory.CreateDirectory(folder.Path);
        for (var i = 0; i < CemPinStore.MaxVisibleRecords - 1; i++)
            File.WriteAllBytes(folder.File(Guid.NewGuid()), new byte[] { 1 });

        using var firstStaging = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        using var firstCandidate = Result();
        using var secondCandidate = Result();
        var firstStore = new CemPinStore(folder.Path, (path, bytes) =>
        {
            firstStaging.Set();
            if (!releaseFirst.Wait(TimeSpan.FromSeconds(10)))
                throw new IOException("Synthetic stage timeout");
            File.WriteAllBytes(path, bytes);
        });
        var secondStore = new CemPinStore(folder.Path);
        var first = Task.Run(() => firstStore.Save(firstCandidate, 30682981));
        Task<Exception?>? second = null;
        try
        {
            Assert.True(firstStaging.Wait(TimeSpan.FromSeconds(5)));
            second = Task.Run(() =>
            {
                secondStarted.Set();
                return Record.Exception(() => secondStore.Save(secondCandidate, 30682981));
            });
            Assert.True(secondStarted.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(100);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            releaseFirst.Set();
        }

        var accepted = await first.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(accepted.Id, Assert.Single(folder.Store.List().Entries).Id);
        var secondFailure = await second!.WaitAsync(TimeSpan.FromSeconds(10));
        var error = Assert.IsType<InvalidOperationException>(secondFailure);
        Assert.Equal("The encrypted PIN record could not be saved.", error.Message);
        Assert.Equal(CemPinStore.MaxVisibleRecords,
            Directory.GetFiles(folder.Path, "*.dpapi").Length);
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
        Assert.True(secondCandidate.HasPin);
    }

    [Fact]
    public async Task DifferentVaultDirectoriesDoNotShareWriteGate()
    {
        using var firstFolder = new Folder();
        using var otherFolder = new Folder();
        using var firstStaging = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var firstCandidate = Result();
        using var otherCandidate = Result();
        var stalledStore = new CemPinStore(firstFolder.Path, (path, bytes) =>
        {
            firstStaging.Set();
            if (!releaseFirst.Wait(TimeSpan.FromSeconds(10)))
                throw new IOException("Synthetic stage timeout");
            File.WriteAllBytes(path, bytes);
        });

        var stalledSave = Task.Run(() => stalledStore.Save(firstCandidate, 30682981));
        try
        {
            Assert.True(firstStaging.Wait(TimeSpan.FromSeconds(5)));
            var independentSave = Task.Run(() =>
                otherFolder.Store.Save(otherCandidate, 30682981));
            var saved = await independentSave.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(saved.Id, Assert.Single(otherFolder.Store.List().Entries).Id);
            Assert.False(stalledSave.IsCompleted);
        }
        finally
        {
            releaseFirst.Set();
        }
        await stalledSave.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CancellingContendedVaultSaveReturnsBeforeMutexDeadline()
    {
        using var folder = new Folder();
        using var firstStaging = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        using var original = Result();
        using var pending = Result();
        using var cancellation = new CancellationTokenSource();

        var firstStore = new CemPinStore(folder.Path, (path, encrypted) =>
        {
            firstStaging.Set();
            if (!releaseFirst.Wait(TimeSpan.FromSeconds(10)))
                throw new IOException("SYNTHETIC-FIRST-WRITER-TIMEOUT");
            File.WriteAllBytes(path, encrypted);
        });
        var first = Task.Run(() => firstStore.Save(original, 30682981));
        Task<Exception?>? blocked = null;
        try
        {
            Assert.True(firstStaging.Wait(TimeSpan.FromSeconds(5)));
            blocked = Task.Run(() =>
            {
                secondStarted.Set();
                return Record.Exception(() =>
                    folder.Store.Save(pending, 30682981, cancellation.Token));
            });
            Assert.True(secondStarted.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(100);
            Assert.False(blocked.IsCompleted);

            cancellation.Cancel();
            var failure = await blocked.WaitAsync(TimeSpan.FromSeconds(5));
            var error = Assert.IsType<InvalidOperationException>(failure);
            Assert.Equal("The encrypted PIN record could not be saved.", error.Message);
            Assert.False(first.IsCompleted); // No need to wait for the other writer.
        }
        finally { releaseFirst.Set(); }

        var committed = await first.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(committed.Id, Assert.Single(folder.Store.List().Entries).Id);
        Assert.Single(Directory.GetFiles(folder.Path, "*.dpapi"));
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
    }

    [Fact]
    public void CancellationDuringEncryptedStagingNeverPublishesNewRecord()
    {
        using var folder = new Folder();
        using var previous = Result();
        var existing = folder.Store.Save(previous, 30682981);
        var original = File.ReadAllBytes(folder.File(existing.Id));
        using var candidate = Result();
        using var cancellation = new CancellationTokenSource();

        var store = new CemPinStore(folder.Path, (path, encrypted) =>
        {
            File.WriteAllBytes(path, encrypted);
            cancellation.Cancel();
        });
        var error = Assert.Throws<InvalidOperationException>(() =>
            store.Save(candidate, 30682981, cancellation.Token));

        Assert.Equal("The encrypted PIN record could not be saved.", error.Message);
        Assert.Equal(original, File.ReadAllBytes(folder.File(existing.Id)));
        Assert.Equal(existing.Id, Assert.Single(folder.Store.List().Entries).Id);
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
        Assert.True(candidate.HasPin); // Caller cancellation is not PIN disposal.
    }

    [Fact]
    public void LateCancellationRollbackDeletesOnlyNewEncryptedRecord()
    {
        using var folder = new Folder();
        using var older = Result();
        var retained = folder.Store.Save(older, 30682981);
        var retainedBytes = File.ReadAllBytes(folder.File(retained.Id));

        using var newer = Result();
        var newlySaved = folder.Store.Save(newer, 30682981);
        // Simulate the WinUI cancellation continuation after Save completed:
        // it owns the new receipt and may remove only that specific file.
        folder.Store.Delete(newlySaved.Id);

        Assert.False(File.Exists(folder.File(newlySaved.Id)));
        Assert.Equal(retainedBytes, File.ReadAllBytes(folder.File(retained.Id)));
        Assert.Equal(retained.Id, Assert.Single(folder.Store.List().Entries).Id);
        using var restored = folder.Store.Load(retained.Id);
        Assert.True(restored.TryReveal(bytes => Assert.True(bytes.SequenceEqual(Value()))));
    }

    [Fact]
    public async Task DeleteWaitsForConcurrentSaveInSameVault()
    {
        using var folder = new Folder();
        using var oldCandidate = Result();
        var oldEntry = folder.Store.Save(oldCandidate, 30682981);
        using var newCandidate = Result();
        using var staging = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var deleting = new ManualResetEventSlim();

        var slowStore = new CemPinStore(folder.Path, (path, data) =>
        {
            staging.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new IOException("Synthetic blocked stage");
            File.WriteAllBytes(path, data);
        });
        var save = Task.Run(() => slowStore.Save(newCandidate, 30682981));
        Task? delete = null;
        try
        {
            Assert.True(staging.Wait(TimeSpan.FromSeconds(5)));
            delete = Task.Run(() =>
            {
                deleting.Set();
                folder.Store.Delete(oldEntry.Id);
            });
            Assert.True(deleting.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(100);
            Assert.False(delete.IsCompleted);
            Assert.True(File.Exists(folder.File(oldEntry.Id)));
        }
        finally { release.Set(); }

        var newEntry = await save.WaitAsync(TimeSpan.FromSeconds(10));
        await delete!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(File.Exists(folder.File(oldEntry.Id)));
        Assert.True(File.Exists(folder.File(newEntry.Id)));
        Assert.Equal(newEntry.Id, Assert.Single(folder.Store.List().Entries).Id);
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
    }

    [Fact]
    public void DirectoryNamedLikePinRecordCannotBeSilentlyDeleted()
    {
        using var folder = new Folder();
        using var candidate = Result();
        var retained = folder.Store.Save(candidate, 30682981);
        var original = File.ReadAllBytes(folder.File(retained.Id));
        var directoryId = Guid.NewGuid();
        var disguisedDirectory = folder.File(directoryId);
        Directory.CreateDirectory(disguisedDirectory);

        var error = Assert.Throws<InvalidOperationException>(() =>
            folder.Store.Delete(directoryId));
        Assert.Equal("The saved PIN record could not be deleted.", error.Message);
        Assert.True(Directory.Exists(disguisedDirectory));
        Assert.Equal(original, File.ReadAllBytes(folder.File(retained.Id)));
        Assert.Equal(retained.Id, Assert.Single(folder.Store.List().Entries).Id);
    }

    [Fact]
    public void DamagedRecordCanBeExplicitlyDeletedWithoutTouchingOtherPins()
    {
        using var folder = new Folder();
        using var candidate = Result();
        var retained = folder.Store.Save(candidate, 30682981);
        var original = File.ReadAllBytes(folder.File(retained.Id));
        var corruptId = Guid.NewGuid();
        File.WriteAllBytes(folder.File(corruptId), new byte[] { 0x01, 0x02 });
        Assert.Throws<InvalidOperationException>(() => folder.Store.Load(corruptId));
        Assert.Equal(1, folder.Store.List().UnavailableCount);

        folder.Store.Delete(corruptId);
        folder.Store.Delete(corruptId); // Confirm missing-record idempotence.
        Assert.False(File.Exists(folder.File(corruptId)));
        Assert.Equal(original, File.ReadAllBytes(folder.File(retained.Id)));
        Assert.Equal(retained.Id, Assert.Single(folder.Store.List().Entries).Id);
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
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
    public void UndecryptableRecordsExposeOnlyIdsForExplicitDeletion()
    {
        using var folder = new Folder();
        using var candidate = Result();
        var retained = folder.Store.Save(candidate, 30682981);
        var original = File.ReadAllBytes(folder.File(retained.Id));
        var unavailableId = Guid.NewGuid();
        File.WriteAllBytes(folder.File(unavailableId), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(System.IO.Path.Combine(folder.Path, "unrelated.dpapi"), new byte[] { 4 });
        var inventory = folder.Store.List();

        Assert.Equal(retained.Id, Assert.Single(inventory.Entries).Id);
        Assert.Equal(2, inventory.UnavailableCount);
        Assert.Equal(unavailableId, Assert.Single(inventory.UnavailableRecordIds));
        Assert.DoesNotContain(folder.Path, inventory.ToString());
        Assert.True(File.Exists(folder.File(unavailableId))); // Inventory never deletes.
        Assert.Equal(original, File.ReadAllBytes(folder.File(retained.Id)));

        // Only the explicitly chosen GUID and inventory fingerprint can be
        // removed; a malformed name never becomes an actionable record ID.
        Assert.True(inventory.UnavailableSnapshots.TryGetValue(unavailableId, out var fingerprint));
        folder.Store.DeleteUnavailable(inventory.UnavailableRecordIds[0], fingerprint!);
        var after = folder.Store.List();
        Assert.Empty(after.UnavailableRecordIds);
        Assert.Equal(1, after.UnavailableCount);
        Assert.Equal(retained.Id, Assert.Single(after.Entries).Id);
        Assert.Equal(original, File.ReadAllBytes(folder.File(retained.Id)));
    }

    [Fact]
    public void UnavailableSelectionCannotDeleteRecordThatBecomesReadable()
    {
        using var folder = new Folder();
        using var candidate = Result();
        var retained = folder.Store.Save(candidate, 30682981);
        var original = File.ReadAllBytes(folder.File(retained.Id));
        File.WriteAllBytes(folder.File(retained.Id), new byte[] { 0x01, 0x02 });

        var inventory = folder.Store.List();
        Assert.Equal(retained.Id, Assert.Single(inventory.UnavailableRecordIds));
        Assert.Empty(inventory.Entries);

        // Simulate the encrypted file being repaired after inventory was
        // opened but before the user confirms its removal.
        File.WriteAllBytes(folder.File(retained.Id), original);
        var error = Assert.Throws<InvalidOperationException>(() =>
            folder.Store.DeleteUnavailable(retained.Id, inventory.UnavailableSnapshots[retained.Id]));
        Assert.Equal("The saved PIN record could not be deleted.", error.Message);
        Assert.Equal(original, File.ReadAllBytes(folder.File(retained.Id)));
        Assert.Equal(retained.Id, Assert.Single(folder.Store.List().Entries).Id);
    }

    [Fact]
    public void ConfirmedUnavailableDeletionOnlyRemovesStillUnreadableRecord()
    {
        using var folder = new Folder();
        using var candidate = Result();
        var retained = folder.Store.Save(candidate, 30682981);
        var original = File.ReadAllBytes(folder.File(retained.Id));
        var corruptId = Guid.NewGuid();
        File.WriteAllBytes(folder.File(corruptId), new byte[] { 0x01, 0x02 });
        var inventory = folder.Store.List();
        Assert.Equal(corruptId, Assert.Single(inventory.UnavailableRecordIds));

        var fingerprint = inventory.UnavailableSnapshots[corruptId];
        folder.Store.DeleteUnavailable(corruptId, fingerprint);
        folder.Store.DeleteUnavailable(corruptId, fingerprint); // Missing is idempotent.
        Assert.False(File.Exists(folder.File(corruptId)));
        Assert.Equal(original, File.ReadAllBytes(folder.File(retained.Id)));
        Assert.Equal(retained.Id, Assert.Single(folder.Store.List().Entries).Id);
    }

    [Fact]
    public void ChangedButStillUnreadableRecordCannotBeDeletedUsingOldInventory()
    {
        using var folder = new Folder();
        using var candidate = Result();
        var retained = folder.Store.Save(candidate, 30682981);
        var original = File.ReadAllBytes(folder.File(retained.Id));
        var corruptId = Guid.NewGuid();
        File.WriteAllBytes(folder.File(corruptId), new byte[] { 1, 2, 3 });
        var inventory = folder.Store.List();
        var previousFingerprint = inventory.UnavailableSnapshots[corruptId];

        // Same UUID but different unreadable ciphertext between list and
        // confirmation must refuse deletion of the new file.
        var replacement = new byte[] { 1, 2, 4 };
        File.WriteAllBytes(folder.File(corruptId), replacement);
        Assert.Throws<InvalidOperationException>(() =>
            folder.Store.DeleteUnavailable(corruptId, previousFingerprint));
        Assert.Equal(replacement, File.ReadAllBytes(folder.File(corruptId)));
        Assert.Equal(original, File.ReadAllBytes(folder.File(retained.Id)));

        // A refreshed inventory is explicit new evidence for deletion.
        var refreshed = folder.Store.List();
        Assert.NotEqual(previousFingerprint, refreshed.UnavailableSnapshots[corruptId]);
        folder.Store.DeleteUnavailable(corruptId, refreshed.UnavailableSnapshots[corruptId]);
        Assert.False(File.Exists(folder.File(corruptId)));
        Assert.Equal(original, File.ReadAllBytes(folder.File(retained.Id)));
    }

    [Fact]
    public void OversizedUnreadableRecordIsCountedButNotOfferedForDeletion()
    {
        using var folder = new Folder();
        Directory.CreateDirectory(folder.Path);
        var oversizedId = Guid.NewGuid();
        File.WriteAllBytes(folder.File(oversizedId), new byte[8193]);
        var inventory = folder.Store.List();
        Assert.Equal(1, inventory.UnavailableCount);
        Assert.Empty(inventory.UnavailableRecordIds);
        Assert.Empty(inventory.UnavailableSnapshots);
        Assert.True(File.Exists(folder.File(oversizedId)));
    }

    [Fact]
    public void NonCanonicalGuidFilenameIsCountedButNeverSelectableForDeletion()
    {
        using var folder = new Folder();
        using var candidate = Result();
        var retained = folder.Store.Save(candidate, 30682981);
        var original = File.ReadAllBytes(folder.File(retained.Id));

        // GUID parsing accepts uppercase letters, but Dvinholm saves lowercase
        // canonical N-format names. On a case-sensitive Windows directory,
        // reconstructing the lowercase path could target another record.
        var alias = Guid.Parse("abcdefab-cdef-abcd-abcd-abcdefabcdef");
        var nonCanonical = System.IO.Path.Combine(folder.Path,
            alias.ToString("N").ToUpperInvariant() + ".dpapi");
        File.WriteAllBytes(nonCanonical, new byte[] { 0x01, 0x02 });

        var inventory = folder.Store.List();
        Assert.Equal(retained.Id, Assert.Single(inventory.Entries).Id);
        Assert.Equal(1, inventory.UnavailableCount);
        Assert.Empty(inventory.UnavailableRecordIds);
        Assert.True(File.Exists(nonCanonical));
        Assert.Equal(original, File.ReadAllBytes(folder.File(retained.Id)));
    }

    [Fact]
    public void DamagedRecordIdsAreBoundedByVisibleVaultInventory()
    {
        using var folder = new Folder();
        Directory.CreateDirectory(folder.Path);
        for (var i = 0; i <= CemPinStore.MaxVisibleRecords; i++)
            File.WriteAllBytes(folder.File(Guid.NewGuid()), new byte[] { 1 });

        var inventory = folder.Store.List();
        Assert.Empty(inventory.Entries);
        Assert.Equal(CemPinStore.MaxVisibleRecords + 1, inventory.UnavailableCount);
        Assert.Equal(CemPinStore.MaxVisibleRecords, inventory.UnavailableRecordIds.Count);
        Assert.Equal(inventory.UnavailableRecordIds.Count,
            inventory.UnavailableRecordIds.Distinct().Count());
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
        var unexpectedReveal = false;
        Assert.False(restored.TryReveal(_ => unexpectedReveal = true));
        Assert.False(unexpectedReveal, "Disposed stored material must not reach the callback.");
        Assert.DoesNotContain("123456789012", saved.ToString());
    }
}
#endif
