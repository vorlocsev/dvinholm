namespace Dvinholm.App.Tests;

public sealed class UpdateSourcePreferenceStoreTests
{
    [Fact]
    public void MissingPreference_UsesCallerDefaultWithoutWriting()
    {
        using var folder = new TemporaryDirectory();
        var file = Path.Combine(folder.Path, "update-source.txt");
        var store = new UpdateSourcePreferenceStore(file);
        Assert.Equal(UpdateSourceChannel.Development, store.Load());
        Assert.Equal(UpdateSourceChannel.Stable, store.Load(UpdateSourceChannel.Stable));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void SelectedStable_PersistsAcrossNewStoreInstancesAndRestart()
    {
        using var folder = new TemporaryDirectory();
        var file = Path.Combine(folder.Path, "update-source.txt");
        new UpdateSourcePreferenceStore(file).Save(UpdateSourceChannel.Stable);
        Assert.Equal(UpdateSourceChannel.Stable, new UpdateSourcePreferenceStore(file).Load());
        Assert.Equal("stable", File.ReadAllText(file));
    }

    [Fact]
    public void SwitchingBackToDevelopment_PersistsAfterStable()
    {
        using var folder = new TemporaryDirectory();
        var file = Path.Combine(folder.Path, "update-source.txt");
        new UpdateSourcePreferenceStore(file).Save(UpdateSourceChannel.Stable);
        new UpdateSourcePreferenceStore(file).Save(UpdateSourceChannel.Development);
        Assert.Equal(UpdateSourceChannel.Development,
            new UpdateSourcePreferenceStore(file).Load(UpdateSourceChannel.Stable));
    }

    [Fact]
    public void CorruptedOrUnknownPreference_FallsBackWithoutReplacingFile()
    {
        using var folder = new TemporaryDirectory();
        var file = Path.Combine(folder.Path, "update-source.txt");
        foreach (var text in new[] { "", "auto", "stable\nDEVELOPMENT", "ci-3582" })
        {
            File.WriteAllText(file, text);
            Assert.Equal(UpdateSourceChannel.Stable,
                new UpdateSourcePreferenceStore(file).Load(UpdateSourceChannel.Stable));
            Assert.Equal(text, File.ReadAllText(file));
        }
    }

    [Fact]
    public void Save_RejectsUnknownChannelWithoutWriting()
    {
        using var folder = new TemporaryDirectory();
        var file = Path.Combine(folder.Path, "update-source.txt");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UpdateSourcePreferenceStore(file).Save((UpdateSourceChannel)42));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void InterruptedStaging_PreservesPreviouslySavedChannelAndCleansTemporaryFile()
    {
        using var folder = new TemporaryDirectory();
        var file = Path.Combine(folder.Path, "update-source.txt");
        new UpdateSourcePreferenceStore(file).Save(UpdateSourceChannel.Stable);
        var interrupted = new UpdateSourcePreferenceStore(file, (temp, _) =>
            File.WriteAllText(temp, "devel"));

        Assert.Throws<IOException>(() => interrupted.Save(UpdateSourceChannel.Development));
        Assert.Equal(UpdateSourceChannel.Stable, new UpdateSourcePreferenceStore(file).Load());
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
    }

    [Fact]
    public void Save_UsesUniqueStagingFileAndLeavesNoTemporaryFiles()
    {
        using var folder = new TemporaryDirectory();
        var file = Path.Combine(folder.Path, "nested", "update-source.txt");
        var store = new UpdateSourcePreferenceStore(file);
        store.Save(UpdateSourceChannel.Stable);
        store.Save(UpdateSourceChannel.Stable);
        Assert.Equal(UpdateSourceChannel.Stable, store.Load());
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(file)!, "*.tmp"));
    }

    [Fact]
    public void FailedDestinationReplacement_DoesNotLeaveTemporaryFile()
    {
        using var folder = new TemporaryDirectory();
        var file = Path.Combine(folder.Path, "update-source.txt");
        Directory.CreateDirectory(file);
        var store = new UpdateSourcePreferenceStore(file);
        var failure = Record.Exception(() => store.Save(UpdateSourceChannel.Stable));
        Assert.True(failure is IOException or UnauthorizedAccessException,
            $"Expected a blocked Windows file replacement, got {failure?.GetType().Name ?? "no exception"}.");
        Assert.True(Directory.Exists(file));
        Assert.Empty(Directory.GetFiles(folder.Path, "*.tmp"));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DvinholmChannelTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
