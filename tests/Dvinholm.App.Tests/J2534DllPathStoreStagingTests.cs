using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline staging checks. Only private settings files
/// are touched; no vendor DLL is loaded and no physical adapter is needed.
/// </summary>
public sealed class J2534DllPathStoreStagingTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "Dvinholm-j2534-path-staging", Guid.NewGuid().ToString("N"));

    public J2534DllPathStoreStagingTests() => Directory.CreateDirectory(_root);

    private string SettingsPath => Path.Combine(_root, "j2534-dll-path.txt");

    private J2534DllPathStore Store(Action<string, string>? stage = null) =>
        new(SettingsPath, stage);

    private void AssertNoNewTemporaryFiles() =>
        Assert.Empty(Directory.GetFiles(_root, "j2534-dll-path.txt.*.tmp"));

    [Fact]
    public void NormalSaveCreatesNestedParentAndUtf8PathWithoutBom()
    {
        var path = Path.Combine(_root, "nested", "j2534-dll-path.txt");
        var store = new J2534DllPathStore(path);

        store.Save(@"  C:\Драйверы\J2534.dll  ");

        Assert.Equal(@"C:\Драйверы\J2534.dll", store.Load());
        var bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public void OldFixedNameTemporaryDirectoryDoesNotBlockNewSave()
    {
        var legacyTemp = SettingsPath + ".tmp";
        Directory.CreateDirectory(legacyTemp);
        File.WriteAllText(Path.Combine(legacyTemp, "sentinel.txt"), "keep old scratch");

        Store().Save(@"C:\Drivers\CurrentJ2534.dll");

        Assert.Equal(@"C:\Drivers\CurrentJ2534.dll", Store().Load());
        Assert.Equal("keep old scratch", File.ReadAllText(Path.Combine(legacyTemp, "sentinel.txt")));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void ExceptionAfterPartialStagePreservesEarlierSettingsAndCleansTemp()
    {
        Store().Save(@"C:\Drivers\OldJ2534.dll");
        var prior = File.ReadAllBytes(SettingsPath);

        Assert.Throws<IOException>(() =>
            Store((temp, _) =>
            {
                File.WriteAllText(temp, "C:\\part");
                throw new IOException("Injected interrupted settings write.");
            }).Save(@"C:\Drivers\NewJ2534.dll"));

        Assert.Equal(prior, File.ReadAllBytes(SettingsPath));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void SilentShortWriteCannotReplaceLastValidDllPath()
    {
        Store().Save(@"C:\Drivers\OldJ2534.dll");
        var prior = File.ReadAllText(SettingsPath);

        Assert.Throws<IOException>(() =>
            Store((temp, value) => File.WriteAllText(temp, value[..5]))
                .Save(@"C:\Drivers\NewJ2534.dll"));

        Assert.Equal(prior, File.ReadAllText(SettingsPath));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void SameLengthModifiedStagingCannotReplaceSettings()
    {
        Store().Save(@"C:\Drivers\OldJ2534.dll");
        var prior = File.ReadAllText(SettingsPath);

        Assert.Throws<IOException>(() =>
            Store((temp, value) =>
                File.WriteAllText(temp, value.Replace("New", "Bad", StringComparison.Ordinal)))
                .Save(@"C:\Drivers\NewJ2534.dll"));

        Assert.Equal(prior, File.ReadAllText(SettingsPath));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void MissingStageReturningNormallyDoesNotCreateDestination()
    {
        Assert.Throws<IOException>(() =>
            Store((_, _) => { }).Save(@"C:\Drivers\NewJ2534.dll"));

        Assert.False(File.Exists(SettingsPath));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void FailedFinalReplaceLeavesDirectoryAndItsContentsUntouched()
    {
        Directory.CreateDirectory(SettingsPath);
        var sentinel = Path.Combine(SettingsPath, "keep.txt");
        File.WriteAllText(sentinel, "original contents");

        var error = Record.Exception(() => Store().Save(@"C:\Drivers\NewJ2534.dll"));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal("original contents", File.ReadAllText(sentinel));
        AssertNoNewTemporaryFiles();
    }

    [Fact]
    public void RetryAfterRejectedStageCommitsFullValueAndNullClearsSettings()
    {
        Store().Save(@"C:\Drivers\OldJ2534.dll");
        Assert.Throws<IOException>(() =>
            Store((temp, _) => File.WriteAllText(temp, "wrong"))
                .Save(@"C:\Drivers\NewJ2534.dll"));

        Store().Save(@"C:\Drivers\NewJ2534.dll");
        Assert.Equal(@"C:\Drivers\NewJ2534.dll", Store().Load());
        AssertNoNewTemporaryFiles();

        Store().Save(null);
        Assert.Equal(string.Empty, Store().Load());
        Assert.True(File.Exists(SettingsPath));
        AssertNoNewTemporaryFiles();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
