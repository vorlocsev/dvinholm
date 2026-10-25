using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline checks of atomic CAN baud-rate settings saves.
/// No J2534 DLL, real adapter or WinUI instance is required.
/// </summary>
public sealed class CanBaudRateStoreStagingIntegrityTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "Dvinholm-can-baud-staging", Guid.NewGuid().ToString("N"));

    public CanBaudRateStoreStagingIntegrityTests() => Directory.CreateDirectory(_root);

    private string SettingsPath => Path.Combine(_root, "can-baud-rate.txt");

    private CanBaudRateStore Store(Action<string, string>? writer = null) =>
        new(SettingsPath, writer);

    private void AssertNoTemporaryFiles() =>
        Assert.Empty(Directory.GetFiles(_root, "can-baud-rate.txt.*.tmp"));

    [Fact]
    public void SilentShortWriteDoesNotReplacePreviousSpeed()
    {
        Store().Save(250000);
        var bytes = File.ReadAllBytes(SettingsPath);

        Assert.Throws<IOException>(() =>
            Store((temp, value) => File.WriteAllText(temp, value[..3]))
                .Save(125000));

        Assert.Equal(bytes, File.ReadAllBytes(SettingsPath));
        Assert.Equal(250000u, Store().Load());
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void DifferentSupportedSpeedStagedWithoutErrorIsRejected()
    {
        Store().Save(125000);
        var original = File.ReadAllText(SettingsPath);

        // "500000" is valid by itself but is not the user's requested 250000.
        Assert.Throws<IOException>(() =>
            Store((temp, _) => File.WriteAllText(temp, "500000")).Save(250000));

        Assert.Equal(original, File.ReadAllText(SettingsPath));
        Assert.Equal(125000u, Store().Load());
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void ExtraNewlineCannotSilentlyChangeSavedRepresentation()
    {
        Store().Save(500000);

        Assert.Throws<IOException>(() =>
            Store((temp, value) => File.WriteAllText(temp, value + "\n")).Save(125000));

        Assert.Equal(500000u, Store().Load());
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void WriterThatReturnsWithoutCreatingStagePreservesLastValue()
    {
        Store().Save(250000);

        Assert.Throws<IOException>(() =>
            Store((_, _) => { }).Save(125000));

        Assert.Equal(250000u, Store().Load());
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void EmptyFirstStageDoesNotCreateSettingsFile()
    {
        Assert.Throws<IOException>(() =>
            Store((temp, _) => File.WriteAllText(temp, "")).Save(125000));

        Assert.False(File.Exists(SettingsPath));
        Assert.Equal(CanBaudRateStore.DefaultBaudRate, Store().Load());
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void WriterExceptionAfterPartialStagePreservesSavedSpeed()
    {
        Store().Save(250000);
        var original = File.ReadAllBytes(SettingsPath);

        Assert.Throws<IOException>(() =>
            Store((temp, _) =>
            {
                File.WriteAllText(temp, "125");
                throw new IOException("Simulated interrupted write.");
            }).Save(125000));

        Assert.Equal(original, File.ReadAllBytes(SettingsPath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void RetryAfterBadStagingCommitsRequestedSpeed()
    {
        Store().Save(500000);
        Assert.Throws<IOException>(() =>
            Store((temp, _) => File.WriteAllText(temp, "250")).Save(250000));

        Store().Save(250000);
        Assert.Equal(250000u, Store().Load());
        Store().Save(125000);
        Assert.Equal(125000u, Store().Load());
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void NormalSaveUsesUniqueTemporaryNameAndUtf8WithoutBom()
    {
        Directory.CreateDirectory(SettingsPath + ".tmp");
        File.WriteAllText(Path.Combine(SettingsPath + ".tmp", "sentinel.txt"), "stale temp");

        Store().Save(125000);

        Assert.Equal(125000u, Store().Load());
        Assert.Equal("stale temp", File.ReadAllText(
            Path.Combine(SettingsPath + ".tmp", "sentinel.txt")));
        Assert.Equal("125000", File.ReadAllText(SettingsPath));
        Assert.False(File.ReadAllBytes(SettingsPath).Take(3)
            .SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        AssertNoTemporaryFiles();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
